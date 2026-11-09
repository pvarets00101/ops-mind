using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace OpsMind.Core.Llm.ClaudeCli;

/// <summary>
/// Вызов функций для модели, которая не отдаёт tool_use наружу (`claude -p` гоняет свой цикл агента внутри).
///
/// Приём: инструменты описываются в системном промпте, а ответ модели ограничивается JSON-схемой:
/// либо {"tool_calls": [{"name": …, "arguments": {…}}]}, либо {"answer": "…"}.
/// Вызовы превращаются в FunctionCallContent — дальше работает стандартный FunctionInvokingChatClient:
/// выполняет функции и снова зовёт модель с результатами. Так же устроен нативный tool calling «под капотом»:
/// модель генерирует JSON с именем функции и аргументами, а выполняет её код приложения.
/// </summary>
public static class ClaudeToolEmulation {
    /// <summary>Инструменты, которые модель может вызвать в этом запросе (ToolMode = None — никаких).</summary>
    public static IReadOnlyList<AIFunctionDeclaration> ActiveTools(ChatOptions? options) =>
        (options?.ToolMode is NoneChatToolMode)
            ? []
            : options?.Tools?.OfType<AIFunctionDeclaration>().ToList() ?? [];

    /// <summary>
    /// Схема ответа. Для каждого инструмента — свой вариант в anyOf: имя фиксировано (const), аргументы — по схеме функции.
    /// CLI проверяет ответ по схеме, поэтому модель не может вызвать несуществующий инструмент или забыть обязательный аргумент.
    /// </summary>
    public static string Schema(IReadOnlyList<AIFunctionDeclaration> tools, ChatToolMode? mode) {
        var variants = new JsonArray();
        foreach (var tool in tools) {
            variants.Add(new JsonObject {
                ["type"] = "object",
                ["properties"] = new JsonObject {
                    ["name"] = new JsonObject { ["const"] = tool.Name },
                    ["arguments"] = JsonNode.Parse(tool.JsonSchema.GetRawText()),
                },
                ["required"] = new JsonArray("name", "arguments"),
            });
        }

        var toolCalls = new JsonObject {
            ["type"] = "array",
            ["description"] = "Инструменты, которые нужно вызвать сейчас. Независимые вызовы можно перечислить сразу.",
            ["items"] = new JsonObject { ["anyOf"] = variants },
        };
        // RequireAny: модель обязана вызвать хотя бы один инструмент.
        if (mode is RequiredChatToolMode)
            toolCalls["minItems"] = 1;

        var schema = new JsonObject {
            ["type"] = "object",
            ["properties"] = new JsonObject {
                ["tool_calls"] = toolCalls,
                ["answer"] = new JsonObject {
                    ["type"] = "string",
                    ["description"] = "Окончательный ответ пользователю, когда инструменты больше не нужны.",
                },
            },
        };
        return schema.ToJsonString();
    }

    /// <summary>Раздел системного промпта: какие инструменты есть и как ими пользоваться через формат ответа.</summary>
    public static string Instructions(IReadOnlyList<AIFunctionDeclaration> tools) {
        var sb = new StringBuilder();
        sb.AppendLine("## Инструменты");
        sb.AppendLine();
        sb.AppendLine("Ты можешь вызывать инструменты. Ответ всегда даётся в формате JSON по заданной схеме:");
        sb.AppendLine("- чтобы вызвать инструменты — заполни `tool_calls` (имя и аргументы по схеме инструмента), `answer` не заполняй;");
        sb.AppendLine("- результаты придут следующим сообщением в теге <tool_result>; после этого вызови ещё инструменты или ответь;");
        sb.AppendLine("- когда информации достаточно — заполни `answer` окончательным ответом пользователю, `tool_calls` оставь пустым.");
        sb.AppendLine("Не выдумывай результаты инструментов: если нужны данные — вызови инструмент.");
        sb.AppendLine();
        foreach (var tool in tools) {
            sb.AppendLine($"### {tool.Name}");
            if (!string.IsNullOrWhiteSpace(tool.Description))
                sb.AppendLine(tool.Description.Trim());
            sb.AppendLine($"Аргументы (JSON Schema): {tool.JsonSchema.GetRawText()}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Ответ модели → вызовы функций или текст.</summary>
    public static IList<AIContent> ParseResponse(string json) {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array && calls.GetArrayLength() > 0) {
            var contents = new List<AIContent>();
            foreach (var call in calls.EnumerateArray()) {
                var name = call.GetProperty("name").GetString()!;
                var arguments = call.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object
                    ? a.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())
                    : new Dictionary<string, object?>();
                // CallId связывает вызов с его результатом в истории; у CLI своих id нет — генерируем.
                contents.Add(new FunctionCallContent($"call_{Guid.NewGuid().ToString("N")[..12]}", name, arguments));
            }
            return contents;
        }

        var answer = root.TryGetProperty("answer", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        return [new TextContent(answer ?? "")];
    }
}