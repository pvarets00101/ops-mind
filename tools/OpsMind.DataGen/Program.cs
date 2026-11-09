using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpsMind.DataGen;

// Генератор синтетических тикетов и инцидентов. Детерминирован: один и тот же seed даёт один и тот же датасет.
// Запуск из корня репозитория: dotnet run --project tools/OpsMind.DataGen -- [ticketCount] [seed]

var ticketCount = args.Length > 0 ? int.Parse(args[0]) : 3000;
var seed = args.Length > 1 ? int.Parse(args[1]) : 42;
var rnd = new Random(seed);
var dataDir = Path.Combine(FindRepoRoot(), "data");

var periodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3));
var periodEnd = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.FromHours(3));

var normal = Templates.All.Where(t => !t.IsMajor).ToArray();
var major = Templates.All.Where(t => t.IsMajor).ToArray();

// ---------- крупные инциденты ----------
var incidents = new List<Incident>();
for (var i = 1; i <= 30; i++) {
    var t = Pick(major);
    var started = WorkTime(RandomDate());
    var duration = t.Priority == "P1" ? rnd.Next(25, 300) : rnd.Next(40, 480);
    var (cause, resolution) = Split(Pick(t.Causes!));
    incidents.Add(new Incident(
        $"MI-2026-{i:D3}", Fill(Pick(t.Subjects)), t.Service!, t.Category, t.Priority, t.Team, t.RunbookId,
        started, started.AddMinutes(duration), cause, resolution, rnd.Next(30, 900), t));
}
incidents.Sort((a, b) => a.StartedAt.CompareTo(b.StartedAt));

// ---------- тикеты ----------
var tickets = new List<Ticket>();
var seen = new HashSet<string>();

foreach (var inc in incidents) {
    var count = rnd.Next(2, 9);
    for (var k = 0; k < count; k++) {
        var created = inc.StartedAt.AddMinutes(rnd.Next(0, Math.Max(5, (int)(inc.ResolvedAt - inc.StartedAt).TotalMinutes / 2)));
        AddTicket(inc.Template, created, inc);
    }
}

var totalWeight = normal.Sum(t => t.Weight);
var attempts = 0;
while (tickets.Count < ticketCount && attempts++ < ticketCount * 50)
    AddTicket(PickWeighted(normal, totalWeight), WorkTime(RandomDate()), null);

tickets.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
for (var i = 0; i < tickets.Count; i++)
    tickets[i] = tickets[i] with { Id = $"T-2026-{i + 1:D5}" };

// ---------- запись ----------
var json = new JsonSerializerOptions {
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

WriteJsonl(Path.Combine(dataDir, "tickets", "tickets.jsonl"), tickets, json);
WriteJsonl(Path.Combine(dataDir, "incidents", "incidents.jsonl"), incidents.Select(i => i.ToRow()), json);
WriteFinetune(Path.Combine(dataDir, "finetune"), tickets, json);
WriteSqlSeed(Path.Combine(dataDir, "sql", "002_seed.sql"), incidents, tickets);

Console.WriteLine($"Инцидентов: {incidents.Count}, тикетов: {tickets.Count} (seed {seed})");
foreach (var g in tickets.GroupBy(t => t.Priority).OrderBy(g => g.Key))
    Console.WriteLine($"  {g.Key}: {g.Count()}");
foreach (var g in tickets.GroupBy(t => t.Category).OrderByDescending(g => g.Count()))
    Console.WriteLine($"  {g.Key,-13} {g.Count()}");
return;

// ---------- генерация ----------
void AddTicket(TicketTemplate t, DateTimeOffset created, Incident? inc) {
    var subject = Fill(Pick(t.Subjects));
    var body = Pick(Slots.Greetings) + Fill(Pick(t.Descriptions)) + Pick(Slots.Closings);
    if (!seen.Add(subject + "|" + body))
        return;

    var vip = rnd.NextDouble() < 0.05;
    var priority = vip && t.Priority == "P4" ? "P3" : t.Priority; // RB-001: VIP не ниже P3

    var dept = Pick(Slots.Departments);
    var status = created > periodEnd.AddDays(-3) ? Pick(["new", "in_progress"]) : "resolved";
    DateTimeOffset? resolved = null;
    int? minutes = null;
    if (status == "resolved") {
        minutes = inc is not null
            ? Math.Max(1, (int)(inc.ResolvedAt - created).TotalMinutes)
            : ResolutionMinutes(priority);
        resolved = created.AddMinutes(minutes.Value);
    }

    tickets.Add(new Ticket(
        Id: "",
        CreatedAt: created,
        Channel: Pick(Slots.Channels),
        Department: dept,
        Requester: Pick(Slots.People),
        IsVip: vip,
        Subject: subject,
        Description: body,
        Category: t.Category,
        Priority: priority,
        Team: t.Team,
        RunbookId: t.RunbookId,
        Status: status,
        ResolvedAt: resolved,
        ResolutionMinutes: minutes,
        SlaBreached: minutes is not null && minutes > SlaMinutes(priority),
        IncidentId: inc?.Id));
}

// Распределение времени решения: большинство укладывается в SLA, около 7% нарушают.
int ResolutionMinutes(string priority) {
    var sla = SlaMinutes(priority);
    var share = rnd.NextDouble() < 0.07 ? 1.05 + rnd.NextDouble() : 0.05 + rnd.NextDouble() * 0.9;
    return Math.Max(5, (int)(sla * share));
}

// SLA решения из RB-001 в календарных минутах (упрощение: рабочие дни ≈ 24 часа).
static int SlaMinutes(string priority) => priority switch {
    "P1" => 4 * 60,
    "P2" => 8 * 60,
    "P3" => 3 * 24 * 60,
    _ => 10 * 24 * 60,
};

string Fill(string template) => Regex.Replace(template, @"\{(\w+)\}", m => m.Groups[1].Value switch {
    "login" => Pick(Slots.Logins),
    "fio" => Pick(Slots.People),
    "dept" => Pick(Slots.Departments),
    "share" => Pick(Slots.Shares),
    "partner" => Pick(Slots.Partners),
    "contractor" => Pick(Slots.Contractors),
    "contract" => $"Д-{rnd.Next(100, 999)}/2026",
    "server" => Pick(Slots.Servers),
    "svc" => Pick(Slots.Services),
    "socket" => $"{rnd.Next(1, 6)}-{rnd.Next(100, 520)}-{rnd.Next(1, 24):D2}",
    "room" => Pick(Slots.Rooms),
    "date" => RandomDate().ToString("d MMMM", CultureInfo.GetCultureInfo("ru-RU")),
    "site" => Pick(Slots.Sites),
    "n" => rnd.Next(2, 250).ToString(),
    "floor" => rnd.Next(1, 6).ToString(),
    "software" => Pick(Slots.Software),
    "printer" => Pick(Slots.Printers),
    "db" => Pick(Slots.Databases),
    "app" => Pick(Slots.Apps),
    "ns" => Pick(Slots.Namespaces),
    "base" => Pick(Slots.Bases1C),
    var other => throw new InvalidOperationException($"Неизвестный слот {other}"),
});

T Pick<T>(T[] items) => items[rnd.Next(items.Length)];

TicketTemplate PickWeighted(TicketTemplate[] items, int total) {
    var x = rnd.Next(total);
    foreach (var t in items) {
        if ((x -= t.Weight) < 0)
            return t;
    }
    return items[^1];
}

DateTimeOffset RandomDate() =>
    periodStart.AddMinutes(rnd.NextInt64((long)(periodEnd - periodStart).TotalMinutes));

// 85% обращений приходится на рабочее время пн–пт 09:00–18:00.
DateTimeOffset WorkTime(DateTimeOffset d) {
    if (rnd.NextDouble() > 0.85)
        return d;
    while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        d = d.AddDays(1);
    return new DateTimeOffset(d.Year, d.Month, d.Day, rnd.Next(9, 18), rnd.Next(60), 0, d.Offset);
}

static (string, string) Split(string s) {
    var parts = s.Split('|');
    return (parts[0], parts[1]);
}

// ---------- вывод ----------
static void WriteJsonl<T>(string path, IEnumerable<T> rows, JsonSerializerOptions options) {
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllLines(path, rows.Select(r => JsonSerializer.Serialize(r, options)), new UTF8Encoding(false));
}

void WriteFinetune(string dir, List<Ticket> all, JsonSerializerOptions options) {
    const string system =
        "Ты классификатор обращений IT-поддержки. Верни JSON с полями category, priority, team. " +
        "category: access, email, vpn, network, workstation, printing, server, database, kubernetes, cicd, security, business_app. " +
        "priority: P1, P2, P3, P4. team: ServiceDesk, Network, Infra, Database, DevOps, Security, Apps.";

    var shuffled = all.OrderBy(_ => rnd.Next()).ToList();
    var trainEnd = (int)(shuffled.Count * 0.8);
    var valEnd = (int)(shuffled.Count * 0.9);
    var splits = new Dictionary<string, List<Ticket>> {
        ["train"] = shuffled[..trainEnd],
        ["val"] = shuffled[trainEnd..valEnd],
        ["test"] = shuffled[valEnd..],
    };

    foreach (var (name, items) in splits) {
        var rows = items.Select(t => new {
            id = t.Id,
            messages = new object[] {
                new { role = "system", content = system },
                new { role = "user", content = $"Тема: {t.Subject}\nОписание: {t.Description}" },
                new { role = "assistant", content = JsonSerializer.Serialize(new { category = t.Category, priority = t.Priority, team = t.Team }) },
            },
        });
        WriteJsonl(Path.Combine(dir, $"{name}.jsonl"), rows, options);
    }
}

static void WriteSqlSeed(string path, List<Incident> incidents, List<Ticket> tickets) {
    var sb = new StringBuilder("-- Сгенерировано tools/OpsMind.DataGen. Не редактировать вручную.\nBEGIN;\nTRUNCATE tickets, incidents;\n\n");
    foreach (var i in incidents) {
        sb.Append("INSERT INTO incidents VALUES (")
            .AppendJoin(", ", Q(i.Id), Q(i.Title), Q(i.Service), Q(i.Category), Q(i.Priority), Q(i.Team), Q(i.RunbookId),
                Q(i.StartedAt), Q(i.ResolvedAt), Q(i.RootCause), Q(i.Resolution), i.AffectedUsers.ToString())
            .Append(");\n");
    }
    sb.Append('\n');
    foreach (var t in tickets) {
        sb.Append("INSERT INTO tickets VALUES (")
            .AppendJoin(", ", Q(t.Id), Q(t.CreatedAt), Q(t.Channel), Q(t.Department), Q(t.Requester), t.IsVip ? "true" : "false",
                Q(t.Subject), Q(t.Description), Q(t.Category), Q(t.Priority), Q(t.Team), Q(t.RunbookId), Q(t.Status),
                Q(t.ResolvedAt), t.ResolutionMinutes?.ToString() ?? "NULL", t.SlaBreached ? "true" : "false", Q(t.IncidentId))
            .Append(");\n");
    }
    sb.Append("\nCOMMIT;\n");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

    static string Q(object? v) => v switch {
        null => "NULL",
        DateTimeOffset d => $"'{d:yyyy-MM-dd HH:mm:sszzz}'",
        _ => $"'{v.ToString()!.Replace("'", "''")}'",
    };
}

static string FindRepoRoot() {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpsMind.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Не найден корень репозитория (OpsMind.sln)");
}

record Ticket(
    string Id, DateTimeOffset CreatedAt, string Channel, string Department, string Requester, bool IsVip,
    string Subject, string Description, string Category, string Priority, string Team, string RunbookId,
    string Status, DateTimeOffset? ResolvedAt, int? ResolutionMinutes, bool SlaBreached, string? IncidentId);

record Incident(
    string Id, string Title, string Service, string Category, string Priority, string Team, string RunbookId,
    DateTimeOffset StartedAt, DateTimeOffset ResolvedAt, string RootCause, string Resolution, int AffectedUsers,
    [property: JsonIgnore] TicketTemplate Template) {
    public object ToRow() => new {
        id = Id,
        title = Title,
        service = Service,
        category = Category,
        priority = Priority,
        team = Team,
        runbook_id = RunbookId,
        started_at = StartedAt,
        resolved_at = ResolvedAt,
        root_cause = RootCause,
        resolution = Resolution,
        affected_users = AffectedUsers,
    };
}