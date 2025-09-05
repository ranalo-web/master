namespace Ranalo.Woocommece.Api.Models
{
    public class Contact
    {
        public Guid Id { get; set; }
        public long OrderId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }
}
