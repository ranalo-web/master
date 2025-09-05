namespace Ranalo.Models
{
    public class CustomerNote
    {
        public Guid Id { get; set; }
        public long OrderId { get; set; }
        public int UserId { get; set; }
        public string Note { get; set; }
        public DateTime Created { get; set; }
        public string? UserName { get; set; }
    }
}
