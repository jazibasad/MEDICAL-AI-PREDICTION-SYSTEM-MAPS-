using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.Constants;
using MAPS.Shared.DTOs.Chat;
using MAPS.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace MAPS.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext        _context;
    private readonly IAssignmentRepository _assignRepo;
    private readonly IAuditRepository    _auditRepo;
    private readonly ILogger<ChatHub>    _logger;

    // Track connected users: userId → connectionId
    private static readonly Dictionary<string, HashSet<string>> _connections = new();
    private static readonly object _lock = new();

    public ChatHub(
        AppDbContext          context,
        IAssignmentRepository assignRepo,
        IAuditRepository      auditRepo,
        ILogger<ChatHub>      logger)
    {
        _context    = context;
        _assignRepo = assignRepo;
        _auditRepo  = auditRepo;
        _logger     = logger;
    }

    // ── Connection Lifecycle ──────────────────────────────────────────────────
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        lock (_lock)
        {
            if (!_connections.ContainsKey(userId))
                _connections[userId] = new HashSet<string>();
            _connections[userId].Add(Context.ConnectionId);
        }

        // Join personal group for targeted messages
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        _logger.LogInformation("User {UserId} connected to ChatHub", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        lock (_lock)
        {
            if (_connections.ContainsKey(userId))
            {
                _connections[userId].Remove(Context.ConnectionId);
                if (_connections[userId].Count == 0)
                    _connections.Remove(userId);
            }
        }

        _logger.LogInformation("User {UserId} disconnected from ChatHub", userId);
        await base.OnDisconnectedAsync(exception);
    }

    // ── Send Message ──────────────────────────────────────────────────────────
    public async Task SendMessage(SendMessageRequest request)
    {
        var senderId   = Guid.Parse(GetUserId());
        var receiverId = request.ReceiverId;

        // Role-based permission check
        if (!await CanSendMessageAsync(senderId, receiverId))
        {
            await Clients.Caller.SendAsync("Error",
                "You are not permitted to send messages to this user.");
            return;
        }

        // Persist to database
        var message = new ChatMessage
        {
            SenderId    = senderId,
            ReceiverId  = receiverId,
            Content     = request.Content,
            MessageType = request.MessageType,
            IsRead      = false,
            SentAt      = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // Build DTO for delivery
        var senderName = await _context.Users
            .Where(u => u.UserId == senderId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Unknown";

        var dto = new MessageDto
        {
            MessageId    = message.MessageId,
            SenderId     = senderId,
            SenderName   = senderName,
            ReceiverId   = receiverId,
            Content      = message.Content,
            MessageType  = message.MessageType,
            IsRead       = false,
            SentAt       = message.SentAt
        };

        // Deliver to receiver group (all their connected devices)
        await Clients.Group($"user-{receiverId}")
            .SendAsync("ReceiveMessage", dto);

        // Echo back to sender (confirm delivery)
        await Clients.Caller.SendAsync("MessageSent", dto);

        _logger.LogInformation(
            "Message sent: {SenderId} → {ReceiverId} ({Type})",
            senderId, receiverId, message.MessageType);
    }

    // ── Typing Indicator ──────────────────────────────────────────────────────
    public async Task SendTyping(Guid receiverId, bool isTyping)
    {
        var senderId   = Guid.Parse(GetUserId());
        var senderName = await _context.Users
            .Where(u => u.UserId == senderId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Someone";

        await Clients.Group($"user-{receiverId}")
            .SendAsync("TypingIndicator", new TypingIndicatorDto
            {
                SenderId   = senderId,
                SenderName = senderName,
                IsTyping   = isTyping
            });
    }

    // ── Mark Messages as Read ─────────────────────────────────────────────────
    public async Task MarkRead(MarkReadRequest request)
    {
        var userId   = Guid.Parse(GetUserId());
        var messages = await _context.ChatMessages
            .Where(m => request.MessageIds.Contains(m.MessageId) &&
                        m.ReceiverId == userId)
            .ToListAsync();

        foreach (var msg in messages)
            msg.IsRead = true;

        await _context.SaveChangesAsync();

        // Notify the sender that messages were read
        if (messages.Any())
        {
            var senderId = messages.First().SenderId;
            await Clients.Group($"user-{senderId}")
                .SendAsync("MessagesRead", request.MessageIds);
        }
    }

    // ── Notification Broadcast ────────────────────────────────────────────────
    public static async Task SendNotificationAsync(
        IHubContext<ChatHub> hubContext,
        Guid userId,
        string type,
        object payload)
    {
        await hubContext.Clients
            .Group($"user-{userId}")
            .SendAsync("Notification", new { type, payload, timestamp = DateTime.UtcNow });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private string GetUserId() =>
        Context.User?.FindFirst(ClaimTypeNames.UserId)?.Value
        ?? throw new HubException("User not authenticated.");

    private async Task<bool> CanSendMessageAsync(Guid senderId, Guid receiverId)
    {
        var sender = await _context.Users.FindAsync(senderId);
        var receiver = await _context.Users.FindAsync(receiverId);
        if (sender is null || receiver is null) return false;

        // Admin can chat with anyone
        if (sender.Role == UserRole.Admin) return true;

        // Doctor can chat with admin or assigned patients only
        if (sender.Role == UserRole.Doctor)
        {
            if (receiver.Role == UserRole.Admin) return true;
            if (receiver.Role == UserRole.Doctor) return true; // Colleagues

            // Check if patient is assigned to this doctor
            var doctorProfile = await _context.DoctorProfiles
                .FirstOrDefaultAsync(d => d.UserId == senderId);
            var patientProfile = await _context.PatientProfiles
                .FirstOrDefaultAsync(p => p.UserId == receiverId);
            if (doctorProfile is null || patientProfile is null) return false;
            return await _assignRepo.IsPatientAssignedToDoctor(
                patientProfile.PatientId, doctorProfile.DoctorId);
        }

        // Patient can only chat with their assigned doctor or admin
        if (sender.Role == UserRole.Patient)
        {
            if (receiver.Role == UserRole.Admin) return true;
            if (receiver.Role == UserRole.Doctor)
            {
                var patientProfile = await _context.PatientProfiles
                    .FirstOrDefaultAsync(p => p.UserId == senderId);
                var doctorProfile = await _context.DoctorProfiles
                    .FirstOrDefaultAsync(d => d.UserId == receiverId);
                if (patientProfile is null || doctorProfile is null) return false;
                return await _assignRepo.IsPatientAssignedToDoctor(
                    patientProfile.PatientId, doctorProfile.DoctorId);
            }
        }

        return false;
    }
}
