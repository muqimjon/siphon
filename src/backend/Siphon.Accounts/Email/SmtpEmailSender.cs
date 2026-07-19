using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Siphon.Accounts.Email;

public sealed class SmtpEmailSender(IOptions<AccountsOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly AccountsOptions.SmtpOptions _smtp = options.Value.Smtp;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (!_smtp.Enabled)
        {
            logger.LogInformation("[dev-email] to={To} subject={Subject} body={Body}", to, subject, body);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_smtp.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host!, _smtp.Port, SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(_smtp.User))
            await client.AuthenticateAsync(_smtp.User, _smtp.Password ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
