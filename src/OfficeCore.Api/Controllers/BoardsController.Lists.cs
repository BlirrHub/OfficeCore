using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

public partial class BoardsController
{    public class CreateListRequest
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
        orderedLists.Insert(Math.Clamp(req.NewOrderIndex, 0, orderedLists.Count), targetList);

        for (int i = 0; i < orderedLists.Count; i++)
        {
            orderedLists[i].OrderIndex = i;
        }

        await _context.SaveChangesAsync();
        await NotifyBoardUpdated(id);
        return Ok();
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
}
