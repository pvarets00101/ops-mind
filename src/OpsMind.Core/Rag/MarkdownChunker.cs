using System.Text;

namespace OpsMind.Core.Rag;

/// <summary>
/// Режет markdown-документ по разделам «## ...». Длинные разделы дробятся по абзацам.
/// К каждому фрагменту добавляется заголовок документа и раздела (contextual chunk header):
/// без него фрагмент «Решение» из разных runbook'ов для эмбеддинга выглядит почти одинаково.
/// </summary>
public static class MarkdownChunker
{
    public static IReadOnlyList<DocumentChunk> Chunk(string markdown, string source, int maxChars = 1500)
    {
        var (meta, body) = ParseFrontMatter(markdown);
        var docId = meta.GetValueOrDefault("id") ?? Path.GetFileNameWithoutExtension(source);
        var title = meta.GetValueOrDefault("title") ?? docId;
        var category = meta.GetValueOrDefault("category") ?? "";
        var team = meta.GetValueOrDefault("team") ?? "";

        var chunks = new List<DocumentChunk>();
        foreach (var (section, content) in SplitSections(body))
        {
            foreach (var part in SplitBySize(content, maxChars))
            {
                var text = $"{title}. {section}\n{part}";
                chunks.Add(new DocumentChunk(docId, title, category, team, section, chunks.Count, text, source));
            }
        }
        return chunks;
    }

    static (Dictionary<string, string> Meta, string[] Body) ParseFrontMatter(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return (meta, lines);

        var end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        if (end < 0)
            return (meta, lines);

        foreach (var line in lines[1..end])
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
                meta[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return (meta, lines[(end + 1)..]);
    }

    static IEnumerable<(string Section, string Content)> SplitSections(string[] body)
    {
        var section = "Общее";
        var sb = new StringBuilder();
        var inCode = false;

        foreach (var line in body)
        {
            if (line.TrimStart().StartsWith("```"))
                inCode = !inCode;

            if (!inCode && line.StartsWith("# "))
                continue; // заголовок документа уже есть в title

            if (!inCode && line.StartsWith("## "))
            {
                if (sb.ToString().Trim() is { Length: > 0 } text)
                    yield return (section, text);
                section = line[3..].Trim();
                sb.Clear();
                continue;
            }
            sb.Append(line).Append('\n'); // не AppendLine: на Windows он пишет \r\n и ломает разбиение по абзацам
        }

        if (sb.ToString().Trim() is { Length: > 0 } last)
            yield return (section, last);
    }

    static IEnumerable<string> SplitBySize(string content, int maxChars)
    {
        if (content.Length <= maxChars)
        {
            yield return content;
            yield break;
        }

        var sb = new StringBuilder();
        foreach (var paragraph in content.Split("\n\n"))
        {
            if (sb.Length > 0 && sb.Length + paragraph.Length > maxChars)
            {
                yield return sb.ToString().Trim();
                sb.Clear();
            }
            sb.Append(paragraph).Append("\n\n");
        }
        if (sb.Length > 0)
            yield return sb.ToString().Trim();
    }
}
