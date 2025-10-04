using Ranalo.Calculator.Logic.Models;

namespace Ranalo.Models
{
    public class ContractViewModel
    {
        public List<ContractInfo>? Contracts { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; } = 10;
    }
}
