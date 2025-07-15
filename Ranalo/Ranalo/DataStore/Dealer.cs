using Ranalo.DataStore.DataModels;
using System.ComponentModel.DataAnnotations;

namespace Ranalo.DataStore
{
    public class Dealer
    {
        [Key]
        public int DealerId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string DealerReference { get; set; }

        [Required]
        [StringLength(200)]
        public string CompanyName { get; set; }

        [StringLength(300)]
        public string Address { get; set; }

        [Phone]
        public string Phone { get; set; }

        public bool Marketing { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        // Navigation property
        public User User { get; set; }
    }
}