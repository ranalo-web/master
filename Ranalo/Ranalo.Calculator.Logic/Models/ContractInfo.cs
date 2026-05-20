using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ranalo.Calculator.Logic.Models
{
    public class ContractInfo
    {
        public int ContractID { get; set; }
        public int ID { get; set; }
        public decimal Deposit { get; set; }
        public decimal Daily { get; set; }
        public decimal Weekly { get; set; }
        public decimal Monthly { get; set; }
        public string RePaymentIntervals { get; set; }
        public decimal TermInMonths { get; set; } = 12.00000m;
        public decimal TotalLoan { get; set; }
        public decimal TotalCost { get; set; }
        public string FirstName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? BuyingPrice { get; set; }
    }
}
