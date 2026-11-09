using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpsMind.Core.Llm;
using OpsMind.Core.Rag;

// Оценка поиска (retrieval):
//  1. data/eval/rag-questions.jsonl — hit@k и MRR по документам для каждой конфигурации поиска;
//  2. data/eval/rag-offtopic.jsonl — калибровка порога отказа: вопросы вне базы против вопросов из базы.
// Запуск из корня репозитория:
//   dotnet run --project tools/OpsMind.RagEval [-- --label "описание" --sparse-weight 0.5 --no-rerank]
// Перед запуском документы должны быть проиндексированы (src/OpsMind.Ingestion), reranker — запущен (deploy/docker-compose.yml).

var label = Arg("--label") ?? "без описания";
var sparseWeight = double.Parse(Arg("--sparse-weight") ?? "0.5", CultureInfo.InvariantCulture);
var withRerank = !args.Contains("--no-rerank");
const int depth = 10;
var inv = CultureInfo.InvariantCulture;
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

var options = new RagOptions { SparseWeight = sparseWeight, RerankerUrl = "http://localhost:8081/" };
var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
    .AddOpsMindLlm(new LlmOptions())
    .AddOpsMindRag(options)
    .BuildServiceProvider();
var kb = services.GetRequiredService<KnowledgeBase>();

var root = FindRepoRoot();
var questions = ReadJsonl<EvalQuestion>(Path.Combine(root, "data", "eval", "rag-questions.jsonl"));
var offTopic = ReadJsonl<OffTopicQuestion>(Path.Combine(root, "data", "eval", "rag-offtopic.jsonl"));

List<Config> configs =
[
    new("Dense", SearchMode.Dense, false),
    new("Sparse (BM25)", SearchMode.Sparse, false),
    new($"Hybrid (вес BM25 {sparseWeight.ToString(inv)})", SearchMode.Hybrid, false),
];
if (withRerank)
    configs.Add(new("Hybrid + rerank", SearchMode.Hybrid, true));

var report = new StringBuilder()
    .AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm} — {label}")
    .AppendLine()
    .AppendLine($"Вопросов: {questions.Count}, глубина: {depth} фрагментов. Ранг считается по уникальным документам.")
    .AppendLine()
    .AppendLine("| Конфигурация | hit@1 | hit@3 | hit@5 | MRR | Время поиска |")
    .AppendLine("|---|---|---|---|---|---|");

var results = new Dictionary<Config, List<Result>>();
foreach (var config in configs)
{
    var list = new List<Result>();
    var sw = Stopwatch.StartNew();
    foreach (var q in questions)
        list.Add(Evaluate(q, await kb.SearchAsync(q.Question, depth, config.Mode, config.Rerank)));
    results[config] = list;

    double HitAt(int k) => list.Count(r => r.Rank > 0 && r.Rank <= k) / (double)list.Count;
    var mrr = list.Average(r => r.Rank > 0 ? 1.0 / r.Rank : 0);
    var ms = sw.Elapsed.TotalMilliseconds / questions.Count;
    report.AppendLine(string.Create(inv, $"| {config.Name} | {HitAt(1):P0} | {HitAt(3):P0} | {HitAt(5):P0} | {mrr:F3} | {ms:N0} мс |"));
}
report.AppendLine();

// Промахи: какие вопросы какая конфигурация «чинит» или «ломает».
var missIds = results.Values.SelectMany(r => r).Where(r => r.Rank != 1).Select(r => r.Question.Id).Distinct().Order().ToList();
if (missIds.Count > 0)
{
    report.AppendLine("Промахи (ранг правильного документа; — — не нашёлся в топ-10):").AppendLine()
        .AppendLine($"| Вопрос | Ожидался | {string.Join(" | ", configs.Select(c => c.Name))} |")
        .AppendLine($"|---|---|{string.Concat(configs.Select(_ => "---|"))}");
    foreach (var id in missIds)
    {
        var q = questions.First(x => x.Id == id);
        var ranks = configs.Select(c => results[c].First(r => r.Question.Id == id).Rank is var r and > 0 ? r.ToString() : "—");
        report.AppendLine($"| {q.Id}: {q.Question} | {q.ExpectedRunbook} | {string.Join(" | ", ranks)} |");
    }
    report.AppendLine();
}

// Калибровка порога отказа по двум сигналам: косинус лучшего фрагмента и оценка reranker'а.
var offTopicHits = new List<(OffTopicQuestion Q, IReadOnlyList<SearchHit> Hits)>();
foreach (var q in offTopic)
    offTopicHits.Add((q, await kb.SearchAsync(q.Question, depth, SearchMode.Hybrid, withRerank)));

var hybrid = results[configs[^1]];
Calibrate("косинус лучшего фрагмента (dense)", options.MinScore,
    hybrid.Select(r => r.BestDense).ToList(),
    offTopicHits.Select(o => (o.Q, o.Hits.Max(h => h.DenseScore) ?? 0)).ToList());
if (withRerank)
{
    Calibrate("оценка reranker'а лучшего фрагмента", null,
        hybrid.Select(r => r.BestRerank).ToList(),
        offTopicHits.Select(o => (o.Q, o.Hits.Max(h => h.RerankScore) ?? 0)).ToList());
}

Console.WriteLine(report);
AppendReport(Path.Combine(root, "docs", "rag-eval.md"), report.ToString());
return;

Result Evaluate(EvalQuestion q, IReadOnlyList<SearchHit> hits)
{
    var docs = hits.Select(h => h.DocId).Distinct().ToList();
    return new Result(q, docs.IndexOf(q.ExpectedRunbook) + 1, hits.Max(h => h.DenseScore) ?? 0, hits.Max(h => h.RerankScore) ?? 0);
}

void Calibrate(string signal, float? current, List<float> inScores, List<(OffTopicQuestion Q, float Score)> outScores)
{
    report.AppendLine($"### Порог отказа: {signal}").AppendLine()
        .AppendLine("| Набор | min | p10 | медиана | max |").AppendLine("|---|---|---|---|---|")
        .AppendLine(Stats("Вопросы из базы", inScores))
        .AppendLine(Stats("Вопросы вне базы", outScores.Select(o => o.Score).ToList()))
        .AppendLine();

    // Перебираем пороги: ошибки двух видов — отказ на вопрос из базы и пропуск вопроса вне базы.
    var best = inScores.Concat(outScores.Select(o => o.Score)).Distinct().Order()
        .Select(t => (Threshold: t, Rejects: inScores.Count(s => s < t), Accepts: outScores.Count(o => o.Score >= t)))
        .OrderBy(x => x.Rejects + x.Accepts)
        .ThenBy(x => x.Rejects) // лучше пропустить лишнее к LLM, чем отказать на реальный вопрос
        .First();

    if (current is { } c)
    {
        report.AppendLine(string.Create(inv,
            $"Текущий порог {c:F2}: отказов на вопросы из базы — {inScores.Count(s => s < c)}, " +
            $"пропущено вопросов вне базы — {outScores.Count(o => o.Score >= c)} из {outScores.Count}."));
    }
    report.AppendLine(string.Create(inv,
            $"Лучший порог по сумме ошибок: {best.Threshold:F3} — отказов {best.Rejects}, пропущено {best.Accepts}."))
        .AppendLine()
        .AppendLine("Вопросы вне базы с наибольшей оценкой:").AppendLine();
    foreach (var (q, score) in outScores.OrderByDescending(o => o.Score).Take(5))
        report.AppendLine(string.Create(inv, $"- {score:F3} — {q.Question} ({q.Kind})"));
    report.AppendLine();
}

string Stats(string name, List<float> s)
{
    var sorted = s.Order().ToList();
    float P(double p) => sorted[(int)Math.Floor(p * (sorted.Count - 1))];
    return string.Create(inv, $"| {name} ({s.Count}) | {sorted[0]:F3} | {P(0.1):F3} | {P(0.5):F3} | {sorted[^1]:F3} |");
}

string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

List<T> ReadJsonl<T>(string path) => File.ReadLines(path)
    .Where(l => l.Length > 0)
    .Select(l => JsonSerializer.Deserialize<T>(l, json)!)
    .ToList();

static void AppendReport(string path, string report)
{
    if (!File.Exists(path))
        File.WriteAllText(path, "# Оценка RAG (retrieval)\n\nИстория запусков tools/OpsMind.RagEval, новые сверху.\n\n");
    var existing = File.ReadAllText(path);
    var firstRun = existing.IndexOf("\n## ", StringComparison.Ordinal);
    File.WriteAllText(path, firstRun < 0
        ? existing + report
        : existing[..(firstRun + 1)] + report + existing[(firstRun + 1)..]);
    Console.WriteLine($"Отчёт дописан в {path}");
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpsMind.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Не найден корень репозитория (OpsMind.sln)");
}

record Config(string Name, SearchMode Mode, bool Rerank);

record EvalQuestion(string Id, string Question, string ExpectedRunbook, string ReferenceAnswer);

record OffTopicQuestion(string Id, string Question, string Kind);

record Result(EvalQuestion Question, int Rank, float BestDense, float BestRerank);
