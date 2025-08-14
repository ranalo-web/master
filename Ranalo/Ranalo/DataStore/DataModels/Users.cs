using System.Data;

namespace Ranalo.DataStore.DataModels
{
    public class User
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public UserRole RoleId { get; set; }
        public int DealerId { get; set; }
        public string? KnownAs { get; set; }
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public string? LastName { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastLogin { get; set; }
        public string? City { get; set; }
        public string? Phone { get; set; }

        public Role Role { get; set; }
        public ICollection<Dealer> Dealers { get; set; } = new List<Dealer>();
    }
}
