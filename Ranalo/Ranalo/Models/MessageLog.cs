namespace Ranalo.Models
{
    public class MessageLog
    {
        public Guid Id { get; set; }
        public string? AccountNo { get; set; }
        public string? MessageType { get; set; }
        public string? Message { get; set; }
        public DateTime? DateSent { get; set; }
        public string? MessageStatus { get; set; }
        public string? MessageError { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
