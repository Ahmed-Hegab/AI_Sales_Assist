using OpenAI;
using OpenAI.Chat;
using Propulse.Core.Entities;
using Propulse.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Propulse.Infrastructure.Services;

public class OpenAiService : IAiService
{
    private readonly ChatClient? _chatClient;
    private readonly ILogger<OpenAiService> _logger;

    private const string SystemPrompt = """
        You are Propulse, a friendly and professional AI sales assistant for a real estate company.
        
        Your role:
        - Help customers find properties (apartments, villas, offices, land)
        - Ask clarifying questions: budget, preferred location, size, number of rooms, buy vs rent
        - Provide helpful guidance through the buying/renting process
        - Be warm, natural, and conversational — like a knowledgeable friend, not a robot
        
        Rules:
        - ALWAYS reply in the SAME LANGUAGE the customer is using (Arabic, English, etc.)
        - Keep responses concise (2-4 sentences max) — this is WhatsApp, not email
        - Never make up property listings or prices
        - If you don't know something, say you'll check and get back to them
        - Use the conversation history to maintain context — don't re-ask questions already answered
        """;

    public OpenAiService(IConfiguration configuration, ILogger<OpenAiService> logger)
    {
        _logger = logger;

        var apiKey = configuration["OpenAI:ApiKey"];
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        if (!string.IsNullOrEmpty(apiKey))
        {
            _chatClient = new ChatClient(model, apiKey);
            _logger.LogInformation("OpenAI service initialized with model: {Model}", model);
        }
        else
        {
            _logger.LogWarning("OpenAI API key not configured — AI replies disabled");
        }
    }

    public async Task<string> GenerateReplyAsync(string currentMessage, List<WhatsAppMessage> conversationHistory)
    {
        if (_chatClient == null)
            return $"Propulse (Beta): {currentMessage}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt)
        };

        foreach (var msg in conversationHistory)
        {
            messages.Add(new UserChatMessage(msg.Content));
            if (!string.IsNullOrEmpty(msg.BotReply))
                messages.Add(new AssistantChatMessage(msg.BotReply));
        }

        messages.Add(new UserChatMessage(currentMessage));

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages);
            var reply = completion.Value.Content[0].Text;
            _logger.LogInformation("AI generated reply for message");
            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed");
            return $"Propulse (Beta): {currentMessage}";
        }
    }
}
