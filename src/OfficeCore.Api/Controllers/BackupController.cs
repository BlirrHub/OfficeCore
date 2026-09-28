using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backupService;
    private readonly TenantService _tenant;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly IAuditService _audit;

    public BackupController(
        BackupService backupService,
        TenantService tenant,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IAuditService audit)
    {
        _backupService = backupService;
        _tenant = tenant;
        _userManager = userManager;
        _context = context;
        _audit = audit;
    }

    private async Task<bool> IsAuthorizedForBackupsAsync()
    {
        // Allow local Server Station host orchestration requests from loopback
        if (Request.Headers.TryGetValue("X-OfficeCore-Station", out var stationHeader) && stationHeader == "ServerStationOrchestrator")
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp != null && System.Net.IPAddress.IsLoopback(remoteIp))
            {
                return true;
            }
        }

        if (User.Identity?.IsAuthenticated != true)
            return false;

        var userId = _userManager.GetUserId(User);
        if (userId == null) return false;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null)
        {
            // If no active workspace is selected, check if user is the creator of any organization
            return await _context.Organizations.AnyAsync(o => o.CreatedByUserId == userId);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == orgId.Value);
        if (org != null && org.CreatedByUserId == userId)
            return true;

        var isAdmin = await _context.EmployeeRoles
            .AnyAsync(er => er.Employee != null && er.Employee.UserId == userId && er.Employee.OrganizationId == orgId.Value &&
                            (er.Role != null && (er.Role.Name == "Admin" || er.Role.Name == "Owner")));

        return isAdmin;
    }

    [HttpGet]
    public async Task<IActionResult> GetBackups()
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to access database backups.");

        var backups = await _backupService.GetBackupsAsync();
        var pgBinPath = _backupService.DetectPgBinPath();
        var settings = _backupService.GetSettings();

        var totalSizeBytes = backups.Sum(b => b.SizeBytes);

        return Ok(new
        {
            Backups = backups,
            PgBinPath = pgBinPath,
            IsPgToolsAvailable = !string.IsNullOrEmpty(pgBinPath),
            TotalSizeBytes = totalSizeBytes,
            Settings = settings
        });
    }

    public class CreateBackupRequest
    {
        public string? Label { get; set; }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest? req)
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to create database backups.");

        var result = await _backupService.CreateBackupAsync(req?.Label, isAutomatic: false);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var backupFileName = result.BackupInfo?.FileName ?? "Manual Backup";
        await _audit.LogBackupAsync(
            AuditActions.Create,
            $"Created manual database snapshot '{(string.IsNullOrWhiteSpace(req?.Label) ? backupFileName : req.Label)}' ({backupFileName})",
            backupFileName);

        return Ok(result);
    }

    [HttpGet("{fileName}/download")]
    public async Task<IActionResult> DownloadBackup(string fileName)
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to download database backups.");

        var filePath = _backupService.GetBackupFilePath(fileName);
        if (filePath == null)
            return NotFound("Backup file not found.");

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var mimeType = "application/octet-stream";
        return File(stream, mimeType, fileName);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(500 * 1024 * 1024)] // 500 MB limit
    public async Task<IActionResult> UploadBackup(IFormFile file)
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to upload database backups.");

        if (file == null || file.Length == 0)
            return BadRequest("Uploaded file is empty.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".dump" && ext != ".sql")
            return BadRequest("Invalid file type. Only .dump and .sql files are accepted.");

        var tempDir = Path.Combine(Path.GetTempPath(), "OfficeCoreUploads");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"{Guid.NewGuid()}{ext}");

        using (var stream = new FileStream(tempFile, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var result = await _backupService.RestoreFromFileAsync(tempFile);

        try { System.IO.File.Delete(tempFile); } catch { }

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message, preRestoreSnapshot = result.PreRestoreSnapshotName });
        }

        return Ok(new
        {
            message = "Backup uploaded and restored successfully.",
            preRestoreSnapshot = result.PreRestoreSnapshotName
        });
    }

    [HttpPost("{fileName}/restore")]
    public async Task<IActionResult> RestoreBackup(string fileName)
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to restore database backups.");

        var result = await _backupService.RestoreBackupAsync(fileName);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message, preRestoreSnapshot = result.PreRestoreSnapshotName });
        }

        await _audit.LogBackupAsync(
            AuditActions.Restore,
            $"Restored database from snapshot '{fileName}' (Safety pre-restore backup: {result.PreRestoreSnapshotName})",
            fileName);

        return Ok(new
        {
            message = "Database restored successfully.",
            preRestoreSnapshot = result.PreRestoreSnapshotName
        });
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeleteBackup(string fileName)
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to delete database backups.");

        var deleted = await _backupService.DeleteBackupAsync(fileName);
        if (!deleted)
            return NotFound("Backup file not found or could not be deleted.");

        await _audit.LogBackupAsync(
            AuditActions.Delete,
            $"Deleted database backup archive '{fileName}'",
            fileName);

        return Ok(new { message = "Backup deleted successfully." });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to view backup settings.");

        var settings = _backupService.GetSettings();
        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] BackupSettings settings)
    {
        if (!await IsAuthorizedForBackupsAsync())
            return Forbid("Administrative privileges required to update backup settings.");

        await _backupService.SaveSettingsAsync(settings);
        return Ok(new { message = "Backup settings updated successfully." });
    }
}
