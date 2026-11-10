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

    static SearchHit Hit(string doc, int index, float? rerank = null) =>
        new(doc, doc, $"раздел {index}", $"{doc}#{index}", 0, null, rerank, index);

    [Fact]
    public void NeighborsOfTopHitGoFirstInDocumentOrder() {
        // Q030: найден «Диагностика» RB-040#1, «Решение» RB-040#2 не прошло в TopK.
        IReadOnlyList<SearchHit> hits = [Hit("RB-040", 1, 0.53f), Hit("RB-111", 1, 0.008f), Hit("RB-111", 3, 0.007f)];
        var context = RagAnswerer.WithNeighbors(hits, [Hit("RB-040", 0), Hit("RB-040", 1), Hit("RB-040", 2)]);

        Assert.Equal(["RB-040#0", "RB-040#1", "RB-040#2", "RB-111#1", "RB-111#3"], context.Select(h => h.Text));
        Assert.Equal([true, false, true, false, false], context.Select(h => h.Neighbor));
        Assert.Equal(0.53f, context[1].RerankScore); // найденный поиском фрагмент сохраняет свою оценку
    }

    [Fact]
    public void NeighborAlreadyFoundBySearchIsNotDuplicated() {
        IReadOnlyList<SearchHit> hits = [Hit("RB-040", 1, 0.5f), Hit("RB-111", 0, 0.1f), Hit("RB-040", 2, 0.05f)];
        var context = RagAnswerer.WithNeighbors(hits, [Hit("RB-040", 0), Hit("RB-040", 1), Hit("RB-040", 2)]);

        Assert.Equal(["RB-040#0", "RB-040#1", "RB-040#2", "RB-111#0"], context.Select(h => h.Text));
        Assert.False(context[2].Neighbor);
    }

    [Fact]
    public void KeepsHitsWhenNeighborsDoNotContainTopHit() {
        IReadOnlyList<SearchHit> hits = [Hit("RB-040", 1, 0.5f), Hit("RB-111", 0, 0.1f)];
        Assert.Same(hits, RagAnswerer.WithNeighbors(hits, []));
    }
}
