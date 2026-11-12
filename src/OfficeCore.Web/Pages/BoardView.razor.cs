using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using OfficeCore.Shared.Authorization;
using OfficeCore.Shared.DTOs;
using OfficeCore.Web.Services;

namespace OfficeCore.Web.Client.Pages;

public partial class BoardView
{
    [Parameter] public Guid Id { get; set; }
    [SupplyParameterFromQuery(Name = "taskId")] public Guid? TaskId { get; set; }

    private Guid _currentLoadedBoardId = Guid.Empty;
    private Guid? _handledTaskId = null;

    // Media Viewer State
    private bool _isViewerOpen = false;
    private string _viewerUrl = "";
    private string _viewerFileName = "";
    private string _viewerTitle = "Attachment";

    private void OpenAttachmentViewer(BoardCardAttachmentDetailDto att)
    {
        if (att == null || string.IsNullOrWhiteSpace(att.FilePath)) return;
        _viewerUrl = MediaUrlHelper.Resolve(att.FilePath, Configuration);
        _viewerFileName = att.FileName;
        _viewerTitle = att.FileName;
        _isViewerOpen = true;
    }

    private BoardDetailDto? _board;
    private bool _isLoading = true;
    private bool _isOwner = false;
    private bool _isAdmin = false;
    private bool _isMember = false;
    private bool _isViewer = false;
    private bool _canManageBoardMembers = false;
    private bool _canEditMemberRoles = false;
    private bool _canEditBoardStructure = false;
    private List<BoardEmployeeDto> _allUsers = new();
    private string? _currentEmployeeId = null;
    private int _fileInputKey = 0;
    private bool _isUploadingAttachment = false;

    // Drag and Drop state
    private BoardCardDetailDto? _draggedCard;
    private BoardListDetailDto? _draggedList;

    // Add List State
    private bool _isAddingList = false;
    private string _newListName = string.Empty;
    private string _newListColor = "#18181b";

    private Guid? _editingListId = null;
    private string _editingListName = string.Empty;
    private string _editingListColor = string.Empty;
    private readonly string[] _presetColors = new[]
    {
        "#18181b", // Zinc / Default
        "#0284c7", // Sky / Blue
        "#0d9488", // Teal
        "#16a34a", // Emerald / Green
        "#ca8a04", // Amber / Yellow
        "#e11d48", // Rose / Red
        "#9333ea", // Purple
        "#db2777"  // Fuchsia / Pink
    };

    // Modal State
    private bool _showDeleteBoardModal = false;
    private bool _showMembersModal = false;
    private string _newMemberUserId = string.Empty;
    private BoardMemberRole _newMemberRole = BoardMemberRole.Member;

    private bool _showRemoveMemberModal = false;
    private BoardEmployeeDto? _memberToRemove;

    private bool _showCardModal = false;
    private Guid _activeListIdForCard;
    private BoardCardDetailDto? _editingCard;
    private CardEditModel _cardModel = new();

    private bool _showGenericDeleteModal = false;
    private string _genericDeleteTitle = "";
    private string _genericDeleteText = "";
    private Func<Task>? _genericDeleteAction;

    private void PromptDelete(string title, string text, Func<Task> action)
    {
        _genericDeleteTitle = title;
        _genericDeleteText = text;
        _genericDeleteAction = action;
        _showGenericDeleteModal = true;
    }

    private async Task ConfirmGenericDelete()
    {
        if (_genericDeleteAction != null) await _genericDeleteAction();
        _showGenericDeleteModal = false;
    }

    private DateTime _lastActionTime = DateTime.MinValue;
    private void MarkAction() => _lastActionTime = DateTime.UtcNow;

    private HubConnection? hubConnection;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = HubFactory.Create("hubs/boards");

        hubConnection.Reconnected += async (connectionId) =>
        {
            if (Id != Guid.Empty)
            {
                try { await hubConnection.SendAsync("JoinBoardGroup", Id.ToString()); } catch { }
            }
        };

        hubConnection.On("BoardUpdated", async () =>
        {
            if (DateTime.UtcNow - _lastActionTime < TimeSpan.FromSeconds(2)) return;
            await LoadBoard(false);
            await InvokeAsync(StateHasChanged);
        });

        try
        {
            await hubConnection.StartAsync();
            if (Id != Guid.Empty)
            {
                await hubConnection.SendAsync("JoinBoardGroup", Id.ToString());
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during navigation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to SignalR hub: {ex.Message}");
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        bool boardChanged = _currentLoadedBoardId != Id;
        if (boardChanged)
        {
            if (hubConnection is not null && _currentLoadedBoardId != Guid.Empty && hubConnection.State == HubConnectionState.Connected)
            {
                try { await hubConnection.SendAsync("LeaveBoardGroup", _currentLoadedBoardId.ToString()); } catch { }
            }

            _currentLoadedBoardId = Id;
            await LoadBoard(true);

            if (_canManageBoardMembers)
            {
                await LoadAllUsers();
            }

            if (hubConnection is not null && hubConnection.State == HubConnectionState.Connected && Id != Guid.Empty)
            {
                try { await hubConnection.SendAsync("JoinBoardGroup", Id.ToString()); } catch { }
            }
        }

        // Open card if taskId query param is present
        if (TaskId.HasValue && TaskId != _handledTaskId && _board != null)
        {
            _handledTaskId = TaskId;
            var targetCard = _board.Lists.SelectMany(l => l.Cards).FirstOrDefault(c => c.Id == TaskId.Value);
            if (targetCard != null)
            {
                EditCard(targetCard);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            if (hubConnection.State == HubConnectionState.Connected && Id != Guid.Empty)
            {
                try { await hubConnection.SendAsync("LeaveBoardGroup", Id.ToString()); } catch { }
            }
            await hubConnection.DisposeAsync();
        }
    }

    private async Task LoadAllUsers()
    {
        try
        {
            var users = await Http.GetFromJsonAsync<List<BoardEmployeeDto>>("/api/chat/users");
            if (users != null) _allUsers = users;
        }
        catch { }
    }

    private async Task LoadBoard(bool showLoading = false)
    {
        if (showLoading) _isLoading = true;
        try
        {
            _board = await Http.GetFromJsonAsync<BoardDetailDto>($"/api/boards/{Id}");
            if (_board != null)
            {
                var authState = await AuthProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _canManageBoardMembers = user.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.BoardsManageMembers)
                    || user.HasClaim("IsWorkspaceOwner", "true");

                _canEditMemberRoles = _canManageBoardMembers;

                var myMember = _board.Members.FirstOrDefault(m => m.User.ApplicationUserId == userId);
                if (myMember != null)
                {
                    _currentEmployeeId = myMember.User.Id;
                    _isOwner = myMember.Role == BoardMemberRole.Owner;
                    _isAdmin = myMember.Role == BoardMemberRole.Admin;
                    _isMember = myMember.Role == BoardMemberRole.Member;
                    _isViewer = myMember.Role == BoardMemberRole.Viewer;

                    _canEditBoardStructure = _isOwner || _isAdmin;

                    if (_isOwner)
                    {
                        _canManageBoardMembers = true;
                        _canEditMemberRoles = true;
                    }
                    if (_isAdmin)
                    {
                        _canManageBoardMembers = true;
                    }
                }

                if (_canManageBoardMembers && _allUsers.Count == 0)
                {
                    await LoadAllUsers();
                }

                if (_editingCard != null)
                {
                    _editingCard = _board.Lists.SelectMany(l => l.Cards).FirstOrDefault(c => c.Id == _editingCard.Id) ?? _editingCard;
                }
            }
        }
        catch
        {
            _board = null;
        }
        _isLoading = false;
    }

    private void HandleDragStart(BoardCardDetailDto card)
    {
        _draggedCard = card;
        _draggedList = null;
    }

    private void HandleListDragStart(BoardListDetailDto list)
    {
        _draggedList = list;
        _draggedCard = null;
    }

    private async Task HandleDropOnCard(BoardCardDetailDto targetCard)
    {
        if (_draggedCard == null || _draggedCard.Id == targetCard.Id) return;

        var sourceList = _board?.Lists.FirstOrDefault(l => l.Id == _draggedCard.ListId);
        var destList = _board?.Lists.FirstOrDefault(l => l.Id == targetCard.ListId);

        if (sourceList != null && destList != null)
        {
            sourceList.Cards.Remove(_draggedCard);
            _draggedCard.ListId = targetCard.ListId;

            var targetIndex = destList.Cards.IndexOf(targetCard);
            destList.Cards.Insert(targetIndex, _draggedCard);

            for (int i = 0; i < destList.Cards.Count; i++)
            {
                destList.Cards[i].OrderIndex = i;
            }

            MarkAction();
            var moveReq = new { NewListId = _draggedCard.ListId, NewOrderIndex = targetIndex };
            await Http.PutAsJsonAsync($"/api/boards/cards/{_draggedCard.Id}/move", moveReq);
        }
    }

    private async Task HandleDropOnList(Guid targetListId)
    {
        if (_draggedCard != null)
        {
            var sourceList = _board?.Lists.FirstOrDefault(l => l.Id == _draggedCard.ListId);
            var destList = _board?.Lists.FirstOrDefault(l => l.Id == targetListId);

            if (sourceList != null && destList != null)
            {
                if (sourceList.Id != destList.Id)
                {
                    sourceList.Cards.Remove(_draggedCard);
                    _draggedCard.ListId = targetListId;
                    destList.Cards.Add(_draggedCard);
                }

                MarkAction();
                var moveReq = new { NewListId = targetListId, NewOrderIndex = destList.Cards.Count };
                await Http.PutAsJsonAsync($"/api/boards/cards/{_draggedCard.Id}/move", moveReq);
            }

            _draggedCard = null;
        }
        else if (_draggedList != null)
        {
            if (_draggedList.Id == targetListId) return;

            var targetList = _board?.Lists.FirstOrDefault(l => l.Id == targetListId);
            if (_board != null && targetList != null)
            {
                var newIndex = _board.Lists.IndexOf(targetList);
                _board.Lists.Remove(_draggedList);
                _board.Lists.Insert(newIndex, _draggedList);

                for (int i = 0; i < _board.Lists.Count; i++)
                {
                    _board.Lists[i].OrderIndex = i;
                }

                MarkAction();
                var moveReq = new { NewOrderIndex = newIndex };
                await Http.PutAsJsonAsync($"/api/boards/{Id}/lists/{_draggedList.Id}/move", moveReq);
            }

            _draggedList = null;
        }
    }

    private async Task CreateList()
    {
        if (string.IsNullOrWhiteSpace(_newListName) || _board == null) return;

        MarkAction();
        var req = new { Name = _newListName, ColorHex = _newListColor };
        var response = await Http.PostAsJsonAsync($"/api/boards/{Id}/lists", req);

        if (response.IsSuccessStatusCode)
        {
            var createdList = await response.Content.ReadFromJsonAsync<BoardListDetailDto>();
            if (createdList != null)
            {
                _board.Lists.Add(createdList);
                ToastService.ShowSuccess($"List \"{createdList.Name}\" created.");
            }
            _isAddingList = false;
            _newListName = string.Empty;
            _newListColor = "#18181b";
        }
        else
        {
            ToastService.ShowError("Failed to create list.");
        }
    }

    private void StartEditList(BoardListDetailDto list)
    {
        _editingListId = list.Id;
        _editingListName = list.Name;
        _editingListColor = list.ColorHex ?? "#18181b";
    }

    private void CancelListEdit()
    {
        _editingListId = null;
    }

    private async Task SaveListEdit(BoardListDetailDto list)
    {
        if (string.IsNullOrWhiteSpace(_editingListName)) return;

        MarkAction();
        var req = new { Name = _editingListName, ColorHex = _editingListColor };
        var res = await Http.PutAsJsonAsync($"/api/boards/lists/{list.Id}", req);
        if (res.IsSuccessStatusCode)
        {
            list.Name = _editingListName;
            list.ColorHex = _editingListColor;
            _editingListId = null;
            StateHasChanged();
            ToastService.ShowSuccess($"List \"{list.Name}\" updated.");
        }
        else
        {
            ToastService.ShowError("Failed to update list.");
        }
    }

    private async Task HandleListInputKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CreateList();
        }
        else if (e.Key == "Escape")
        {
            _isAddingList = false;
            _newListName = string.Empty;
        }
    }

    private void ShowAddCardModal(Guid listId)
    {
        _activeListIdForCard = listId;
        _editingCard = null;
        _cardModel = new CardEditModel();
        _showCardModal = true;
    }

    private void EditCard(BoardCardDetailDto card)
    {
        if (_isViewer) return;
        _activeListIdForCard = card.ListId;
        _editingCard = card;
        _cardModel = new CardEditModel
        {
            Title = card.Title,
            Description = card.Description,
            AssignedToUserId = card.AssignedToUserId ?? card.AssignedToUser?.Id,
            AssigneeIds = card.GetAssignees().Select(u => u.Id).Distinct().ToList(),
            DueDate = card.DueDate
        };
        _showCardModal = true;
    }

    private void ToggleAssignee(string memberUserId)
    {
        if (_cardModel.AssigneeIds.Contains(memberUserId))
        {
            _cardModel.AssigneeIds.Remove(memberUserId);
        }
        else
        {
            _cardModel.AssigneeIds.Add(memberUserId);
        }
        _cardModel.AssignedToUserId = _cardModel.AssigneeIds.FirstOrDefault();
    }

    private void CloseCardModal()
    {
        _showCardModal = false;
        _editingCard = null;
        _handledTaskId = null;
    }

    private async Task SaveCard()
    {
        if (!_canEditBoardStructure || string.IsNullOrWhiteSpace(_cardModel.Title)) return;

        _cardModel.AssignedToUserId = _cardModel.AssigneeIds.FirstOrDefault();

        var req = new
        {
            Title = _cardModel.Title,
            Description = _cardModel.Description,
            AssignedToUserId = _cardModel.AssignedToUserId,
            AssigneeIds = _cardModel.AssigneeIds,
            DueDate = _cardModel.DueDate
        };

        if (_editingCard == null)
        {
            MarkAction();
            var response = await Http.PostAsJsonAsync($"/api/boards/lists/{_activeListIdForCard}/cards", req);
            if (response.IsSuccessStatusCode)
            {
                var createdCard = await response.Content.ReadFromJsonAsync<BoardCardDetailDto>();
                if (createdCard != null)
                {
                    var selectedMembers = _board?.Members
                        .Where(m => _cardModel.AssigneeIds.Contains(m.User.Id))
                        .Select(m => m.User)
                        .ToList() ?? new List<BoardEmployeeDto>();

                    createdCard.Assignees = selectedMembers.Select(u => new BoardCardAssigneeDetailDto { EmployeeId = u.Id, User = u }).ToList();
                    createdCard.AssignedToUser = selectedMembers.FirstOrDefault();
                    createdCard.AssignedToUserId = _cardModel.AssignedToUserId;

                    var list = _board?.Lists.FirstOrDefault(l => l.Id == _activeListIdForCard);
                    list?.Cards.Add(createdCard);
                    ToastService.ShowSuccess("Card created.");
                }
            }
            else
            {
                ToastService.ShowError("Failed to create card.");
            }
        }
        else
        {
            MarkAction();
            var response = await Http.PutAsJsonAsync($"/api/boards/cards/{_editingCard.Id}", req);
            if (response.IsSuccessStatusCode)
            {
                _editingCard.Title = _cardModel.Title;
                _editingCard.Description = _cardModel.Description;

                var selectedMembers = _board?.Members
                    .Where(m => _cardModel.AssigneeIds.Contains(m.User.Id))
                    .Select(m => m.User)
                    .ToList() ?? new List<BoardEmployeeDto>();

                _editingCard.Assignees = selectedMembers.Select(u => new BoardCardAssigneeDetailDto { EmployeeId = u.Id, User = u }).ToList();
                _editingCard.AssignedToUserId = _cardModel.AssignedToUserId;
                _editingCard.AssignedToUser = selectedMembers.FirstOrDefault();
                _editingCard.DueDate = _cardModel.DueDate;
                ToastService.ShowSuccess("Card updated.");
            }
            else
            {
                ToastService.ShowError("Failed to update card.");
            }
        }

        _showCardModal = false;
    }

    private async Task ConfirmDeleteBoard()
    {
        var response = await Http.DeleteAsync($"/api/boards/{Id}");
        if (response.IsSuccessStatusCode)
        {
            ToastService.ShowSuccess("Board deleted.");
            NavigationManager.NavigateTo("/boards");
        }
        else
        {
            ToastService.ShowError("Failed to delete board.");
        }
    }

    private async Task DeleteList(Guid listId)
    {
        MarkAction();
        var response = await Http.DeleteAsync($"/api/boards/lists/{listId}");
        if (response.IsSuccessStatusCode)
        {
            var list = _board?.Lists.FirstOrDefault(l => l.Id == listId);
            if (list != null) _board?.Lists.Remove(list);
            ToastService.ShowSuccess("List deleted.");
        }
        else
        {
            ToastService.ShowError("Failed to delete list.");
        }
    }

    private async Task DeleteCard(Guid cardId)
    {
        MarkAction();
        var response = await Http.DeleteAsync($"/api/boards/cards/{cardId}");
        if (response.IsSuccessStatusCode)
        {
            foreach (var list in _board?.Lists ?? new())
            {
                var card = list.Cards.FirstOrDefault(c => c.Id == cardId);
                if (card != null)
                {
                    list.Cards.Remove(card);
                    ToastService.ShowSuccess("Card deleted.");
                    break;
                }
            }
        }
        else
        {
            ToastService.ShowError("Failed to delete card.");
        }
    }

    private async Task AddMember()
    {
        if (string.IsNullOrEmpty(_newMemberUserId)) return;

        var req = new { UserId = _newMemberUserId, Role = _newMemberRole };
        MarkAction();
        var response = await Http.PostAsJsonAsync($"/api/boards/{Id}/members", req);
        if (response.IsSuccessStatusCode)
        {
            await LoadBoard(false);
            _newMemberUserId = string.Empty;
            ToastService.ShowSuccess("Member added.");
        }
        else
        {
            ToastService.ShowError("Failed to add member.");
        }
    }

    private async Task RemoveMember(string userId)
    {
        MarkAction();
        var response = await Http.DeleteAsync($"/api/boards/{Id}/members/{userId}");
        if (response.IsSuccessStatusCode)
        {
            var member = _board?.Members.FirstOrDefault(m => m.User.Id == userId);
            if (member != null) _board?.Members.Remove(member);
            ToastService.ShowSuccess("Member removed.");
        }
        else
        {
            ToastService.ShowError("Failed to remove member.");
        }
    }

    private void ConfirmRemoveMember(BoardEmployeeDto user)
    {
        _memberToRemove = user;
        _showRemoveMemberModal = true;
    }

    private async Task ExecuteRemoveMember()
    {
        if (_memberToRemove == null) return;
        await RemoveMember(_memberToRemove.Id);
        _showRemoveMemberModal = false;
        _memberToRemove = null;
    }

    private async Task ChangeMemberRole(string userId, string? roleStr)
    {
        if (Enum.TryParse<BoardMemberRole>(roleStr, out var newRole))
        {
            var req = new { UserId = userId, Role = newRole };
            MarkAction();
            var response = await Http.PostAsJsonAsync($"/api/boards/{Id}/members", req);
            if (response.IsSuccessStatusCode)
            {
                var member = _board?.Members.FirstOrDefault(m => m.User.Id == userId);
                if (member != null) member.Role = newRole;
                ToastService.ShowSuccess("Member role changed.");
            }
            else
            {
                ToastService.ShowError("Failed to change member role.");
            }
        }
    }

    public class CardEditModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
        public List<string> AssigneeIds { get; set; } = new();
        public DateTime? DueDate { get; set; }
    }

    private string _newCommentText = string.Empty;
    private string _newChecklistTitle = string.Empty;
    private Dictionary<Guid, string> _newChecklistItemTexts = new();

    private async Task AddComment()
    {
        if (string.IsNullOrWhiteSpace(_newCommentText) || _editingCard == null) return;
        var req = new { Text = _newCommentText };
        MarkAction();
        var response = await Http.PostAsJsonAsync($"/api/boards/cards/{_editingCard.Id}/comments", req);
        if (response.IsSuccessStatusCode)
        {
            var comment = await response.Content.ReadFromJsonAsync<BoardCardCommentDetailDto>();
            if (comment != null)
            {
                var authState = await AuthProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                comment.User = new BoardEmployeeDto
                {
                    FirstName = user.Claims.FirstOrDefault(c => c.Type == "FirstName")?.Value ?? "Me",
                    LastName = user.Claims.FirstOrDefault(c => c.Type == "LastName")?.Value ?? ""
                };
                _editingCard.Comments.Insert(0, comment);
            }
            _newCommentText = string.Empty;
        }
    }

    private string _newLabelName = string.Empty;
    private string _newLabelColor = "#db2777";

    private async Task CreateLabel()
    {
        if (string.IsNullOrWhiteSpace(_newLabelName) || _board == null || !_canEditBoardStructure) return;

        var req = new { Name = _newLabelName.Trim(), ColorHex = _newLabelColor };
        MarkAction();
        var response = await Http.PostAsJsonAsync($"/api/boards/{Id}/labels", req);
        if (response.IsSuccessStatusCode)
        {
            var newLabel = await response.Content.ReadFromJsonAsync<BoardLabelDetailDto>();
            if (newLabel != null)
            {
                _board.Labels.Add(newLabel);
                if (_editingCard != null)
                {
                    await ToggleLabel(newLabel.Id);
                }
                _newLabelName = string.Empty;
                _newLabelColor = "#db2777";
                ToastService.ShowSuccess($"Created label \"{newLabel.Name}\"");
                StateHasChanged();
            }
        }
        else
        {
            var err = await response.Content.ReadAsStringAsync();
            ToastService.ShowError("Failed to create label: " + (string.IsNullOrWhiteSpace(err) ? "Server error" : err));
        }
    }

    private async Task ToggleLabel(Guid labelId)
    {
        if (_editingCard == null || !_canEditBoardStructure) return;
        var existing = _editingCard.Labels.FirstOrDefault(l => l.LabelId == labelId);
        MarkAction();
        if (existing != null)
        {
            await Http.DeleteAsync($"/api/boards/cards/{_editingCard.Id}/labels/{labelId}");
            _editingCard.Labels.Remove(existing);
        }
        else
        {
            await Http.PostAsync($"/api/boards/cards/{_editingCard.Id}/labels/{labelId}", null);
            var labelObj = _board?.Labels.FirstOrDefault(l => l.Id == labelId);
            if (labelObj != null)
            {
                _editingCard.Labels.Add(new BoardCardLabelDetailDto { LabelId = labelId, Label = labelObj });
            }
        }
        StateHasChanged();
    }

    private async Task DeleteBoardLabel(BoardLabelDetailDto label)
    {
        if (_board == null || !_canEditBoardStructure) return;
        MarkAction();
        var response = await Http.DeleteAsync($"/api/boards/{Id}/labels/{label.Id}");
        if (response.IsSuccessStatusCode)
        {
            _board.Labels.Remove(label);
            foreach (var list in _board.Lists)
            {
                foreach (var card in list.Cards)
                {
                    card.Labels.RemoveAll(l => l.LabelId == label.Id);
                }
            }
            if (_editingCard != null)
            {
                _editingCard.Labels.RemoveAll(l => l.LabelId == label.Id);
            }
            ToastService.ShowSuccess($"Label \"{label.Name}\" deleted");
            StateHasChanged();
        }
        else
        {
            var err = await response.Content.ReadAsStringAsync();
            ToastService.ShowError("Failed to delete label: " + (string.IsNullOrWhiteSpace(err) ? "Server error" : err));
        }
    }

    private async Task AddChecklist()
    {
        if (string.IsNullOrWhiteSpace(_newChecklistTitle) || _editingCard == null || !_canEditBoardStructure) return;
        var req = new { Title = _newChecklistTitle };
        MarkAction();
        var response = await Http.PostAsJsonAsync($"/api/boards/cards/{_editingCard.Id}/checklists", req);
        if (response.IsSuccessStatusCode)
        {
            var checklist = await response.Content.ReadFromJsonAsync<BoardCardChecklistDetailDto>();
            if (checklist != null) _editingCard.Checklists.Add(checklist);
            _newChecklistTitle = string.Empty;
            ToastService.ShowSuccess("Checklist added.");
        }
        else
        {
            ToastService.ShowError("Failed to add checklist.");
        }
    }

    private async Task AddChecklistItem(Guid checklistId)
    {
        if (!_canEditBoardStructure || !_newChecklistItemTexts.TryGetValue(checklistId, out var text) || string.IsNullOrWhiteSpace(text) || _editingCard == null) return;
        var req = new { Title = text };
        MarkAction();
        var response = await Http.PostAsJsonAsync($"/api/boards/checklists/{checklistId}/items", req);
        if (response.IsSuccessStatusCode)
        {
            var item = await response.Content.ReadFromJsonAsync<BoardCardChecklistItemDetailDto>();
            if (item != null)
            {
                var cl = _editingCard.Checklists.FirstOrDefault(c => c.Id == checklistId);
                cl?.Items.Add(item);
            }
            _newChecklistItemTexts[checklistId] = string.Empty;
        }
        else
        {
            ToastService.ShowError("Failed to add checklist item.");
        }
    }

    private async Task ToggleChecklistItem(Guid checklistId, BoardCardChecklistItemDetailDto item)
    {
        MarkAction();
        await Http.PutAsync($"/api/boards/checklists/items/{item.Id}/toggle", null);
        item.IsCompleted = !item.IsCompleted;
    }

    private async Task DeleteChecklist(BoardCardChecklistDetailDto checklist)
    {
        if (_editingCard == null || !_canEditBoardStructure) return;
        MarkAction();
        var response = await Http.DeleteAsync($"/api/boards/cards/{_editingCard.Id}/checklists/{checklist.Id}");
        if (response.IsSuccessStatusCode)
        {
            _editingCard.Checklists.Remove(checklist);
            ToastService.ShowSuccess("Checklist deleted.");
        }
        else
        {
            ToastService.ShowError("Failed to delete checklist.");
        }
    }

    private async Task HandleFileUpload(InputFileChangeEventArgs e)
    {
        if (_editingCard == null || _isUploadingAttachment) return;

        var file = e.File;
        if (file == null) return;

        const long maxAllowedSize = 25 * 1024 * 1024; // 25MB
        if (file.Size > maxAllowedSize)
        {
            ToastService.ShowError("File exceeds the 25MB limit.");
            _fileInputKey++;
            return;
        }

        _isUploadingAttachment = true;
        StateHasChanged();

        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(file.OpenReadStream(maxAllowedSize));
            if (!string.IsNullOrEmpty(file.ContentType))
            {
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            }
            content.Add(streamContent, "file", file.Name);

            MarkAction();
            var response = await Http.PostAsync($"/api/boards/cards/{_editingCard.Id}/attachments", content);
            if (response.IsSuccessStatusCode)
            {
                var attachment = await response.Content.ReadFromJsonAsync<BoardCardAttachmentDetailDto>();
                if (attachment != null)
                {
                    _editingCard.Attachments.Add(attachment);
                    ToastService.ShowSuccess("Attachment uploaded.");
                }
            }
            else
            {
                var errMsg = await response.Content.ReadAsStringAsync();
                ToastService.ShowError(!string.IsNullOrWhiteSpace(errMsg) ? errMsg : "Failed to upload attachment.");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Upload error: {ex.Message}");
        }
        finally
        {
            _isUploadingAttachment = false;
            _fileInputKey++;
            StateHasChanged();
        }
    }

    private async Task DeleteAttachment(BoardCardAttachmentDetailDto attachment)
    {
        if (_editingCard == null) return;
        MarkAction();
        var response = await Http.DeleteAsync($"/api/boards/attachments/{attachment.Id}");
        if (response.IsSuccessStatusCode)
        {
            _editingCard.Attachments.Remove(attachment);
            ToastService.ShowSuccess("Attachment deleted.");
        }
        else
        {
            ToastService.ShowError("Failed to delete attachment.");
        }
    }
}
