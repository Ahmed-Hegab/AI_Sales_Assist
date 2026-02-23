using OpenAI;
using OpenAI.Chat;
using Propulse.Core.Entities;
using Propulse.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Propulse.Infrastructure.Services;

public class OpenAiService : IAiService
{
    private readonly ChatClient? _chatClient;
    private readonly ILogger<OpenAiService> _logger;

    private const string SystemPrompt = """
        You are Propulse, an AI sales assistant for a real estate company.
        You help customers find properties by searching ONLY from the data provided to you.

        CRITICAL RULES:
        - You will receive a section called [AVAILABLE DATA FROM DATABASE].
          This contains real messages/listings from the company's database.
        - If the database has matching properties → present them immediately in a friendly, humanized way.
          Summarize the key info (location, size, price, rooms) clearly.
        - If the database has NO matching results → be HONEST. Say something like:
          "للأسف مش لاقي حاجة مطابقة دلوقتي" or "Sorry, I couldn't find a match right now"
          Then ask if they'd like to adjust their criteria or leave their details so a sales agent can follow up.
        - NEVER invent or fabricate property listings, prices, or details.
        - ALWAYS reply in the SAME LANGUAGE the customer is using (Arabic, English, Franco-Arab, etc.)
        - Keep responses concise and natural — this is WhatsApp, not a formal email.
        - Use conversation history to maintain context — don't re-ask what was already answered.
        - Be warm and helpful like a knowledgeable friend, not a robot.
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

    public async Task<string> GenerateReplyAsync(
        string currentMessage,
        List<WhatsAppMessage> conversationHistory,
        List<WhatsAppMessage> relevantData)
    {
        if (_chatClient == null)
            return $"Propulse (Beta): {currentMessage}";

        var messages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt) };

        foreach (var msg in conversationHistory)
        {
            messages.Add(new UserChatMessage(msg.Content));
            if (!string.IsNullOrEmpty(msg.BotReply))
                messages.Add(new AssistantChatMessage(msg.BotReply));
        }

        var dataContext = BuildDataContext(relevantData);
        var userMessageWithData = $"""
            [CUSTOMER MESSAGE]
            {currentMessage}

            [AVAILABLE DATA FROM DATABASE]
            {dataContext}
            """;

        messages.Add(new UserChatMessage(userMessageWithData));

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages);
            var reply = completion.Value.Content[0].Text;
            _logger.LogInformation("AI generated reply ({Results} DB results used)", relevantData.Count);
            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed");
            return $"Propulse (Beta): {currentMessage}";
        }
    }

    private static string BuildDataContext(List<WhatsAppMessage> relevantData)
    {
        if (relevantData.Count == 0)
            return "No matching results found in the database.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {relevantData.Count} potentially relevant message(s):");
        sb.AppendLine();

        foreach (var msg in relevantData)
        {
            sb.AppendLine($"- From: {msg.SenderName} | Date: {msg.CreatedAt:yyyy-MM-dd}");
            sb.AppendLine($"  Content: {msg.Content}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
