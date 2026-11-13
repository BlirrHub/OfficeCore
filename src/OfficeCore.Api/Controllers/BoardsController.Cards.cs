using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Shared.DTOs;

namespace OfficeCore.Api.Controllers;

public partial class BoardsController
{    public class CreateCardRequest
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

        var sanitizedFileName = Path.GetFileName(file?.FileName ?? string.Empty);
        var allowedBoardExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip", ".rar", ".7z", ".mp4", ".mp3", ".wav" };
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";

        var stored = await _fileStorage.ValidateAndSaveAsync(file, "boards", uniqueFileName, allowedBoardExts, 25 * 1024 * 1024);
        if (!stored.Success || stored.RelativeUrl == null)
            return BadRequest(stored.ErrorMessage ?? "File type is not permitted.");

        var attachment = new BoardCardAttachment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = employee.Id,
            FileName = sanitizedFileName,
            FilePath = stored.RelativeUrl,
            FileSize = file!.Length,
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
