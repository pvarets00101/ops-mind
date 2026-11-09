using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace OpsMind.Core.Llm.ClaudeCli;

/// <summary>
/// Превращает список ChatMessage в то, что принимает `claude -p`: системный промпт (файлом) и один текст (в stdin).
/// В отличие от Messages API, CLI не принимает готовую историю с ответами ассистента,
/// поэтому диалог разворачивается в стенограмму с XML-тегами — Claude хорошо понимает такую разметку.
/// </summary>
public static class ClaudePrompt {
    const string DefaultSystem = "Ты полезный ассистент.";

    // Кириллица в аргументах — как есть, а не В…: модели так проще читать, и токенов меньше.
    static readonly JsonSerializerOptions Json = new() {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static (string System, string Prompt) Build(IEnumerable<ChatMessage> messages, ChatOptions? options) {
        var list = messages.ToList();

        var systemParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
            systemParts.Add(options.Instructions.Trim());
        systemParts.AddRange(list.Where(m => m.Role == ChatRole.System).Select(m => m.Text.Trim()).Where(t => t.Length > 0));
        // Без своего системного промпта CLI подставил бы стандартный промпт Claude Code (агент для программирования).
        var system = systemParts.Count > 0 ? string.Join("\n\n", systemParts) : DefaultSystem;

        var dialog = list.Where(m => m.Role != ChatRole.System).ToList();

        // Частый случай — один вопрос пользователя (RAG, классификатор): отдаём его как есть, без разметки.
        if (dialog is [{ } single] && single.Role == ChatRole.User && single.Contents.All(c => c is TextContent))
            return (system, single.Text);

        var sb = new StringBuilder("<conversation>\n");
        foreach (var message in dialog)
            AppendMessage(sb, message);
        sb.Append("</conversation>\n\n");
        sb.Append("Выше — диалог, включая вызовы инструментов и их результаты. Продолжи его как assistant: ответь на последнее сообщение пользователя, без XML-тегов.");
        return (system, sb.ToString());
    }

    static void AppendMessage(StringBuilder sb, ChatMessage message) {
        var role = message.Role == ChatRole.User ? "user" : message.Role == ChatRole.Tool ? "tool" : "assistant";
        foreach (var content in message.Contents) {
            switch (content) {
                case TextContent { Text.Length: > 0 } text:
                    sb.Append($"<{role}>\n{text.Text.Trim()}\n</{role}>\n");
                    break;
                // Вызовы инструментов и их результаты попадают в историю агента (agent_messages).
                // Показываем их модели текстом, чтобы она знала, что уже было найдено.
                case FunctionCallContent call:
                    sb.Append($"<tool_call name=\"{call.Name}\">{JsonSerializer.Serialize(call.Arguments, Json)}</tool_call>\n");
                    break;
                case FunctionResultContent result:
                    sb.Append($"<tool_result>\n{result.Result}\n</tool_result>\n");
                    break;
            }
        }
    }
}