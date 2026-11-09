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
    public void ToolSchemaFixesNamesAndArgumentSchemas() {
        var search = AIFunctionFactory.Create((string query) => "", "search_docs", "Поиск по регламентам");
        var ticket = AIFunctionFactory.Create((string ticketId) => "", "get_ticket");

        using var schema = System.Text.Json.JsonDocument.Parse(ClaudeToolEmulation.Schema([search, ticket], null));

        var variants = schema.RootElement.GetProperty("properties").GetProperty("tool_calls").GetProperty("items").GetProperty("anyOf");
        Assert.Equal(2, variants.GetArrayLength());
        Assert.Equal("search_docs", variants[0].GetProperty("properties").GetProperty("name").GetProperty("const").GetString());
        // Схема аргументов — та, что AIFunctionFactory построил по параметрам метода.
        Assert.True(variants[0].GetProperty("properties").GetProperty("arguments").GetProperty("properties").TryGetProperty("query", out _));
        Assert.False(schema.RootElement.GetProperty("properties").GetProperty("tool_calls").TryGetProperty("minItems", out _));
    }

    [Fact]
    public void RequireAnyMeansAtLeastOneCall() {
        var tool = AIFunctionFactory.Create(() => "", "noop");
        using var schema = System.Text.Json.JsonDocument.Parse(ClaudeToolEmulation.Schema([tool], ChatToolMode.RequireAny));
        Assert.Equal(1, schema.RootElement.GetProperty("properties").GetProperty("tool_calls").GetProperty("minItems").GetInt32());
    }

    [Fact]
    public void ToolModeNoneDisablesEmulation() {
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(() => "", "noop")], ToolMode = ChatToolMode.None };
        Assert.Empty(ClaudeToolEmulation.ActiveTools(options));
    }

    [Fact]
    public void ParsesToolCalls() {
        var contents = ClaudeToolEmulation.ParseResponse(
            """{"tool_calls":[{"name":"get_ticket","arguments":{"ticketId":"T-2026-02994"}},{"name":"read_attachments","arguments":{"ticketId":"T-2026-02994"}}]}""");

        Assert.Equal(2, contents.Count);
        var first = Assert.IsType<FunctionCallContent>(contents[0]);
        Assert.Equal("get_ticket", first.Name);
        Assert.Equal("T-2026-02994", first.Arguments!["ticketId"]!.ToString());
        // У каждого вызова свой id — по нему FunctionInvokingChatClient сопоставит результат.
        Assert.NotEqual(first.CallId, ((FunctionCallContent)contents[1]).CallId);
    }

    [Theory]
    [InlineData("""{"answer":"Готово. [RB-030]"}""", "Готово. [RB-030]")]
    [InlineData("""{"tool_calls":[],"answer":"Готово."}""", "Готово.")]
    public void ParsesAnswer(string json, string expected) {
        var text = Assert.IsType<TextContent>(Assert.Single(ClaudeToolEmulation.ParseResponse(json)));
        Assert.Equal(expected, text.Text);
    }

    /// <summary>
    /// Полный цикл без CLI: FunctionInvokingChatClient поверх «модели», которая сначала просит инструмент, потом отвечает.
    /// Проверяет, что вызовы из эмуляции понимает стандартная обвязка Microsoft.Extensions.AI.
    /// </summary>
    [Fact]
    public async Task EmulatedCallsDriveStandardFunctionInvocation() {
        var calls = 0;
        var tool = AIFunctionFactory.Create((string ticketId) => { calls++; return $"{ticketId}: статус in_progress"; }, "get_ticket");
        var replies = new Queue<string>([
            """{"tool_calls":[{"name":"get_ticket","arguments":{"ticketId":"T-1"}}]}""",
            """{"answer":"Тикет T-1 в работе."}""",
        ]);
        var fake = new TestChatClient((_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, ClaudeToolEmulation.ParseResponse(replies.Dequeue()))));
        using var client = new ChatClientBuilder(fake).UseFunctionInvocation().Build();

        var response = await client.GetResponseAsync("Что с T-1?", new ChatOptions { Tools = [tool] });

        Assert.Equal(1, calls);
        Assert.Equal("Тикет T-1 в работе.", response.Text);
    }

    sealed class TestChatClient(Func<IEnumerable<ChatMessage>, ChatOptions?, ChatResponse> respond) : IChatClient {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(respond(messages, options));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() {
        }
    }
}