# OpenCodeSharp.Acp

Visual Studio 2022용 C# OpenCode ACP wrapper입니다.

이 버전은 외부 API를 단순하게 유지합니다. ACP의 raw JSON, kind, status 같은 세부 필드는 외부에 노출하지 않고, 애플리케이션에서 바로 사용하기 쉬운 lifecycle 이벤트와 텍스트 이벤트만 제공합니다.

## 요구사항

- Visual Studio 2022
- .NET 8 SDK
- Windows x64
- OpenCode CLI
- `opencode acp`가 실행 가능한 환경

## 주요 이벤트

### 전체 작업

```csharp
session.WorkingStarted += () => { };
session.WorkingCompleted += () => { };
```

### Thinking

```csharp
session.ThinkingStarted += () => { };
session.ThinkingTextReceived += text => Console.Write(text);
session.ThinkingCompleted += () => { };
```

`agent_thought_chunk`가 시작되면 `ThinkingStarted`가 한 번 호출되고, 연속되는 생각 텍스트는 `ThinkingTextReceived`로 전달됩니다. Tool 실행 또는 응답 출력으로 전환되면 `ThinkingCompleted`가 호출됩니다.

### Response

```csharp
session.ResponseStarted += () => { };
session.ResponseTextReceived += text => Console.Write(text);
session.ResponseCompleted += () => { };
```

`agent_message_chunk`가 시작되면 `ResponseStarted`가 한 번 호출됩니다. 사용자에게 보여줄 답변 텍스트만 `ResponseTextReceived`로 전달합니다. Tool/Thinking으로 전환되거나 Prompt가 끝나면 `ResponseCompleted`가 호출됩니다.

### Tool

```csharp
session.ToolStarted += toolName => Console.WriteLine(toolName);
session.ToolCompleted += toolName => Console.WriteLine(toolName);
```

예:

```text
read
edit
bash
skill
```

내부적으로 `toolCallId`를 추적하지만 외부에는 Tool 이름만 전달합니다.

### Skill

```csharp
session.SkillStarted += skillName => Console.WriteLine(skillName);
session.SkillCompleted += skillName => Console.WriteLine(skillName);
```

OpenCode의 `skill` Tool이 실행될 때 `rawInput.name`에서 실제 Skill 이름을 찾아 전달합니다.

예:

```text
initiate-msbuild
msbuild
```

OpenCode에서는 Skill이 native `skill({ name: "..." })` Tool 호출로 로드되므로, Skill 호출 시 일반 Tool 이벤트의 `skill`과 Skill 전용 이벤트가 모두 발생할 수 있습니다.

### Error

Library 또는 OpenCode ACP process의 stderr에서 발생한 오류/로그 텍스트를 받습니다.

```csharp
client.ErrorReceived += text =>
{
    Console.Error.WriteLine(text);
};
```

JSON parsing 실패, ACP RPC error, transport 오류 등의 library 내부 오류도 이 이벤트로 전달됩니다. 예외가 필요한 경우 기존 `Task` 예외도 그대로 throw됩니다.

## 최소 사용 예

```csharp
await using var client = new OpenCodeAcpClient(new OpenCodeAcpOptions
{
    ExecutablePath = "opencode",
    ProcessWorkingDirectory = projectDirectory,
    ForceUtf8OnWindows = true
});

client.ErrorReceived += text => Console.Error.WriteLine(text);

await client.StartAsync();
var session = await client.CreateSessionAsync(projectDirectory);

session.ThinkingStarted += () => Console.WriteLine("Thinking...");
session.ThinkingTextReceived += text => Console.Write(text);
session.ThinkingCompleted += () => Console.WriteLine();

session.ResponseStarted += () => Console.WriteLine("Response...");
session.ResponseTextReceived += text => Console.Write(text);
session.ResponseCompleted += () => Console.WriteLine();

session.ToolStarted += name => Console.WriteLine($"Tool: {name}");
session.SkillStarted += name => Console.WriteLine($"Skill: {name}");

await session.PromptAsync("프로젝트를 분석해줘.");
```

## UTF-8 / BOM

ACP stdin/stdout/stderr는 UTF-8 no-BOM으로 처리합니다. Windows에서는 기본적으로 `chcp 65001`을 적용하고, ACP stdout을 오염시키지 않도록 CHCP 출력은 `nul`로 보냅니다.

수신 시에는 실제 `U+FEFF` BOM과 `ï»¿` 형태로 잘못 변환된 BOM도 방어적으로 제거합니다.

## 빌드

Visual Studio 2022에서 다음 솔루션을 엽니다.

```text
OpenCodeSharp.Acp.sln
```

또는 CMD:

```cmd
scripts\build_vs2022.cmd
```
