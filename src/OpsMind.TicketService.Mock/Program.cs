using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

// Имитация внешней ITSM-системы (источник правды по тикетам). Postgres в OpsMind — аналитическая реплика.
// При старте загружает data/tickets/tickets.jsonl. Случайные задержки и сбои (503) — для проверки
// повторных попыток (Polly) на стороне клиента. OpenAPI-описание: /openapi/v1.json
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => {
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();
app.MapOpenApi();

var failureRate = app.Configuration.GetValue("Chaos:FailureRate", 0.2);
var maxDelayMs = app.Configuration.GetValue("Chaos:MaxDelayMs", 300);
var store = TicketStore.Load(FindFile(Path.Combine("data", "tickets", "tickets.jsonl")));
app.Logger.LogInformation("Загружено {Count} тикетов, доля сбоев {FailureRate:P0}", store.Count, failureRate);

// Хаос: случайная задержка и ответ 503, как у нестабильной внешней системы.
app.Use(async (ctx, next) => {
    if (!ctx.Request.Path.StartsWithSegments("/tickets")) {
        await next();
        return;
    }
    await Task.Delay(Random.Shared.Next(maxDelayMs));
    if (Random.Shared.NextDouble() < failureRate) {
        ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await ctx.Response.WriteAsync("ITSM temporarily unavailable");
        return;
    }
    await next();
});

app.MapGet("/tickets/{id}", (string id) =>
    store.Get(id) is { } t ? Results.Ok(t) : Results.NotFound(new { error = $"Тикет {id} не найден" }));

app.MapGet("/tickets", (string? status, string? requester, int? limit) =>
    Results.Ok(store.Search(status, requester, Math.Clamp(limit ?? 20, 1, 100))));

app.MapPost("/tickets", (CreateTicket request) => {
    if (string.IsNullOrWhiteSpace(request.Subject))
        return Results.BadRequest(new { error = "subject обязателен" });
    var ticket = store.Create(request);
    return Results.Created($"/tickets/{ticket.Id}", ticket);
});

app.MapPatch("/tickets/{id}", (string id, UpdateTicket request) =>
    store.Update(id, request) is { } t ? Results.Ok(t) : Results.NotFound(new { error = $"Тикет {id} не найден" }));

app.Run();

static string FindFile(string relative) {
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent) {
        var candidate = Path.Combine(dir.FullName, relative);
        if (File.Exists(candidate))
            return candidate;
    }
    throw new FileNotFoundException($"Не найден {relative}");
}

record Ticket(
    string Id, DateTimeOffset CreatedAt, string Channel, string Department, string Requester,
    string Subject, string Description, string Category, string Priority, string Team,
    string Status, DateTimeOffset? ResolvedAt, List<string> Comments);

record CreateTicket(string Subject, string Description, string? Category, string? Priority, string? Team, string? Requester);

record UpdateTicket(string? Status, string? Priority, string? Comment);

sealed class TicketStore {
    readonly ConcurrentDictionary<string, Ticket> _tickets = new();
    int _lastNumber;

    public int Count => _tickets.Count;

    public static TicketStore Load(string path) {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var store = new TicketStore();
        foreach (var line in File.ReadLines(path).Where(l => l.Length > 0)) {
            var t = JsonSerializer.Deserialize<Ticket>(line, options)! with { Comments = [] };
            store._tickets[t.Id] = t;
            store._lastNumber = Math.Max(store._lastNumber, int.Parse(t.Id[^5..]));
        }
        return store;
    }

    public Ticket? Get(string id) => _tickets.GetValueOrDefault(id.Trim().ToUpperInvariant());

    public IEnumerable<Ticket> Search(string? status, string? requester, int limit) => _tickets.Values
        .Where(t => status is null || t.Status == status)
        .Where(t => requester is null || t.Requester.Contains(requester, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(t => t.CreatedAt)
        .Take(limit);

    public Ticket Create(CreateTicket r) {
        var id = $"T-2026-{Interlocked.Increment(ref _lastNumber):D5}";
        var ticket = new Ticket(id, DateTimeOffset.Now, "agent", "—", r.Requester ?? "OpsMind Agent",
            r.Subject, r.Description, r.Category ?? "unknown", r.Priority ?? "P3", r.Team ?? "ServiceDesk",
            "new", null, []);
        _tickets[id] = ticket;
        return ticket;
    }

    public Ticket? Update(string id, UpdateTicket r) {
        if (Get(id) is not { } t)
            return null;
        var comments = r.Comment is null ? t.Comments : [.. t.Comments, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm}: {r.Comment}"];
        var updated = t with {
            Status = r.Status ?? t.Status,
            Priority = r.Priority ?? t.Priority,
            ResolvedAt = r.Status == "resolved" ? DateTimeOffset.Now : t.ResolvedAt,
            Comments = comments,
        };
        _tickets[t.Id] = updated;
        return updated;
    }
}