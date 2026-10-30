using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace OfficeCore.Api.Services;

public interface ISmsOtpService
{
    Task<(bool Success, string Message, string? DebugCode)> SendOtpAsync(string phoneNumber);
    bool VerifyOtp(string phoneNumber, string code);
    bool IsPhoneVerified(string phoneNumber, string? code = null);
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
    // Cache for phone numbers that have been successfully verified within the last 15 minutes
    private static readonly ConcurrentDictionary<string, DateTime> _verifiedPhones = new();


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
            Attempts = _otpStore.TryGetValue(normalized, out var existingEntry) && DateTime.UtcNow <= existingEntry.ExpiresAt ? existingEntry.Attempts : 0
        };

        _otpStore[normalized] = entry;

        _logger.LogInformation("====================================================");
        _logger.LogDebug("📱 [SMS OTP Verification] To: {PhoneNumber} | Code: {Code} | Valid for 5 mins", normalized, code);
        _logger.LogInformation("====================================================");

        return Task.FromResult<(bool, string, string?)>((true, "Verification code sent to your phone.", null));
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
                // Record verified status for 15 minutes
                _verifiedPhones[normalized] = DateTime.UtcNow.AddMinutes(15);
                // Remove upon successful verification
                _otpStore.TryRemove(normalized, out _);
                return true;
            }
        }

        return false;
    }

    public bool IsPhoneVerified(string phoneNumber, string? code = null)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        // If it was already verified within the last 15 minutes, accept
        if (_verifiedPhones.TryGetValue(normalized, out var expiresAt))
        {
            if (DateTime.UtcNow <= expiresAt)
            {
                return true;
            }
            _verifiedPhones.TryRemove(normalized, out _);
        }

        // If a code is provided, try verifying against the active OTP store as fallback
        if (!string.IsNullOrWhiteSpace(code))
        {
            return VerifyOtp(phoneNumber, code);
        }

        return false;
    }
}

