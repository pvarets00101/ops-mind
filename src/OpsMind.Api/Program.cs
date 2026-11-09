using Microsoft.Extensions.AI;
using OpsMind.Core.Agent;
using OpsMind.Core.Llm;
using OpsMind.Core.Rag;

var builder = WebApplication.CreateBuilder(args);

var llmOptions = builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
builder.Services.AddOpsMindLlm(llmOptions);
builder.Services.AddOpsMindRag(builder.Configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions());
builder.Services.AddOpsMindAgent(builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions());

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", model = llmOptions.ChatModel }));

app.MapGet("/api/search", async (string q, int? k, KnowledgeBase kb, CancellationToken ct) =>
    Results.Ok(await kb.SearchAsync(q, k ?? 5, ct)));

app.MapPost("/api/ask", async (AskRequest request, RagAnswerer rag, CancellationToken ct) => {
    var result = await rag.AskAsync(request.Question, ct);
    return Results.Ok(new {
        answer = result.Answer,
        grounded = result.Grounded,
        sources = result.Sources.Select(s => new { s.DocId, s.Title, s.Section, score = Math.Round(s.Score, 3) }),
    });
});

// Агент: sessionId из ответа передаётся в следующих запросах, чтобы продолжить диалог.
app.MapPost("/api/agent", async (AgentRequest request, OpsMindAgent agent, CancellationToken ct) =>
    Results.Ok(await agent.ChatAsync(request.SessionId, request.Message, ct)));

// Сохранённые диалоги агента (Postgres): список, сообщения, удаление.
app.MapGet("/api/agent/conversations", async (int? limit, ConversationStore store, CancellationToken ct) =>
    Results.Ok(await store.ListAsync(limit ?? 50, ct)));

app.MapGet("/api/agent/conversations/{id}", async (string id, ConversationStore store, CancellationToken ct) =>
    await store.GetMessagesAsync(id, ct) is { } messages
        ? Results.Ok(messages.Select(m => new {
            m.Id,
            m.Role,
            m.Text,
            // Служебная реплика проверки ссылок: модель её видела, но пользователю показывать не нужно.
            @internal = m.Message.AuthorName == OpsMindAgent.VerifierAuthor,
            // Вызовы инструментов показываем именами и аргументами; результаты есть в сообщениях с ролью tool.
            tools = m.Message.Contents.OfType<FunctionCallContent>().Select(c => new { c.Name, c.Arguments }),
            m.CreatedAt,
        }))
        : Results.NotFound());

app.MapDelete("/api/agent/conversations/{id}", async (string id, ConversationStore store, CancellationToken ct) =>
    await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

// Human-in-the-loop: действия агента, ожидающие подтверждения человеком.
app.MapGet("/api/agent/actions", async (string? sessionId, PendingActionStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(sessionId, ct)));

app.MapPost("/api/agent/actions/{id}/approve", async (string id, PendingActionStore store, CancellationToken ct) =>
    await ApproveAsync(id, store, ct));

app.MapPost("/api/agent/actions/{id}/reject", async (string id, PendingActionStore store, CancellationToken ct) =>
    await store.RejectAsync(id, ct) is { } action ? Results.Ok(action) : Results.NotFound());

app.MapPost("/api/chat", async (ChatRequest request, IChatClient chat, CancellationToken ct) => {
    var response = await chat.GetResponseAsync(request.Message, cancellationToken: ct);
    return Results.Ok(new ChatResponseDto(response.Text));
});

// Server-Sent Events вручную: TypedResults.ServerSentEvents появится после перехода на .NET 10.
app.MapPost("/api/chat/stream", async (ChatRequest request, IChatClient chat, HttpResponse response, CancellationToken ct) => {
    response.ContentType = "text/event-stream";
    await foreach (var update in chat.GetStreamingResponseAsync(request.Message, cancellationToken: ct)) {
        if (string.IsNullOrEmpty(update.Text))
            continue;

        await response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(update.Text)}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
});

app.Run();

// 404 — действия нет или оно уже обработано; 502 — ITSM не ответила даже после повторов, действие снова ждёт подтверждения.
static async Task<IResult> ApproveAsync(string id, PendingActionStore store, CancellationToken ct) {
    try {
        return await store.ApproveAsync(id, ct) is { } action ? Results.Ok(action) : Results.NotFound();
    } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status502BadGateway, title: "ITSM недоступна");
    }
}

record ChatRequest(string Message);

record AskRequest(string Question);

record AgentRequest(string? SessionId, string Message);

record ChatResponseDto(string Answer);