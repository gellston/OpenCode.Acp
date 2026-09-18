using System.Text.Json;

namespace OpenCode.Acp;

internal sealed class AcpSessionUpdate
{
    public AcpSessionUpdate(string sessionId, JsonElement update)
    {
        SessionId = sessionId;
        Update = update.Clone();
    }

    public string SessionId { get; }
    public JsonElement Update { get; }

    public string? Kind => TryGetString(Update, "sessionUpdate");
    public string? ToolCallId => TryGetString(Update, "toolCallId");
    public string? ToolStatus => TryGetString(Update, "status");
    public string? ToolName => TryGetString(Update, "name") ?? TryGetString(Update, "title");

    public string? Text
    {
        get
        {
            if (!Update.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
                return null;

            if (TryGetString(content, "type") != "text")
                return null;

            return TryGetString(content, "text");
        }
    }

    public string? SkillName
    {
        get
        {
            if (!Update.TryGetProperty("rawInput", out var rawInput) || rawInput.ValueKind != JsonValueKind.Object)
                return null;

            return TryGetString(rawInput, "name");
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
