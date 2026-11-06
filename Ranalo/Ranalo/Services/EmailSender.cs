using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Ranalo.Services
{
    // Outgoing email for system messages, from the no-reply address.
    // Section "Email" in appsettings; the password lives in Azure only
    // (Email__Password). Off until Enabled and Host, From and the login are set.
    public class EmailSettings
    {
        public bool Enabled { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = "noreply@ranalocredit.com";
        public string FromName { get; set; } = "Ranalo";

        public bool Ready => Enabled && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From)
            && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
    }

    public interface IEmailSender
    {
        bool Ready { get; }
        Task SendAsync(string to, string subject, string body);
    }

    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public bool Ready => _settings.Ready;

        public async Task SendAsync(string to, string subject, string body)
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password),
            };
            using var mail = new MailMessage(new MailAddress(_settings.From, _settings.FromName), new MailAddress(to))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            await client.SendMailAsync(mail);
        }
    }
}
