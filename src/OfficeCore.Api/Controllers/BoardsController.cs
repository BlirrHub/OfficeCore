using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;
using OfficeCore.Shared.DTOs;

using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public partial class BoardsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<BoardHub> _hubContext;
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly TenantService _tenant;
    private readonly IWebHostEnvironment _env;

    public BoardsController(
        AppDbContext context, 
        UserManager<ApplicationUser> userManager, 
        IHubContext<BoardHub> hubContext, 
        IHubContext<NotificationHub> notificationHub,
        TenantService tenant,
        IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _hubContext = hubContext;
        _notificationHub = notificationHub;
        _tenant = tenant;
        _env = env;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBoard(Guid id)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();

        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId);
        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (currentUserMember == null || currentUserMember.Role != BoardRole.Owner)
            return Forbid();

        _context.Boards.Remove(board);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok();
    }

    [HttpPut("{id}/star")]
    public async Task<IActionResult> ToggleStar(Guid id)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();
        
        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId);
        if (board == null) return NotFound();

        var member = board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null) return Forbid();

        member.IsStarred = !member.IsStarred;
        await _context.SaveChangesAsync();

        return Ok(new { isStarred = member.IsStarred });
    }

    [HttpGet]
    public async Task<IActionResult> GetBoards()
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();
        
        // SQLite EF Core provider does not support complex sub-queries inside Select.
        // We fetch the entities with Includes first, then project in-memory.
        var boards = await _context.Boards
            .Include(b => b.CreatedBy)
                .ThenInclude(e => e.User)
            .Include(b => b.Members)
                .ThenInclude(m => m.User)
                    .ThenInclude(e => e.User)
            .Where(b => b.OrganizationId == orgId && b.Members.Any(m => m.UserId == employee.Id))
            .ToListAsync();

        var result = boards.Select(b => new
        {
            b.Id,
            b.Name,
            b.Description,
            b.CreatedAt,
            b.CreatedById,
            CreatorName = $"{b.CreatedBy?.User?.FirstName} {b.CreatedBy?.User?.LastName}".Trim(),
            MembersCount = b.Members.Count,
            IsStarred = b.Members.FirstOrDefault(m => m.UserId == employee.Id)?.IsStarred ?? false,
            MemberAvatars = b.Members.OrderByDescending(m => m.Role).Take(3).Select(m => m.User?.User?.ProfilePictureUrl).ToList()
        });

        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchBoardsAndTasks([FromQuery] string? q)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();
        
        var boardQuery = _context.Boards
            .Where(b => b.OrganizationId == orgId && b.Members.Any(m => m.UserId == employee.Id));
            
        var taskQuery = _context.BoardCards
            .Where(c => c.List.Board.OrganizationId == orgId && 
                        c.List.Board.Members.Any(m => m.UserId == employee.Id));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lowerQ = q.ToLower();
            boardQuery = boardQuery.Where(b => b.Name.ToLower().Contains(lowerQ));
            taskQuery = taskQuery.Where(c => c.Title.ToLower().Contains(lowerQ));
        }

        var boards = await boardQuery
            .Take(5)
            .Select(b => new { Id = b.Id.ToString(), Name = b.Name, Type = "Board" })
            .ToListAsync();

        var tasks = await taskQuery
            .Take(10)
            .Select(c => new { Id = c.Id.ToString(), Name = c.Title, Type = "Task", BoardName = c.List.Board.Name, BoardId = c.List.BoardId.ToString() })
            .ToListAsync();

        return Ok(new { boards, tasks });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBoard(Guid id)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();

        var board = await _context.Boards
            .Include(b => b.CreatedBy)
            .Include(b => b.Labels)
            .Include(b => b.Members)
                .ThenInclude(m => m.User)
                    .ThenInclude(e => e.User)
            .Include(b => b.Lists.OrderBy(l => l.OrderIndex))
                .ThenInclude(l => l.Cards.OrderBy(c => c.OrderIndex))
                    .ThenInclude(c => c.AssignedToUser!)
                        .ThenInclude(e => e.User)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Assignees)
                        .ThenInclude(a => a.Employee)
                            .ThenInclude(e => e.User)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Labels)
                        .ThenInclude(cl => cl.Label)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Comments.OrderByDescending(cm => cm.CreatedAt))
                        .ThenInclude(cm => cm.User)
                            .ThenInclude(e => e.User)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Checklists)
                        .ThenInclude(cl => cl.Items)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId);

        if (board == null) return NotFound();

        // Check if user is a member
        if (!board.Members.Any(m => m.UserId == employee.Id))
            return Forbid();

        var boardDto = new {
            board.Id,
            board.Name,
            board.Description,
            Labels = board.Labels.Select(l => new { l.Id, l.Name, l.ColorHex }),
            Members = board.Members.Select(m => new {
                m.Role,
                User = new {
                    Id = m.UserId,
                    ApplicationUserId = m.User?.UserId,
                    FirstName = m.User?.User?.FirstName,
                    LastName = m.User?.User?.LastName,
                    ProfilePictureUrl = m.User?.User?.ProfilePictureUrl
                }
            }),
            Lists = board.Lists.Select(l => new {
                l.Id,
                l.Name,
                l.OrderIndex,
                l.ColorHex,
                Cards = l.Cards.Select(c => new {
                    c.Id,
                    c.ListId,
                    c.Title,
                    c.Description,
                    c.OrderIndex,
                    c.AssignedToUserId,
                    AssignedToUser = c.AssignedToUser == null ? null : new {
                        Id = c.AssignedToUser.Id,
                        FirstName = c.AssignedToUser.User?.FirstName,
                        LastName = c.AssignedToUser.User?.LastName,
                        ProfilePictureUrl = c.AssignedToUser.User?.ProfilePictureUrl
                    },
                    Assignees = (c.Assignees != null && c.Assignees.Any())
                        ? c.Assignees.Select(a => new BoardCardAssigneeResponseDto {
                            EmployeeId = a.EmployeeId,
                            User = a.Employee?.User == null ? null : new BoardCardUserResponseDto {
                                Id = a.Employee.Id,
                                FirstName = a.Employee.User.FirstName,
                                LastName = a.Employee.User.LastName,
                                ProfilePictureUrl = a.Employee.User.ProfilePictureUrl
                            }
                        }).ToList()
                        : (c.AssignedToUser != null ? new List<BoardCardAssigneeResponseDto> {
                            new BoardCardAssigneeResponseDto {
                                EmployeeId = c.AssignedToUser.Id,
                                User = new BoardCardUserResponseDto {
                                    Id = c.AssignedToUser.Id,
                                    FirstName = c.AssignedToUser.User?.FirstName ?? string.Empty,
                                    LastName = c.AssignedToUser.User?.LastName ?? string.Empty,
                                    ProfilePictureUrl = c.AssignedToUser.User?.ProfilePictureUrl
                                }
                            }
                        } : new List<BoardCardAssigneeResponseDto>()),
                    c.DueDate,
                    Labels = c.Labels.Select(cl => {
                        var lbl = cl.Label ?? board.Labels.FirstOrDefault(l => l.Id == cl.LabelId);
                        return new {
                            cl.LabelId,
                            Label = lbl == null ? null : new { lbl.Id, lbl.Name, lbl.ColorHex }
                        };
                    }).Where(cl => cl.Label != null),
                    Comments = c.Comments.Select(cm => new {
                        cm.Id,
                        cm.Text,
                        cm.CreatedAt,
                        User = new {
                            Id = cm.UserId,
                            FirstName = cm.User?.User?.FirstName,
                            LastName = cm.User?.User?.LastName,
                            ProfilePictureUrl = cm.User?.User?.ProfilePictureUrl
                        }
                    }),
                    Checklists = c.Checklists.Select(chk => new {
                        chk.Id,
                        chk.Title,
                        Items = chk.Items.Select(ci => new { ci.Id, ci.Text, ci.IsCompleted })
                    }),
                    Attachments = c.Attachments.Select(a => new { a.Id, a.UserId, a.FileName, a.FilePath, a.FileSize, a.UploadedAt })
                })
            })
        };

        return Ok(boardDto);
    }

    public class CreateBoardRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBoardRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedById = employee.Id,
            OrganizationId = orgId
        };

        // Add creator as owner
        board.Members.Add(new BoardMember
        {
            Id = Guid.NewGuid(),
            UserId = employee.Id,
            Role = BoardRole.Owner,
            OrganizationId = orgId
        });

        // Add default lists
        board.Lists.Add(new BoardList { Id = Guid.NewGuid(), Name = "To Do", OrderIndex = 0, OrganizationId = orgId });
        board.Lists.Add(new BoardList { Id = Guid.NewGuid(), Name = "In Progress", OrderIndex = 1, OrganizationId = orgId });
        board.Lists.Add(new BoardList { Id = Guid.NewGuid(), Name = "Done", OrderIndex = 2, OrganizationId = orgId });

        _context.Boards.Add(board);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(board.Id);

        return Ok(board);
    }

    public class ManageMemberRequest
    {
        public string UserId { get; set; } = string.Empty;
        public BoardRole Role { get; set; }
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> ManageMember(Guid id, [FromBody] ManageMemberRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();
        
        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId);
        if (board == null) return NotFound();

        // Only Workspace Owner or Workspace Admin can add/modify board members
        var org = await _context.Organizations.FindAsync(orgId);
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        bool isWorkspaceOwner = org != null && org.CreatedByUserId == currentUserId;
        bool isWorkspaceAdmin = User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.BoardsManageMembers);
        bool isBoardOwner = board.Members.Any(m => m.UserId == employee.Id && m.Role == BoardRole.Owner);
        bool isBoardAdmin = board.Members.Any(m => m.UserId == employee.Id && m.Role == BoardRole.Admin);

        if (!isWorkspaceOwner && !isWorkspaceAdmin && !isBoardOwner && !isBoardAdmin)
            return Forbid();

        var targetEmployee = await _context.Employees
            .FirstOrDefaultAsync(e => (e.Id == req.UserId || e.UserId == req.UserId) && e.OrganizationId == orgId);

        if (targetEmployee == null)
            return NotFound("Employee not found in this organization.");

        if (req.Role == BoardRole.Owner && !isWorkspaceOwner && !isWorkspaceAdmin && !isBoardOwner)
            return Forbid();

        var targetMember = board.Members.FirstOrDefault(m => m.UserId == targetEmployee.Id);
        
        if (targetMember != null)
        {
            // Admins cannot edit existing member roles
            if (!isWorkspaceOwner && !isWorkspaceAdmin && !isBoardOwner)
                return Forbid();

            targetMember.Role = req.Role;
        }
        else
        {
            var newMember = new BoardMember
            {
                Id = Guid.NewGuid(),
                BoardId = id,
                UserId = targetEmployee.Id,
                Role = req.Role,
                OrganizationId = orgId!.Value
            };
            _context.Set<BoardMember>().Add(newMember);
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}/members/{targetUserId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string targetUserId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = _tenant.GetActiveOrganizationId();
        
        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId);
        if (board == null) return NotFound();

        // Members can remove themselves, but only Workspace Owner, Workspace Admin, or Board Owner can remove others
        var targetEmp = await _context.Employees.FirstOrDefaultAsync(e => e.OrganizationId == orgId && (e.Id == targetUserId || e.UserId == targetUserId));
        var resolvedTargetEmpId = targetEmp?.Id ?? targetUserId;
        if (employee.Id != resolvedTargetEmpId)
        {
            var org = await _context.Organizations.FindAsync(orgId);
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            bool isWorkspaceOwner = org != null && org.CreatedByUserId == currentUserId;
            bool isWorkspaceAdmin = User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.BoardsManageMembers);
            bool isBoardOwner = board.Members.Any(m => m.UserId == employee.Id && m.Role == BoardRole.Owner);
            bool isBoardAdmin = board.Members.Any(m => m.UserId == employee.Id && m.Role == BoardRole.Admin);

            if (!isWorkspaceOwner && !isWorkspaceAdmin && !isBoardOwner && !isBoardAdmin)
                return Forbid();
        }

        var targetMember = board.Members.FirstOrDefault(m => m.UserId == resolvedTargetEmpId || m.UserId == targetUserId);
        if (targetMember != null)
        {
            _context.BoardMembers.Remove(targetMember);
            await _context.SaveChangesAsync();
            await NotifyBoardUpdated(id);
        }

        return Ok();
    }

    private async Task NotifyBoardUpdated(Guid boardId)
    {
        await _hubContext.Clients.Group(boardId.ToString()).SendAsync("BoardUpdated");
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId.HasValue)
        {
            await _notificationHub.Clients.Group($"org_{orgId.Value}").SendAsync("BoardUpdated");
            await _notificationHub.Clients.Group($"org_{orgId.Value}").SendAsync("TaskUpdated");
        }
    }
}
