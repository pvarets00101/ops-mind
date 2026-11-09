using Microsoft.Extensions.AI;
using OpsMind.Core.Agent;

namespace OpsMind.Tests;

public class ConversationHistoryTests
{
    [Fact]
    public void ToolCallSurvivesJsonRoundTrip()
    {
        var call = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_1", "search_docs", new Dictionary<string, object?> { ["query"] = "VPN не подключается" })]);
        var result = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", "[RB-014] Перезапустите клиент")]);

        var restoredCall = ConversationStore.Deserialize(ConversationStore.Serialize(call));
        var restoredResult = ConversationStore.Deserialize(ConversationStore.Serialize(result));

        var fc = Assert.IsType<FunctionCallContent>(Assert.Single(restoredCall.Contents));
        Assert.Equal(ChatRole.Assistant, restoredCall.Role);
        Assert.Equal("call_1", fc.CallId);
        Assert.Equal("search_docs", fc.Name);
        Assert.Equal("VPN не подключается", fc.Arguments!["query"]!.ToString());

        var fr = Assert.IsType<FunctionResultContent>(Assert.Single(restoredResult.Contents));
        Assert.Equal("call_1", fr.CallId);
        Assert.Equal("[RB-014] Перезапустите клиент", fr.Result!.ToString());
    }

    [Fact]
    public void ReadsMessageWhenTypeIsNotFirst()
    {
        // Так сообщение возвращает Postgres: jsonb сортирует ключи, и "$type" оказывается после "callId".
        const string fromJsonb =
            """{"role": "assistant", "contents": [{"name": "search_docs", "callId": "call_1", "$type": "functionCall", "arguments": {"query": "VPN"}}]}""";

        var message = ConversationStore.Deserialize(fromJsonb);

        var fc = Assert.IsType<FunctionCallContent>(Assert.Single(message.Contents));
        Assert.Equal("search_docs", fc.Name);
    }

    [Fact]
    public void VerifierAuthorSurvivesRoundTrip()
    {
        var message = new ChatMessage(ChatRole.User, "[Проверка ответа] …") { AuthorName = OpsMindAgent.VerifierAuthor };

        var restored = ConversationStore.Deserialize(ConversationStore.Serialize(message));

        Assert.Equal(OpsMindAgent.VerifierAuthor, restored.AuthorName);
    }

    [Fact]
    public void ShortHistoryIsReturnedAsIs()
    {
        var history = Turn("привет", toolCalls: 0);
        Assert.Same(history, HistoryWindow.Trim(history, 10));
    }

    [Fact]
    public void WindowStartsAtUserMessage()
    {
        // Ход 1: user, assistant(call), tool, assistant — 4 сообщения. Ход 2: user, assistant — 2.
        List<ChatMessage> history = [.. Turn("первый", toolCalls: 1), .. Turn("второй", toolCalls: 0)];

        // Окно 5 начиналось бы с assistant(call) первого хода — без вопроса пользователя. Его надо пропустить.
        var window = HistoryWindow.Trim(history, 5);

        Assert.Equal(2, window.Count);
        Assert.Equal(ChatRole.User, window[0].Role);
        Assert.Equal("второй", window[0].Text);
    }

    [Fact]
    public void ToolResultIsNeverFirst()
    {
        List<ChatMessage> history = [.. Turn("первый", toolCalls: 2), .. Turn("второй", toolCalls: 1)];

        for (var max = 1; max <= history.Count; max++)
        {
            var window = HistoryWindow.Trim(history, max);
            if (window.Count > 0)
                Assert.True(window[0].Role == ChatRole.User, $"max={max}: окно начинается с {window[0].Role}");
        }
    }

    static List<ChatMessage> Turn(string question, int toolCalls)
    {
        List<ChatMessage> turn = [new(ChatRole.User, question)];
        for (var i = 0; i < toolCalls; i++)
        {
            var id = $"{question}_{i}";
            turn.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(id, "search_docs")]));
            turn.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(id, "результат")]));
        }
        turn.Add(new ChatMessage(ChatRole.Assistant, $"ответ на «{question}»"));
        return turn;
    }
}
