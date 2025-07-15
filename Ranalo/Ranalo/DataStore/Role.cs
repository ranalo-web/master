using System.ComponentModel.DataAnnotations;

namespace Ranalo.DataStore
{
    public class Role
    {
        [Key]
        public int RoleId { get; set; }
    }
}