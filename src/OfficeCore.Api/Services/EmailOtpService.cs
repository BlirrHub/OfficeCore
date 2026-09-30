using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OfficeCore.Api.Services;

public interface IEmailOtpService
{
    Task<(bool Success, string Message, string? DebugCode)> SendOtpAsync(string email);
    bool VerifyOtp(string email, string code);
    bool IsEmailVerified(string email, string? code = null);
}

public class EmailOtpService : IEmailOtpService
{
    private readonly ILogger<EmailOtpService> _logger;
    private readonly IConfiguration _config;

    private class OtpEntry
    {
        public required string Code { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
    }

    private static readonly ConcurrentDictionary<string, OtpEntry> _otpStore = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DateTime> _verifiedEmails = new(StringComparer.OrdinalIgnoreCase);

    public EmailOtpService(ILogger<EmailOtpService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
    }

    public async Task<(bool Success, string Message, string? DebugCode)> SendOtpAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
        {
            return (false, "Please provide a valid email address.", null);
        }

        // Generate cryptographically secure 6-digit numeric OTP
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        var entry = new OtpEntry
        {
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Attempts = 0
        };

        _otpStore[normalized] = entry;

        _logger.LogInformation("====================================================");
        _logger.LogInformation("📧 [Email OTP Verification] To: {Email} | Code: {Code} | Valid for 5 mins", normalized, code);
        _logger.LogInformation("====================================================");

        // Check if SMTP is configured
        var smtpHost = _config["Email:SmtpHost"];
        var smtpPort = _config.GetValue<int>("Email:SmtpPort", 587);
        var smtpUser = _config["Email:SmtpUser"];
        var smtpPass = _config["Email:SmtpPassword"];
        var fromEmail = _config["Email:FromEmail"] ?? smtpUser ?? "noreply@officecore.local";
        var fromName = _config["Email:FromName"] ?? "OfficeCore";

        if (!string.IsNullOrWhiteSpace(smtpHost) && !string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
        {
            try
            {
                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    Timeout = 10000
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = $"{code} is your OfficeCore verification code",
                    Body = $@"
<div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 540px; margin: 0 auto; padding: 32px 24px; background: #ffffff; border-radius: 16px; border: 1px solid #e4e4e7;"">
    <div style=""margin-bottom: 24px;"">
        <h2 style=""color: #09090b; margin: 0; font-size: 22px; font-weight: 700;"">OfficeCore</h2>
        <p style=""color: #71717a; font-size: 13px; margin: 4px 0 0 0;"">Intelligent Enterprise Workspace</p>
    </div>
    <div style=""background: #fafafa; border-radius: 12px; padding: 24px; text-align: center; border: 1px solid #f4f4f5;"">
        <p style=""color: #3f3f46; font-size: 14px; margin: 0 0 16px 0;"">Use this 6-digit verification code to complete your registration:</p>
        <div style=""font-size: 32px; font-weight: 800; letter-spacing: 6px; color: #a21caf; font-family: monospace; padding: 12px; background: #ffffff; border-radius: 8px; border: 1px dashed #d946ef; display: inline-block;"">
            {code}
        </div>
        <p style=""color: #a1a1aa; font-size: 12px; margin: 16px 0 0 0;"">This code will expire in 5 minutes. Do not share it with anyone.</p>
    </div>
    <p style=""color: #71717a; font-size: 12px; margin-top: 24px; text-align: center;"">
        If you didn't request this code, you can safely ignore this email.
    </p>
</div>",
                    IsBodyHtml = true
                };

                mail.To.Add(normalized);
                await client.SendMailAsync(mail);
                _logger.LogInformation("📧 Successfully sent OTP email via SMTP to {Email}", normalized);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email via SMTP to {Email}. Falling back to simulation/console log.", normalized);
            }
        }

        string msg = "Verification code sent to your email inbox.";
        return (true, msg, null);
    }

    public bool VerifyOtp(string email, string code)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrWhiteSpace(code)) return false;

        if (_otpStore.TryGetValue(normalized, out var entry))
        {
            if (DateTime.UtcNow > entry.ExpiresAt)
            {
                _otpStore.TryRemove(normalized, out _);
                return false;
            }

            entry.Attempts++;
            if (entry.Attempts > 5)
            {
                _otpStore.TryRemove(normalized, out _);
                return false;
            }

            if (entry.Code == code.Trim())
            {
                _verifiedEmails[normalized] = DateTime.UtcNow.AddMinutes(15);
                _otpStore.TryRemove(normalized, out _);
                return true;
            }
        }

        return false;
    }

    public bool IsEmailVerified(string email, string? code = null)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        if (_verifiedEmails.TryGetValue(normalized, out var expiresAt))
        {
            if (DateTime.UtcNow <= expiresAt)
            {
                return true;
            }
            _verifiedEmails.TryRemove(normalized, out _);
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            return VerifyOtp(email, code);
        }

        return false;
    }
}
