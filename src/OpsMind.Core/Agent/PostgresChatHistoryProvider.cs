using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OpsMind.Core.Agent;

/// <summary>
/// История диалога агента в Postgres вместо памяти процесса.
/// Agent Framework вызывает провайдер дважды за запуск агента:
/// перед обращением к модели — «дай историю» (ProvideChatHistoryAsync),
/// после ответа — «сохрани новые сообщения» (StoreChatHistoryAsync).
/// Какой диалог грузить, провайдер узнаёт из состояния сессии (StateBag): там лежит id диалога.
/// </summary>
public sealed class PostgresChatHistoryProvider(ConversationStore store, AgentOptions options) : ChatHistoryProvider {
    public const string ConversationIdKey = "opsmind.conversation_id";

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context, CancellationToken cancellationToken = default) {
        var history = await store.LoadAsync(GetConversationId(context.Session), cancellationToken);
        return HistoryWindow.Trim(history, options.MaxHistoryMessages);
    }

    // Базовый класс уже отфильтровал RequestMessages: там только новые сообщения пользователя,
    // без истории, которую мы сами подали. ResponseMessages — ответ агента, включая вызовы инструментов и их результаты.
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default) {
        if (context.InvokeException is not null)
            return; // неудачный запуск не сохраняем: иначе в истории останется вопрос без ответа

        List<ChatMessage> messages = [.. context.RequestMessages, .. context.ResponseMessages ?? []];
        await store.AppendAsync(GetConversationId(context.Session), messages, cancellationToken);
    }

    static string GetConversationId(AgentSession? session) =>
        session is not null && session.StateBag.TryGetValue<string>(ConversationIdKey, out var id) && id is not null
            ? id
            : throw new InvalidOperationException($"В сессии агента нет {ConversationIdKey}: создавайте сессию через OpsMindAgent.");
}

/// <summary>Сколько истории отдавать модели: контекст маленькой модели быстро переполняется результатами инструментов.</summary>
public static class HistoryWindow {
    /// <summary>
    /// Последние <paramref name="max"/> сообщений, но окно начинается с сообщения пользователя.
    /// Нельзя начинать с середины хода: результат инструмента без своего вызова
    /// OpenAI-совместимый API отклоняет, а модель теряет смысл реплики.
    /// </summary>
    public static IReadOnlyList<ChatMessage> Trim(IReadOnlyList<ChatMessage> history, int max) {
        if (history.Count <= max)
            return history;

        var start = history.Count - max;
        while (start < history.Count && history[start].Role != ChatRole.User)
            start++;
        return history.Skip(start).ToList();
    }
}