using Propulse.Core.Entities;

namespace Propulse.Core.Interfaces;

public interface IAiService
{
    Task<string> GenerateReplyAsync(
        string currentMessage,
        List<WhatsAppMessage> conversationHistory,
        List<WhatsAppMessage> relevantData);
}
