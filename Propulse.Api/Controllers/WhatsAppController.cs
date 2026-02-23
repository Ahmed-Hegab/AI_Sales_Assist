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
        _logger.LogInformation($"Received message from {message.From}: {message.Body}");

        // 1. Save to Database
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

        _logger.LogInformation($"Message saved efficiently with ID: {newMessage.Id}");
        
        // 2. AI Processing (Currently Disabled/Mocked)
        if (!string.IsNullOrEmpty(newMessage.Content))
        {
            try 
            {
               var aiResponse = await _aiService.ParseMessageAsync(newMessage.Content);
               newMessage.StructuredDataJson = aiResponse;
               
               newMessage.IsProcessed = true; // Mark as processed even if AI is dummy
               await _context.SaveChangesAsync();
               
               // Simple Echo Reply (Since AI is off)
               if (!string.IsNullOrEmpty(newMessage.SenderPhoneNumber) && !newMessage.SenderPhoneNumber.Contains("g.us"))
               {
                    await _whatsAppService.SendMessageAsync(newMessage.SenderPhoneNumber, $"Propulse (Beta) Saved: {newMessage.Content}");
               }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Processing failed");
            }
        }
        
        return Ok("Message Received and Saved");
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
                m.MessageType,
                m.IsProcessed,
                m.IsOffer,
                m.StructuredDataJson,
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
