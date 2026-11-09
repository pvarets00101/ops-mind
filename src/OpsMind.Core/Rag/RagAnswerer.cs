using System.Text;
using Microsoft.Extensions.AI;

namespace OpsMind.Core.Rag;

public sealed record RagAnswer(string Answer, IReadOnlyList<SearchHit> Sources, bool Grounded);

/// <summary>Отвечает на вопрос по найденным фрагментам базы знаний, со ссылками на источники.</summary>
public sealed class RagAnswerer(KnowledgeBase knowledgeBase, IChatClient chat, RagOptions options)
{
    const string NotFound = "В базе знаний нет ответа на этот вопрос. Создайте обращение на https://help.corp.local.";

    const string SystemPrompt = """
        Ты ассистент IT-поддержки компании. Отвечай на вопрос только по приведённым фрагментам регламентов.
        Правила:
        - Если во фрагментах нет ответа, так и скажи: «В регламентах нет ответа». Не придумывай.
        - Сначала дай ответ по существу полным предложением, затем в конце укажи регламент в квадратных скобках.
        - Отвечай кратко и по делу, на русском языке. Команды и адреса приводи точно как во фрагментах.

        Пример ответа:
        Учётная запись блокируется на 30 минут после 5 неверных попыток ввода пароля. [RB-011]
        """;

    public async Task<RagAnswer> AskAsync(string question, CancellationToken ct = default)
    {
        var hits = await knowledgeBase.SearchAsync(question, options.TopK, ct);
        if (hits.Count == 0 || !IsRelevant(hits))
            return new RagAnswer(NotFound, hits, Grounded: false);

        var context = new StringBuilder();
        foreach (var hit in hits)
            context.Append($"[{hit.DocId}] {hit.Text}\n\n");

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"Фрагменты регламентов:\n\n{context}Вопрос: {question}"),
        ];

        var response = await chat.GetResponseAsync(messages, new ChatOptions { Temperature = 0.1f }, ct);

        // Второй уровень защиты: порог MinScore не отсекает близкие по теме вопросы («зарплата», «отпуск»),
        // и тогда модель сама отвечает фразой из промпта.
        var grounded = !response.Text.Contains("В регламентах нет ответа", StringComparison.OrdinalIgnoreCase);
        return new RagAnswer(response.Text, hits, grounded);
    }

    // Оценка reranker'а разделяет «есть ответ / нет ответа» надёжнее косинуса (см. docs/rag-eval.md).
    // Оценки RRF и BM25 для порога не годятся: у них нет абсолютной шкалы.
    bool IsRelevant(IReadOnlyList<SearchHit> hits) =>
        hits.Max(h => h.RerankScore) is { } rerank
            ? rerank >= options.RerankMinScore
            : (hits.Max(h => h.DenseScore) ?? 0) >= options.MinScore;
}
