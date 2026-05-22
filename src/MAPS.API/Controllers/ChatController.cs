using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Services.Storage;
using MAPS.Shared.DTOs.Chat;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext      _context;
    private readonly IMinioStorageService _storage;

    public ChatController(AppDbContext context, IMinioStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    private Guid UserId => Guid.Parse(
        User.FindFirst(MAPS.Shared.Constants.ClaimTypeNames.UserId)!.Value);

    /// <summary>GET /api/chat/history/{userId} — message history with a user</summary>
    [HttpGet("history/{otherUserId:guid}")]
    public async Task<IActionResult> GetHistory(
        Guid otherUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var messages = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => (m.SenderId == UserId   && m.ReceiverId == otherUserId) ||
                        (m.SenderId == otherUserId && m.ReceiverId == UserId))
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto
            {
                MessageId   = m.MessageId,
                SenderId    = m.SenderId,
                SenderName  = m.Sender.FullName,
                ReceiverId  = m.ReceiverId,
                Content     = m.Content,
                MessageType = m.MessageType,
                IsRead      = m.IsRead,
                SentAt      = m.SentAt
            })
            .ToListAsync();

        // Return in chronological order (oldest first)
        messages.Reverse();

        return Ok(ApiResponse<List<MessageDto>>.Ok(messages));
    }

    /// <summary>GET /api/chat/contacts — list of chat partners</summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts()
    {
        var contactIds = await _context.ChatMessages
            .Where(m => m.SenderId == UserId || m.ReceiverId == UserId)
            .Select(m => m.SenderId == UserId ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToListAsync();

        var contacts = await _context.Users
            .Where(u => contactIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Role,
                UnreadCount = _context.ChatMessages.Count(m =>
                    m.SenderId == u.UserId &&
                    m.ReceiverId == UserId &&
                    !m.IsRead),
                LastMessage = _context.ChatMessages
                    .Where(m => (m.SenderId == UserId && m.ReceiverId == u.UserId) ||
                                (m.SenderId == u.UserId && m.ReceiverId == UserId))
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new { m.Content, m.SentAt })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(contacts));
    }

    /// <summary>POST /api/chat/upload — upload file attachment for chat</summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadAttachment(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        // Validate file type
        var allowed = new[] { "image/jpeg", "image/png", "image/gif",
                               "application/pdf",
                               "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(ApiResponse.Fail(
                "Only images, PDF, and DOCX files are allowed."));

        // 10MB limit
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File size must not exceed 10MB."));

        using var stream = file.OpenReadStream();
        var objectKey    = $"chat/{UserId}/{Guid.NewGuid()}/{file.FileName}";
        var url          = await _storage.UploadAsync(objectKey, stream, file.ContentType);

        return Ok(ApiResponse<object>.Ok(new
        {
            objectKey,
            url,
            fileName    = file.FileName,
            contentType = file.ContentType,
            sizeBytes   = file.Length
        }));
    }

    /// <summary>GET /api/chat/unread-count</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _context.ChatMessages
            .CountAsync(m => m.ReceiverId == UserId && !m.IsRead);
        return Ok(ApiResponse<int>.Ok(count));
    }
}
