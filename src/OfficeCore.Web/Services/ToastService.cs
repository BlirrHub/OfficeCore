namespace OfficeCore.Web.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class ToastMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ToastLevel Level { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ToastService
{
    public event Action<ToastMessage>? OnShow;
    public event Action<Guid>? OnHide;

    public void ShowToast(ToastLevel level, string message, string title = "")
    {
        OnShow?.Invoke(new ToastMessage
        {
            Level = level,
            Message = message,
            Title = title
        });
    }

    public void ShowInfo(string message, string title = "Info")
    {
        ShowToast(ToastLevel.Info, message, title);
    }

    public void ShowSuccess(string message, string title = "Success")
    {
        ShowToast(ToastLevel.Success, message, title);
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        ShowToast(ToastLevel.Warning, message, title);
    }

    public void ShowError(string message, string title = "Error")
    {
        ShowToast(ToastLevel.Error, message, title);
    }
    
    public void HideToast(Guid id)
    {
        OnHide?.Invoke(id);
    }
}
