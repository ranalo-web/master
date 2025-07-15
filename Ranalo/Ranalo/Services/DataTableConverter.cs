using System.Data;
using System.Reflection;

namespace Ranalo.Services
{
    public static class DataTableConverter
    {
        public static DataTable ToDataTable<T>(List<T> items)
        {
            var table = new DataTable(typeof(T).Name);

            // Get all the public instance properties of T
            PropertyInfo[] props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                // Use Nullable.GetUnderlyingType to support nullable types
                Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                table.Columns.Add(prop.Name, propType);
            }

            foreach (var item in items)
            {
                var row = table.NewRow();
                foreach (var prop in props)
                {
                    object val = prop.GetValue(item, null) ?? DBNull.Value;
                    row[prop.Name] = val;
                }
                table.Rows.Add(row);
            }

            return table;
        }


        public static DataTable GetPTable1(DataTable dt)
        {
            var grouped = dt.AsEnumerable()
                .GroupBy(row => row["account_no"])
                .Select(g => new
                {
                    account_no = g.Key,
                    Total_Paid = g.Sum(r => r.Field<decimal>("amount"))
                })
                .OrderBy(x => x.account_no);

            // Create result DataTable
            DataTable result = new DataTable();
            result.Columns.Add("account_no", dt.Columns["account_no"].DataType);
            result.Columns.Add("Total_Paid", typeof(decimal));

            foreach (var item in grouped)
            {
                var row = result.NewRow();
                row["account_no"] = item.account_no;
                row["Total_Paid"] = item.Total_Paid;
                result.Rows.Add(row);
            }

            return result;
        }

        public static DataTable GetPTable2(DataTable dt)
        {
            var grouped = dt.AsEnumerable()
                .GroupBy(row => row["account_no"])
                .Select(g => new
                {
                    account_no = g.Key,
                    First_Payment_Date = g.Min(r => r.Field<DateTime>("payment_date"))
                })
                .OrderBy(x => x.account_no);

            // Create result DataTable
            DataTable result = new DataTable();
            result.Columns.Add("account_no", dt.Columns["account_no"].DataType);
            result.Columns.Add("First_Payment_Date", typeof(DateTime));

            foreach (var item in grouped)
            {
                var row = result.NewRow();
                row["account_no"] = item.account_no;
                row["First_Payment_Date"] = item.First_Payment_Date;
                result.Rows.Add(row);
            }

            return result;
        }

        public static DataTable GetPTable3(DataTable dt)
        {
            var grouped = dt.AsEnumerable()
                .GroupBy(row => row["account_no"])
                .Select(g => new
                {
                    account_no = g.Key,
                    Last_Payment_Date = g.Max(r => r.Field<DateTime>("payment_date"))
                })
                .OrderBy(x => x.account_no);

            // Create result DataTable
            DataTable result = new DataTable();
            result.Columns.Add("account_no", dt.Columns["account_no"].DataType);
            result.Columns.Add("Last_Payment_Date", typeof(DateTime));

            foreach (var item in grouped)
            {
                var row = result.NewRow();
                row["account_no"] = item.account_no;
                row["Last_Payment_Date"] = item.Last_Payment_Date;
                result.Rows.Add(row);
            }

            return result;
        }

        public static DataTable GetPTable4(DataTable dt_payments, DataTable PTable3)
        {
            // Join dt_payments with PTable3 on account_no
            var joined = from payment in dt_payments.AsEnumerable()
                         join lastDate in PTable3.AsEnumerable()
                         on payment["account_no"] equals lastDate["account_no"] into lj
                         from lastDate in lj.DefaultIfEmpty()
                         select new
                         {
                             Row = payment,
                             Last_Payment_Date = lastDate?["Last_Payment_Date"] as DateTime?
                         };

            // Filter: payment_date == Last_Payment_Date
            var filtered = joined
                .Where(x => x.Last_Payment_Date.HasValue &&
                            x.Row.Field<DateTime>("payment_date") == x.Last_Payment_Date.Value);

            // Create resulting DataTable with all columns from dt_payments EXCEPT "id"
            DataTable result = dt_payments.Clone();
            result.Columns.Remove("id"); // Drop 'id'
            result.Columns.Add("Last_Payment_Date", typeof(DateTime)); // Optionally drop this later

            foreach (var item in filtered)
            {
                var newRow = result.NewRow();
                foreach (DataColumn col in dt_payments.Columns)
                {
                    if (col.ColumnName == "id") continue;
                    newRow[col.ColumnName] = item.Row[col.ColumnName];
                }
                newRow["Last_Payment_Date"] = item.Last_Payment_Date.Value;
                result.Rows.Add(newRow);
            }

            // Remove 'Last_Payment_Date' as in R version
            result.Columns.Remove("Last_Payment_Date");

            return result;
        }


        public static DataTable GetPTable5(DataTable dt_payments, DataTable PTable2)
        {
            // Join dt_payments with PTable2 on account_no (left join)
            var joined = from payment in dt_payments.AsEnumerable()
                         join firstDate in PTable2.AsEnumerable()
                         on payment["account_no"] equals firstDate["account_no"] into lj
                         from firstDate in lj.DefaultIfEmpty()
                         select new
                         {
                             Row = payment,
                             First_Payment_Date = firstDate?["First_Payment_Date"] as DateTime?
                         };

            // Filter: payment_date == First_Payment_Date
            var filtered = joined
                .Where(x => x.First_Payment_Date.HasValue &&
                            x.Row.Field<DateTime>("payment_date") == x.First_Payment_Date.Value);

            // Create result table with all columns from dt_payments except "id"
            DataTable result = dt_payments.Clone();
            result.Columns.Remove("id"); // Drop 'id'
            result.Columns.Add("First_Payment_Date", typeof(DateTime)); // Needed for filtering; we'll drop it later

            foreach (var item in filtered)
            {
                var newRow = result.NewRow();
                foreach (DataColumn col in dt_payments.Columns)
                {
                    if (col.ColumnName == "id") continue;
                    newRow[col.ColumnName] = item.Row[col.ColumnName];
                }
                newRow["First_Payment_Date"] = item.First_Payment_Date.Value;
                result.Rows.Add(newRow);
            }

            // Remove 'First_Payment_Date' as in R
            result.Columns.Remove("First_Payment_Date");

            return result;
        }


    }
}
