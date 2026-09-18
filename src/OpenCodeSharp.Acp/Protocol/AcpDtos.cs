using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenCodeSharp.Acp.Protocol;

public sealed class InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = 1;

    [JsonPropertyName("clientCapabilities")]
    public ClientCapabilities ClientCapabilities { get; init; } = new();

    [JsonPropertyName("clientInfo")]
    public ClientInfo ClientInfo { get; init; } = new();
}

public sealed class ClientCapabilities
{
    [JsonPropertyName("fs")]
    public FileSystemCapabilities Fs { get; init; } = new();

    [JsonPropertyName("terminal")]
    public bool Terminal { get; init; }
}

public sealed class FileSystemCapabilities
{
    [JsonPropertyName("readTextFile")]
    public bool ReadTextFile { get; init; }

    [JsonPropertyName("writeTextFile")]
    public bool WriteTextFile { get; init; }
}

public sealed class ClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "OpenCodeSharp.Acp";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.4.0";
}

public sealed class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public sealed class SessionNewParams
{
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }

    [JsonPropertyName("mcpServers")]
    public object[] McpServers { get; init; } = [];
}

public sealed class SessionNewResult
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public sealed class SessionPromptParams
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("prompt")]
    public required IReadOnlyList<ContentBlock> Prompt { get; init; }
}

public sealed class ContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    public static ContentBlock FromText(string text) => new() { Text = text };
}

public sealed class PromptResult
{
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

public sealed class SessionCancelParams
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
}

public sealed class SessionUpdateParams
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("update")]
    public JsonElement Update { get; init; }
}

public sealed class PermissionRequestParams
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("toolCall")]
    public JsonElement ToolCall { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<PermissionOption> Options { get; init; } = [];
}

public sealed class PermissionOption
{
    [JsonPropertyName("optionId")]
    public required string OptionId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }
}
