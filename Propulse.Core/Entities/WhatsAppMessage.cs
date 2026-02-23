namespace Propulse.Core.Entities;

public class WhatsAppMessage : BaseEntity
{
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    
    public bool IsProcessed { get; set; } = false;
    public bool IsOffer { get; set; } = false;
    public string? StructuredDataJson { get; set; }
    public string? BotReply { get; set; }
}
