using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpsMind.Core.Agent;
using OpsMind.Core.Llm;
using OpsMind.Core.Rag;

// Оценка агента на data/eval/agent-scenarios.jsonl:
//  - выбор инструментов: вызваны ли ожидаемые (и ничего лишнего для «болтовни»);
//  - ответ: эталон из SQL (числа — с допуском 1%) или ключевые слова;
//  - приоритет подготовленной заявки.
// Нужны запущенные: docker-compose, OpsMind.TicketService.Mock (порт 5090), проиндексированная база.
// Запуск: dotnet run --project tools/OpsMind.AgentEval [-- --label "описание" --only A03,A11]

var label = Arg("--label") ?? "без описания";
var only = Arg("--only")?.Split(',').ToHashSet();
var inv = CultureInfo.InvariantCulture;
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

var agentOptions = new AgentOptions();
var services = new ServiceCollection()
    .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
    .AddOpsMindLlm(new LlmOptions())
    .AddOpsMindRag(new RagOptions { RerankerUrl = "http://localhost:8081/" })
    .AddOpsMindAgent(agentOptions)
    .BuildServiceProvider();
var agent = services.GetRequiredService<OpsMindAgent>();

var root = FindRepoRoot();
var scenarios = File.ReadLines(Path.Combine(root, "data", "eval", "agent-scenarios.jsonl"))
    .Where(l => l.Length > 0)
    .Select(l => JsonSerializer.Deserialize<Scenario>(l, json)!)
    .Where(s => only is null || only.Contains(s.Id))
    .ToList();

var results = new List<Outcome>();
foreach (var s in scenarios) {
    Console.Write($"{s.Id} {s.Message} … ");
    var expected = s.SqlCheck is null ? null : await ScalarAsync(s.SqlCheck);
    AgentReply reply;
    try {
        reply = await agent.ChatAsync(null, s.Message);
    } catch (Exception e) {
        results.Add(new Outcome(s, false, false, null, [], $"ОШИБКА: {e.Message}", 0, 0));
        Console.WriteLine("ошибка");
        continue;
    }

    var called = reply.ToolCalls.Select(c => c.Name).ToList();
    var toolsOk = s.ExpectedTools is null
        || (s.ExpectedTools.Count == 0 ? called.Count == 0 : s.ExpectedTools.All(called.Contains));

    var answerOk = true;
    if (expected is not null)
        answerOk = AnswerMatches(reply.Answer, expected);
    if (s.AnswerAny is { Count: > 0 })
        answerOk &= s.AnswerAny.Any(k => reply.Answer.Contains(k, StringComparison.OrdinalIgnoreCase));

    // Допустимых приоритетов может быть несколько («P3|P4»): эталон тоже бывает неоднозначным.
    bool? priorityOk = s.ExpectedPriority is null ? null
        : s.ExpectedPriority.Split('|').Contains(reply.PendingActions.FirstOrDefault()?.Ticket.Priority);
    var details = string.Join("; ", reply.ToolCalls.Select(c => $"{c.Name}({Short(c.Arguments, 160)})"));
    if (s.ExpectedPriority is not null)
        details += $"; приоритет {reply.PendingActions.FirstOrDefault()?.Ticket.Priority ?? "—"} (ожидался {s.ExpectedPriority})";
    if (expected is not null)
        details += $"; эталон: {expected}";

    results.Add(new Outcome(s, toolsOk, answerOk, priorityOk, called, details, reply.Seconds, reply.Corrections.Count) { Answer = reply.Answer });
    Console.WriteLine($"{(toolsOk ? "инструменты OK" : "инструменты ✗")}, {(answerOk ? "ответ OK" : "ответ ✗")}, {reply.Seconds:N0} с");
}

// ---------- отчёт ----------
double Share(Func<Outcome, bool> f) => results.Count(f) / (double)results.Count;
var withPriority = results.Where(r => r.PriorityOk is not null).ToList();

var report = new StringBuilder()
    .AppendLine($"## {DateTime.Now:yyyy-MM-dd HH:mm} — {label}").AppendLine()
    .AppendLine(string.Create(inv, $"Сценариев: {results.Count}. Модель: {new LlmOptions().ChatModel}.")).AppendLine()
    .AppendLine("| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |")
    .AppendLine("|---|---|---|---|---|---|")
    .AppendLine(string.Create(inv,
        $"| {Share(r => r.ToolsOk):P0} | {Share(r => r.AnswerOk):P0} | {Share(r => r.ToolsOk && r.AnswerOk):P0} | " +
        $"{withPriority.Count(r => r.PriorityOk == true)}/{withPriority.Count} | {results.Average(r => r.Seconds):N0} с | {results.Sum(r => r.Corrections)} |"))
    .AppendLine()
    .AppendLine("| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |")
    .AppendLine("|---|---|---|---|---|---|");
foreach (var r in results) {
    report.AppendLine(string.Create(inv,
        $"| {r.Scenario.Id}: {r.Scenario.Message} | {r.Scenario.Kind} | {(r.ToolsOk ? "✓" : "✗")} | {(r.AnswerOk ? "✓" : "✗")} | {r.Seconds:N0} с | {Escape(r.Details)} |"));
}
report.AppendLine();

var failed = results.Where(r => !r.ToolsOk || !r.AnswerOk).ToList();
if (failed.Count > 0) {
    report.AppendLine("Ответы в неудачных сценариях:").AppendLine();
    foreach (var r in failed)
        report.AppendLine($"- **{r.Scenario.Id}**: {Escape(Short(r.Answer ?? "", 400))}");
    report.AppendLine();
}

Console.WriteLine();
Console.WriteLine(report);
AppendReport(Path.Combine(root, "docs", "agent-eval.md"), report.ToString());
return;

async Task<string?> ScalarAsync(string sql) {
    await using var connection = new NpgsqlConnection(agentOptions.ReadOnlyConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    return Convert.ToString(await command.ExecuteScalarAsync(), inv);
}

// Число — ищем в ответе любое число в пределах 1% (модель может написать «2 инцидента» или «1 245,5 минут»).
static bool AnswerMatches(string answer, string expected) {
    if (!double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        return answer.Contains(expected, StringComparison.OrdinalIgnoreCase);

    var normalized = Regex.Replace(answer, @"(?<=\d)[\s ](?=\d{3}\b)", ""); // «1 245» → «1245»
    return Regex.Matches(normalized, @"\d+(?:[.,]\d+)?")
        .Select(m => double.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture))
        .Any(x => Math.Abs(x - number) <= Math.Max(0.5, Math.Abs(number) * 0.01));
}

string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

static string Short(string s, int max) {
    s = s.Replace("\r", " ").Replace("\n", " ");
    return s.Length <= max ? s : s[..max] + "…";
}

static string Escape(string s) => s.Replace("|", "\\|");

static void AppendReport(string path, string report) {
    if (!File.Exists(path))
        File.WriteAllText(path, "# Оценка агента\n\nИстория запусков tools/OpsMind.AgentEval, новые сверху.\n\n");
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

record Scenario(
    string Id, string Kind, string Message, List<string>? ExpectedTools, List<string>? AnswerAny,
    string? SqlCheck, string? ExpectedPriority);

record Outcome(
    Scenario Scenario, bool ToolsOk, bool AnswerOk, bool? PriorityOk, List<string> Called, string Details,
    double Seconds, int Corrections) {
    public string? Answer { get; init; }
}