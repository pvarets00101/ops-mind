using System.Text.Json;

namespace OpsMind.Core.Llm.ClaudeCli;

/// <summary>Событие из вывода `claude -p --output-format stream-json`, которое нам важно. Остальные пропускаем.</summary>
public abstract record ClaudeStreamEvent {
    /// <summary>
    /// Первое событие: какую модель CLI реально выбрал по алиасу (haiku → claude-haiku-5-5)
    /// и какие инструменты получила модель — в режиме «просто модель» список обязан быть пустым.
    /// </summary>
    public sealed record Init(string? Model, IReadOnlyList<string> Tools) : ClaudeStreamEvent;

    /// <summary>Кусок ответа по мере генерации (нужен флаг --include-partial-messages).</summary>
    public sealed record TextDelta(string Text) : ClaudeStreamEvent;

    /// <summary>Загрузка лимитов подписки: доля пятичасового и недельного окна.</summary>
    public sealed record RateLimit(string? Status, double? FiveHour, double? SevenDay) : ClaudeStreamEvent;

    /// <summary>
    /// Последнее событие: итоговый текст, ошибка, токены. CostUsd — сколько стоил бы запрос по ценам API;
    /// по подписке мы не платим, но это удобная мера «тяжести» запроса.
    /// </summary>
    public sealed record Result(
        string Text, bool IsError, string? StopReason, long InputTokens, long OutputTokens, decimal? CostUsd, int DurationMs)
        : ClaudeStreamEvent;

    /// <summary>Разбирает одну строку вывода. null — строка не JSON или событие нам не нужно.</summary>
    public static ClaudeStreamEvent? Parse(string line) {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(line);
        } catch (JsonException) {
            return null; // CLI иногда пишет в stdout обычный текст (предупреждения) — пропускаем
        }

        using (doc) {
            var e = doc.RootElement;
            switch (Str(e, "type")) {
                case "system" when Str(e, "subtype") == "init":
                    var tools = e.TryGetProperty("tools", out var t) && t.ValueKind == JsonValueKind.Array
                        ? t.EnumerateArray().Select(x => x.ToString()).ToList()
                        : [];
                    return new Init(Str(e, "model"), tools);

                case "stream_event" when e.TryGetProperty("event", out var ev)
                                         && Str(ev, "type") == "content_block_delta"
                                         && ev.TryGetProperty("delta", out var delta)
                                         && Str(delta, "type") == "text_delta":
                    return Str(delta, "text") is { Length: > 0 } text ? new TextDelta(text) : null;

                case "rate_limit_event" when e.TryGetProperty("rate_limit_info", out var info):
                    var windows = info.TryGetProperty("unifiedWindows", out var w) ? w : default;
                    return new RateLimit(Str(info, "status"), Utilization(windows, "five_hour"), Utilization(windows, "seven_day"));

                case "result":
                    var usage = e.TryGetProperty("usage", out var u) ? u : default;
                    return new Result(
                        Text: Str(e, "result") ?? "",
                        IsError: e.TryGetProperty("is_error", out var err) && err.ValueKind == JsonValueKind.True,
                        StopReason: Str(e, "stop_reason"),
                        // Входные токены у Anthropic делятся на обычные, записанные в кэш и прочитанные из кэша — складываем.
                        InputTokens: Long(usage, "input_tokens") + Long(usage, "cache_creation_input_tokens") + Long(usage, "cache_read_input_tokens"),
                        OutputTokens: Long(usage, "output_tokens"),
                        CostUsd: e.TryGetProperty("total_cost_usd", out var cost) && cost.ValueKind == JsonValueKind.Number ? cost.GetDecimal() : null,
                        DurationMs: e.TryGetProperty("duration_ms", out var d) && d.TryGetInt32(out var ms) ? ms : 0);

                default:
                    return null;
            }
        }
    }

    static double? Utilization(JsonElement windows, string name) =>
        windows.ValueKind == JsonValueKind.Object
        && windows.TryGetProperty(name, out var w)
        && w.TryGetProperty("utilization", out var u)
        && u.ValueKind == JsonValueKind.Number
            ? u.GetDouble()
            : null;

    static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    static long Long(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;
}