using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("uploads")]
[Authorize]
public sealed class FilesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly IFileStorageService _fileStorage;

    public FilesController(AppDbContext context, TenantService tenant, IFileStorageService fileStorage)
    {
        _context = context;
        _tenant = tenant;
        _fileStorage = fileStorage;
    }

    [HttpGet("{category}/{fileName}")]
    public async Task<IActionResult> GetUploadedFile(string category, string fileName)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var resolved = _fileStorage.ResolveFile(category, fileName);
        if (resolved == null)
            return NotFound();

        var safeName = Path.GetFileName(fileName);
        var normalizedCategory = category.ToLowerInvariant();
        var relativeUrl = $"/uploads/{normalizedCategory}/{safeName}";

        bool allowed = normalizedCategory switch
        {
            "profiles" => await CanAccessProfileFileAsync(userId, safeName, relativeUrl),
            "organizations" => await CanAccessOrganizationFileAsync(userId, relativeUrl),
            "leaves" => await CanAccessLeaveFileAsync(userId, relativeUrl),
            "chat" => await CanAccessChatFileAsync(safeName, relativeUrl),
            "boards" => await CanAccessBoardFileAsync(relativeUrl),
            _ => false
        };

        if (!allowed)
            return this.ForbiddenMessage("You do not have permission to access this file.");

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=300";

        if (resolved.AllowInline)
        {
            return PhysicalFile(resolved.FullPath, resolved.ContentType);
        }

        return PhysicalFile(resolved.FullPath, resolved.ContentType, safeName);
    }

    private async Task<bool> CanAccessProfileFileAsync(string callerUserId, string fileName, string relativeUrl)
    {
        if (fileName.StartsWith(callerUserId + "_", StringComparison.Ordinal))
            return true;

        var targetUserId = await _context.Users
            .AsNoTracking()
            .Where(u => u.ProfilePictureUrl == relativeUrl)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(targetUserId))
        {
            var underscoreIdx = fileName.IndexOf('_');
            if (underscoreIdx > 0)
                targetUserId = fileName[..underscoreIdx];
        }

        if (string.IsNullOrEmpty(targetUserId))
            return false;

        if (string.Equals(targetUserId, callerUserId, StringComparison.Ordinal))
            return true;

        var callerOrgIds = _context.Employees
            .Where(e => e.UserId == callerUserId && e.IsActive)
            .Select(e => e.OrganizationId);

        return await _context.Employees
            .AsNoTracking()
            .AnyAsync(e => e.UserId == targetUserId && callerOrgIds.Contains(e.OrganizationId));
    }

    private async Task<bool> CanAccessOrganizationFileAsync(string callerUserId, string relativeUrl)
    {
        var orgId = await _context.Organizations
            .AsNoTracking()
            .Where(o => o.LogoUrl == relativeUrl)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync();

        if (orgId == null)
            return false;

        if (await _context.Employees.AsNoTracking().AnyAsync(e => e.OrganizationId == orgId.Value && e.UserId == callerUserId && e.IsActive))
            return true;

        var callerEmail = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        return await _context.OrganizationInvitations
            .AsNoTracking()
            .AnyAsync(i => i.OrganizationId == orgId.Value
                        && i.ExpiresAt > DateTime.UtcNow
                        && !i.IsAccepted
                        && (string.IsNullOrEmpty(i.Email) || (callerEmail != null && i.Email.ToLower() == callerEmail.ToLower())));
    }

    private async Task<bool> CanAccessLeaveFileAsync(string callerUserId, string relativeUrl)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return false;

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return false;

        var leave = await _context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.OrganizationId == orgId.Value && r.AttachmentPath == relativeUrl)
            .Select(r => new { r.EmployeeId })
            .FirstOrDefaultAsync();

        if (leave == null) return false;
        if (leave.EmployeeId == employee.Id) return true;

        if (await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId.Value))
            return true;

        return User.HasClaim("ModuleAccess", AccessModules.LeaveApprovals)
            || User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.LeaveApprovalsView)
            || User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.LeaveApprovalsManage);
    }

    private async Task<bool> CanAccessChatFileAsync(string safeName, string relativeUrl)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (orgId == null || employee == null) return false;

        // 1. Direct upload / group picture match where user is member of the originating room
        Guid? originRoomId = TryParsePrefixedRoomId(safeName, "chat_") ?? TryParsePrefixedRoomId(safeName, "group_");
        if (originRoomId != null)
        {
            var inOriginRoom = await _context.ChatRoomMembers
                .AsNoTracking()
                .AnyAsync(m => m.ChatRoomId == originRoomId.Value
                            && m.OrganizationId == orgId.Value
                            && m.UserId == employee.Id);
            if (inOriginRoom) return true;
        }

        // 2. Forwarded chat message attachments or stored group picture URLs in any room caller belongs to
        return await _context.ChatMessages
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId.Value
                        && m.AttachmentUrl == relativeUrl
                        && m.ChatRoom != null
                        && m.ChatRoom.Members.Any(cm => cm.UserId == employee.Id))
            || await _context.ChatRooms
                .AsNoTracking()
                .AnyAsync(r => r.OrganizationId == orgId.Value
                            && r.PictureUrl == relativeUrl
                            && r.Members.Any(cm => cm.UserId == employee.Id));
    }

    private async Task<bool> CanAccessBoardFileAsync(string relativeUrl)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (orgId == null || employee == null) return false;

        var boardId = await _context.BoardCardAttachments
            .AsNoTracking()
            .Where(a => a.OrganizationId == orgId.Value && a.FilePath == relativeUrl)
            .Select(a => a.Card != null && a.Card.List != null ? (Guid?)a.Card.List.BoardId : null)
            .FirstOrDefaultAsync();

        if (boardId == null) return false;

        if (await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId.Value))
            return true;

        return await _context.BoardMembers
            .AsNoTracking()
            .AnyAsync(m => m.BoardId == boardId.Value && m.UserId == employee.Id);
    }

    private static Guid? TryParsePrefixedRoomId(string fileName, string prefix)
    {
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var rest = fileName.AsSpan(prefix.Length);
        var sep = rest.IndexOf('_');
        if (sep <= 0) return null;

        return Guid.TryParse(rest[..sep], out var id) ? id : null;
    }
}
