using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace OpsMind.Core.Rag;

/// <param name="Grounded">Ответ найден во фрагментах. false — отказ (по порогу поиска или решением модели).</param>
/// <param name="ModelCalled">false — отказ по порогу поиска, модель не вызывалась.</param>
/// <param name="Usage">Токены вызова модели (null, если модель не вызывалась или провайдер их не сообщил).</param>
public sealed record RagAnswer(
    string Answer, IReadOnlyList<SearchHit> Sources, bool Grounded, bool ModelCalled = true, UsageDetails? Usage = null);

/// <summary>Отвечает на вопрос по найденным фрагментам базы знаний, со ссылками на источники.</summary>
public sealed class RagAnswerer(KnowledgeBase knowledgeBase, IChatClient chat, RagOptions options) {
    const string NotFound = "В базе знаний нет ответа на этот вопрос. Создайте обращение на https://help.corp.local.";

    const string SystemPrompt = """
        Ты ассистент IT-поддержки компании. Отвечай на вопрос только по приведённым фрагментам регламентов.
        Ответ — JSON с двумя полями:
        - found: true, если во фрагментах есть ответ на вопрос; false, если нет.
        - answer: при found = true — ответ по существу полным предложением, в конце регламент в квадратных скобках;
          при found = false — одно предложение о том, что в регламентах ответа нет. Не придумывай и не отвечай из общих знаний.
        Отвечай кратко и по делу, на русском языке. Команды и адреса приводи точно как во фрагментах.

        Пример:
        {"found": true, "answer": "Учётная запись блокируется на 30 минут после 5 неверных попыток ввода пароля. [RB-011]"}
        """;

    // Отказ — отдельное поле, а не фраза в тексте: сильная модель переформулирует «В регламентах нет ответа»
    // («Регламенты этого не описывают…»), и проверка по строке засчитала бы отказ как ответ.
    static readonly ChatResponseFormat Format = ChatResponseFormat.ForJsonSchema(
        JsonSerializer.SerializeToElement(new {
            type = "object",
            properties = new {
                found = new { type = "boolean" },
                answer = new { type = "string" },
            },
            required = new[] { "found", "answer" },
            additionalProperties = false,
        }),
        "rag_answer");

    public async Task<RagAnswer> AskAsync(string question, CancellationToken ct = default) {
        var hits = await knowledgeBase.SearchAsync(question, options.TopK, ct);
        if (hits.Count == 0 || !IsRelevant(hits))
            return new RagAnswer(NotFound, hits, Grounded: false, ModelCalled: false);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"Фрагменты регламентов:\n\n{FormatContext(hits)}Вопрос: {question}"),
        ];

        var response = await chat.GetResponseAsync(messages, new ChatOptions { Temperature = 0.1f, ResponseFormat = Format }, ct);
        var (found, answer) = ParseAnswer(response.Text);
        return new RagAnswer(answer, hits, found, ModelCalled: true, response.Usage);
    }

    /// <summary>Фрагменты в том виде, в каком их видит модель (и судья при оценке).</summary>
    public static string FormatContext(IEnumerable<SearchHit> hits) {
        var context = new StringBuilder();
        foreach (var hit in hits)
            context.Append($"[{hit.DocId}] {hit.Text}\n\n");
        return context.ToString();
    }

    /// <summary>
    /// Разбирает JSON-ответ модели. Если модель вернула не JSON (провайдер без поддержки схемы),
    /// берём текст как есть и распознаём отказ по фразе из прежней версии промпта.
    /// </summary>
    public static (bool Found, string Answer) ParseAnswer(string text) {
        try {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("found", out var found) && found.ValueKind is JsonValueKind.True or JsonValueKind.False
                && root.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String) {
                return (found.GetBoolean(), answer.GetString()!);
            }
        } catch (JsonException) {
            // не JSON — ниже
        }
        return (!text.Contains("В регламентах нет ответа", StringComparison.OrdinalIgnoreCase), text);
    }

    // Оценка reranker'а разделяет «есть ответ / нет ответа» надёжнее косинуса (см. docs/rag-eval.md).
    // Оценки RRF и BM25 для порога не годятся: у них нет абсолютной шкалы.
    bool IsRelevant(IReadOnlyList<SearchHit> hits) =>
        hits.Max(h => h.RerankScore) is { } rerank
            ? rerank >= options.RerankMinScore
            : (hits.Max(h => h.DenseScore) ?? 0) >= options.MinScore;
}
