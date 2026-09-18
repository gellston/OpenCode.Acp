using System.Text;
using OpenCodeSharp.Acp;
using OpenCodeSharp.Acp.Permissions;

if (OperatingSystem.IsWindows())
{
    Console.InputEncoding = new UTF8Encoding(false);
    Console.OutputEncoding = new UTF8Encoding(false);
}

var projectDirectory = "E:\\Github\\ai-harness\\test";
var prompt = "C++로 두수를 더하는 계산기를 만들어줘";

await using var client = new OpenCodeAcpClient(new OpenCodeAcpOptions
{
    ExecutablePath = "opencode",
    ProcessWorkingDirectory = projectDirectory,
    ForceUtf8OnWindows = true,
    PermissionHandler = new DenyAllPermissionHandler()
});

client.ErrorReceived += text => Console.Error.WriteLine($"[Error] {text}");

await client.StartAsync();
var session = await client.CreateSessionAsync(projectDirectory);

session.WorkingStarted += () => Console.WriteLine("[Working Started]");
session.WorkingCompleted += () => Console.WriteLine("\n[Working Completed]");

session.ThinkingStarted += () => Console.WriteLine("[Thinking Started]");
session.ThinkingCompleted += () => Console.WriteLine("\n[Thinking Completed]");
session.ThinkingTextReceived += text => Console.Write(text);

session.ResponseStarted += () => Console.WriteLine("[Response Started]");
session.ResponseCompleted += () => Console.WriteLine("\n[Response Completed]");
session.ResponseTextReceived += text => Console.Write(text);

session.ToolStarted += toolName => Console.WriteLine($"[Tool Started] {toolName}");
session.ToolCompleted += toolName => Console.WriteLine($"[Tool Completed] {toolName}");

session.SkillStarted += skillName => Console.WriteLine($"[Skill Started] {skillName}");
session.SkillCompleted += skillName => Console.WriteLine($"[Skill Completed] {skillName}");

await session.PromptAsync(prompt);
