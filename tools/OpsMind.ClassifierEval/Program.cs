using System.ClientModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using OpenAI;

// Оценка классификатора тикетов: category / priority / team.
// Режимы промпта:
//   bare    — только системный промпт из обучающей выборки (ровно то, что видит дообученная модель);
//   fewshot — + правила приоритетов RB-001, соответствие категорий командам и 12 примеров из train.
// Наборы: test — data/finetune/test.jsonl (шаблонные формулировки, как в обучении), hard — data/finetune/test_hard.jsonl (написан вручную).
// Запуск: dotnet run --project tools/OpsMind.ClassifierEval -- --model qwen3:4b-instruct --mode fewshot --set hard [--limit 150] [--base-url ...]

var model = Arg("--model") ?? "qwen3:4b-instruct";
var mode = Arg("--mode") ?? "bare";
var set = Arg("--set") ?? "hard";
var limit = int.Parse(Arg("--limit") ?? "1000");
var baseUrl = Arg("--base-url") ?? "http://localhost:11434/v1";
var inv = CultureInfo.InvariantCulture;

var root = FindRepoRoot();
var finetuneDir = Path.Combine(root, "data", "finetune");
var train = ReadSet(Path.Combine(finetuneDir, "train.jsonl"));
var items = ReadSet(Path.Combine(finetuneDir, set == "hard" ? "test_hard.jsonl" : "test.jsonl")).Take(limit).ToList();
var systemPrompt = File.ReadLines(Path.Combine(finetuneDir, "train.jsonl")).Select(ParseMessages).First().System;

var chat = new OpenAIClient(new ApiKeyCredential("local"), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(model).AsIChatClient();

List<ChatMessage> prefix = [new(ChatRole.System, mode == "fewshot" ? FewShotSystem(systemPrompt) : systemPrompt)];
if (mode == "fewshot") {
    // По одному примеру на категорию — как пары user/assistant, так модель лучше перенимает формат.
    foreach (var example in train.GroupBy(t => t.Category).Select(g => g.First())) {
        prefix.Add(new(ChatRole.User, example.Input));
        prefix.Add(new(ChatRole.Assistant, JsonSerializer.Serialize(new { category = example.Category, priority = example.Priority, team = example.Team })));
    }
}

var options = new ChatOptions { Temperature = 0, ResponseFormat = ChatResponseFormat.Json, MaxOutputTokens = 100 };
var results = new List<Prediction>();
var sw = Stopwatch.StartNew();
foreach (var item in items) {
    var started = sw.Elapsed;
    var response = await chat.GetResponseAsync([.. prefix, new(ChatRole.User, item.Input)], options);
    results.Add(Parse(item, response.Text, (sw.Elapsed - started).TotalSeconds));
    Console.Write(results[^1].Valid ? (results[^1].ExactMatch ? "." : "x") : "!");
}
Console.WriteLine();

// ---------- метрики ----------
double Acc(Func<Prediction, bool> f) => results.Count(f) / (double)results.Count;
string[] priorities = ["P1", "P2", "P3", "P4"];
var macroF1 = priorities.Average(p => {
    var tp = results.Count(r => r.Item.Priority == p && r.Priority == p);
    var fp = results.Count(r => r.Item.Priority != p && r.Priority == p);
    var fn = results.Count(r => r.Item.Priority == p && r.Priority != p);
    return tp == 0 ? 0 : 2.0 * tp / (2 * tp + fp + fn);
});

var report = new StringBuilder()
    .AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm} — {model}, {mode}, набор {set} ({results.Count})").AppendLine()
    .AppendLine("| Валидный JSON | category | priority | team | Все три | Macro-F1 (priority) | Время на тикет |")
    .AppendLine("|---|---|---|---|---|---|---|")
    .AppendLine(string.Create(inv,
        $"| {Acc(r => r.Valid):P0} | {Acc(r => r.Category == r.Item.Category):P0} | {Acc(r => r.Priority == r.Item.Priority):P0} | " +
        $"{Acc(r => r.Team == r.Item.Team):P0} | {Acc(r => r.ExactMatch):P0} | {macroF1:F3} | {results.Average(r => r.Seconds):N1} с |"))
    .AppendLine()
    .AppendLine("Матрица ошибок priority (строки — эталон, столбцы — ответ модели):").AppendLine()
    .AppendLine("| | P1 | P2 | P3 | P4 | иное |").AppendLine("|---|---|---|---|---|---|");
foreach (var truth in priorities) {
    var row = results.Where(r => r.Item.Priority == truth).ToList();
    report.AppendLine($"| **{truth}** | {string.Join(" | ", priorities.Select(p => row.Count(r => r.Priority == p)))} | {row.Count(r => !priorities.Contains(r.Priority))} |");
}
report.AppendLine();

var errors = results.Where(r => !r.ExactMatch).Take(15).ToList();
if (errors.Count > 0) {
    report.AppendLine("Примеры ошибок (эталон → ответ):").AppendLine();
    foreach (var e in errors) {
        report.AppendLine($"- {e.Item.Id}: «{Short(e.Item.Input.Replace("Тема: ", "").Replace("\nОписание: ", " — "), 90)}» " +
                          $"{e.Item.Category}/{e.Item.Priority}/{e.Item.Team} → {(e.Valid ? $"{e.Category}/{e.Priority}/{e.Team}" : $"невалидный ответ: {Short(e.Raw, 60)}")}");
    }
    report.AppendLine();
}

Console.WriteLine(report);
AppendReport(Path.Combine(root, "docs", "classifier-eval.md"), report.ToString());
return;

// ---------- helpers ----------
static string FewShotSystem(string basePrompt) => basePrompt + """

    Правила приоритета — по МАСШТАБУ влияния, слова «срочно», «горит» на приоритет не влияют:
    P1 — недоступен сервис для клиентов, или работа остановлена у 50+ сотрудников, или шифровальщик;
    P2 — затронут отдел или ключевой процесс, деградация продуктивного сервиса, компрометация учётной записи, утеря устройства;
    P3 — проблема у одного сотрудника или одного рабочего места/принтера, есть обходной путь;
    P4 — запрос: доступ, установка ПО, новое оборудование, консультация, расходные материалы.
    Команды: access/email/workstation/printing → ServiceDesk (доставка писем внешним получателям — Infra);
    vpn/network → Network (блокировка сайтов прокси — Security); server → Infra; database → Database;
    kubernetes/cicd → DevOps (утечка секретов — Security); security → Security; business_app (1С, CRM) → Apps.
    Ответь только JSON.
    """;

static Prediction Parse(Item item, string raw, double seconds) {
    var match = Regex.Match(raw, @"\{[^{}]*\}");
    if (!match.Success)
        return new Prediction(item, false, null, null, null, raw, seconds);
    try {
        using var doc = JsonDocument.Parse(match.Value);
        string? Get(string name) => doc.RootElement.TryGetProperty(name, out var v) ? v.GetString()?.Trim() : null;
        return new Prediction(item, true, Get("category")?.ToLowerInvariant(), Get("priority")?.ToUpperInvariant(), Get("team"), raw, seconds);
    } catch (JsonException) {
        return new Prediction(item, false, null, null, null, raw, seconds);
    }
}

// Читает оба формата: обучающий (messages) и ручной (subject/description + метки).
static List<Item> ReadSet(string path) => File.ReadLines(path).Where(l => l.Length > 0).Select(line => {
    using var doc = JsonDocument.Parse(line);
    var r = doc.RootElement;
    if (r.TryGetProperty("messages", out _)) {
        var (_, user, assistant) = ParseMessages(line);
        using var labels = JsonDocument.Parse(assistant);
        var l = labels.RootElement;
        return new Item(r.GetProperty("id").GetString()!, user,
            l.GetProperty("category").GetString()!, l.GetProperty("priority").GetString()!, l.GetProperty("team").GetString()!);
    }
    return new Item(r.GetProperty("id").GetString()!,
        $"Тема: {r.GetProperty("subject").GetString()}\nОписание: {r.GetProperty("description").GetString()}",
        r.GetProperty("category").GetString()!, r.GetProperty("priority").GetString()!, r.GetProperty("team").GetString()!);
}).ToList();

static (string System, string User, string Assistant) ParseMessages(string line) {
    using var doc = JsonDocument.Parse(line);
    var m = doc.RootElement.GetProperty("messages");
    return (m[0].GetProperty("content").GetString()!, m[1].GetProperty("content").GetString()!, m[2].GetProperty("content").GetString()!);
}

string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

static string Short(string s, int max) {
    s = s.Replace("\r", " ").Replace("\n", " ");
    return s.Length <= max ? s : s[..max] + "…";
}

static void AppendReport(string path, string report) {
    if (!File.Exists(path))
        File.WriteAllText(path, "# Оценка классификатора тикетов\n\nИстория запусков tools/OpsMind.ClassifierEval, новые сверху.\n\n");
    var existing = File.ReadAllText(path);
    var firstRun = existing.IndexOf("\n## ", StringComparison.Ordinal);
    File.WriteAllText(path, firstRun < 0
        ? existing + report
        : existing[..(firstRun + 1)] + report + existing[(firstRun + 1)..]);
    Console.WriteLine($"Отчёт дописан в {path}");
}

static string FindRepoRoot() {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpsMind.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Не найден корень репозитория (OpsMind.sln)");
}

record Item(string Id, string Input, string Category, string Priority, string Team);

record Prediction(Item Item, bool Valid, string? Category, string? Priority, string? Team, string Raw, double Seconds) {
    public bool ExactMatch => Valid && Category == Item.Category && Priority == Item.Priority && Team == Item.Team;
}