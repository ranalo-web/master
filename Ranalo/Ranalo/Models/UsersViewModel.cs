using Ranalo.DataStore.DataModels;

namespace Ranalo.Models
{
    public class UsersViewModel
    {
        public List<User>? Users { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
    }
}
