using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BoardsController : ControllerBase
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

    public class CreateListRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
    }

    [HttpPost("{id}/lists")]
    public async Task<IActionResult> CreateList(Guid id, [FromBody] CreateListRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();
        
        var board = await _context.Boards.Include(b => b.Members).Include(b => b.Lists)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId.Value);
        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid(); // Viewers and Members can't create lists

        var nextOrder = board.Lists.Any() ? board.Lists.Max(l => l.OrderIndex) + 1 : 0;

        var list = new BoardList
        {
            Id = Guid.NewGuid(),
            BoardId = id,
            Name = req.Name,
            OrderIndex = nextOrder,
            ColorHex = req.ColorHex,
            OrganizationId = orgId.Value
        };

        _context.BoardLists.Add(list);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);

        return Ok(list);
    }

    public class UpdateListRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ColorHex { get; set; }
    }

    [HttpPut("lists/{listId}")]
    public async Task<IActionResult> UpdateList(Guid listId, [FromBody] UpdateListRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var list = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(l => l.Id == listId && l.OrganizationId == orgId.Value);

        if (list == null) return NotFound();

        var currentUserMember = list.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid(); // Viewers and Members can't edit lists

        list.Name = req.Name;
        list.ColorHex = req.ColorHex;

        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(list.BoardId);

        return Ok(list);
    }

    public class CreateCardRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
        public List<string>? AssigneeIds { get; set; }
        public DateTime? DueDate { get; set; }
    }

    [HttpPost("lists/{listId}/cards")]
    public async Task<IActionResult> CreateCard(Guid listId, [FromBody] CreateCardRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();
        
        var list = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.Id == listId && l.OrganizationId == orgId.Value);
            
        if (list == null) return NotFound();

        var currentUserMember = list.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid(); // Viewers and Members can't create cards

        var nextOrder = list.Cards.Any() ? list.Cards.Max(c => c.OrderIndex) + 1 : 0;

        var assigneeIds = (req.AssigneeIds != null && req.AssigneeIds.Count > 0)
            ? req.AssigneeIds.Distinct().ToList()
            : (!string.IsNullOrWhiteSpace(req.AssignedToUserId) ? new List<string> { req.AssignedToUserId } : new List<string>());

        assigneeIds = assigneeIds.Count > 0 ? await _context.Employees.Where(e => e.OrganizationId == orgId.Value && e.IsActive && (assigneeIds.Contains(e.Id) || (e.UserId != null && assigneeIds.Contains(e.UserId)))).Select(e => e.Id).Distinct().ToListAsync() : new List<string>();
        var primaryAssignee = assigneeIds.FirstOrDefault();

        var card = new BoardCard
        {
            Id = Guid.NewGuid(),
            ListId = listId,
            Title = req.Title,
            Description = req.Description,
            AssignedToUserId = primaryAssignee,
            DueDate = req.DueDate.HasValue ? DateTime.SpecifyKind(req.DueDate.Value, DateTimeKind.Utc) : null,
            OrderIndex = nextOrder,
            OrganizationId = orgId.Value
        };

        foreach (var empId in assigneeIds)
        {
            card.Assignees.Add(new BoardCardAssignee
            {
                CardId = card.Id,
                EmployeeId = empId,
                OrganizationId = orgId.Value,
                AssignedAt = DateTime.UtcNow
            });
        }

        _context.BoardCards.Add(card);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(list.BoardId);

        // Notify assigned employees
        if (assigneeIds.Count > 0)
        {
            var assignedEmps = await _context.Employees
                .AsNoTracking()
                .Where(e => e.OrganizationId == orgId.Value && (assigneeIds.Contains(e.Id) || (e.UserId != null && assigneeIds.Contains(e.UserId))))
                .ToListAsync();

            foreach (var emp in assignedEmps)
            {
                if (!string.IsNullOrEmpty(emp.UserId) && emp.UserId != employee.UserId)
                {
                    var notif = new NotificationPayload
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Task Assigned",
                        Message = $"You were assigned to task \"{card.Title}\" on board \"{list.Board.Name}\".",
                        Category = "Task",
                        Status = "Assigned",
                        TargetUrl = $"/boards/{list.BoardId}",
                        Timestamp = DateTime.UtcNow
                    };
                    await _notificationHub.Clients.Group($"user_{emp.UserId}").SendAsync("ReceiveNotification", notif);
                }
            }
        }

        return Ok(card);
    }

    [HttpPut("cards/{cardId}")]
    public async Task<IActionResult> UpdateCard(Guid cardId, [FromBody] CreateCardRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .Include(c => c.Assignees)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);
        if (card == null) return NotFound();

        var currentUserMember = card.List.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid();

        var assigneeIds = (req.AssigneeIds != null)
            ? req.AssigneeIds.Distinct().ToList()
            : (!string.IsNullOrWhiteSpace(req.AssignedToUserId) ? new List<string> { req.AssignedToUserId } : new List<string>());

        assigneeIds = assigneeIds.Count > 0 ? await _context.Employees.Where(e => e.OrganizationId == orgId.Value && e.IsActive && (assigneeIds.Contains(e.Id) || (e.UserId != null && assigneeIds.Contains(e.UserId)))).Select(e => e.Id).Distinct().ToListAsync() : new List<string>();
        card.Title = req.Title;
        card.Description = req.Description;
        card.AssignedToUserId = assigneeIds.FirstOrDefault();
        card.DueDate = req.DueDate.HasValue ? DateTime.SpecifyKind(req.DueDate.Value, DateTimeKind.Utc) : null;

        // Synchronize assignees
        var currentAssigneeIds = card.Assignees.Select(a => a.EmployeeId).ToHashSet();
        var toRemove = card.Assignees.Where(a => !assigneeIds.Contains(a.EmployeeId)).ToList();
        foreach (var rem in toRemove)
        {
            _context.BoardCardAssignees.Remove(rem);
        }

        var newlyAddedEmpIds = new List<string>();
        foreach (var empId in assigneeIds)
        {
            if (!currentAssigneeIds.Contains(empId))
            {
                newlyAddedEmpIds.Add(empId);
                _context.BoardCardAssignees.Add(new BoardCardAssignee
                {
                    CardId = card.Id,
                    EmployeeId = empId,
                    OrganizationId = orgId.Value,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List!.BoardId);

        // Notify newly added assignees
        if (newlyAddedEmpIds.Count > 0)
        {
            var newEmps = await _context.Employees
                .AsNoTracking()
                .Where(e => e.OrganizationId == orgId.Value && (newlyAddedEmpIds.Contains(e.Id) || (e.UserId != null && newlyAddedEmpIds.Contains(e.UserId))))
                .ToListAsync();

            foreach (var emp in newEmps)
            {
                if (!string.IsNullOrEmpty(emp.UserId) && emp.UserId != employee.UserId)
                {
                    var notif = new NotificationPayload
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "Task Assigned",
                        Message = $"You were assigned to task \"{card.Title}\" on board \"{card.List.Board.Name}\".",
                        Category = "Task",
                        Status = "Assigned",
                        TargetUrl = $"/boards/{card.List.BoardId}",
                        Timestamp = DateTime.UtcNow
                    };
                    await _notificationHub.Clients.Group($"user_{emp.UserId}").SendAsync("ReceiveNotification", notif);
                }
            }
        }

        return Ok(card);
    }

    public class MoveCardRequest
    {
        public Guid NewListId { get; set; }
        public int NewOrderIndex { get; set; }
    }

    [HttpPut("cards/{cardId}/move")]
    public async Task<IActionResult> MoveCard(Guid cardId, [FromBody] MoveCardRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);

        if (card == null) return NotFound();

        // Check if user has permission in the source board
        var currentUserMember = card.List.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid();

        var targetList = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.Id == req.NewListId && l.OrganizationId == orgId.Value);

        if (targetList == null) return NotFound();

        // Target list must belong to a board the user has access to
        var targetBoardMember = targetList.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (targetBoardMember == null || targetBoardMember.Role == BoardRole.Viewer)
            return Forbid();

        // Remove from old list ordering
        var oldListCards = await _context.BoardCards
            .Where(c => c.ListId == card.ListId && c.Id != card.Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync();

        // If moving to a new list
        if (card.ListId != req.NewListId)
        {
            card.ListId = req.NewListId;
            
            // Reorder old list to fill gap
            for (int i = 0; i < oldListCards.Count; i++)
            {
                oldListCards[i].OrderIndex = i;
            }

            // Insert into new list
            var newListCards = targetList.Cards.Where(c => c.Id != card.Id).OrderBy(c => c.OrderIndex).ToList();
            newListCards.Insert(Math.Min(req.NewOrderIndex, newListCards.Count), card);
            
            for (int i = 0; i < newListCards.Count; i++)
            {
                newListCards[i].OrderIndex = i;
            }
        }
        else // Moving within same list
        {
            oldListCards.Insert(Math.Min(req.NewOrderIndex, oldListCards.Count), card);
            for (int i = 0; i < oldListCards.Count; i++)
            {
                oldListCards[i].OrderIndex = i;
            }
        }

        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List!.BoardId);
        if (targetList.BoardId != card.List.BoardId)
        {
            await NotifyBoardUpdated(targetList.BoardId);
        }
        return Ok();
    }

    [HttpDelete("lists/{listId}")]
    public async Task<IActionResult> DeleteList(Guid listId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();
        
        var list = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(l => l.Id == listId && l.OrganizationId == orgId.Value);
            
        if (list == null) return NotFound();

        var currentUserMember = list.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid();

        _context.BoardLists.Remove(list);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(list.BoardId);
        return Ok();
    }

    [HttpDelete("cards/{cardId}")]
    public async Task<IActionResult> DeleteCard(Guid cardId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);

        if (card == null) return NotFound();

        var currentUserMember = card.List.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid();

        _context.BoardCards.Remove(card);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List!.BoardId);
        return Ok();
    }

    public class MoveListRequest
    {
        public int NewOrderIndex { get; set; }
    }

    [HttpPut("{id}/lists/{listId}/move")]
    public async Task<IActionResult> MoveList(Guid id, Guid listId, [FromBody] MoveListRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var board = await _context.Boards
            .Include(b => b.Members)
            .Include(b => b.Lists)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId.Value);

        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer || currentUserMember.Role == BoardRole.Member)
            return Forbid();

        var targetList = board.Lists.FirstOrDefault(l => l.Id == listId);
        if (targetList == null) return NotFound();

        var orderedLists = board.Lists.Where(l => l.Id != listId).OrderBy(l => l.OrderIndex).ToList();
        orderedLists.Insert(Math.Min(req.NewOrderIndex, orderedLists.Count), targetList);

        for (int i = 0; i < orderedLists.Count; i++)
        {
            orderedLists[i].OrderIndex = i;
        }

        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok();
    }
    private async Task NotifyBoardUpdated(Guid boardId)
    {
        await _hubContext.Clients.Group(boardId.ToString()).SendAsync("BoardUpdated");
        await _hubContext.Clients.Group(boardId.ToString()).SendAsync("BoardUpdated");
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId.HasValue)
        {
            await _notificationHub.Clients.Group($"org_{orgId.Value}").SendAsync("BoardUpdated");
            await _notificationHub.Clients.Group($"org_{orgId.Value}").SendAsync("TaskUpdated");
        }
        else
        {
            // Skip cross-tenant broadcast when no active organization is resolved
            // Skip cross-tenant TaskUpdated broadcast when no active organization is resolved
        }
    }

    // --- LABELS ---
    public class CreateLabelRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#71717a";
    }

    [HttpPost("{id}/labels")]
    public async Task<IActionResult> CreateLabel(Guid id, [FromBody] CreateLabelRequest labelReq)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(labelReq.Name))
            return BadRequest("Label name cannot be empty.");

        var board = await _context.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId.Value);
        if (board == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        var label = new BoardLabel
        {
            Id = Guid.NewGuid(),
            BoardId = id,
            Name = labelReq.Name.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(labelReq.ColorHex) ? "#71717a" : labelReq.ColorHex,
            OrganizationId = orgId.Value
        };
        _context.BoardLabels.Add(label);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok(new { label.Id, label.Name, label.ColorHex });
    }

    [HttpDelete("{id}/labels/{labelId}")]
    public async Task<IActionResult> DeleteLabel(Guid id, Guid labelId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var board = await _context.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId.Value);
        if (board == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        var label = await _context.BoardLabels.FirstOrDefaultAsync(l => l.Id == labelId && l.BoardId == id && l.OrganizationId == orgId.Value);
        if (label == null) return NotFound();

        var cardLabels = _context.BoardCardLabels.Where(cl => cl.LabelId == labelId);
        _context.BoardCardLabels.RemoveRange(cardLabels);

        _context.BoardLabels.Remove(label);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok();
    }

    [HttpPost("cards/{cardId}/labels/{labelId}")]
    public async Task<IActionResult> AddLabelToCard(Guid cardId, Guid labelId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);
        if (card == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        var labelExists = await _context.BoardLabels.AnyAsync(l => l.Id == labelId && l.OrganizationId == orgId.Value);
        if (!labelExists) return NotFound("Label not found.");

        var alreadyExists = await _context.BoardCardLabels.AnyAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);
        if (!alreadyExists)
        {
            _context.BoardCardLabels.Add(new BoardCardLabel { CardId = cardId, LabelId = labelId, OrganizationId = orgId.Value });
            await _context.SaveChangesAsync();
        }
        await NotifyBoardUpdated(card.List!.BoardId);
        return Ok();
    }

    [HttpDelete("cards/{cardId}/labels/{labelId}")]
    public async Task<IActionResult> RemoveLabelFromCard(Guid cardId, Guid labelId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);
        if (card == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        var cardLabel = await _context.BoardCardLabels.FirstOrDefaultAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);
        if (cardLabel != null)
        {
            _context.BoardCardLabels.Remove(cardLabel);
            await _context.SaveChangesAsync();
            await NotifyBoardUpdated(card.List!.BoardId);
        }
        return Ok();
    }

    // --- COMMENTS ---
    public class CreateCommentRequest { public string Text { get; set; } = string.Empty; }
    
    [HttpPost("cards/{cardId}/comments")]
    public async Task<IActionResult> AddComment(Guid cardId, [FromBody] CreateCommentRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var userId = employee.Id;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards.Include(c => c.List).ThenInclude(l => l.Board).ThenInclude(b => b.Members).FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);
        if (card == null) return NotFound();
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        var commentMember = card.List?.Board?.Members.FirstOrDefault(m => m.UserId == userId);
        if (!isOwner && (commentMember == null || commentMember.Role == BoardRole.Viewer)) return Forbid();
        if (card == null) return NotFound();

        var comment = new BoardCardComment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = userId!,
            Text = req.Text,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId.Value   
        };
        _context.BoardCardComments.Add(comment);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List!.BoardId);
        
        return Ok(comment);
    }

    // --- CHECKLISTS ---
    public class CreateChecklistRequest { public string Title { get; set; } = string.Empty; }
    
    [HttpPost("cards/{cardId}/checklists")]
    public async Task<IActionResult> AddChecklist(Guid cardId, [FromBody] CreateChecklistRequest req)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);
        if (card == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        var checklist = new BoardCardChecklist
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Title = req.Title,
            OrganizationId = orgId.Value
        };
        _context.BoardCardChecklists.Add(checklist);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List!.BoardId);
        
        return Ok(checklist);
    }

    [HttpPost("checklists/{checklistId}/items")]
    public async Task<IActionResult> AddChecklistItem(Guid checklistId, [FromBody] CreateChecklistRequest req)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var checklist = await _context.BoardCardChecklists
            .Include(c => c.Card)
                .ThenInclude(c => c.List)
                    .ThenInclude(l => l.Board)
                        .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == checklistId && c.OrganizationId == orgId.Value);
        if (checklist == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = checklist.Card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        var item = new BoardCardChecklistItem
        {
            Id = Guid.NewGuid(),
            ChecklistId = checklistId,
            Text = req.Title,
            IsCompleted = false,
            OrganizationId = orgId.Value
        };
        _context.BoardCardChecklistItems.Add(item);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(checklist.Card.List.BoardId);
        
        return Ok(item);
    }

    [HttpPut("checklists/items/{itemId}/toggle")]
    public async Task<IActionResult> ToggleChecklistItem(Guid itemId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var item = await _context.BoardCardChecklistItems
            .Include(i => i.Checklist)
                .ThenInclude(c => c.Card)
                    .ThenInclude(c => c.List)
                        .ThenInclude(l => l.Board)
                            .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.OrganizationId == orgId.Value);
            
        if (item == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = item.Checklist.Card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer)
            return Forbid();

        item.IsCompleted = !item.IsCompleted;
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(item.Checklist.Card.List.BoardId);
        
        return Ok(item);
    }

    [HttpDelete("cards/{cardId}/checklists/{checklistId}")]
    public async Task<IActionResult> DeleteChecklist(Guid cardId, Guid checklistId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var checklist = await _context.BoardCardChecklists
            .Include(c => c.Card)
                .ThenInclude(c => c.List)
                    .ThenInclude(l => l.Board)
                        .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == checklistId && c.CardId == cardId && c.OrganizationId == orgId.Value);
            
        if (checklist == null) return NotFound();

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var member = checklist.Card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer || member.Role == BoardRole.Member)
            return Forbid();

        _context.BoardCardChecklists.Remove(checklist);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(checklist.Card.List.BoardId);
        
        return Ok();
    }

    // --- ATTACHMENTS ---
    [HttpPost("cards/{cardId}/attachments")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> AddAttachment(Guid cardId, [FromForm] IFormFile? file)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OrganizationId == orgId.Value);

        if (card == null) return NotFound("Card not found.");

        // Check if user is a member of the board and has editing privileges (not a Viewer)
        var member = card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null || member.Role == BoardRole.Viewer)
            return Forbid();

        if (file == null || file.Length == 0) return BadRequest("File is empty.");

        // Limit file size to 25MB
        if (file.Length > 25 * 1024 * 1024)
            return BadRequest("File size exceeds the 25MB limit.");

        var webRoot = !string.IsNullOrEmpty(_env.WebRootPath) ? _env.WebRootPath : Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads", "boards");
        Directory.CreateDirectory(uploadDir);

        var sanitizedFileName = Path.GetFileName(file.FileName);
        var allowedBoardExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip", ".rar", ".7z", ".mp4", ".mp3", ".wav" };
        var boardExt = Path.GetExtension(sanitizedFileName)?.ToLowerInvariant() ?? string.Empty;
        if (!allowedBoardExts.Contains(boardExt))
            return BadRequest("File type is not permitted.");
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var filePath = Path.Combine(uploadDir, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new BoardCardAttachment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = employee.Id,
            FileName = sanitizedFileName,
            FilePath = $"/uploads/boards/{uniqueFileName}",
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };

        _context.BoardCardAttachments.Add(attachment);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List!.BoardId);

        return Ok(new
        {
            attachment.Id,
            attachment.UserId,
            attachment.FileName,
            attachment.FilePath,
            attachment.FileSize,
            attachment.UploadedAt
        });
    }

    [HttpDelete("attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var attachment = await _context.BoardCardAttachments
            .Include(a => a.Card)
                .ThenInclude(c => c.List)
                    .ThenInclude(l => l.Board)
                        .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.OrganizationId == orgId.Value);
            
        if (attachment == null) return NotFound("Attachment not found.");

        var boardMember = attachment.Card.List.Board.Members.FirstOrDefault(m => m.UserId == employee.Id);
        var isUploader = attachment.UserId == employee.Id;
        var isBoardAdminOrOwner = boardMember?.Role == BoardRole.Owner || boardMember?.Role == BoardRole.Admin;

        // Only the uploader or board owner/admin can delete an attachment
        if (!isUploader && !isBoardAdminOrOwner)
            return Forbid();

        var webRoot = !string.IsNullOrEmpty(_env.WebRootPath) ? _env.WebRootPath : Path.Combine(_env.ContentRootPath, "wwwroot");
        var physicalPath = Path.Combine(webRoot, attachment.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(physicalPath))
        {
            try
            {
                System.IO.File.Delete(physicalPath);
            }
            catch (Exception)
            {
                // File deletion failure shouldn't block database record removal
            }
        }

        var boardId = attachment.Card.List.BoardId;
        _context.BoardCardAttachments.Remove(attachment);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(boardId);

        return Ok(new { message = "Attachment deleted successfully." });
    }
}

public class BoardCardAssigneeResponseDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public BoardCardUserResponseDto? User { get; set; }
}

public class BoardCardUserResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
}
