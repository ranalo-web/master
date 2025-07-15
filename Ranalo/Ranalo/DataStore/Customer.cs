using Ranalo.DataStore.DataModels;
using System.ComponentModel.DataAnnotations;

namespace Ranalo.DataStore
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int DealerId { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(100)]
        public string? UserName { get; set; }

        public DateTime? LastActive { get; set; }

        public DateTime DateRegistered { get; set; }

        [MaxLength(150)]
        [EmailAddress]
        public string? Email { get; set; }

        public int TotalOrders { get; set; }

        public decimal TotalSpend { get; set; }

        public decimal Aov { get; set; }

        [MaxLength(100)]
        public string? CountryRegion { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Region { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public Dealer? Dealer { get; set; }
    }
}