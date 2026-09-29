using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace OfficeCore.Api.Services;

public interface ISmsOtpService
{
    Task<(bool Success, string Message, string? DebugCode)> SendOtpAsync(string phoneNumber);
    bool VerifyOtp(string phoneNumber, string code);
}

public class SmsOtpService : ISmsOtpService
{
    private readonly ILogger<SmsOtpService> _logger;

    private class OtpEntry
    {
        public required string Code { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
    }

    // In-memory thread-safe store for OTP codes with expiration
    private static readonly ConcurrentDictionary<string, OtpEntry> _otpStore = new();

    public SmsOtpService(ILogger<SmsOtpService> logger)
    {
        _logger = logger;
    }

    public static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        // Handle common Philippine formats: 09171234567 or 639171234567 -> +639171234567
        if (digits.StartsWith("0") && digits.Length == 11)
        {
            return "+63" + digits[1..];
        }
        if (digits.StartsWith("63") && digits.Length == 12)
        {
            return "+" + digits;
        }
        if (phone.StartsWith("+"))
        {
            return "+" + digits;
        }
        return digits;
    }

    public Task<(bool Success, string Message, string? DebugCode)> SendOtpAsync(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 10)
        {
            return Task.FromResult<(bool, string, string?)>((false, "Invalid phone number format.", null));
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
        _logger.LogInformation("📱 [SMS OTP Verification] To: {PhoneNumber} | Code: {Code} | Valid for 5 mins", normalized, code);
        _logger.LogInformation("====================================================");

        // In development / local network demo environments, we return DebugCode so the UI can auto-suggest or test cleanly.
        return Task.FromResult<(bool, string, string?)>((true, "Verification code sent to your phone.", code));
    }

    public bool VerifyOtp(string phoneNumber, string code)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
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
                // Remove upon successful verification
                _otpStore.TryRemove(normalized, out _);
                return true;
            }
        }

        return false;
    }
}
