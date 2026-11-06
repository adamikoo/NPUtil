namespace NPUtil.Core.Services;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public record NotificationItem(
    string Title,
    string Message,
    NotificationSeverity Severity = NotificationSeverity.Info,
    DateTime Timestamp = default
)
{
    public DateTime Timestamp { get; init; } = Timestamp == default ? DateTime.Now : Timestamp;
}

public interface INotificationService
{
    event EventHandler<NotificationItem>? NotificationReceived;
    void Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.Info);
}

public class NotificationService : INotificationService
{
    public event EventHandler<NotificationItem>? NotificationReceived;

    public void Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        var item = new NotificationItem(title, message, severity);
        NotificationReceived?.Invoke(this, item);
    }
}
