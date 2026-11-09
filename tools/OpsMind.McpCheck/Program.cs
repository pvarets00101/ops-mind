using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Проверка MCP-сервера OpsMind по протоколу — так же, как это делает Claude Code или любой другой MCP-клиент.
// Запуск: dotnet run --project tools/OpsMind.McpCheck [-- http://localhost:5100/mcp]
var endpoint = new Uri(args.FirstOrDefault() ?? "http://localhost:5100/mcp");

await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions {
    Endpoint = endpoint,
    Name = "opsmind-check",
}));

Console.WriteLine($"Сервер: {client.ServerInfo.Name} {client.ServerInfo.Version}");
Console.WriteLine($"Инструкции сервера: {client.ServerInstructions}\n");

Console.WriteLine("== tools ==");
var tools = await client.ListToolsAsync();
foreach (var tool in tools) {
    var a = tool.ProtocolTool.Annotations;
    Console.WriteLine($"- {tool.Name} [readOnly={a?.ReadOnlyHint}, destructive={a?.DestructiveHint}] {Short(tool.Description, 90)}");
}

Console.WriteLine("\n== resources ==");
var resources = await client.ListResourcesAsync();
Console.WriteLine($"{resources.Count} ресурсов, например: {string.Join(", ", resources.Take(3).Select(r => $"{r.Uri} ({r.Title})"))}");
var runbook = await client.ReadResourceAsync("runbook://RB-011");
Console.WriteLine($"runbook://RB-011 → {Short(runbook.Contents.OfType<TextResourceContents>().First().Text, 120)}");

Console.WriteLine("\n== prompts ==");
var prompts = await client.ListPromptsAsync();
foreach (var p in prompts)
    Console.WriteLine($"- {p.Name}: {p.Description}");
var prompt = await client.GetPromptAsync("triage_ticket", new Dictionary<string, object?> { ["ticketId"] = "T-2026-02967" });
Console.WriteLine($"triage_ticket(T-2026-02967) → {Short(prompt.Messages[0].Content is TextContentBlock t ? t.Text : "?", 120)}");

Console.WriteLine("\n== вызовы инструментов ==");
await Call("search_docs", new() { ["query"] = "учётная запись заблокирована, как найти источник блокировки" });
await Call("query_history", new() { ["sql"] = "SELECT category, count(*) AS n FROM tickets GROUP BY category ORDER BY n DESC LIMIT 3" });
await Call("query_history", new() { ["sql"] = "DELETE FROM tickets" });
await Call("get_ticket", new() { ["ticketId"] = "T-2026-02967" });
await Call("read_attachments", new() { ["ticketId"] = "T-2026-02967" });
return;

async Task Call(string name, Dictionary<string, object?> arguments) {
    var result = await client.CallToolAsync(name, arguments);
    var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
    Console.WriteLine($"- {name}({string.Join(", ", arguments.Values)}){(result.IsError == true ? " [ОШИБКА]" : "")}\n  → {Short(text, 220)}");
}

static string Short(string? s, int max) {
    s = (s ?? "").Replace("\r", " ").Replace("\n", " / ");
    return s.Length <= max ? s : s[..max] + "…";
}