using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpsMind.Core.Agent;

/// <summary>
/// Клиент внешней ITSM-системы. Повторные попытки при 5xx и таймаутах настраиваются в DI
/// (AddStandardResilienceHandler, Polly) — сам клиент о них не знает.
/// </summary>
public sealed class TicketClient(HttpClient http) {
    static readonly JsonSerializerOptions Json = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<JsonElement?> GetAsync(string id, CancellationToken ct = default) {
        var response = await http.GetAsync($"tickets/{Uri.EscapeDataString(id.Trim())}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
    }

    /// <param name="idempotencyKey">
    /// Повтор POST после таймаута может создать дубликат, если сервер успел обработать первый запрос.
    /// Ключ идемпотентности позволяет реальной ITSM отбросить повтор (наш mock его игнорирует).
    /// </param>
    public async Task<JsonElement> CreateAsync(NewTicket ticket, string idempotencyKey, CancellationToken ct = default) {
        using var request = new HttpRequestMessage(HttpMethod.Post, "tickets") { Content = JsonContent.Create(ticket, options: Json) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
    }

    public static string Format(JsonElement ticket) => JsonSerializer.Serialize(ticket, Json);
}

public sealed record NewTicket(string Subject, string Description, string Category, string Priority, string Team, string Requester);