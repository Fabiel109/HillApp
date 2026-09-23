using System.Net;
using System.Net.Mail;
using System.Text;

namespace HillApp.Services;

public sealed class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Email:Host"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Email:Username"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Email:Password"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Email:ToEmail"]);

    public async Task<bool> SendFeedbackAsync(
        string username,
        string userEmail,
        string feedbackType,
        string message)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("El correo de comentarios no está configurado.");
            return false;
        }

        try
        {
            var host = _configuration["Email:Host"]!;
            var port = int.TryParse(_configuration["Email:Port"], out var configuredPort)
                ? configuredPort
                : 587;
            var smtpUsername = _configuration["Email:Username"]!;
            var smtpPassword = _configuration["Email:Password"]!;
            var toEmail = _configuration["Email:ToEmail"]!;
            var fromEmail = _configuration["Email:FromEmail"];
            if (string.IsNullOrWhiteSpace(fromEmail))
                fromEmail = smtpUsername;

            var body = new StringBuilder()
                .AppendLine("Nuevo comentario recibido desde HillApp / Récords HCR2")
                .AppendLine()
                .AppendLine($"Tipo: {feedbackType}")
                .AppendLine($"Usuario: {username}")
                .AppendLine($"Correo registrado: {userEmail}")
                .AppendLine($"Fecha UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
                .AppendLine()
                .AppendLine("Mensaje:")
                .AppendLine(message)
                .ToString();

            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, "HillApp HCR2"),
                Subject = $"[HillApp - {feedbackType}] Comentario de {username}",
                Body = body,
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);
            if (MailAddress.TryCreate(userEmail, out var replyTo))
                mail.ReplyToList.Add(replyTo);

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15_000
            };

            await smtp.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "No fue posible enviar el comentario por correo.");
            return false;
        }
    }
}
