using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using OpsMind.Core.Agent;
using OpsMind.Core.Llm;
using OpsMind.Core.Rag;

// MCP-сервер OpsMind (Streamable HTTP, http://localhost:5100/mcp).
// Публикует три примитива MCP:
//  - tools     — те же инструменты, что у агента (OpsMindTools), с подсказками readOnly/destructive для клиента;
//  - resources — регламенты как документы runbook://RB-xxx, их можно подключить в контекст целиком;
//  - prompts   — шаблон «разбор тикета», который клиент показывает пользователю как готовую команду.
// Подключение к Claude Code: .mcp.json в корне репозитория.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpsMindLlm(builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions());
builder.Services.AddOpsMindRag(builder.Configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions());
builder.Services.AddOpsMindAgent(builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions());

var runbooksDir = FindDirectory(Path.Combine("data", "runbooks"));

builder.Services
    .AddMcpServer(o => {
        o.ServerInfo = new() { Name = "opsmind", Version = "0.1.0" };
        o.ServerInstructions =
            "Инструменты IT-поддержки OpsMind: регламенты (search_docs), история обращений (query_history), " +
            "тикеты ITSM (get_ticket, read_attachments, create_ticket). create_ticket только готовит заявку — " +
            "её подтверждает человек через POST /actions/{id}/approve.";
    })
    .WithHttpTransport()
    .WithResources(RunbookResources(runbooksDir))
    .WithPrompts([McpServerPrompt.Create(TriageTicketPrompt, new() { Name = "triage_ticket", Title = "Разбор тикета" })]);

// Инструменты берём у агента: экземпляр OpsMindTools создаётся DI, поэтому регистрируем их через options.
builder.Services.AddOptions<McpServerOptions>().Configure<OpsMindTools>((options, tools) => {
    options.ToolCollection ??= [];
    foreach (var function in tools.All().OfType<AIFunction>())
        options.ToolCollection.Add(McpServerTool.Create(new PlainTextResult(function), Annotations(function.Name)));
});

var app = builder.Build();
app.MapMcp("/mcp");

// Human-in-the-loop для MCP: подтверждение заявок, подготовленных через create_ticket.
app.MapGet("/actions", async (PendingActionStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct: ct)));
app.MapPost("/actions/{id}/approve", async (string id, PendingActionStore store, CancellationToken ct) =>
    await ApproveAsync(id, store, ct));
app.MapPost("/actions/{id}/reject", async (string id, PendingActionStore store, CancellationToken ct) =>
    await store.RejectAsync(id, ct) is { } action ? Results.Ok(action) : Results.NotFound());

app.Run();

// 404 — действия нет или оно уже обработано; 502 — ITSM не ответила даже после повторов, действие снова ждёт подтверждения.
static async Task<IResult> ApproveAsync(string id, PendingActionStore store, CancellationToken ct) {
    try {
        return await store.ApproveAsync(id, ct) is { } action ? Results.Ok(action) : Results.NotFound();
    } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status502BadGateway, title: "ITSM недоступна");
    }
}

// Подсказки клиенту о поведении инструмента: Claude Code и другие клиенты по ним решают,
// можно ли вызывать инструмент без подтверждения и предупреждать ли пользователя.
static McpServerToolCreateOptions Annotations(string tool) => tool switch {
    "create_ticket" => new() { ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true, Title = "Подготовить заявку в ITSM" },
    "get_ticket" => new() { ReadOnly = true, OpenWorld = true, Title = "Тикет из ITSM" },
    "read_attachments" => new() { ReadOnly = true, OpenWorld = false, Title = "Вложения тикета" },
    "query_history" => new() { ReadOnly = true, OpenWorld = false, Title = "SQL по истории обращений" },
    _ => new() { ReadOnly = true, OpenWorld = false, Title = "Поиск по регламентам" },
};

// Каждый регламент — отдельный ресурс runbook://RB-xxx с текстом markdown.
static IEnumerable<McpServerResource> RunbookResources(string dir) =>
    Directory.GetFiles(dir, "*.md").Order().Select(path => {
        var chunks = MarkdownChunker.Chunk(File.ReadAllText(path), Path.GetFileName(path));
        var id = chunks.Count > 0 ? chunks[0].DocId : Path.GetFileNameWithoutExtension(path);
        var title = chunks.Count > 0 ? chunks[0].Title : id;
        return McpServerResource.Create(
            () => File.ReadAllText(path),
            new() { UriTemplate = $"runbook://{id}", Name = id, Title = $"{id}: {title}", Description = title, MimeType = "text/markdown" });
    });

[Description("Разобрать тикет: состояние, вложения, похожие случаи в истории и решение по регламенту")]
static string TriageTicketPrompt([Description("Номер тикета, например T-2026-02967")] string ticketId) => $"""
    Разбери тикет {ticketId} с помощью инструментов OpsMind:
    1. get_ticket — текущее состояние и описание проблемы.
    2. read_attachments — логи и вложения; найди в них причину.
    3. query_history — были ли похожие обращения (та же категория и runbook_id) и как быстро их решали.
    4. search_docs — решение по регламенту, со ссылкой [RB-xxx].
    Итог: причина, что сделать пользователю и инженеру, нужна ли эскалация. Не придумывай фактов, которых нет в результатах.
    """;

static string FindDirectory(string relative) {
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent) {
        var candidate = Path.Combine(dir.FullName, relative);
        if (Directory.Exists(candidate))
            return candidate;
    }
    throw new DirectoryNotFoundException($"Не найдена папка {relative}");
}

/// <summary>
/// AIFunctionFactory упаковывает строковый результат в JsonElement, и MCP отдаёт его клиенту как JSON-строку
/// в кавычках с «\n». Разворачиваем в обычный текст — модели клиента так читать проще.
/// </summary>
sealed class PlainTextResult(AIFunction inner) : DelegatingAIFunction(inner) {
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) {
        var result = await base.InvokeCoreAsync(arguments, cancellationToken);
        return result is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } s ? s.GetString() : result;
    }
}