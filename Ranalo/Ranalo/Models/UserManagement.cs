using Ranalo.DataStore.DataModels;

namespace Ranalo.Models
{
    public class EditUserForm
    {
        public string? Name { get; set; }
        public string? LastName { get; set; }
        public string? KnownAs { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public UserRole RoleId { get; set; }
        public int DealerId { get; set; }
    }

    public class EditUserViewModel
    {
        public User User { get; set; } = null!;
        public IReadOnlyList<UserRole> AssignableRoles { get; set; } = Array.Empty<UserRole>();
        public List<Ranalo.DataStore.Dealer> Dealers { get; set; } = new();
    }

    // My Account: what people can change about themselves.
    public class AccountForm
    {
        public string? Name { get; set; }
        public string? LastName { get; set; }
        public string? KnownAs { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
    }

    public class AccountViewModel
    {
        public User User { get; set; } = null!;
        public string? DealerName { get; set; }
    }
}
