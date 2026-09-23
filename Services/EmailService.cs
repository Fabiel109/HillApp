using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;

namespace HillApp.Services;

public sealed class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private string Provider => (_configuration["Email:Provider"] ?? "Smtp").Trim();

    public bool IsConfigured
    {
        get
        {
            if (Provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(_configuration["Email:ApiKey"]) &&
                       !string.IsNullOrWhiteSpace(_configuration["Email:FromEmail"]) &&
                       !string.IsNullOrWhiteSpace(_configuration["Email:ToEmail"]);
            }

            return !string.IsNullOrWhiteSpace(_configuration["Email:Host"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Email:Username"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Email:Password"]) &&
                   !string.IsNullOrWhiteSpace(_configuration["Email:ToEmail"]);
        }
    }

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

        return Provider.Equals("Resend", StringComparison.OrdinalIgnoreCase)
            ? await SendWithResendAsync(username, userEmail, feedbackType, message)
            : await SendWithSmtpAsync(username, userEmail, feedbackType, message);
    }

    private static string BuildBody(
        string username,
        string userEmail,
        string feedbackType,
        string message)
    {
        return new StringBuilder()
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
    }

    private async Task<bool> SendWithResendAsync(
        string username,
        string userEmail,
        string feedbackType,
        string message)
    {
        try
        {
            var apiKey = _configuration["Email:ApiKey"]!;
            var fromEmail = _configuration["Email:FromEmail"]!;
            var toEmail = _configuration["Email:ToEmail"]!;
            var body = BuildBody(username, userEmail, feedbackType, message);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new
            {
                from = fromEmail,
                to = new[] { toEmail },
                subject = $"[HillApp - {feedbackType}] Comentario de {username}",
                text = body,
                reply_to = userEmail
            });

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            using var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return true;

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Resend rechazó el correo. Código HTTP: {StatusCode}. Respuesta: {Response}",
                (int)response.StatusCode,
                responseBody);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogError(ex, "No fue posible enviar el comentario mediante Resend.");
            return false;
        }
    }

    private async Task<bool> SendWithSmtpAsync(
        string username,
        string userEmail,
        string feedbackType,
        string message)
    {
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

            var body = BuildBody(username, userEmail, feedbackType, message);

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
            _logger.LogError(ex, "No fue posible enviar el comentario por SMTP.");
            return false;
        }
    }
}
