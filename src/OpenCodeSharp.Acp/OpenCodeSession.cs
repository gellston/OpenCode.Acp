using System.Collections.Concurrent;
using OpenCodeSharp.Acp.Protocol;

namespace OpenCodeSharp.Acp;

public sealed class OpenCodeSession
{
    private readonly OpenCodeAcpClient client_;
    private readonly SemaphoreSlim promptLock_ = new(1, 1);
    private readonly ConcurrentDictionary<string, ToolState> tools_ = new();
    private int isWorking_;
    private int isThinking_;
    private int isResponding_;

    internal OpenCodeSession(OpenCodeAcpClient client, string id, string workingDirectory)
    {
        client_ = client;
        Id = id;
        WorkingDirectory = workingDirectory;
    }

    public string Id { get; }
    public string WorkingDirectory { get; }

    public bool IsWorking => Volatile.Read(ref isWorking_) != 0;
    public bool IsThinking => Volatile.Read(ref isThinking_) != 0;
    public bool IsResponding => Volatile.Read(ref isResponding_) != 0;

    // Whole prompt turn lifecycle.
    public event Action? WorkingStarted;
    public event Action? WorkingCompleted;

    // Reasoning lifecycle and clean reasoning text stream.
    public event Action? ThinkingStarted;
    public event Action? ThinkingCompleted;
    public event Action<string>? ThinkingTextReceived;

    // User-facing response lifecycle and clean response text stream.
    public event Action? ResponseStarted;
    public event Action? ResponseCompleted;
    public event Action<string>? ResponseTextReceived;

    // Tool lifecycle. Only the tool name is exposed.
    public event Action<string>? ToolStarted;
    public event Action<string>? ToolCompleted;

    // Skill lifecycle. Only the loaded skill name is exposed.
    public event Action<string>? SkillStarted;
    public event Action<string>? SkillCompleted;

    public async Task<PromptResult> PromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await promptLock_.WaitAsync(cancellationToken).ConfigureAwait(false);
        BeginWorking();

        try
        {
            return await client_.PromptAsync(Id, prompt, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CompleteThinking();
            CompleteResponse();
            CompleteActiveTools();
            CompleteWorking();
            promptLock_.Release();
        }
    }

    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        return client_.CancelSessionAsync(Id, cancellationToken);
    }

    internal void HandleUpdate(AcpSessionUpdate update)
    {
        switch (update.Kind)
        {
            case "agent_thought_chunk":
                CompleteResponse();
                BeginThinking();
                EmitText(ThinkingTextReceived, update.Text);
                break;

            case "agent_message_chunk":
                CompleteThinking();
                BeginResponse();
                EmitText(ResponseTextReceived, update.Text);
                break;

            case "tool_call":
                CompleteThinking();
                CompleteResponse();
                HandleToolCall(update);
                break;

            case "tool_call_update":
                CompleteThinking();
                CompleteResponse();
                HandleToolCall(update);
                break;
        }
    }

    private void HandleToolCall(AcpSessionUpdate update)
    {
        if (string.IsNullOrWhiteSpace(update.ToolCallId))
            return;

        var state = tools_.GetOrAdd(update.ToolCallId, static id => new ToolState(id));

        if (!string.IsNullOrWhiteSpace(update.ToolName))
            state.Name = update.ToolName;

        if (!string.IsNullOrWhiteSpace(update.SkillName))
            state.SkillName = update.SkillName;

        // OpenCode normally includes the tool name in the initial tool_call title/name.
        // If it arrives later, start the event as soon as the name becomes known.
        if (!state.ToolStarted && !string.IsNullOrWhiteSpace(state.Name))
        {
            state.ToolStarted = true;
            ToolStarted?.Invoke(state.Name);
        }

        if (IsSkillTool(state.Name) && !state.SkillStarted && !string.IsNullOrWhiteSpace(state.SkillName))
        {
            state.SkillStarted = true;
            SkillStarted?.Invoke(state.SkillName);
        }

        if (IsTerminalToolStatus(update.ToolStatus))
            CompleteTool(state);
    }

    private void CompleteTool(ToolState state)
    {
        if (state.Completed)
            return;

        state.Completed = true;

        if (state.SkillStarted && !string.IsNullOrWhiteSpace(state.SkillName))
            SkillCompleted?.Invoke(state.SkillName);

        if (state.ToolStarted && !string.IsNullOrWhiteSpace(state.Name))
            ToolCompleted?.Invoke(state.Name);

        tools_.TryRemove(state.Id, out _);
    }

    private void CompleteActiveTools()
    {
        foreach (var state in tools_.Values)
            CompleteTool(state);

        tools_.Clear();
    }

    private static bool IsTerminalToolStatus(string? status)
    {
        return string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSkillTool(string? toolName)
    {
        return string.Equals(toolName, "skill", StringComparison.OrdinalIgnoreCase);
    }

    private void BeginWorking()
    {
        if (Interlocked.Exchange(ref isWorking_, 1) == 0)
            WorkingStarted?.Invoke();
    }

    private void CompleteWorking()
    {
        if (Interlocked.Exchange(ref isWorking_, 0) != 0)
            WorkingCompleted?.Invoke();
    }

    private void BeginThinking()
    {
        if (Interlocked.Exchange(ref isThinking_, 1) == 0)
            ThinkingStarted?.Invoke();
    }

    private void CompleteThinking()
    {
        if (Interlocked.Exchange(ref isThinking_, 0) != 0)
            ThinkingCompleted?.Invoke();
    }

    private void BeginResponse()
    {
        if (Interlocked.Exchange(ref isResponding_, 1) == 0)
            ResponseStarted?.Invoke();
    }

    private void CompleteResponse()
    {
        if (Interlocked.Exchange(ref isResponding_, 0) != 0)
            ResponseCompleted?.Invoke();
    }

    private static void EmitText(Action<string>? handler, string? text)
    {
        if (!string.IsNullOrEmpty(text))
            handler?.Invoke(text);
    }

    private sealed class ToolState
    {
        public ToolState(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public string? Name { get; set; }
        public string? SkillName { get; set; }
        public bool ToolStarted { get; set; }
        public bool SkillStarted { get; set; }
        public bool Completed { get; set; }
    }
}
