using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly AppDbContext _context;
    private readonly Services.TenantService _tenant;
    private readonly Services.ISmsOtpService _smsOtpService;
    private readonly Services.IEmailOtpService _emailOtpService;
    private readonly IFileStorageService _fileStorage;
    private readonly IWorkspaceAuthorizationService _workspaceAuth;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<AppRole> roleManager,
        AppDbContext context,
        Services.TenantService tenant,
        Services.ISmsOtpService smsOtpService,
        Services.IEmailOtpService emailOtpService,
        IFileStorageService? fileStorage = null,
        IWorkspaceAuthorizationService? workspaceAuth = null)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tenant = tenant;
        _smsOtpService = smsOtpService;
        _emailOtpService = emailOtpService;
        _fileStorage = fileStorage ?? new LocalFileStorageService();
        _workspaceAuth = workspaceAuth ?? new WorkspaceAuthorizationService(context);
    }

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "Invalid login attempt or account disabled." });

        // lockoutOnFailure: true activates the 5-attempt / 5-minute lockout configured in Identity options
        var result = await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        
        if (result.Succeeded)
            return Ok(new { message = "Login successful" });

        if (result.IsLockedOut)
            return StatusCode(429, new { message = "Account locked due to too many failed attempts. Please try again in 5 minutes." });

        return Unauthorized(new { message = "Invalid login attempt." });
    }

    [HttpPost("send-otp")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { message = "Email or phone number is required." });

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest(new { message = "An account with this email address already exists." });

            var (success, message, _) = await _emailOtpService.SendOtpAsync(request.Email);
            if (!success)
                return BadRequest(new { message });

            return Ok(new 
            { 
                message,
                email = Services.EmailOtpService.NormalizeEmail(request.Email)
            });
        }
        else
        {
            var (success, message, _) = await _smsOtpService.SendOtpAsync(request.PhoneNumber!);
            if (!success)
                return BadRequest(new { message });

            return Ok(new 
            { 
                message,
                phoneNumber = Services.SmsOtpService.NormalizePhoneNumber(request.PhoneNumber!)
            });
        }
    }

    [HttpPost("verify-otp")]
    [EnableRateLimiting("LoginPolicy")]
    public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Verification code is required." });

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            bool isValid = _emailOtpService.VerifyOtp(request.Email, request.Code);
            if (!isValid)
                return BadRequest(new { message = "Invalid or expired email verification code." });

            return Ok(new { message = "Email address verified successfully.", verified = true });
        }
        else if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            bool isValid = _smsOtpService.VerifyOtp(request.PhoneNumber, request.Code);
            if (!isValid)
                return BadRequest(new { message = "Invalid or expired verification code." });

            return Ok(new { message = "Phone number verified successfully.", verified = true });
        }

        return BadRequest(new { message = "Email or phone number is required." });
    }

    [HttpPost("register")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return BadRequest("User already exists with this email address.");

        // Validate Email OTP verification
        bool isEmailVerified = _emailOtpService.IsEmailVerified(request.Email, request.OtpCode);
        if (!isEmailVerified)
        {
            return BadRequest("Please verify your email address using the 6-digit verification code first.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            PhoneNumber = !string.IsNullOrWhiteSpace(request.PhoneNumber) ? Services.SmsOtpService.NormalizePhoneNumber(request.PhoneNumber) : null,
            PhoneNumberConfirmed = false,

            FirstName = TextHelper.ToProperCase(request.FirstName),
            LastName = TextHelper.ToProperCase(request.LastName),
            MustChangePassword = false // It's their own password, so they don't need to change it on first login
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));

        // Sign the user in immediately after registration
        await _signInManager.SignInAsync(user, isPersistent: false);

        return Ok(new { message = "Registration successful" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Logout successful" });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return Unauthorized();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var access = orgId.HasValue
            ? await _workspaceAuth.GetAccessAsync(user.Id, orgId.Value)
            : WorkspaceAccessContext.Empty;

        var moduleClaims = access.IsActiveMember
            ? access.Modules
            : ModuleCatalog.DefaultKeys;
        var defaultTitle = access.IsOwner ? "Owner" : "Employee";
        var titleRole = access.TitleRoles.Any() ? string.Join(" / ", access.TitleRoles) : defaultTitle;

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            profilePictureUrl = user.ProfilePictureUrl,
            mustChangePassword = user.MustChangePassword,
            roles = access.Roles,
            titleRole = titleRole,
            modules = moduleClaims,
            permissions = access.Permissions
        });
    }

    [Authorize]
    [HttpPost("profile-picture")]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var allowedImageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();
        var fileName = $"{user.Id}_{Guid.NewGuid()}{ext}";

        var stored = await _fileStorage.ValidateAndSaveAsync(file, "profiles", fileName, allowedImageExts, 10 * 1024 * 1024);
        if (!stored.Success || stored.RelativeUrl == null)
            return BadRequest(stored.ErrorMessage ?? "Invalid profile picture.");

        user.ProfilePictureUrl = stored.RelativeUrl;
        await _userManager.UpdateAsync(user);

        return Ok(new { profilePictureUrl = user.ProfilePictureUrl });
    }

    [Authorize]
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

        // Re-sign in to refresh the security stamp/cookie
        await _signInManager.RefreshSignInAsync(user);

        return Ok(new { message = "Password changed successfully" });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.FirstName = TextHelper.ToProperCase(request.FirstName);
        user.LastName = TextHelper.ToProperCase(request.LastName);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "Profile updated successfully" });
    }
}

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public bool RememberMe { get; set; }
}

public class RegisterRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? PhoneNumber { get; set; }
    public string? OtpCode { get; set; }
}

public class SendOtpRequest
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

public class VerifyOtpRequest
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string Code { get; set; }
}


public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}

public class UpdateProfileRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}
