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
    public async Task<ActionResult<IEnumerable<Client>>> GetClients()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        return await _context.Clients
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Businesses)
            .OrderByDescending(c => c.DateAdded)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Client>> GetClient(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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

    [HttpPost]
    public async Task<ActionResult<Client>> CreateClient(Client client)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        client.Id = Guid.NewGuid();
        client.DateAdded = DateTime.UtcNow;
        client.IsActive = true;
        client.OrganizationId = orgId.Value;
        
        // Ensure businesses have new IDs and point to this client
        if (client.Businesses != null)
        {
            foreach (var business in client.Businesses)
            {
                business.Id = Guid.NewGuid();
                business.ClientId = client.Id;
                business.OrganizationId = orgId.Value;
            }
        }

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateClient(Guid id, Client clientUpdate)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        if (id != clientUpdate.Id)
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

        client.FullName = clientUpdate.FullName;
        client.Email = clientUpdate.Email;
        client.PhoneNumber = clientUpdate.PhoneNumber;
        client.IsActive = clientUpdate.IsActive;
        client.OrganizationId = orgId.Value;

        // Update Businesses - very simple approach
        _context.Businesses.RemoveRange(client.Businesses);
        
        if (clientUpdate.Businesses != null)
        {
            foreach (var b in clientUpdate.Businesses)
            {
                b.Id = Guid.NewGuid();
                b.ClientId = client.Id;
                b.OrganizationId = orgId.Value;
                _context.Businesses.Add(b);
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> ArchiveClient(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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
