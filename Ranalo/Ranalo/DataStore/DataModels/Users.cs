using System.Data;

namespace Ranalo.DataStore.DataModels
{
    public class User
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public int RoleId { get; set; }
        public string? KnownAs { get; set; }
        public Role Role { get; set; }
        public ICollection<Dealer> Dealers { get; set; } = new List<Dealer>();
    }
}
