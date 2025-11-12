using MySql.Data.MySqlClient;
using Ranalo.DataStore.DataModels;
using System.Data;

namespace Ranalo.DataStore.MySql
{
    public class MySqlPaymentsRepository : IMySqlPaymentsRepository
    {
        //private readonly string _connectionString = "\"Server=db5015859534.hosting-data.io;Database=dbs12929966;User ID=dbu741803;Password=TopGolShop23.\";";
        private readonly string _connectionString = "Server=db5015859534.hosting-data.io;Database=dbs12929966;User ID=dbu741803;Password=TopGolShop23.;Port=3306;SslMode=Required;\";";
        public async Task<object>? GetPaymentByIdAsync(int id)
        {
            const string query = "SELECT * from Mpesa_Transactions";
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);
            string accountNo = "";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                accountNo = reader.GetString("account_no");
                string name = reader.GetString("name");
                Console.WriteLine($"{id}: {name}");
            }

            return accountNo;
        }
    }
}
