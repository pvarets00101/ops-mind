using OpsMind.Core.Rag;

namespace OpsMind.Tests;

public class MarkdownChunkerTests
{
    const string Doc = """
        ---
        id: RB-999
        title: Тестовый регламент
        category: vpn
        team: Network
        ---
        # Тестовый регламент

        ## Симптомы
        VPN не подключается.

        ## Решение
        ```bash
        ## это комментарий в коде, а не заголовок
        ping vpn.corp.local
        ```
        Перезапустить клиент.
        """;

    [Fact]
    public void Chunk_ReadsFrontMatter_AndSplitsBySections()
    {
        var chunks = MarkdownChunker.Chunk(Doc, "RB-999.md");

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.Equal("RB-999", c.DocId));
        Assert.Equal(["Симптомы", "Решение"], chunks.Select(c => c.Section));
        Assert.Equal("vpn", chunks[0].Category);
    }

    [Fact]
    public void Chunk_PrefixesTextWithTitleAndSection()
    {
        var chunks = MarkdownChunker.Chunk(Doc, "RB-999.md");

        Assert.StartsWith("Тестовый регламент. Симптомы\n", chunks[0].Text);
    }

    [Fact]
    public void Chunk_IgnoresHeadingsInsideCodeBlocks()
    {
        var solution = MarkdownChunker.Chunk(Doc, "RB-999.md")[1];

        Assert.Contains("## это комментарий в коде", solution.Text);
        Assert.Contains("Перезапустить клиент.", solution.Text);
    }

    [Fact]
    public void Chunk_SplitsLongSectionByParagraphs()
    {
        var longSection = string.Join("\n\n", Enumerable.Repeat(new string('а', 400), 5));
        var doc = $"---\nid: RB-1\ntitle: T\n---\n## Раздел\n{longSection}";

        var chunks = MarkdownChunker.Chunk(doc, "x.md", maxChars: 1000);

        Assert.True(chunks.Count > 1);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Index));
    }
}
