using MAPS.Shared.Enums;

namespace MAPS.Shared.DTOs.Chat;

public class MessageDto
{
    public Guid MessageId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
    public FileAttachmentDto? Attachment { get; set; }
}

public class SendMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;
    public string? AttachmentKey { get; set; } // MinIO object key
}

public class FileAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}

public class TypingIndicatorDto
{
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
}

public class MarkReadRequest
{
    public List<Guid> MessageIds { get; set; } = new();
}
