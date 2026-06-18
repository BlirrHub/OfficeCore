using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BoardsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<Employee> _userManager;

    public BoardsController(AppDbContext context, UserManager<Employee> userManager)
    {
        _context = context;
        _userManager = userManager;
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
        return Ok();
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
                MembersCount = b.Members.Count
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
            .Include(b => b.Members)
                .ThenInclude(m => m.User)
            .Include(b => b.Lists.OrderBy(l => l.OrderIndex))
                .ThenInclude(l => l.Cards.OrderBy(c => c.OrderIndex))
                    .ThenInclude(c => c.AssignedToUser)
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
            AssignedToUserId = req.AssignedToUserId,
            DueDate = req.DueDate,
            OrderIndex = nextOrder
        };

        _context.BoardCards.Add(card);
        await _context.SaveChangesAsync();

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
        return Ok();
    }
}
