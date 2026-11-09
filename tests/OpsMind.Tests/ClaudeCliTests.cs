using Microsoft.Extensions.AI;
using OpsMind.Core.Llm.ClaudeCli;

namespace OpsMind.Tests;

/// <summary>Разбор вывода `claude -p --output-format stream-json` и сборка промпта — без запуска CLI.</summary>
public class ClaudeCliTests {
    // Строки из реального вывода claude 2.1.318 (сокращены до значимых полей).
    const string InitLine =
        """{"type":"system","subtype":"init","cwd":"C:\\Temp\\opsmind-claude","session_id":"941cfa48","tools":[],"mcp_servers":[],"model":"claude-haiku-5-5","permissionMode":"default"}""";
    const string DeltaLine =
        """{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Перезапустите V"}},"session_id":"941cfa48"}""";
    const string RateLimitLine =
        """{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","rateLimitType":"five_hour","unifiedWindows":{"five_hour":{"utilization":0.01,"status":"allowed"},"seven_day":{"utilization":0.03,"status":"allowed"}}}}""";
    const string ResultLine =
        """{"type":"result","subtype":"success","is_error":false,"duration_ms":1387,"result":"Перезапустите VPN-клиент.","stop_reason":"end_turn","total_cost_usd":0.0001657,"usage":{"input_tokens":2,"cache_creation_input_tokens":705,"cache_read_input_tokens":0,"output_tokens":49}}""";

    [Fact]
    public void ParsesInit() {
        var init = Assert.IsType<ClaudeStreamEvent.Init>(ClaudeStreamEvent.Parse(InitLine));
        Assert.Equal("claude-haiku-5-5", init.Model);
        Assert.Empty(init.Tools);
    }

    [Fact]
    public void InitReportsToolsWhenCliHasThem() {
        var line = InitLine.Replace("\"tools\":[]", "\"tools\":[\"Bash\",\"Read\"]");
        var init = Assert.IsType<ClaudeStreamEvent.Init>(ClaudeStreamEvent.Parse(line));
        Assert.Equal(["Bash", "Read"], init.Tools);
    }

    [Fact]
    public void ParsesTextDelta() {
        var delta = Assert.IsType<ClaudeStreamEvent.TextDelta>(ClaudeStreamEvent.Parse(DeltaLine));
        Assert.Equal("Перезапустите V", delta.Text);
    }

    [Fact]
    public void ParsesRateLimit() {
        var limit = Assert.IsType<ClaudeStreamEvent.RateLimit>(ClaudeStreamEvent.Parse(RateLimitLine));
        Assert.Equal("allowed", limit.Status);
        Assert.Equal(0.01, limit.FiveHour);
        Assert.Equal(0.03, limit.SevenDay);
    }

    [Fact]
    public void ParsesResultAndSumsCachedInputTokens() {
        var result = Assert.IsType<ClaudeStreamEvent.Result>(ClaudeStreamEvent.Parse(ResultLine));
        Assert.False(result.IsError);
        Assert.Equal("Перезапустите VPN-клиент.", result.Text);
        Assert.Equal(707, result.InputTokens); // 2 обычных + 705 записанных в кэш
        Assert.Equal(49, result.OutputTokens);
        Assert.Equal(0.0001657m, result.CostUsd);
        Assert.Equal("end_turn", result.StopReason);
    }

    [Fact]
    public void ParsesStructuredOutput() {
        // Так выглядит итог запроса с --json-schema: JSON и строкой в result, и объектом в structured_output.
        const string line =
            """{"type":"result","is_error":false,"result":"{\"category\":\"printing\",\"priority\":\"P3\"}","structured_output":{"category":"printing","priority":"P3"},"usage":{"input_tokens":5,"output_tokens":20}}""";

        var result = Assert.IsType<ClaudeStreamEvent.Result>(ClaudeStreamEvent.Parse(line));

        Assert.Equal("""{"category":"printing","priority":"P3"}""", result.StructuredOutput);
    }

    [Fact]
    public void JsonSchemaArgumentDependsOnResponseFormat() {
        Assert.Null(ClaudeCliChatClient.JsonSchemaArgument(null));
        Assert.Null(ClaudeCliChatClient.JsonSchemaArgument(new ChatOptions { ResponseFormat = ChatResponseFormat.Text }));
        Assert.Equal("""{"type":"object"}""", ClaudeCliChatClient.JsonSchemaArgument(new ChatOptions { ResponseFormat = ChatResponseFormat.Json }));

        using var schema = System.Text.Json.JsonDocument.Parse("""
            { "type": "object", "properties": { "priority": { "enum": ["P1", "P2"] } } }
            """);
        var format = ChatResponseFormat.ForJsonSchema(schema.RootElement.Clone(), "ticket");
        // Одной строкой, без переносов: это значение аргумента командной строки.
        Assert.Equal("""{"type":"object","properties":{"priority":{"enum":["P1","P2"]}}}""",
            ClaudeCliChatClient.JsonSchemaArgument(new ChatOptions { ResponseFormat = format }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("не JSON, а предупреждение CLI")]
    [InlineData("""{"type":"stream_event","event":{"type":"message_stop"}}""")]
    [InlineData("""{"type":"system","subtype":"status","status":"requesting"}""")]
    public void IgnoresIrrelevantLines(string line) => Assert.Null(ClaudeStreamEvent.Parse(line));

    [Fact]
    public void SingleQuestionGoesAsIs() {
        List<ChatMessage> messages = [new(ChatRole.System, "Ты ассистент поддержки."), new(ChatRole.User, "Как сбросить пароль?")];

        var (system, prompt) = ClaudePrompt.Build(messages, new ChatOptions { Instructions = "Отвечай кратко." });

        Assert.Equal("Отвечай кратко.\n\nТы ассистент поддержки.", system);
        Assert.Equal("Как сбросить пароль?", prompt);
    }

    [Fact]
    public void DefaultSystemPromptReplacesClaudeCodeOne() {
        var (system, _) = ClaudePrompt.Build([new ChatMessage(ChatRole.User, "Привет")], null);
        Assert.False(string.IsNullOrWhiteSpace(system));
    }

    [Fact]
    public void DialogBecomesTranscriptWithToolCalls() {
        List<ChatMessage> messages = [
            new(ChatRole.User, "Не работает VPN"),
            new(ChatRole.Assistant, [new FunctionCallContent("c1", "search_docs", new Dictionary<string, object?> { ["query"] = "VPN" })]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", "[RB-030] Перезапустите клиент")]),
            new(ChatRole.Assistant, "Перезапустите клиент. [RB-030]"),
            new(ChatRole.User, "Не помогло"),
        ];

        var (_, prompt) = ClaudePrompt.Build(messages, null);

        Assert.Contains("<user>\nНе работает VPN\n</user>", prompt);
        Assert.Contains("""<tool_call name="search_docs">{"query":"VPN"}</tool_call>""", prompt);
        Assert.Contains("<tool_result>\n[RB-030] Перезапустите клиент\n</tool_result>", prompt);
        Assert.Contains("<assistant>\nПерезапустите клиент. [RB-030]\n</assistant>", prompt);
        // Последней в стенограмме должна быть реплика, на которую отвечаем.
        Assert.True(prompt.IndexOf("Не помогло", StringComparison.Ordinal) > prompt.IndexOf("[RB-030]</", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ToolsAreRejectedUntilEmulationExists() {
        using var client = new ClaudeCliChatClient(new ClaudeCliOptions { ExecutablePath = "не-запускается" });
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(() => "ok", "noop")] };

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetResponseAsync("вопрос", options));
    }
}