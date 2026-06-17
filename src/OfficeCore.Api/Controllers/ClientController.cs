using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[Authorize(Policy = "RequireClients")]
[Route("api/[controller]")]
[ApiController]
public class ClientController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClientController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Client>>> GetClients()
    {
        return await _context.Clients
            .Include(c => c.Businesses)
            .OrderByDescending(c => c.DateAdded)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Client>> GetClient(Guid id)
    {
        var client = await _context.Clients
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
        client.Id = Guid.NewGuid();
        client.DateAdded = DateTime.UtcNow;
        client.IsActive = true;
        
        // Ensure businesses have new IDs and point to this client
        if (client.Businesses != null)
        {
            foreach (var business in client.Businesses)
            {
                business.Id = Guid.NewGuid();
                business.ClientId = client.Id;
            }
        }

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateClient(Guid id, Client clientUpdate)
    {
        if (id != clientUpdate.Id)
        {
            return BadRequest();
        }

        var client = await _context.Clients
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

        // Update Businesses - very simple approach
        _context.Businesses.RemoveRange(client.Businesses);
        
        if (clientUpdate.Businesses != null)
        {
            foreach (var b in clientUpdate.Businesses)
            {
                b.Id = Guid.NewGuid();
                b.ClientId = client.Id;
                _context.Businesses.Add(b);
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> ArchiveClient(Guid id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null)
        {
            return NotFound();
        }

        client.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
