using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OpsMind.Core.Agent;

public sealed record ToolCall(string Name, string Arguments, string Result) {
    /// <summary>Полный результат — для проверки ссылок; в ответ API уходит сокращённый Result.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string FullResult { get; init; } = Result;
}

/// <param name="Corrections">Замечания шага проверки ответа (пусто, если ответ прошёл проверку сразу).</param>
public sealed record AgentReply(
    string SessionId, string Answer, IReadOnlyList<ToolCall> ToolCalls, IReadOnlyList<PendingAction> PendingActions,
    IReadOnlyList<string> Corrections, double Seconds);

/// <summary>
/// Агент IT-поддержки на Microsoft Agent Framework. Цикл «модель → вызов инструмента → результат → модель»
/// выполняет фреймворк; здесь — инструкции, набор инструментов и хранение диалогов (сессий).
/// </summary>
public sealed partial class OpsMindAgent {
    const string Instructions = """
        Ты OpsMind — ассистент IT-поддержки компании. У тебя есть инструменты; используй их, а не память.
        Ты НЕ знаешь правил этой компании: на любой вопрос о правилах, сроках, запретах, настройках
        сначала вызови search_docs, даже если ответ кажется очевидным.
        Если в вопросе несколько частей — ответь на каждую.
        Выбор инструмента:
        - «как сделать», «что делать, если», правила, сроки, лимиты → search_docs;
        - «сколько», «какие были», статистика, история, похожие случаи → query_history (пиши SQL по схеме из описания: обращения — таблица tickets, крупные инциденты — incidents);
        - вопрос о конкретном тикете T-2026-xxxxx → get_ticket; разобраться в причине по логам тикета → read_attachments,
          затем search_docs, чтобы найти решение по регламенту;
        - пользователь просит создать заявку → create_ticket. Заявка ждёт подтверждения — обязательно скажи об этом.
          Приоритет ставь по масштабу (сколько людей и сервисов затронуто), а не по срочности в тексте:
          P1 — сервис недоступен для клиентов или 50+ сотрудников, P2 — отдел, P3 — один сотрудник или одно место, P4 — запрос.
        Если инструмент вернул ошибку SQL — исправь запрос и попробуй ещё раз.
        Не придумывай факты, которых нет в результатах инструментов. Если ответа нет — так и скажи.
        Ссылайся на регламенты в формате [RB-xxx]. Отвечай кратко, на русском языке.
        Для приветствий и вопросов о себе инструменты не нужны.
        """;

    /// <summary>Автор служебных сообщений проверки ответа (см. ChatAsync).</summary>
    public const string VerifierAuthor = "verifier";

    readonly AIAgent _agent;
    readonly PendingActionStore _pendingActions;

    public OpsMindAgent(
        IChatClient chatClient, OpsMindTools tools, PendingActionStore pendingActions, PostgresChatHistoryProvider history) {
        _pendingActions = pendingActions;
        _agent = chatClient.AsAIAgent(new ChatClientAgentOptions {
            Name = "OpsMind",
            // История диалогов — в Postgres: переживает перезапуск, видна в API и в SQL.
            ChatHistoryProvider = history,
            ChatOptions = new ChatOptions {
                Instructions = Instructions,
                Tools = tools.All(),
                // Низкая температура: агенту нужна предсказуемость (выбор инструмента, SQL), а не разнообразие.
                // С температурой по умолчанию один и тот же вопрос то вызывал search_docs, то нет.
                Temperature = 0.1f,
            },
        });
    }

    public async Task<AgentReply> ChatAsync(string? sessionId, string message, CancellationToken ct = default) {
        sessionId ??= Guid.NewGuid().ToString("N")[..12];
        // Сессия создаётся на каждый запрос и несёт только id диалога; сами сообщения провайдер берёт из Postgres.
        // Поэтому неважно, какой процесс (или экземпляр сервиса после перезапуска) обрабатывает запрос.
        var session = await _agent.CreateSessionAsync(ct);
        session.StateBag.SetValue(PostgresChatHistoryProvider.ConversationIdKey, sessionId);

        var pendingBefore = (await _pendingActions.ListAsync(sessionId, ct)).Select(a => a.Id).ToHashSet();
        PendingActionStore.CurrentSession.Value = sessionId;

        var sw = Stopwatch.StartNew();
        var response = await _agent.RunAsync(message, session, cancellationToken: ct);
        var toolCalls = ExtractToolCalls(response.Messages);
        var answer = response.Text;
        var corrections = new List<string>();

        // Проверка ответа: каждая ссылка [RB-xxx] должна встречаться в результатах инструментов этого запуска.
        // Иначе модель её выдумала — даём одну попытку исправиться.
        var unverified = UnverifiedCitations(answer, toolCalls);
        if (unverified.Count > 0) {
            var feedback = $"[Проверка ответа] Ссылки {string.Join(", ", unverified)} не подтверждены результатами инструментов. " +
                           "Найди решение через search_docs и ссылайся только на регламенты из его результатов, либо убери ссылки.";
            corrections.Add(feedback);

            // Для модели это реплика пользователя, но написал её код проверки: AuthorName сохраняется в истории,
            // и UI по нему отличает служебное сообщение от слов человека.
            var verifierMessage = new ChatMessage(ChatRole.User, feedback) { AuthorName = VerifierAuthor };
            var retry = await _agent.RunAsync(verifierMessage, session, cancellationToken: ct);
            toolCalls.AddRange(ExtractToolCalls(retry.Messages));
            answer = retry.Text;

            if (UnverifiedCitations(answer, toolCalls) is { Count: > 0 } stillBad) {
                answer = CitationRegex().Replace(answer, m => stillBad.Contains(m.Value) ? "" : m.Value);
                corrections.Add($"Неподтверждённые ссылки удалены из ответа: {string.Join(", ", stillBad)}");
            }
        }

        var created = (await _pendingActions.ListAsync(sessionId, ct)).Where(a => !pendingBefore.Contains(a.Id)).ToList();
        return new AgentReply(sessionId, answer, toolCalls, created, corrections, sw.Elapsed.TotalSeconds);
    }

    static List<string> UnverifiedCitations(string answer, List<ToolCall> toolCalls) {
        var evidence = string.Join("\n", toolCalls.Select(c => c.FullResult));
        return CitationRegex().Matches(answer).Select(m => m.Value).Distinct()
            .Where(id => !evidence.Contains(id, StringComparison.Ordinal))
            .ToList();
    }

    [GeneratedRegex(@"\[RB-\d{3}\]")]
    private static partial Regex CitationRegex();

    /// <summary>Вызовы инструментов из истории ответа: запрос модели (FunctionCallContent) + результат (FunctionResultContent).</summary>
    internal static List<ToolCall> ExtractToolCalls(IEnumerable<ChatMessage> messages) {
        var contents = messages.SelectMany(m => m.Contents).ToList();
        var results = contents.OfType<FunctionResultContent>().ToDictionary(r => r.CallId, r => r.Result?.ToString() ?? "");
        return contents.OfType<FunctionCallContent>()
            .Select(c => new ToolCall(
                c.Name,
                JsonSerializer.Serialize(c.Arguments, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
                Truncate(results.GetValueOrDefault(c.CallId, ""), 500)) {
                FullResult = results.GetValueOrDefault(c.CallId, ""),
            })
            .ToList();
    }

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}