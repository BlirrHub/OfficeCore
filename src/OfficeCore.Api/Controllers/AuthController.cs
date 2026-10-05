using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

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

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<AppRole> roleManager,
        AppDbContext context,
        Services.TenantService tenant,
        Services.ISmsOtpService smsOtpService,
        Services.IEmailOtpService emailOtpService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tenant = tenant;
        _smsOtpService = smsOtpService;
        _emailOtpService = emailOtpService;
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
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { message = "Email or phone number is required." });

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest(new { message = "An account with this email address already exists." });

            var (success, message, debugCode) = await _emailOtpService.SendOtpAsync(request.Email);
            if (!success)
                return BadRequest(new { message });

            return Ok(new 
            { 
                message,
                debugCode,
                email = Services.EmailOtpService.NormalizeEmail(request.Email)
            });
        }
        else
        {
            var (success, message, debugCode) = await _smsOtpService.SendOtpAsync(request.PhoneNumber!);
            if (!success)
                return BadRequest(new { message });

            return Ok(new 
            { 
                message,
                debugCode,
                phoneNumber = Services.SmsOtpService.NormalizePhoneNumber(request.PhoneNumber!)
            });
        }
    }

    [HttpPost("verify-otp")]
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

            FirstName = request.FirstName,
            LastName = request.LastName,
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

        // Gather role claims (module access) and find TitleRoles from Organization roles
        var moduleClaims = new List<string> { "Dashboard", "MyAttendance", "MyOvertime", "MyLeaves", "Advances", "Boards" };
        var titleRoles = new List<string>();
        var roles = new List<string>();
        bool isOwner = false;

        var orgId = _tenant.GetActiveOrganizationId();

        if (orgId.HasValue)
        {
            // Check if workspace owner — grant all modules
            var org = await _context.Organizations.FindAsync(orgId.Value);
            isOwner = org != null && org.CreatedByUserId == user.Id;
            if (isOwner)
            {
                moduleClaims = new List<string>
                {
                    "Dashboard", "MyAttendance", "MyOvertime", "MyLeaves", "Advances", "Boards",
                    "Clients", "Attendance", "Ledger", "Payroll", "Invoices",
                    "Users", "Roles", "LeaveApprovals", "OvertimeApprovals", "WorkSchedules", "TimeOff",
                    "Locations", "Organization", "AuditLogs"
                };
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == user.Id && e.OrganizationId == orgId.Value);

            if (employee != null)
            {
                var employeeRoles = await _context.EmployeeRoles
                    .Include(er => er.Role)
                        .ThenInclude(r => r!.Claims)
                    .Where(er => er.EmployeeId == employee.Id)
                    .ToListAsync();

                foreach (var er in employeeRoles)
                {
                    if (er.Role == null) continue;
                    
                    roles.Add(er.Role.Name);

                    if (er.Role.ShowAsTitle && !string.IsNullOrEmpty(er.Role.Name))
                    {
                        titleRoles.Add(er.Role.Name);
                    }

                    moduleClaims.AddRange(er.Role.Claims
                        .Where(c => c.ClaimType == "ModuleAccess")
                        .Select(c => c.ClaimValue));
                }
            }
        }

        var defaultTitle = isOwner ? "Owner" : "Employee";
        var titleRole = titleRoles.Any() ? string.Join(" / ", titleRoles) : defaultTitle;

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            profilePictureUrl = user.ProfilePictureUrl,
            mustChangePassword = user.MustChangePassword,
            roles = roles,
            titleRole = titleRole,
            modules = moduleClaims.Distinct()
        });
    }

    [HttpPost("profile-picture")]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (file == null || file.Length == 0) return BadRequest("File is empty");

        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{user.Id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        user.ProfilePictureUrl = $"/uploads/profiles/{fileName}";
        await _userManager.UpdateAsync(user);

        return Ok(new { profilePictureUrl = user.ProfilePictureUrl });
    }

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

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;

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
