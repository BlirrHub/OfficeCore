using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[Authorize(Policy = "RequireClients")]
[Route("api/[controller]")]
[ApiController]
public class ClientController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public ClientController(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:clients.view")]
    public async Task<ActionResult<IEnumerable<Client>>> GetClients()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        return await _context.Clients
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Businesses)
            .OrderByDescending(c => c.DateAdded)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:clients.view")]
    public async Task<ActionResult<Client>> GetClient(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var client = await _context.Clients
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Businesses)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null)
        {
            return NotFound();
        }

        return client;
    }

    public sealed class BusinessRequest
    {
        public Guid Id { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string? Address { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? PhoneNumber { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? TinId { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? BusinessType { get; set; }
        public bool IsVatRegistered { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class ClientRequest
    {
        public Guid Id { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string FullName { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.EmailAddress]
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? Email { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public List<BusinessRequest>? Businesses { get; set; }
    }

    [HttpPost]
    [Authorize(Policy = "Permission:clients.manage")]
    public async Task<ActionResult<Client>> CreateClient([FromBody] ClientRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var client = new Client
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            DateAdded = DateTime.UtcNow,
            IsActive = true,
            OrganizationId = orgId.Value
        };

        if (request.Businesses != null)
        {
            foreach (var b in request.Businesses)
            {
                client.Businesses.Add(new Business
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    Name = b.Name.Trim(),
                    Address = b.Address,
                    PhoneNumber = b.PhoneNumber,
                    TinId = b.TinId,
                    BusinessType = b.BusinessType,
                    IsVatRegistered = b.IsVatRegistered,
                    IsActive = b.IsActive,
                    OrganizationId = orgId.Value
                });
            }
        }

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:clients.manage")]
    public async Task<IActionResult> UpdateClient(Guid id, [FromBody] ClientRequest clientUpdate)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (clientUpdate.Id != Guid.Empty && id != clientUpdate.Id)
        {
            return BadRequest();
        }

        var client = await _context.Clients
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Businesses)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client == null)
        {
            return NotFound();
        }

        client.FullName = clientUpdate.FullName.Trim();
        client.Email = string.IsNullOrWhiteSpace(clientUpdate.Email) ? null : clientUpdate.Email.Trim();
        client.PhoneNumber = string.IsNullOrWhiteSpace(clientUpdate.PhoneNumber) ? null : clientUpdate.PhoneNumber.Trim();
        client.IsActive = clientUpdate.IsActive;
        client.OrganizationId = orgId.Value;

        var incomingBusinesses = clientUpdate.Businesses ?? new List<BusinessRequest>();
        var incomingIds = incomingBusinesses
            .Where(b => b.Id != Guid.Empty)
            .Select(b => b.Id)
            .ToHashSet();

        var toRemove = client.Businesses
            .Where(existing => !incomingIds.Contains(existing.Id))
            .ToList();
        if (toRemove.Count > 0)
        {
            _context.Businesses.RemoveRange(toRemove);
        }

        var existingById = client.Businesses.ToDictionary(b => b.Id);
        foreach (var b in incomingBusinesses)
        {
            if (b.Id != Guid.Empty && existingById.TryGetValue(b.Id, out var existing))
            {
                existing.Name = b.Name.Trim();
                existing.Address = b.Address;
                existing.PhoneNumber = b.PhoneNumber;
                existing.TinId = b.TinId;
                existing.BusinessType = b.BusinessType;
                existing.IsVatRegistered = b.IsVatRegistered;
                existing.IsActive = b.IsActive;
                existing.OrganizationId = orgId.Value;
            }
            else
            {
                _context.Businesses.Add(new Business
                {
                    Id = b.Id != Guid.Empty ? b.Id : Guid.NewGuid(),
                    ClientId = client.Id,
                    Name = b.Name.Trim(),
                    Address = b.Address,
                    PhoneNumber = b.PhoneNumber,
                    TinId = b.TinId,
                    BusinessType = b.BusinessType,
                    IsVatRegistered = b.IsVatRegistered,
                    IsActive = b.IsActive,
                    OrganizationId = orgId.Value
                });
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:clients.manage")]
    public async Task<IActionResult> ArchiveClient(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId);
        if (client == null)
        {
            return NotFound();
        }

        client.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
