using OpsMind.Core.Rag;

namespace OpsMind.Tests;

/// <summary>Разбор ответа RAG: JSON по схеме и запасной путь для провайдеров без схемы.</summary>
public class RagAnswererTests {
    [Fact]
    public void ParsesFoundAnswer() {
        var (found, answer) = RagAnswerer.ParseAnswer("""{"found": true, "answer": "Блокировка на 30 минут. [RB-011]"}""");
        Assert.True(found);
        Assert.Equal("Блокировка на 30 минут. [RB-011]", answer);
    }

    [Fact]
    public void ParsesRefusalWithAnyWording() {
        // Отказ распознаётся по полю, а не по фразе: формулировка может быть любой.
        var (found, answer) = RagAnswerer.ParseAnswer("""{"found": false, "answer": "Регламенты не описывают выплату зарплаты."}""");
        Assert.False(found);
        Assert.Equal("Регламенты не описывают выплату зарплаты.", answer);
    }

    [Theory]
    [InlineData("Блокировка на 30 минут. [RB-011]", true)]
    [InlineData("В регламентах нет ответа.", false)]
    [InlineData("""{"answer": "нет поля found"}""", true)]
    public void FallsBackToTextWhenNotSchemaJson(string text, bool expectedFound) {
        var (found, answer) = RagAnswerer.ParseAnswer(text);
        Assert.Equal(expectedFound, found);
        Assert.Equal(text, answer);
    }
}
