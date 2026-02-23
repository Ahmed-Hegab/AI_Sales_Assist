using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Propulse.Core.Entities;
using Propulse.Core.Interfaces;
using Propulse.Infrastructure.Data;
using System.Text.Json;

namespace Propulse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly ILogger<WhatsAppController> _logger;
    private readonly PropulseDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IAiService _aiService;

    public WhatsAppController(
        ILogger<WhatsAppController> logger, 
        PropulseDbContext context,
        IWhatsAppService whatsAppService,
        IAiService aiService)
    {
        _logger = logger;
        _context = context;
        _whatsAppService = whatsAppService;
        _aiService = aiService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage([FromForm] WhatsAppMessageDto message)
    {
        _logger.LogInformation("Received message from {From}: {Body}", message.From, message.Body);

        var newMessage = new WhatsAppMessage
        {
            SenderPhoneNumber = message.From ?? "Unknown",
            SenderName = message.ProfileName ?? "Unknown",
            Content = message.Body ?? "",
            GroupId = "",
            MessageType = message.NumMedia != "0" ? "media" : "text",
            CreatedAt = DateTime.UtcNow
        };

        _context.WhatsAppMessages.Add(newMessage);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(newMessage.Content)
            && !string.IsNullOrEmpty(newMessage.SenderPhoneNumber)
            && !newMessage.SenderPhoneNumber.Contains("g.us"))
        {
            try
            {
                var history = await _context.WhatsAppMessages
                    .Where(m => m.SenderPhoneNumber == newMessage.SenderPhoneNumber && m.Id != newMessage.Id)
                    .OrderBy(m => m.CreatedAt)
                    .TakeLast(20)
                    .ToListAsync();

                var aiReply = await _aiService.GenerateReplyAsync(newMessage.Content, history);

                newMessage.BotReply = aiReply;
                newMessage.IsProcessed = true;
                await _context.SaveChangesAsync();

                await _whatsAppService.SendMessageAsync(newMessage.SenderPhoneNumber, aiReply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI processing or reply failed");
            }
        }

        return Ok("Message Received and Processed");
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var total = _context.WhatsAppMessages.Count();
        var messages = await _context.WhatsAppMessages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.SenderPhoneNumber,
                m.SenderName,
                m.Content,
                m.BotReply,
                m.MessageType,
                m.IsProcessed,
                m.IsOffer,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, messages });
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok("Propulse API is Running and DB is connected!");
    }
}

public class WhatsAppMessageDto
{
    public string? From { get; set; }
    public string? Body { get; set; }
    public string? SmsMessageSid { get; set; }
    public string? NumMedia { get; set; }
    public string? ProfileName { get; set; }
}
