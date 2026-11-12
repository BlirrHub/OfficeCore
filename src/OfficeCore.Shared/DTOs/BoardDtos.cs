namespace OfficeCore.Shared.DTOs;

public enum BoardMemberRole
{
    Viewer = 0,
    Member = 1,
    Owner = 2,
    Admin = 3
}

public class BoardDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<BoardMemberDetailDto> Members { get; set; } = new();
    public List<BoardListDetailDto> Lists { get; set; } = new();
    public List<BoardLabelDetailDto> Labels { get; set; } = new();
}

public class BoardMemberDetailDto
{
    public BoardEmployeeDto User { get; set; } = new();
    public BoardMemberRole Role { get; set; }
}

public class BoardEmployeeDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicationUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
}

public class BoardListDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string? ColorHex { get; set; }
    public List<BoardCardDetailDto> Cards { get; set; } = new();
}

public class BoardCardDetailDto
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderIndex { get; set; }
    public string? AssignedToUserId { get; set; }
    public BoardEmployeeDto? AssignedToUser { get; set; }
    public List<BoardCardAssigneeDetailDto> Assignees { get; set; } = new();
    public DateTime? DueDate { get; set; }

    public IEnumerable<BoardEmployeeDto> GetAssignees()
    {
        if (Assignees != null && Assignees.Count > 0)
        {
            return Assignees.Where(a => a.User != null).Select(a => a.User!);
        }
        if (AssignedToUser != null)
        {
            return new[] { AssignedToUser };
        }
        return Enumerable.Empty<BoardEmployeeDto>();
    }

    public List<BoardCardLabelDetailDto> Labels { get; set; } = new();
    public List<BoardCardCommentDetailDto> Comments { get; set; } = new();
    public List<BoardCardChecklistDetailDto> Checklists { get; set; } = new();
    public List<BoardCardAttachmentDetailDto> Attachments { get; set; } = new();
}

public class BoardCardAssigneeDetailDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public BoardEmployeeDto? User { get; set; }
}

public class BoardLabelDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
}

public class BoardCardLabelDetailDto
{
    public Guid LabelId { get; set; }
    public BoardLabelDetailDto Label { get; set; } = new();
}

public class BoardCardCommentDetailDto
{
    public Guid Id { get; set; }
    public BoardEmployeeDto User { get; set; } = new();
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BoardCardChecklistDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<BoardCardChecklistItemDetailDto> Items { get; set; } = new();
}

public class BoardCardChecklistItemDetailDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class BoardCardAttachmentDetailDto
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class BoardCardUserResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicationUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
}

public class BoardCardAssigneeResponseDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public BoardCardUserResponseDto? User { get; set; }
}
