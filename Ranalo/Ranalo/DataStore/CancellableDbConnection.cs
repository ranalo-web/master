using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Ranalo.Services;

namespace Ranalo.DataStore
{
    // The connection every repository gets (IDbConnection in Program.cs). It
    // is the normal SqlConnection with one addition: when the page load that
    // asked for a query is no longer wanted (the browser left, or the same
    // person opened another page -- see PageLoadMiddleware), the query is
    // stopped on the database instead of running on to the end for nobody.
    //
    // Only reads are ever stopped: commands that return rows or a value,
    // outside a transaction. Execute (insert/update/delete) and anything in a
    // transaction always run to completion, so nothing is left half-saved.
    // Outside a page load (saves, background jobs, shared cache builds in
    // their own scope) the token is None and this is a plain SqlConnection.
    public sealed class CancellableDbConnection : DbConnection
    {
        private readonly SqlConnection _inner;
        private readonly RequestCancellation _request;

        public CancellableDbConnection(SqlConnection inner, RequestCancellation request)
        {
            _inner = inner;
            _request = request;
            _inner.StateChange += (_, e) => OnStateChange(e);
        }

        public SqlConnection Inner => _inner;

        [AllowNull]
        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override int ConnectionTimeout => _inner.ConnectionTimeout;
        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public override void Open() => _inner.Open();
        public override Task OpenAsync(CancellationToken cancellationToken) => _inner.OpenAsync(cancellationToken);
        public override void Close() => _inner.Close();

        // The real SqlTransaction: commands are run on the inner connection,
        // so they need the inner transaction.
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand() =>
            new CancellableDbCommand(_inner.CreateCommand(), this, _request);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    public sealed class CancellableDbCommand : DbCommand
    {
        private readonly SqlCommand _inner;
        private readonly CancellableDbConnection _connection;
        private readonly RequestCancellation _request;
        private CancellationTokenRegistration _stopOnCancel;

        public CancellableDbCommand(SqlCommand inner, CancellableDbConnection connection, RequestCancellation request)
        {
            _inner = inner;
            _connection = connection;
            _request = request;
        }

        [AllowNull]
        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        // Always the connection that made it; the inner command keeps the
        // real SqlConnection.
        protected override DbConnection? DbConnection
        {
            get => _connection;
            set { }
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

        protected override DbTransaction? DbTransaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = (SqlTransaction?)value;
        }

        public override void Cancel() => _inner.Cancel();
        public override void Prepare() => _inner.Prepare();
        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        // Writes are never stopped.
        public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
            _inner.ExecuteNonQueryAsync(cancellationToken);

        public override object? ExecuteScalar()
        {
            StopWithPageLoad();
            try { return _inner.ExecuteScalar(); }
            catch (SqlException ex) when (_request.Token.IsCancellationRequested) { throw Stopped(ex); }
        }

        public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            StopWithPageLoad();
            try { return await _inner.ExecuteScalarAsync(cancellationToken); }
            catch (SqlException ex) when (_request.Token.IsCancellationRequested) { throw Stopped(ex); }
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            StopWithPageLoad();
            try { return _inner.ExecuteReader(behavior); }
            catch (SqlException ex) when (_request.Token.IsCancellationRequested) { throw Stopped(ex); }
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            StopWithPageLoad();
            try { return await _inner.ExecuteReaderAsync(behavior, cancellationToken); }
            catch (SqlException ex) when (_request.Token.IsCancellationRequested) { throw Stopped(ex); }
        }

        // SQL reports a stopped query as "A severe error occurred on the
        // current command", which reads like a real fault in the logs and
        // in catch blocks. Say what it is.
        private OperationCanceledException Stopped(SqlException ex) =>
            new("Query stopped: the page that asked for it is no longer open.", ex, _request.Token);

        // Runs before a read. If the page load is already over, don't start
        // the query at all; otherwise stop it on the server the moment the
        // page load is over, including while its rows are being read (until
        // the command is disposed, which Dapper does when it has the rows).
        private void StopWithPageLoad()
        {
            var token = _request.Token;
            if (!token.CanBeCanceled || _inner.Transaction != null) return;
            token.ThrowIfCancellationRequested();
            _stopOnCancel.Dispose();
            _stopOnCancel = token.Register(static c => ((SqlCommand)c!).Cancel(), _inner);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stopOnCancel.Dispose();
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
