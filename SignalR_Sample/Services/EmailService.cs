namespace SignalR_Sample.Services
{
    using SendGrid;
    using SendGrid.Helpers.Mail;
    using System;
    using System.Threading.Tasks;

    public class EmailService : IEmailService
    {
        private readonly string _sendGridKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _sendGridKey = configuration.GetValue<string>("SendGridApi");
            _fromEmail = configuration.GetValue<string>("SendGridSenderEmail");
            _fromName = configuration.GetValue<string>("SendGridSenderName");
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var client = new SendGridClient(_sendGridKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail);
            var plainTextContent = message;
            var htmlContent = $"<strong>{message}</strong>";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                throw new InvalidOperationException("Failed to send email.");
            }
        }
    }
}
