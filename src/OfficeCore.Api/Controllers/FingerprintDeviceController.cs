using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class FingerprintDeviceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public FingerprintDeviceController(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetDevice()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var device = await _context.FingerprintDevices
            .FirstOrDefaultAsync(d => d.OrganizationId == orgId.Value);

        if (device == null) return NotFound();

        return Ok(device);
    }

    [HttpPost]
    public async Task<IActionResult> SaveDevice(FingerprintDeviceDto request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var device = await _context.FingerprintDevices
            .FirstOrDefaultAsync(d => d.OrganizationId == orgId.Value);

        if (device == null)
        {
            device = new FingerprintDevice
            {
                OrganizationId = orgId.Value
            };
            _context.FingerprintDevices.Add(device);
        }

        device.Name = request.Name;
        device.ConnectionType = request.ConnectionType;
        device.IpAddress = request.IpAddress;
        device.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(device);
    }

    public class FingerprintDeviceDto
    {
        public string Name { get; set; } = string.Empty;
        public FingerprintConnectionType ConnectionType { get; set; }
        public string? IpAddress { get; set; }
        public bool IsActive { get; set; }
    }
}
