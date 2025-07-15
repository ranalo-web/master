using System.Data;

namespace Ranalo.Services
{
    public class PaymentSummaryProcessor
    {

        private DataTable Devices, PTable1, PTable2, PTable3, PTable4, PTable5;
        private DataTable ContractInfo, DealerInfo;

        public PaymentSummaryProcessor(
            DataTable devices, DataTable paymentsWithOrphaned, DataTable p2,
            DataTable p3, DataTable p4, DataTable p5,
            DataTable contractInfo, DataTable dealerInfo)
        {
            Devices = devices;
            PTable1 = paymentsWithOrphaned;
            PTable2 = p2;
            PTable3 = p3;
            PTable4 = p4;
            PTable5 = p5;
            ContractInfo = contractInfo;
            DealerInfo = dealerInfo;
        }

        public DataTable BuildSummary(IEnumerable<int> fullyPaidIds)
        {
            var now = DateTime.UtcNow;

            // 1. Join all payment tables and filter out fully paid
            var joined = from p in Devices.AsEnumerable()
                         where !fullyPaidIds.Contains(p.Field<int>("id"))
                         let t = PTable1.AsEnumerable().FirstOrDefault(r => r.Field<int>("account_no") == p.Field<int>("id"))
                         let f = PTable2.AsEnumerable().FirstOrDefault(r => r.Field<int>("account_no") == p.Field<int>("id"))
                         let l = PTable3.AsEnumerable().FirstOrDefault(r => r.Field<int>("account_no") == p.Field<int>("id"))
                         let a = PTable4.AsEnumerable().FirstOrDefault(r => r.Field<int>("account_no") == p.Field<int>("id"))
                         let b = PTable5.AsEnumerable().FirstOrDefault(r => r.Field<int>("account_no") == p.Field<int>("id"))
                         let cid = p.Field<int?>("device_group_id") ?? 1000
                         where b != null && b.Field<object>("Amount") != DBNull.Value
                         select new
                         {
                             p,
                             t,
                             f,
                             l,
                             a,
                             b,
                             device_group_id = cid
                         };

            // 2. Merge with contract info
            var merged = from rec in joined
                         let c = ContractInfo.AsEnumerable().FirstOrDefault(r => r.Field<int>("ID") == rec.p.Field<int>("id"))
                         select new
                         {
                             rec.p,
                             rec.t,
                             rec.f,
                             rec.l,
                             rec.a,
                             rec.b,
                             rec.device_group_id,
                             contract = c
                         };

            // 3. Create working DataTable
            var dt = new DataTable();
            string[] columns = {
            "id","imei_no","model","make","locked","lock_type",
            "Last_Payment_Date","First_Paid_Amount","First_Lock_Date","Next_Lock_Date",
            "last_connected_at","Total_Paid",
            "First_Payment_Date","FirstPaidDate","First_MPesaCode",
            "Last_Paid_Amount","LastPaidDate","Last_MPesaCode",
            "status","enrollment_status",
            "device_group_id","enrolled_on",
            // contract fields
            "Deposit","Daily","Weekly","Monthly","Term_in_Months","Total_Loan","Total_Cost","First_Name","rePayment_Intervals"
        };
            foreach (var c in columns)
                dt.Columns.Add(c, typeof(object));

            // date and numeric calc columns
            string[] calcCols = {
            "First_Pay_Date","No_Days_Lifetime","No_Days_Units",
            "Contract_End_Date","Days_Contract_End","Minimum_Days",
            "Total_Due","DailyPaymentALL","Arrears","Loan_Balance",
            "Curr_Run_Time","Auto_lock_date_pmt","Datechar","Units_Left","Lock_Status_Pmt",
            "Live_flag","NotPaying7D","Lag_days","SaleWeek","Date_enrolled","enrolled_dt","paid_dt",
            "Comms","Arrears_amt"
        };
            foreach (var c in calcCols)
                dt.Columns.Add(c, typeof(object));

            // 4. Populate rows with calculations
            foreach (var rec in merged)
            {
                var p = rec.p; var t = rec.t; var f = rec.f; var l = rec.l; var a = rec.a; var b = rec.b; var c = rec.contract;
                var row = dt.NewRow();

                // Basic fields
                row["id"] = p["id"];
                row["imei_no"] = p["imei_no"];
                row["model"] = p["model"];
                row["make"] = p["make"];
                row["locked"] = p["locked"];
                row["lock_type"] = p["lock_type"];
                row["Last_Payment_Date"] = l?["Last_Payment_Date"] ?? DBNull.Value;
                row["First_Paid_Amount"] = b["Amount"];
                row["First_Lock_Date"] = p["first_lock_date_in_iso_format"];
                row["Next_Lock_Date"] = p["next_lock_date_in_iso_format"];
                row["last_connected_at"] = p["last_connected_at"];
                row["Total_Paid"] = t?["Total_Paid"] ?? DBNull.Value;
                row["First_Payment_Date"] = f?["First_Payment_Date"] ?? DBNull.Value;
                row["FirstPaidDate"] = b["Payment_Date"];
                row["First_MPesaCode"] = b["Mpesa_Code"];
                row["Last_Paid_Amount"] = a?["Amount"] ?? DBNull.Value;
                row["LastPaidDate"] = a?["Payment_Date"] ?? DBNull.Value;
                row["Last_MPesaCode"] = a?["Mpesa_Code"] ?? DBNull.Value;
                row["status"] = p["status"];
                row["enrollment_status"] = p["enrollment_status"];
                row["device_group_id"] = rec.device_group_id;
                row["enrolled_on"] = p["enrolled_on"];

                // contract
                if (c != null)
                {
                    row["Deposit"] = c["Deposit"];
                    row["Daily"] = c["Daily"];
                    row["Weekly"] = c["Weekly"];
                    row["Monthly"] = c["Monthly"];
                    row["Term_in_Months"] = c["Term_in_Months"];
                    row["Total_Loan"] = c["Total_Loan"];
                    row["Total_Cost"] = c["Total_Cost"];
                    row["First_Name"] = c["First_Name"];
                    row["rePayment_Intervals"] = c["rePayment_Intervals"];
                }

                // Convert dates to DateTime?
                DateTime? dtFirstPaid = TryParseDateTime(row["First_Payment_Date"]);
                DateTime? dtFirstPaidDate = TryParseDateTime(row["FirstPaidDate"]);
                DateTime? dtLastPaid = TryParseDateTime(row["LastPaidDate"]);

                if (dtFirstPaid.HasValue)
                {
                    row["First_Pay_Date"] = dtFirstPaid.Value;
                    row["No_Days_Lifetime"] = (now - dtFirstPaid.Value).Days;
                    row["No_Days_Units"] = (now - dtFirstPaid.Value).TotalDays;
                }

                if (dtFirstPaidDate.HasValue && dtFirstPaid.HasValue)
                {
                    int termMonths = Convert.ToInt32(row["Term_in_Months"] ?? 0);
                    var contractEnd = dtFirstPaidDate.Value.AddDays(termMonths * 30);
                    row["Contract_End_Date"] = contractEnd;
                    row["Days_Contract_End"] = (contractEnd - dtFirstPaid.Value).Days;

                    double u1 = (contractEnd - dtFirstPaid.Value).TotalDays;
                    double u2 = (now - dtFirstPaid.Value).TotalDays;
                    row["Minimum_Days"] = Math.Min(u1, u2);
                }

                // rate calculations
                double deposit = ToDouble(row["Deposit"]);
                double daily = ToDouble(row["Daily"]);
                double weekly = ToDouble(row["Weekly"]);
                double monthly = ToDouble(row["Monthly"]);
                double minDays = ToDouble(row["Minimum_Days"]);
                double totalPaidD = ToDouble(row["Total_Paid"]);

                row["Total_Due"] = deposit + daily * minDays + weekly * minDays / 7 + monthly * minDays / 30;
                row["DailyPaymentALL"] = daily + weekly / 7 + monthly / 30;
                row["Arrears"] = totalPaidD - (double)row["Total_Due"];

                int termM = Convert.ToInt32(row["Term_in_Months"] ?? 0);
                double loanBalance = deposit + daily * 30 * termM + weekly * (30.0 / 7) * termM + monthly * termM - totalPaidD;
                row["Loan_Balance"] = loanBalance;

                row["Curr_Run_Time"] = now;

                double unitsLeft = daily + weekly / 7 + monthly / 30 > 0
                    ? Convert.ToDouble(row["Arrears"]) / (double)row["DailyPaymentALL"]
                    : 0;
                row["Units_Left"] = unitsLeft;

                var autoLock = now.AddDays(unitsLeft);
                row["Auto_lock_date_pmt"] = autoLock;
                row["Datechar"] = autoLock.ToString("dd/MM/yyyyTHH:mm:ss");
                row["Lock_Status_Pmt"] = (Convert.ToDouble(row["Arrears"]) > 0) ? "unlocked" : "complete";

                // Live_flag, NotPaying7D, etc
                DateTime dtEnrolled = TryParseDateTime(p["enrolled_on"]) ?? now;
                DateTime dtFirst = dtFirstPaid.Value;
                DateTime dateEnrolledDate = dtFirst.Date;
                DateTime dJoinedPaid = dtFirst.Date;
                row["Date_enrolled"] = dateEnrolledDate;
                row["enrolled_dt"] = DateTime.Parse(p["enrolled_on"].ToString());
                row["paid_dt"] = dJoinedPaid;

                row["Lag_days"] = (dJoinedPaid - DateTime.Parse(p["enrolled_on"].ToString())).TotalDays;
                row["Comms"] = 2500;
                double arrearsAmt = Math.Min(0, Convert.ToDouble(row["Arrears"]));
                row["Arrears_amt"] = arrearsAmt;

                DateTime lastSunday = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek));
                DateTime threshold = lastSunday.AddDays(-7);
                row["SaleWeek"] = dateEnrolledDate.AddDays((7 - (int)dateEnrolledDate.DayOfWeek) % 7);
                row["Live_flag"] = dateEnrolledDate <= threshold ? "Live" : "Cooling_Off";

                if (dtLastPaid.HasValue)
                    row["NotPaying7D"] = (now - dtLastPaid.Value).TotalDays > 7 ? 1 : 0;

                dt.Rows.Add(row);
            }

            // 5. Join with DealerInfo
            dt.Columns.Add("Device_Groups", typeof(string));
            dt.Columns.Add("Marketing", typeof(double));
            foreach (DataRow row in dt.Rows)
            {
                int dg = Convert.ToInt32(row["device_group_id"]);
                var match = DealerInfo.AsEnumerable()
                    .FirstOrDefault(r => r.Field<int>("Dealer_Number") == dg);
                if (match != null)
                {
                    row["Device_Groups"] = match["Device_Groups"];
                    row["Marketing"] = Convert.ToDouble(match["Marketing"] ?? 0);
                }
                else
                {
                    row["Device_Groups"] = "Ranaloans";
                    row["Marketing"] = 0;
                }
            }

            return dt;
        }

        private DateTime? TryParseDateTime(object obj)
        {
            if (obj == null || obj == DBNull.Value) return null;
            if (DateTime.TryParse(obj.ToString(), out var dt)) return dt;
            return null;
        }

        private double ToDouble(object obj)
        {
            if (obj == null || obj == DBNull.Value) return 0;
            return Convert.ToDouble(obj);
        }
    }
}
