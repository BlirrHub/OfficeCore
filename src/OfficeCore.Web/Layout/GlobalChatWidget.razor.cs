using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using OfficeCore.Shared.DTOs;
using OfficeCore.Web.Services;

namespace OfficeCore.Web.Client.Layout;

public partial class GlobalChatWidget
{    private Dictionary<string, string> _draftMentions = new(StringComparer.OrdinalIgnoreCase);

    private bool _isOpen = false;
    private HubConnection? _hubConnection;
    private string _currentUserId = string.Empty;  // ApplicationUser.Id from claims
    private string _currentEmployeeId = string.Empty; // Employee.Id in current org

    private bool _isLoadingRooms = true;
    private List<ChatRoomDto> _rooms = new();
    private string _roomFilter = "all"; // "all", "direct", "group"
    private int _totalUnread => _rooms.Sum(r => r.UnreadCount);

    private ChatRoomDto? _activeRoom = null;
    private bool _isLoadingMessages = false;
    private List<ChatMessageDto> _messages = new();
    private string _chatSearchQuery = string.Empty;
    private bool _showChatSearch = false;
    private HashSet<Guid> _pinnedRoomIds = new();
    private HashSet<Guid> _pinnedMessageIds = new();
    private Guid? _activeMessageMenuId = null;
    private Guid? _activeReactionMenuId = null;
    private ChatMessageDto? _replyingToMessage = null;
    private Dictionary<Guid, Dictionary<string, string>> _messageUserReactions = new();

    private bool _showForwardModal = false;
    private ChatMessageDto? _messageToForward = null;
    private string _forwardSearchQuery = string.Empty;
    private HashSet<Guid> _forwardedRoomIds = new();
    private bool _isForwarding = false;
    
    private HashSet<string> _myBoardIds = new();
    
    private string _newMessageContentValue = string.Empty;
    private string _newMessageContent 
    {
        get => _newMessageContentValue;
        set 
        {
            _newMessageContentValue = value;
            CheckForMentions(value);
        }
    }

    private bool _showMentions = false;
    private List<ChatUserDto> _mentionCandidates = new();
    private string _currentMentionSearch = string.Empty;

    private bool _showTags = false;
    private List<ChatSearchTagDto> _tagCandidates = new();
    private string _currentTagSearch = string.Empty;

    private void CheckForMentions(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _showMentions = false;
            _showTags = false;
            return;
        }

        var lastWord = text.Split(new[] { ' ', '\n' }).LastOrDefault();
        if (lastWord != null && lastWord.StartsWith("@"))
        {
            _showTags = false;
            _showMentions = true;
            _currentMentionSearch = lastWord.Substring(1);
            _mentionCandidates = _allUsers.Where(u => 
                (u.FirstName + u.LastName).Contains(_currentMentionSearch, StringComparison.OrdinalIgnoreCase) ||
                u.FirstName.Contains(_currentMentionSearch, StringComparison.OrdinalIgnoreCase)
            ).Take(5).ToList();
        }
        else if (lastWord != null && lastWord.StartsWith("#"))
        {
            _showMentions = false;
            _showTags = true;
            _currentTagSearch = lastWord.Substring(1);
            _ = SearchTagsAsync(_currentTagSearch);
        }
        else
        {
            _showMentions = false;
            _showTags = false;
        }
    }

    private void InsertMention(ChatUserDto user)
    {
        var lastIndex = _newMessageContentValue.LastIndexOf('@');
        if (lastIndex >= 0)
        {
            var plainText = $"@{user.FirstName}{user.LastName}";
            var markdown = $"[{user.FirstName} {user.LastName}](@user-{user.Id})";
            _draftMentions[plainText] = markdown;
            _newMessageContentValue = _newMessageContentValue.Substring(0, lastIndex) + $"{plainText} ";
        }
        _showMentions = false;
    }

    private void InsertTag(ChatSearchTagDto tag)
    {
        if (tag.Type == "Board")
        {
            _myBoardIds.Add(tag.Id);
        }
        else if (!string.IsNullOrEmpty(tag.BoardId))
        {
            _myBoardIds.Add(tag.BoardId);
        }

        var lastIndex = _newMessageContentValue.LastIndexOf('#');
        if (lastIndex >= 0)
        {
            var plainText = $"#{tag.Name.Replace(" ", "-")}";
            var prefix = tag.Type == "Board" ? "#board-" : $"#task-{tag.BoardId}_";
            var markdown = $"[{tag.Name}]({prefix}{tag.Id})";
            _draftMentions[plainText] = markdown;
            _newMessageContentValue = _newMessageContentValue.Substring(0, lastIndex) + $"{plainText} ";
        }
        _showTags = false;
    }

    private async Task SearchTagsAsync(string q)
    {
        try 
        {
            var res = await Http.GetFromJsonAsync<ChatSearchTagsResponse>($"/api/boards/search?q={Uri.EscapeDataString(q)}");
            if (res != null)
            {
                var candidates = new List<ChatSearchTagDto>();
                candidates.AddRange(res.Boards);
                candidates.AddRange(res.Tasks);
                _tagCandidates = candidates;
                foreach (var b in res.Boards) _myBoardIds.Add(b.Id);
                foreach (var t in res.Tasks) if (!string.IsNullOrEmpty(t.BoardId)) _myBoardIds.Add(t.BoardId);
                StateHasChanged();
            }
        }
        catch { }
    }

    private string GetPreviewText(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        // Strip out user mention markdown into clean @Name
        var text = System.Text.RegularExpressions.Regex.Replace(content, @"\[([^\]]+)\]\(@user-[a-zA-Z0-9\-_]+\)", "@$1");
        // Strip out the (#id) part from markdown links but keep the display text
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]\(#(board|task)-.*?\)", "$1");
        // Also strip any raw @user-id into @user if any exists
        text = System.Text.RegularExpressions.Regex.Replace(text, @"@user-[a-zA-Z0-9\-_]+", "@user");
        return text;
    }

    private MarkupString FormatMessageContent(string content, bool isMine)
    {
        if (string.IsNullOrWhiteSpace(content)) return new MarkupString("");
        
        var encoded = System.Net.WebUtility.HtmlEncode(content);

        // 0. Parse reply quotes if present
        if (encoded.StartsWith("&gt; Replying to "))
        {
            var parts = encoded.Split(new[] { "\n\n", "\r\n\r\n" }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var quote = parts[0].Replace("&gt; ", "");
                var actualText = parts[1];
                encoded = $"<div class=\"mb-1.5 px-2 py-1 rounded-lg bg-black/25 text-[10px] opacity-85 border-l-2 border-white/60\">{quote}</div><div>{actualText}</div>";
            }
        }
        
        // 1. Parse Markdown links for User Mentions: [Name](@user-id)
        encoded = System.Text.RegularExpressions.Regex.Replace(
            encoded,
            @"\[([^\]]+)\]\(@user-([a-zA-Z0-9\-_]+)\)",
            match =>
            {
                var name = match.Groups[1].Value;
                var userId = match.Groups[2].Value;
                if (isMine)
                {
                    return $"<span class=\"inline-flex items-center gap-0.5 font-bold text-white bg-white/20 hover:bg-white/30 px-1.5 py-0.5 rounded-md text-xs transition-colors align-baseline\" title=\"Mentioned: {name}\">&#64;{name}</span>";
                }
                else
                {
                    return $"<span class=\"inline-flex items-center gap-0.5 font-medium text-fuchsia-300 bg-fuchsia-500/15 hover:bg-fuchsia-500/25 border border-fuchsia-500/20 px-1.5 py-0.5 rounded-md text-xs transition-colors align-baseline\" title=\"Mentioned: {name}\">&#64;{name}</span>";
                }
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 2. Fallbacks for raw @user-id if any exists
        encoded = System.Text.RegularExpressions.Regex.Replace(
            encoded,
            @"(?<!\]\()@user-([a-zA-Z0-9\-_]+)",
            match =>
            {
                var uid = match.Groups[1].Value;
                var u = _allUsers.FirstOrDefault(x => x.Id == uid);
                var displayName = u != null ? $"{u.FirstName} {u.LastName}".Trim() : "user";
                if (isMine)
                {
                    return $"<span class=\"inline-flex items-center gap-0.5 font-bold text-white bg-white/20 hover:bg-white/30 px-1.5 py-0.5 rounded-md text-xs transition-colors align-baseline\" title=\"Mentioned: {displayName}\">&#64;{displayName}</span>";
                }
                else
                {
                    return $"<span class=\"inline-flex items-center gap-0.5 font-medium text-fuchsia-300 bg-fuchsia-500/15 hover:bg-fuchsia-500/25 border border-fuchsia-500/20 px-1.5 py-0.5 rounded-md text-xs transition-colors align-baseline\" title=\"Mentioned: {displayName}\">&#64;{displayName}</span>";
                }
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 3. Highlight loose Mentions: @Word (avoiding HTML tag attributes, entities or already replaced spans)
        encoded = System.Text.RegularExpressions.Regex.Replace(
            encoded, 
            @"(?<![a-zA-Z0-9/""'=&#;])@([a-zA-Z0-9_]+)", 
            match =>
            {
                var name = match.Groups[1].Value;
                if (isMine)
                {
                    return $"<span class=\"font-bold text-white bg-white/20 px-1.5 py-0.5 rounded-md text-xs\">&#64;{name}</span>";
                }
                else
                {
                    return $"<span class=\"font-medium text-fuchsia-300 bg-fuchsia-500/15 px-1.5 py-0.5 rounded-md text-xs\">&#64;{name}</span>";
                }
            });

        // 4. Parse Markdown links for Boards and Tasks: [Name](#type-ids)
        encoded = System.Text.RegularExpressions.Regex.Replace(
            encoded, 
            @"\[([^\]]+)\]\(#(board|task)-([a-zA-Z0-9\-_]+)\)", 
            match => {
                var name = match.Groups[1].Value;
                var type = match.Groups[2].Value.ToLower(); // "board" or "task"
                var ids = match.Groups[3].Value; 
                
                string boardId = ids;
                string? taskId = null;
                if (type == "task" && ids.Contains("_"))
                {
                    var parts = ids.Split('_');
                    boardId = parts[0];
                    taskId = parts[1];
                }

                // Add to known boards
                // Do not mutate _myBoardIds during render

                var href = type == "board" ? $"/boards/{boardId}" : $"/boards/{boardId}?taskId={taskId}";
                var colorClass = type == "board"
                    ? (isMine ? "text-amber-300 hover:text-amber-200" : "text-amber-400 hover:text-amber-300")
                    : (isMine ? "text-emerald-300 hover:text-emerald-200" : "text-emerald-400 hover:text-emerald-300");
                var iconPath = type == "board" 
                    ? "M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" 
                    : "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4";

                return $"<a href=\"{href}\" class=\"font-bold {colorClass} hover:underline inline-flex items-start gap-1.5 flex-wrap break-words transition-colors\"><svg class=\"size-3 mt-0.5 shrink-0\" fill=\"none\" viewBox=\"0 0 24 24\" stroke=\"currentColor\"><path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"{iconPath}\" /></svg><span>{name}</span></a>";
            }, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Fallbacks for raw #task-id and #board-id
        encoded = System.Text.RegularExpressions.Regex.Replace(
            encoded, 
            @"(?<!\]\()#(task|board)-([a-zA-Z0-9\-_]+)", 
            match => {
                var type = match.Groups[1].Value.ToLower();
                var fullId = match.Groups[2].Value;
                var shortDisplay = fullId.Length > 13 ? fullId.Substring(0, 13) + "..." : fullId;
                
                string boardId = fullId;
                string? taskId = null;
                if (type == "task" && fullId.Contains("_"))
                {
                    var parts = fullId.Split('_');
                    boardId = parts[0];
                    taskId = parts[1];
                }

                // Add to known boards
                // Do not mutate _myBoardIds during render

                var href = type == "board" ? $"/boards/{boardId}" : $"/boards/{boardId}?taskId={taskId}";
                var colorClass = type == "board"
                    ? (isMine ? "text-amber-300 hover:text-amber-200" : "text-amber-400 hover:text-amber-300")
                    : (isMine ? "text-emerald-300 hover:text-emerald-200" : "text-emerald-400 hover:text-emerald-300");
                var iconPath = type == "board" 
                    ? "M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" 
                    : "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4";

                return $"<a href=\"{href}\" class=\"font-bold {colorClass} hover:underline inline-flex items-start gap-1.5 flex-wrap break-words transition-colors\"><svg class=\"size-3 mt-0.5 shrink-0\" fill=\"none\" viewBox=\"0 0 24 24\" stroke=\"currentColor\"><path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"{iconPath}\" /></svg><span>#{shortDisplay}</span></a>";
            }, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return new MarkupString(encoded);
    }

    private bool _isTyping = false;
    private string? _typingUserName;
    private System.Timers.Timer? _typingTimer;
    private DateTime _lastTypingSent = DateTime.MinValue;

    // Creation & Navigation Modals
    private bool _showNewMessage = false;
    private string _newChatTab = "direct"; // "direct" or "group"
    private string _userSearchQuery = string.Empty;
    private List<ChatUserDto> _allUsers = new();

    // Group Chat Creation State
    private string _newGroupName = string.Empty;
    private HashSet<string> _selectedMemberIds = new();
    private bool _isCreatingGroup = false;
    private string? _createGroupError;
    private string _groupSearchQuery = string.Empty;

    // Group Details Drawer State
    private bool _showGroupDetails = false;
    private bool _showAddMembers = false;
    private HashSet<string> _selectedAddMemberIds = new();
    private bool _isAddingMembers = false;
    private string _addMemberSearchQuery = string.Empty;
    private bool _isLeavingGroup = false;

    private IEnumerable<ChatUserDto> _filteredUsers => string.IsNullOrWhiteSpace(_userSearchQuery)
        ? _allUsers
        : _allUsers.Where(u => (u.FirstName + " " + u.LastName).Contains(_userSearchQuery, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<ChatUserDto> _filteredGroupCandidates => string.IsNullOrWhiteSpace(_groupSearchQuery)
        ? _allUsers
        : _allUsers.Where(u => (u.FirstName + " " + u.LastName).Contains(_groupSearchQuery, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<ChatUserDto> _availableMembersToAdd
    {
        get
        {
            if (_activeRoom == null) return Enumerable.Empty<ChatUserDto>();
            var existingIds = _activeRoom.Members.Select(m => m.UserId).ToHashSet();
            var candidates = _allUsers.Where(u => !existingIds.Contains(u.Id));
            if (!string.IsNullOrWhiteSpace(_addMemberSearchQuery))
            {
                candidates = candidates.Where(u => (u.FirstName + " " + u.LastName).Contains(_addMemberSearchQuery, StringComparison.OrdinalIgnoreCase));
            }
            return candidates;
        }
    }

    private IEnumerable<ChatRoomDto> _filteredRooms => _roomFilter switch
    {
        "direct" => _rooms.Where(r => r.Type == 0),
        "group" => _rooms.Where(r => r.Type == 1),
        _ => _rooms
    };

    private class BasicBoardDto { public Guid Id { get; set; } }

    private async Task LoadMyBoards()
    {
        try {
            var res = await Http.GetFromJsonAsync<List<BasicBoardDto>>("/api/boards");
            if (res != null) {
                _myBoardIds = res.Select(b => b.Id.ToString()).ToHashSet();
            }
        } catch { }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_activeMessageMenuId.HasValue || _activeReactionMenuId.HasValue)
        {
            _popoverReference ??= Microsoft.JSInterop.DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("positionChatPopovers", _popoverReference);
        }

        if (firstRender)
        {
            try 
            {
                var storedPins = await JS.InvokeAsync<string>("localStorage.getItem", "pinned_chats");
                if (!string.IsNullOrEmpty(storedPins))
                {
                    _pinnedRoomIds = System.Text.Json.JsonSerializer.Deserialize<HashSet<Guid>>(storedPins) ?? new();
                    StateHasChanged();
                }
            } 
            catch { }
        }
    }

    private async Task TogglePin(Guid roomId)
    {
        if (_pinnedRoomIds.Contains(roomId))
        {
            _pinnedRoomIds.Remove(roomId);
        }
        else
        {
            _pinnedRoomIds.Add(roomId);
        }
        
        try 
        {
            await JS.InvokeVoidAsync("localStorage.setItem", "pinned_chats", System.Text.Json.JsonSerializer.Serialize(_pinnedRoomIds));
        } 
        catch { }
        
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

        if (string.IsNullOrEmpty(_currentUserId)) return;

        // Load employees in org for chat searches (returns Employee.Id as Id)
        await ReloadUsers();
        await LoadMyBoards();

        _hubConnection = HubFactory.Create("hubs/chat");

        _hubConnection.On<Guid, ChatMessageDto>("ReceiveMessage", (roomId, msg) =>
        {
            var room = _rooms.FirstOrDefault(r => r.Id == roomId);
            if (room != null)
            {
                room.LastMessage = msg;
                if (_activeRoom?.Id == roomId)
                {
                    // If this is our own message arriving via SignalR, remove pending optimistic message
                    if (msg.SenderId == _currentEmployeeId)
                    {
                        var optimistic = _messages.FirstOrDefault(m => 
                            m.IsOptimistic && 
                            m.ChatRoomId == roomId && 
                            (
                                (!string.IsNullOrEmpty(msg.AttachmentUrl) && m.AttachmentUrl == msg.AttachmentUrl) ||
                                (!string.IsNullOrEmpty(msg.Content) && m.Content == msg.Content) ||
                                (string.IsNullOrEmpty(msg.Content) && string.IsNullOrEmpty(msg.AttachmentUrl))
                            ));
                        
                        if (optimistic == null)
                        {
                            optimistic = _messages.FirstOrDefault(m => m.IsOptimistic && m.ChatRoomId == roomId);
                        }

                        if (optimistic != null)
                        {
                            _messages.Remove(optimistic);
                        }
                    }

                    if (!_messages.Any(m => m.Id == msg.Id))
                    {
                        _messages.Add(msg);
                    }
                    InvokeAsync(StateHasChanged);
                    _ = Http.PostAsync($"/api/chat/rooms/{roomId}/read", null);
                }
                else
                {
                    room.UnreadCount++;
                    InvokeAsync(StateHasChanged);
                }
            }
            else
            {
                _ = LoadRooms();
            }
        });

        _hubConnection.On<Guid, Guid>("MessageDeleted", (roomId, msgId) =>
        {
            if (_activeRoom?.Id == roomId)
            {
                _messages.RemoveAll(m => m.Id == msgId);
                _pinnedMessageIds.Remove(msgId);
                _messageUserReactions.Remove(msgId);
                InvokeAsync(StateHasChanged);
            }
        });

        _hubConnection.On<Guid, Guid, bool>("MessagePinUpdated", (roomId, msgId, isPinned) =>
        {
            if (_activeRoom?.Id == roomId)
            {
                var msg = _messages.FirstOrDefault(m => m.Id == msgId);
                if (msg != null)
                {
                    msg.IsPinned = isPinned;
                }
                if (isPinned)
                {
                    _pinnedMessageIds.Add(msgId);
                }
                else
                {
                    _pinnedMessageIds.Remove(msgId);
                }
                InvokeAsync(StateHasChanged);
            }
        });

        _hubConnection.On<Guid, Guid, string, string?>("MessageReactionUpdated", (roomId, msgId, userId, emoji) =>
        {
            if (_activeRoom?.Id == roomId)
            {
                if (!_messageUserReactions.TryGetValue(msgId, out var userMap))
                {
                    userMap = new Dictionary<string, string>();
                    _messageUserReactions[msgId] = userMap;
                }

                if (string.IsNullOrEmpty(emoji))
                {
                    userMap.Remove(userId);
                }
                else
                {
                    userMap[userId] = emoji;
                }

                var msg = _messages.FirstOrDefault(m => m.Id == msgId);
                if (msg != null)
                {
                    msg.Reactions.RemoveAll(r => r.UserId == userId);
                    if (!string.IsNullOrEmpty(emoji))
                    {
                        msg.Reactions.Add(new ChatReactionDto { UserId = userId, Emoji = emoji });
                    }
                }

                InvokeAsync(StateHasChanged);
            }
        });

        _hubConnection.On<Guid, string>("UserTyping", (roomId, userId) =>
        {
            if (_activeRoom?.Id == roomId && userId != _currentEmployeeId)
            {
                var member = _activeRoom.Members.FirstOrDefault(m => m.UserId == userId);
                _typingUserName = member != null ? member.FirstName : "Someone";
                _isTyping = true;
                _typingTimer?.Stop();
                _typingTimer?.Dispose();
                _typingTimer = new System.Timers.Timer(2500);
                _typingTimer.Elapsed += (s, e) => { _isTyping = false; _typingUserName = null; InvokeAsync(StateHasChanged); };
                _typingTimer.Start();
                InvokeAsync(StateHasChanged);
            }
        });

        _hubConnection.On<Guid>("RoomCreated", async (roomId) =>
        {
            await LoadRooms();
            if (_activeRoom?.Id == roomId)
            {
                var refreshed = _rooms.FirstOrDefault(r => r.Id == roomId);
                if (refreshed != null)
                {
                    _activeRoom = refreshed;
                    await InvokeAsync(StateHasChanged);
                }
            }
        });

        _hubConnection.On<Guid, string?>("RoomPictureUpdated", (roomId, pictureUrl) =>
        {
            var room = _rooms.FirstOrDefault(r => r.Id == roomId);
            if (room != null)
            {
                room.PictureUrl = pictureUrl;
            }
            if (_activeRoom?.Id == roomId)
            {
                _activeRoom.PictureUrl = pictureUrl;
            }
            InvokeAsync(StateHasChanged);
        });

        _hubConnection.On("MemberJoined", async () =>
        {
            await ReloadUsers();
            await LoadRooms();
            await InvokeAsync(StateHasChanged);
        });

        try
        {
            _hubConnection.Reconnected += async _ => { if (_activeRoom != null) { try { await _hubConnection.InvokeAsync("JoinRoom", _activeRoom.Id); } catch { } } };
            await _hubConnection.StartAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to Chat SignalR: {ex.Message}");
        }
        await LoadRooms();
    }

    private async Task ReloadUsers()
    {
        try 
        { 
            var users = await Http.GetFromJsonAsync<List<ChatUserDto>>("/api/chat/users") ?? new();
            var me = users.FirstOrDefault(u => u.ApplicationUserId == _currentUserId);
            if (me != null) _currentEmployeeId = me.Id;
            users.RemoveAll(u => u.Id == _currentEmployeeId);
            _allUsers = users;
        } 
        catch { }
    }

    private void ToggleChat()
    {
        _isOpen = !_isOpen;
        if (!_isOpen && _activeRoom != null)
        {
            _activeRoom = null;
            _showGroupDetails = false;
        }
        else if (_isOpen)
        {
            _ = LoadMyBoards();
        }
    }

    private async Task LoadRooms()
    {
        try
        {
            _isLoadingRooms = true;
            _rooms = await Http.GetFromJsonAsync<List<ChatRoomDto>>("/api/chat/rooms") ?? new();
        }
        catch { }
        finally
        {
            _isLoadingRooms = false;
            StateHasChanged();
        }
    }

    private void OpenNewDirectMessage()
    {
        _newChatTab = "direct";
        _userSearchQuery = string.Empty;
        _showNewMessage = true;
    }

    private void OpenNewGroupChat()
    {
        _newChatTab = "group";
        _newGroupName = string.Empty;
        _selectedMemberIds.Clear();
        _createGroupError = null;
        _groupSearchQuery = string.Empty;
        ClearNewGroupPicture();
        _showNewMessage = true;
    }

    private void ToggleGroupMember(string userId)
    {
        if (_selectedMemberIds.Contains(userId))
            _selectedMemberIds.Remove(userId);
        else
            _selectedMemberIds.Add(userId);
    }

    private async Task CreateGroupChat()
    {
        if (string.IsNullOrWhiteSpace(_newGroupName))
        {
            _createGroupError = "Please enter a group name.";
            return;
        }

        if (!_selectedMemberIds.Any())
        {
            _createGroupError = "Please select at least 1 colleague.";
            return;
        }

        _isCreatingGroup = true;
        _createGroupError = null;

        try
        {
            var req = new
            {
                Name = _newGroupName.Trim(),
                MemberIds = _selectedMemberIds.ToList()
            };

            var res = await Http.PostAsJsonAsync("/api/chat/group", req);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadFromJsonAsync<ChatRoomCreationResponse>();
                _showNewMessage = false;
                _newGroupName = string.Empty;
                _selectedMemberIds.Clear();

                if (content != null && _newGroupPictureBytes != null)
                {
                    try
                    {
                        using var uploadContent = new MultipartFormDataContent();
                        var byteContent = new ByteArrayContent(_newGroupPictureBytes);
                        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_newGroupPictureContentType ?? "image/jpeg");
                        uploadContent.Add(byteContent, "file", $"{content.RoomId}.jpg");
                        await Http.PostAsync($"/api/chat/rooms/{content.RoomId}/picture", uploadContent);
                    }
                    catch { }
                }

                ClearNewGroupPicture();
                await LoadRooms();

                if (content != null)
                {
                    var room = _rooms.FirstOrDefault(r => r.Id == content.RoomId);
                    if (room != null)
                    {
                        await OpenRoom(room);
                    }
                }
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                _createGroupError = !string.IsNullOrWhiteSpace(err) ? err : "Failed to create group chat.";
            }
        }
        catch
        {
            _createGroupError = "Could not connect to the server.";
        }
        finally
        {
            _isCreatingGroup = false;
        }
    }

    private async Task OpenRoom(ChatRoomDto room)
    {
        _activeRoom = room;
        _messages.Clear();
        _pinnedMessageIds.Clear();
        _messageUserReactions.Clear();
        _isLoadingMessages = true;
        _showGroupDetails = false;
        _showAddMembers = false;
        _activeMessageMenuId = null;
        _activeReactionMenuId = null;
        _replyingToMessage = null;
        room.UnreadCount = 0;
        ClearPendingAttachment();
        _ = LoadMyBoards();
        
        try
        {
            await _hubConnection!.SendAsync("JoinRoom", room.Id.ToString());
            _messages = await Http.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{room.Id}/messages") ?? new();

            _pinnedMessageIds.Clear();
            _messageUserReactions.Clear();
            foreach (var m in _messages)
            {
                if (m.IsPinned)
                {
                    _pinnedMessageIds.Add(m.Id);
                }
                if (m.Reactions != null && m.Reactions.Any())
                {
                    _messageUserReactions[m.Id] = m.Reactions.ToDictionary(r => r.UserId, r => r.Emoji);
                }
            }

            await Http.PostAsync($"/api/chat/rooms/{room.Id}/read", null);
        }
        catch { }
        finally
        {
            _isLoadingMessages = false;
            StateHasChanged();
        }
    }

    private async Task CloseRoom()
    {
        if (_activeRoom != null)
        {
            try { await _hubConnection!.SendAsync("LeaveRoom", _activeRoom.Id.ToString()); } catch { }
            _activeRoom = null;
            _showGroupDetails = false;
            _activeMessageMenuId = null;
            _activeReactionMenuId = null;
            _replyingToMessage = null;
            ClearPendingAttachment();
            await LoadRooms();
        }
    }

    private void ToggleGroupDetails()
    {
        _showGroupDetails = !_showGroupDetails;
        _showAddMembers = false;
        _selectedAddMemberIds.Clear();
        _addMemberSearchQuery = string.Empty;
    }

    private void ToggleAddMemberCandidate(string userId)
    {
        if (_selectedAddMemberIds.Contains(userId))
            _selectedAddMemberIds.Remove(userId);
        else
            _selectedAddMemberIds.Add(userId);
    }

    private async Task AddMembersToActiveGroup()
    {
        if (_activeRoom == null || !_selectedAddMemberIds.Any()) return;

        _isAddingMembers = true;
        try
        {
            var req = new { MemberIds = _selectedAddMemberIds.ToList() };
            var res = await Http.PostAsJsonAsync($"/api/chat/rooms/{_activeRoom.Id}/members", req);
            if (res.IsSuccessStatusCode)
            {
                _selectedAddMemberIds.Clear();
                _showAddMembers = false;
                await LoadRooms();
                var refreshed = _rooms.FirstOrDefault(r => r.Id == _activeRoom.Id);
                if (refreshed != null) _activeRoom = refreshed;
            }
        }
        catch { }
        finally
        {
            _isAddingMembers = false;
        }
    }

    private async Task LeaveActiveGroup()
    {
        if (_activeRoom == null) return;
        _isLeavingGroup = true;

        try
        {
            var res = await Http.PostAsync($"/api/chat/rooms/{_activeRoom.Id}/leave", null);
            if (res.IsSuccessStatusCode)
            {
                _showGroupDetails = false;
                await CloseRoom();
            }
        }
        catch { }
        finally
        {
            _isLeavingGroup = false;
        }
    }

    private bool _isUploadingAttachment = false;
    private string? _pendingAttachmentUrl;
    private string? _pendingAttachmentName;
    private long? _pendingAttachmentSize;
    private int _inputFileKey = 0;

    // Media viewer state for chat attachments
    private bool _showMediaViewer = false;
    private string? _selectedMediaUrl;
    private string? _selectedMediaName;

    private static bool IsImageAttachment(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        var ext = System.IO.Path.GetExtension(url).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif";
    }

    private static bool IsPdfAttachment(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        var ext = System.IO.Path.GetExtension(url).ToLowerInvariant();
        return ext is ".pdf";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private void OpenAttachmentViewer(ChatMessageDto msg)
    {
        if (string.IsNullOrEmpty(msg.AttachmentUrl)) return;
        _selectedMediaUrl = MediaUrlHelper.Resolve(msg.AttachmentUrl, Configuration);
        _selectedMediaName = msg.AttachmentName ?? "Attachment";
        _showMediaViewer = true;
    }

    private void ClearPendingAttachment()
    {
        _pendingAttachmentUrl = null;
        _pendingAttachmentName = null;
        _pendingAttachmentSize = null;
        _inputFileKey++;
    }

    private async Task HandleChatFileUpload(InputFileChangeEventArgs e)
    {
        if (_activeRoom == null || e.File == null) return;

        try
        {
            _isUploadingAttachment = true;
            StateHasChanged();

            var maxFileSize = 25 * 1024 * 1024; // 25MB
            if (e.File.Size > maxFileSize)
            {
                ToastService.ShowError("File exceeds the 25MB limit.");
                return;
            }

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(e.File.OpenReadStream(maxFileSize));
            if (!string.IsNullOrEmpty(e.File.ContentType))
            {
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(e.File.ContentType);
            }
            content.Add(streamContent, "file", e.File.Name);

            var response = await Http.PostAsync($"/api/chat/rooms/{_activeRoom.Id}/attachment", content);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatAttachmentUploadResponse>();
                if (result != null && !string.IsNullOrEmpty(result.AttachmentUrl))
                {
                    _pendingAttachmentUrl = result.AttachmentUrl;
                    _pendingAttachmentName = result.AttachmentName;
                    _pendingAttachmentSize = result.AttachmentSize;
                }
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                ToastService.ShowError(!string.IsNullOrWhiteSpace(err) ? err : "Failed to upload attachment.");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Upload error: {ex.Message}");
        }
        finally
        {
            _isUploadingAttachment = false;
            StateHasChanged();
        }
    }

    private async Task SendMessage()
    {
        if ((string.IsNullOrWhiteSpace(_newMessageContent) && string.IsNullOrEmpty(_pendingAttachmentUrl)) || _activeRoom == null) return;

        var content = _newMessageContent?.Trim() ?? string.Empty;

        // Apply markdown mappings before sending
        foreach(var kvp in _draftMentions.OrderByDescending(k => k.Key.Length))
        {
            content = content.Replace(kvp.Key, kvp.Value);
        }

        if (_replyingToMessage != null)
        {
            var sender = _replyingToMessage.SenderId == _currentEmployeeId ? "yourself" : _replyingToMessage.SenderName;
            var preview = GetPreviewText(_replyingToMessage.Content);
            if (string.IsNullOrEmpty(preview) && !string.IsNullOrEmpty(_replyingToMessage.AttachmentName)) preview = _replyingToMessage.AttachmentName;
            content = $"> Replying to {sender}: \"{preview}\"\n\n{content}";
            _replyingToMessage = null;
        }

        var attUrl = _pendingAttachmentUrl;
        var attName = _pendingAttachmentName;
        var attSize = _pendingAttachmentSize;

        _newMessageContent = string.Empty;
        _draftMentions.Clear();
        ClearPendingAttachment();
        _ = JS.InvokeVoidAsync("resetChatTextarea");

        var tempId = Guid.NewGuid();
        var optimisticMsg = new ChatMessageDto
        {
            Id = tempId,
            ChatRoomId = _activeRoom.Id,
            SenderId = _currentEmployeeId,
            SenderName = "Me",
            Content = content,
            AttachmentUrl = attUrl,
            AttachmentName = attName,
            AttachmentSize = attSize,
            CreatedAt = DateTime.UtcNow,
            IsOptimistic = true
        };
        _messages.Add(optimisticMsg);
        StateHasChanged();

        try
        {
            var req = new
            {
                Content = content,
                AttachmentUrl = attUrl,
                AttachmentName = attName,
                AttachmentSize = attSize
            };
            var response = await Http.PostAsJsonAsync($"/api/chat/rooms/{_activeRoom.Id}/messages", req);
            
            if (response.IsSuccessStatusCode)
            {
                var realMsg = await response.Content.ReadFromJsonAsync<ChatMessageDto>();
                if (realMsg != null)
                {
                    _messages.Remove(optimisticMsg);
                    _messages.RemoveAll(m => m.Id == tempId || (m.IsOptimistic && m.ChatRoomId == _activeRoom.Id && m.AttachmentUrl == attUrl));

                    if (!_messages.Any(m => m.Id == realMsg.Id))
                    {
                        _messages.Add(realMsg);
                    }
                    StateHasChanged();
                }
            }
            else
            {
                _messages.Remove(optimisticMsg);
                _messages.RemoveAll(m => m.Id == tempId);
                StateHasChanged();
            }
        }
        catch
        {
            _messages.Remove(optimisticMsg);
            _messages.RemoveAll(m => m.Id == tempId);
            StateHasChanged();
        }
    }

    private async Task HandleKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
    }

    private void HandleKeyUp(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (_activeRoom != null && (DateTime.UtcNow - _lastTypingSent).TotalSeconds > 1.5)
        {
            _lastTypingSent = DateTime.UtcNow;
            try { _hubConnection?.SendAsync("SendTypingIndicator", _activeRoom.Id.ToString()); } catch { }
        }
    }

    private string GetGroupInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "GC";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        return name.Substring(0, Math.Min(2, name.Length)).ToUpper();
    }

    public async ValueTask DisposeAsync()
    {
        _popoverReference?.Dispose();

        if (_hubConnection != null)
            await _hubConnection.DisposeAsync();
        
        _typingTimer?.Dispose();
    }

    private bool _isUploadingGroupPicture = false;
    private bool _isRemovingGroupPicture = false;

    private async Task HandleGroupPictureUpload(InputFileChangeEventArgs e)
    {
        if (_activeRoom == null || e.File == null) return;

        try
        {
            _isUploadingGroupPicture = true;
            StateHasChanged();

            var resizedImage = await e.File.RequestImageFileAsync("image/jpeg", 600, 600);
            var buffer = new byte[resizedImage.Size];
            using var stream = resizedImage.OpenReadStream(10 * 1024 * 1024);
            await stream.ReadExactlyAsync(buffer);

            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(byteContent, "file", $"{_activeRoom.Id}.jpg");

            var response = await Http.PostAsync($"/api/chat/rooms/{_activeRoom.Id}/picture", content);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatPictureUploadResponse>();
                if (result != null && !string.IsNullOrEmpty(result.PictureUrl))
                {
                    _activeRoom.PictureUrl = result.PictureUrl;
                    var roomInList = _rooms.FirstOrDefault(r => r.Id == _activeRoom.Id);
                    if (roomInList != null) roomInList.PictureUrl = result.PictureUrl;
                    ToastService.ShowSuccess("Group photo updated!");
                }
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                ToastService.ShowError($"Failed to update photo: {err}");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error uploading photo: {ex.Message}");
        }
        finally
        {
            _isUploadingGroupPicture = false;
            StateHasChanged();
        }
    }

    private async Task RemoveGroupPicture()
    {
        if (_activeRoom == null) return;

        try
        {
            _isRemovingGroupPicture = true;
            StateHasChanged();

            var response = await Http.DeleteAsync($"/api/chat/rooms/{_activeRoom.Id}/picture");
            if (response.IsSuccessStatusCode)
            {
                _activeRoom.PictureUrl = null;
                var roomInList = _rooms.FirstOrDefault(r => r.Id == _activeRoom.Id);
                if (roomInList != null) roomInList.PictureUrl = null;
                ToastService.ShowSuccess("Group photo removed.");
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                ToastService.ShowError($"Failed to remove photo: {err}");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error removing photo: {ex.Message}");
        }
        finally
        {
            _isRemovingGroupPicture = false;
            StateHasChanged();
        }
    }

    private string? _newGroupPictureDataUrl;
    private byte[]? _newGroupPictureBytes;
    private string? _newGroupPictureContentType;

    private async Task HandleNewGroupPictureSelected(InputFileChangeEventArgs e)
    {
        if (e.File == null) return;
        try
        {
            var resizedImage = await e.File.RequestImageFileAsync("image/jpeg", 500, 500);
            var buffer = new byte[resizedImage.Size];
            using var stream = resizedImage.OpenReadStream(10 * 1024 * 1024);
            await stream.ReadExactlyAsync(buffer);

            _newGroupPictureBytes = buffer;
            _newGroupPictureContentType = "image/jpeg";
            _newGroupPictureDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(buffer)}";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error selecting image: {ex.Message}");
        }
    }

    private void ClearNewGroupPicture()
    {
        _newGroupPictureDataUrl = null;
        _newGroupPictureBytes = null;
        _newGroupPictureContentType = null;
    }

    private async Task StartDirectMessage(string userId)
    {
        _showNewMessage = false;
        _userSearchQuery = string.Empty;

        try
        {
            var res = await Http.PostAsync($"/api/chat/direct/{userId}", null);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadFromJsonAsync<ChatRoomCreationResponse>();
                if (content != null)
                {
                    await LoadRooms();
                    var room = _rooms.FirstOrDefault(r => r.Id == content.RoomId);
                    if (room != null)
                    {
                        await OpenRoom(room);
                    }
                }
            }
        }
        catch { }
    }

    private Microsoft.JSInterop.DotNetObjectReference<GlobalChatWidget>? _popoverReference;

    [Microsoft.JSInterop.JSInvokable]
    public Task DismissChatPopovers()
    {
        CloseActiveMenus();
        return InvokeAsync(StateHasChanged);
    }

    private void CloseActiveMenus()
    {
        _activeMessageMenuId = null;
        _activeReactionMenuId = null;
    }

    private void ToggleMessageMenu(Guid messageId)
    {
        _activeReactionMenuId = null;
        _activeMessageMenuId = _activeMessageMenuId == messageId ? null : messageId;
    }

    private void ToggleReactionPicker(Guid messageId)
    {
        _activeMessageMenuId = null;
        _activeReactionMenuId = _activeReactionMenuId == messageId ? null : messageId;
    }
    private async Task ToggleReaction(Guid messageId, string emoji)
    {
        _activeReactionMenuId = null;

        if (!_messageUserReactions.TryGetValue(messageId, out var userMap))
        {
            userMap = new Dictionary<string, string>();
            _messageUserReactions[messageId] = userMap;
        }

        var userId = !string.IsNullOrEmpty(_currentEmployeeId) ? _currentEmployeeId : "current-user";

        string? resultingEmoji = null;
        if (userMap.TryGetValue(userId, out var existingEmoji) && existingEmoji == emoji)
        {
            userMap.Remove(userId);
            resultingEmoji = null;
        }
        else
        {
            userMap[userId] = emoji;
            resultingEmoji = emoji;
        }

        var msg = _messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.Reactions.RemoveAll(r => r.UserId == userId);
            if (!string.IsNullOrEmpty(resultingEmoji))
            {
                msg.Reactions.Add(new ChatReactionDto { UserId = userId, Emoji = resultingEmoji });
            }
        }

        StateHasChanged();

        try
        {
            await Http.PostAsJsonAsync($"/api/chat/messages/{messageId}/react", new { Emoji = emoji });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling reaction: {ex.Message}");
        }
    }

    private void StartReply(ChatMessageDto msg)
    {
        _replyingToMessage = msg;
        _activeMessageMenuId = null;
        _activeReactionMenuId = null;
        StateHasChanged();
    }

    private void CancelReply()
    {
        _replyingToMessage = null;
    }

    private async Task TogglePinMessage(Guid messageId)
    {
        _activeMessageMenuId = null;

        bool willBePinned;
        if (_pinnedMessageIds.Contains(messageId))
        {
            _pinnedMessageIds.Remove(messageId);
            willBePinned = false;
        }
        else
        {
            _pinnedMessageIds.Add(messageId);
            willBePinned = true;
        }

        var msg = _messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.IsPinned = willBePinned;
        }

        StateHasChanged();

        try
        {
            await Http.PutAsync($"/api/chat/messages/{messageId}/pin", null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling pin: {ex.Message}");
        }
    }

    private async Task UnsendMessage(Guid messageId)
    {
        _activeMessageMenuId = null;
        try
        {
            var res = await Http.DeleteAsync($"/api/chat/messages/{messageId}");
            if (res.IsSuccessStatusCode)
            {
                _messages.RemoveAll(m => m.Id == messageId);
                _pinnedMessageIds.Remove(messageId);
                _messageUserReactions.Remove(messageId);
                StateHasChanged();
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                ToastService.ShowError(!string.IsNullOrWhiteSpace(err) ? err : "Failed to unsend message.");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error unsending message: {ex.Message}");
        }
    }

    private void ForwardMessage(ChatMessageDto msg)
    {
        _activeMessageMenuId = null;
        _activeReactionMenuId = null;
        _messageToForward = msg;
        _forwardSearchQuery = string.Empty;
        _forwardedRoomIds.Clear();
        _showForwardModal = true;
        StateHasChanged();
    }

    private void CloseForwardModal()
    {
        _showForwardModal = false;
        _messageToForward = null;
        _forwardSearchQuery = string.Empty;
    }

    private async Task SendForwardToRoom(ChatRoomDto targetRoom)
    {
        if (_messageToForward == null || _isForwarding) return;

        try
        {
            _isForwarding = true;
            StateHasChanged();

            var req = new
            {
                Content = _messageToForward.Content ?? string.Empty,
                AttachmentUrl = _messageToForward.AttachmentUrl,
                AttachmentName = _messageToForward.AttachmentName,
                AttachmentSize = _messageToForward.AttachmentSize
            };

            var response = await Http.PostAsJsonAsync($"/api/chat/rooms/{targetRoom.Id}/messages", req);
            if (response.IsSuccessStatusCode)
            {
                _forwardedRoomIds.Add(targetRoom.Id);
                var roomDisplayName = targetRoom.Type == 0
                    ? (targetRoom.Members.FirstOrDefault(m => m.UserId != _currentEmployeeId)?.FirstName ?? "Direct Chat")
                    : targetRoom.Name;

                ToastService.ShowSuccess($"Message forwarded to {roomDisplayName}!");

                if (_activeRoom?.Id == targetRoom.Id)
                {
                    _messages = await Http.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{targetRoom.Id}/messages") ?? new();
                }
                else
                {
                    await LoadRooms();
                }
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                ToastService.ShowError(!string.IsNullOrWhiteSpace(err) ? err : "Failed to forward message.");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error forwarding message: {ex.Message}");
        }
        finally
        {
            _isForwarding = false;
            StateHasChanged();
        }
    }

    private void ReportMessage(ChatMessageDto msg)
    {
        _activeMessageMenuId = null;
        ToastService.ShowSuccess("Message reported to administrator.");
    }

    private bool ShouldShowDivider(DateTime current, DateTime? previous)
    {
        if (!previous.HasValue) return true;
        if (current.ToLocalTime().Date != previous.Value.ToLocalTime().Date) return true;
        return (current - previous.Value).TotalMinutes >= 20;
    }

    private string FormatMessengerDivider(DateTime dt)
    {
        var local = dt.ToLocalTime();
        var now = DateTime.Now;
        if (local.Date == now.Date)
        {
            return local.ToString("h:mm tt");
        }
        else if ((now.Date - local.Date).TotalDays < 7 && local.Date < now.Date)
        {
            return local.ToString("ddd h:mm tt");
        }
        else if (local.Year == now.Year)
        {
            return local.ToString("MMM d, h:mm tt");
        }
        else
        {
            return local.ToString("MMM d, yyyy, h:mm tt");
        }
    }
}
