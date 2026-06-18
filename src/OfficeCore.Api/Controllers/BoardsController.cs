using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BoardsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<Employee> _userManager;
    private readonly IHubContext<BoardHub> _hubContext;

    public BoardsController(AppDbContext context, UserManager<Employee> userManager, IHubContext<BoardHub> hubContext)
    {
        _context = context;
        _userManager = userManager;
        _hubContext = hubContext;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBoard(Guid id)
    {
        var userId = _userManager.GetUserId(User);

        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id);
        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role != BoardRole.Owner)
            return Forbid(); // Only owners can delete

        _context.Boards.Remove(board);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok();
    }

    [HttpPut("{id}/star")]
    public async Task<IActionResult> ToggleStar(Guid id)
    {
        var userId = _userManager.GetUserId(User);
        
        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id);
        if (board == null) return NotFound();

        var member = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null) return Forbid();

        member.IsStarred = !member.IsStarred;
        await _context.SaveChangesAsync();

        return Ok(new { isStarred = member.IsStarred });
    }

    [HttpGet]
    public async Task<IActionResult> GetBoards()
    {
        var userId = _userManager.GetUserId(User);
        
        // Admins can see all boards? Or only boards they are members of. 
        // Let's stick to boards the user is a member of for privacy.
        var boards = await _context.Boards
            .Where(b => b.Members.Any(m => m.UserId == userId))
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Description,
                b.CreatedAt,
                b.CreatedById,
                CreatorName = b.CreatedBy.FirstName + " " + b.CreatedBy.LastName,
                MembersCount = b.Members.Count,
                IsStarred = b.Members.FirstOrDefault(m => m.UserId == userId)!.IsStarred,
                MemberAvatars = b.Members.Take(3).Select(m => m.User.ProfilePictureUrl).ToList()
            })
            .ToListAsync();

        return Ok(boards);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBoard(Guid id)
    {
        var userId = _userManager.GetUserId(User);

        var board = await _context.Boards
            .Include(b => b.CreatedBy)
            .Include(b => b.Labels)
            .Include(b => b.Members)
                .ThenInclude(m => m.User)
            .Include(b => b.Lists.OrderBy(l => l.OrderIndex))
                .ThenInclude(l => l.Cards.OrderBy(c => c.OrderIndex))
                    .ThenInclude(c => c.AssignedToUser)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Labels)
                        .ThenInclude(cl => cl.Label)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Comments.OrderByDescending(cm => cm.CreatedAt))
                        .ThenInclude(cm => cm.User)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Checklists)
                        .ThenInclude(cl => cl.Items)
            .Include(b => b.Lists)
                .ThenInclude(l => l.Cards)
                    .ThenInclude(c => c.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (board == null) return NotFound();

        // Check if user is a member
        if (!board.Members.Any(m => m.UserId == userId))
            return Forbid();

        return Ok(board);
    }

    public class CreateBoardRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBoardRequest req)
    {
        var userId = _userManager.GetUserId(User);

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId!
        };

        // Add creator as owner
        board.Members.Add(new BoardMember
        {
            Id = Guid.NewGuid(),
            UserId = userId!,
            Role = BoardRole.Owner
        });

        // Add default lists
        board.Lists.Add(new BoardList { Id = Guid.NewGuid(), Name = "To Do", OrderIndex = 0 });
        board.Lists.Add(new BoardList { Id = Guid.NewGuid(), Name = "In Progress", OrderIndex = 1 });
        board.Lists.Add(new BoardList { Id = Guid.NewGuid(), Name = "Done", OrderIndex = 2 });

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
        var userId = _userManager.GetUserId(User);
        
        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id);
        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role != BoardRole.Owner)
            return Forbid(); // Only owners can manage members

        var targetMember = board.Members.FirstOrDefault(m => m.UserId == req.UserId);
        
        if (targetMember != null)
        {
            // Update role
            targetMember.Role = req.Role;
        }
        else
        {
            // Add member
            var newMember = new BoardMember
            {
                Id = Guid.NewGuid(),
                BoardId = id,
                UserId = req.UserId,
                Role = req.Role
            };
            _context.Set<BoardMember>().Add(newMember);
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}/members/{targetUserId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string targetUserId)
    {
        var userId = _userManager.GetUserId(User);
        
        var board = await _context.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id);
        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        // Only owners can remove others. Users can remove themselves.
        if (userId != targetUserId && (currentUserMember == null || currentUserMember.Role != BoardRole.Owner))
            return Forbid();

        var targetMember = board.Members.FirstOrDefault(m => m.UserId == targetUserId);
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
    }

    [HttpPost("{id}/lists")]
    public async Task<IActionResult> CreateList(Guid id, [FromBody] CreateListRequest req)
    {
        var userId = _userManager.GetUserId(User);
        
        var board = await _context.Boards.Include(b => b.Members).Include(b => b.Lists).FirstOrDefaultAsync(b => b.Id == id);
        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid(); // Viewers can't create lists

        var nextOrder = board.Lists.Any() ? board.Lists.Max(l => l.OrderIndex) + 1 : 0;

        var list = new BoardList
        {
            Id = Guid.NewGuid(),
            BoardId = id,
            Name = req.Name,
            OrderIndex = nextOrder
        };

        _context.BoardLists.Add(list);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);

        return Ok(list);
    }

    public class CreateCardRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
        public DateTime? DueDate { get; set; }
    }

    [HttpPost("lists/{listId}/cards")]
    public async Task<IActionResult> CreateCard(Guid listId, [FromBody] CreateCardRequest req)
    {
        var userId = _userManager.GetUserId(User);
        
        var list = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.Id == listId);
            
        if (list == null) return NotFound();

        var currentUserMember = list.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid();

        var nextOrder = list.Cards.Any() ? list.Cards.Max(c => c.OrderIndex) + 1 : 0;

        var card = new BoardCard
        {
            Id = Guid.NewGuid(),
            ListId = listId,
            Title = req.Title,
            Description = req.Description,
            AssignedToUserId = string.IsNullOrWhiteSpace(req.AssignedToUserId) ? null : req.AssignedToUserId,
            DueDate = req.DueDate.HasValue ? DateTime.SpecifyKind(req.DueDate.Value, DateTimeKind.Utc) : null,
            OrderIndex = nextOrder
        };

        _context.BoardCards.Add(card);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(list.BoardId);

        return Ok(card);
    }

    [HttpPut("cards/{cardId}")]
    public async Task<IActionResult> UpdateCard(Guid cardId, [FromBody] CreateCardRequest req)
    {
        var userId = _userManager.GetUserId(User);
        var card = await _context.BoardCards.Include(c => c.List).ThenInclude(l => l.Board).ThenInclude(b => b.Members).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return NotFound();

        var currentUserMember = card.List.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid();

        card.Title = req.Title;
        card.Description = req.Description;
        card.AssignedToUserId = string.IsNullOrWhiteSpace(req.AssignedToUserId) ? null : req.AssignedToUserId;
        card.DueDate = req.DueDate.HasValue ? DateTime.SpecifyKind(req.DueDate.Value, DateTimeKind.Utc) : null;

        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List.BoardId);
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
        var userId = _userManager.GetUserId(User);

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        if (card == null) return NotFound();

        // Check if user has permission in the source board
        var currentUserMember = card.List.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid();

        var targetList = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.Id == req.NewListId);

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
        await NotifyBoardUpdated(card.List.BoardId);
        if (targetList.BoardId != card.List.BoardId)
        {
            await NotifyBoardUpdated(targetList.BoardId);
        }
        return Ok();
    }

    [HttpDelete("lists/{listId}")]
    public async Task<IActionResult> DeleteList(Guid listId)
    {
        var userId = _userManager.GetUserId(User);
        
        var list = await _context.BoardLists
            .Include(l => l.Board)
                .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(l => l.Id == listId);
            
        if (list == null) return NotFound();

        var currentUserMember = list.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid();

        _context.BoardLists.Remove(list);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(list.BoardId);
        return Ok();
    }

    [HttpDelete("cards/{cardId}")]
    public async Task<IActionResult> DeleteCard(Guid cardId)
    {
        var userId = _userManager.GetUserId(User);

        var card = await _context.BoardCards
            .Include(c => c.List)
                .ThenInclude(l => l.Board)
                    .ThenInclude(b => b.Members)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        if (card == null) return NotFound();

        var currentUserMember = card.List.Board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
            return Forbid();

        _context.BoardCards.Remove(card);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List.BoardId);
        return Ok();
    }

    public class MoveListRequest
    {
        public int NewOrderIndex { get; set; }
    }

    [HttpPut("{id}/lists/{listId}/move")]
    public async Task<IActionResult> MoveList(Guid id, Guid listId, [FromBody] MoveListRequest req)
    {
        var userId = _userManager.GetUserId(User);

        var board = await _context.Boards
            .Include(b => b.Members)
            .Include(b => b.Lists)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (board == null) return NotFound();

        var currentUserMember = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (currentUserMember == null || currentUserMember.Role == BoardRole.Viewer)
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
    }

    // --- LABELS ---
    [HttpPost("{id}/labels")]
    public async Task<IActionResult> CreateLabel(Guid id, [FromBody] BoardLabel labelReq)
    {
        var board = await _context.Boards.FindAsync(id);
        if (board == null) return NotFound();

        var label = new BoardLabel
        {
            Id = Guid.NewGuid(),
            BoardId = id,
            Name = labelReq.Name,
            ColorHex = labelReq.ColorHex
        };
        _context.BoardLabels.Add(label);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok(label);
    }

    [HttpPost("cards/{cardId}/labels/{labelId}")]
    public async Task<IActionResult> AddLabelToCard(Guid cardId, Guid labelId)
    {
        var card = await _context.BoardCards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return NotFound();

        _context.BoardCardLabels.Add(new BoardCardLabel { CardId = cardId, LabelId = labelId });
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List.BoardId);
        return Ok();
    }

    [HttpDelete("cards/{cardId}/labels/{labelId}")]
    public async Task<IActionResult> RemoveLabelFromCard(Guid cardId, Guid labelId)
    {
        var cardLabel = await _context.BoardCardLabels.FirstOrDefaultAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);
        if (cardLabel != null)
        {
            var card = await _context.BoardCards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
            _context.BoardCardLabels.Remove(cardLabel);
            await _context.SaveChangesAsync();
            if (card != null) await NotifyBoardUpdated(card.List.BoardId);
        }
        return Ok();
    }

    // --- COMMENTS ---
    public class CreateCommentRequest { public string Text { get; set; } = string.Empty; }
    
    [HttpPost("cards/{cardId}/comments")]
    public async Task<IActionResult> AddComment(Guid cardId, [FromBody] CreateCommentRequest req)
    {
        var userId = _userManager.GetUserId(User);
        var card = await _context.BoardCards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return NotFound();

        var comment = new BoardCardComment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = userId!,
            Text = req.Text,
            CreatedAt = DateTime.UtcNow
        };
        _context.BoardCardComments.Add(comment);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List.BoardId);
        
        return Ok(comment);
    }

    // --- CHECKLISTS ---
    public class CreateChecklistRequest { public string Title { get; set; } = string.Empty; }
    
    [HttpPost("cards/{cardId}/checklists")]
    public async Task<IActionResult> AddChecklist(Guid cardId, [FromBody] CreateChecklistRequest req)
    {
        var card = await _context.BoardCards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return NotFound();

        var checklist = new BoardCardChecklist
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Title = req.Title
        };
        _context.BoardCardChecklists.Add(checklist);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List.BoardId);
        
        return Ok(checklist);
    }

    [HttpPost("checklists/{checklistId}/items")]
    public async Task<IActionResult> AddChecklistItem(Guid checklistId, [FromBody] CreateChecklistRequest req)
    {
        var checklist = await _context.BoardCardChecklists.Include(c => c.Card).ThenInclude(c => c.List).FirstOrDefaultAsync(c => c.Id == checklistId);
        if (checklist == null) return NotFound();

        var item = new BoardCardChecklistItem
        {
            Id = Guid.NewGuid(),
            ChecklistId = checklistId,
            Text = req.Title,
            IsCompleted = false
        };
        _context.BoardCardChecklistItems.Add(item);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(checklist.Card.List.BoardId);
        
        return Ok(item);
    }

    [HttpPut("checklists/items/{itemId}/toggle")]
    public async Task<IActionResult> ToggleChecklistItem(Guid itemId)
    {
        var item = await _context.BoardCardChecklistItems
            .Include(i => i.Checklist).ThenInclude(c => c.Card).ThenInclude(c => c.List)
            .FirstOrDefaultAsync(i => i.Id == itemId);
            
        if (item == null) return NotFound();

        item.IsCompleted = !item.IsCompleted;
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(item.Checklist.Card.List.BoardId);
        
        return Ok(item);
    }

    [HttpDelete("cards/{cardId}/checklists/{checklistId}")]
    public async Task<IActionResult> DeleteChecklist(Guid cardId, Guid checklistId)
    {
        var checklist = await _context.BoardCardChecklists
            .Include(c => c.Card).ThenInclude(c => c.List)
            .FirstOrDefaultAsync(c => c.Id == checklistId && c.CardId == cardId);
            
        if (checklist == null) return NotFound();

        _context.BoardCardChecklists.Remove(checklist);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(checklist.Card.List.BoardId);
        
        return Ok();
    }

    // --- ATTACHMENTS ---
    [HttpPost("cards/{cardId}/attachments")]
    public async Task<IActionResult> AddAttachment(Guid cardId, IFormFile file)
    {
        var userId = _userManager.GetUserId(User);
        var card = await _context.BoardCards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return NotFound();

        if (file == null || file.Length == 0) return BadRequest("File is empty");

        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "boards");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new BoardCardAttachment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = userId!,
            FileName = file.FileName,
            FilePath = $"/uploads/boards/{fileName}",
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        _context.BoardCardAttachments.Add(attachment);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(card.List.BoardId);

        return Ok(attachment);
    }

    [HttpDelete("attachments/{attachmentId}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        var attachment = await _context.BoardCardAttachments
            .Include(a => a.Card).ThenInclude(c => c.List)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);
            
        if (attachment == null) return NotFound();

        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(physicalPath))
        {
            System.IO.File.Delete(physicalPath);
        }

        _context.BoardCardAttachments.Remove(attachment);
        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(attachment.Card.List.BoardId);

        return Ok();
    }

}
