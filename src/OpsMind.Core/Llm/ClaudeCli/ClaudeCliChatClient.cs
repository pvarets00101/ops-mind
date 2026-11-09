using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpsMind.Core.Llm.ClaudeCli;

/// <summary>
/// IChatClient поверх `claude -p`: каждый запрос — отдельный процесс Claude Code в режиме «просто модель»
/// (встроенные инструменты и MCP отключены, системный промпт наш). Остальной код OpsMind видит обычный IChatClient
/// и не знает, что под ним CLI, а не HTTP API.
///
/// Ограничения по сравнению с API: нет temperature/top_p/max_tokens, нет нативного вызова функций
/// (CLI сам гоняет цикл агента и не отдаёт tool_use наружу), + ~2 с на запуск процесса.
/// </summary>
public sealed class ClaudeCliChatClient : IChatClient {
    readonly ClaudeCliOptions _options;
    readonly ILogger _log;
    readonly SemaphoreSlim _gate;
    readonly string _workingDirectory;
    readonly string _executable;
    int _warnedAboutTemperature;

    /// <summary>Служебный инструмент, через который CLI выполняет --json-schema.</summary>
    const string StructuredOutputTool = "StructuredOutput";

    public ClaudeCliChatClient(ClaudeCliOptions options, ILogger<ClaudeCliChatClient>? log = null) {
        _options = options;
        _log = log ?? NullLogger<ClaudeCliChatClient>.Instance;
        _gate = new SemaphoreSlim(Math.Max(1, options.MaxConcurrency));
        _workingDirectory = options.WorkingDirectory ?? Path.Combine(Path.GetTempPath(), "opsmind-claude");
        _executable = ClaudeExecutable.Resolve(options.ExecutablePath);
        Metadata = new ChatClientMetadata("claude-cli", defaultModelId: options.Model);
    }

    public ChatClientMetadata Metadata { get; }

    /// <summary>Последняя известная загрузка лимитов подписки (из rate_limit_event).</summary>
    public ClaudeStreamEvent.RateLimit? LastRateLimit { get; private set; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ThrowIfUnsupported(options);

        var (system, prompt) = ClaudePrompt.Build(messages, options);
        var model = options?.ModelId ?? _options.Model;
        var schema = JsonSchemaArgument(options);
        var responseId = Guid.NewGuid().ToString("N");

        await _gate.WaitAsync(cancellationToken);
        Directory.CreateDirectory(_workingDirectory);
        // Системный промпт — файлом: многострочный текст в аргументе ломается при запуске через claude.cmd.
        var systemFile = Path.Combine(_workingDirectory, $"system-{responseId}.md");
        Process? process = null;
        try {
            await File.WriteAllTextAsync(systemFile, system, new UTF8Encoding(false), cancellationToken);
            process = await StartWithRetryAsync(model, systemFile, schema, cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

            await process.StandardInput.WriteAsync(prompt);
            process.StandardInput.Close(); // конец stdin = «вопрос закончен»

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            string? actualModel = null;
            var sawText = false;
            ClaudeStreamEvent.Result? result = null;

            while (await ReadLineAsync(process, timeout.Token, cancellationToken) is { } line) {
                switch (ClaudeStreamEvent.Parse(line)) {
                    case ClaudeStreamEvent.Init init:
                        // Защита: если аргумент --tools "" потерялся по дороге (cmd.exe, другая версия CLI),
                        // модель получила бы Bash/Read/Edit. Не продолжаем — finally убьёт процесс.
                        // Единственное исключение — служебный StructuredOutput в режиме --json-schema.
                        var unexpected = init.Tools.Where(t => schema is null || t != StructuredOutputTool).ToList();
                        if (unexpected.Count > 0)
                            throw new ClaudeCliException($"claude запущен с инструментами ({string.Join(", ", unexpected)}), ожидался режим без них");
                        actualModel = init.Model;
                        break;

                    // В JSON-режиме ответ — аргументы StructuredOutput, а не текст; случайную прозу не отдаём.
                    case ClaudeStreamEvent.TextDelta delta when schema is null:
                        sawText = true;
                        yield return new ChatResponseUpdate(ChatRole.Assistant, delta.Text) {
                            ResponseId = responseId,
                            ModelId = actualModel ?? model,
                        };
                        break;

                    case ClaudeStreamEvent.RateLimit limit:
                        LastRateLimit = limit;
                        break;

                    case ClaudeStreamEvent.Result r:
                        result = r;
                        break;
                }
            }

            await process.WaitForExitAsync(timeout.Token);
            if (result is null)
                throw new ClaudeCliException($"claude завершился с кодом {process.ExitCode} без результата: {(await stderr).Trim()}");
            if (result.IsError)
                throw new ClaudeCliException($"claude вернул ошибку: {result.Text}");

            _log.LogInformation(
                "claude {Model}: {Input} → {Output} токенов, {Ms} мс (по ценам API ${Cost}); лимит 5 ч: {FiveHour:P0}",
                actualModel ?? model, result.InputTokens, result.OutputTokens, result.DurationMs, result.CostUsd,
                LastRateLimit?.FiveHour);

            // Итоговое обновление: остаток текста (если частичных сообщений не было), причина остановки и токены.
            var final = new ChatResponseUpdate {
                Role = ChatRole.Assistant,
                ResponseId = responseId,
                ModelId = actualModel ?? model,
                FinishReason = result.StopReason == "max_tokens" ? ChatFinishReason.Length : ChatFinishReason.Stop,
                Contents = [
                    new UsageContent(new UsageDetails {
                        InputTokenCount = result.InputTokens,
                        OutputTokenCount = result.OutputTokens,
                        TotalTokenCount = result.InputTokens + result.OutputTokens,
                    }),
                ],
            };
            var text = schema is not null ? result.StructuredOutput ?? result.Text
                : sawText ? null
                : result.Text;
            if (!string.IsNullOrEmpty(text))
                final.Contents.Insert(0, new TextContent(text));
            yield return final;
        } finally {
            if (process is { HasExited: false }) {
                // Отмена, таймаут или исключение: процесс claude (и его дочерние node-процессы) не должен остаться висеть.
                try {
                    process.Kill(entireProcessTree: true);
                } catch (InvalidOperationException) {
                    // успел завершиться сам
                }
            }
            process?.Dispose();
            TryDelete(systemFile);
            _gate.Release();
        }
    }

    /// <summary>
    /// Claude Code обновляет себя сам, и на несколько секунд claude.exe бывает недоступен
    /// («not a valid application», ERROR_EXE_MACHINE_TYPE_MISMATCH = 216) — это проверено на прогоне классификатора.
    /// Поэтому неудачный запуск процесса повторяем, а не роняем запрос.
    /// </summary>
    async Task<Process> StartWithRetryAsync(string model, string systemFile, string? schema, CancellationToken ct) {
        for (var attempt = 1; ; attempt++) {
            try {
                return Start(model, systemFile, schema);
            } catch (System.ComponentModel.Win32Exception e) when (attempt < 3) {
                _log.LogWarning("Не удалось запустить {Exe} ({Error}), попытка {Attempt} из 3", _executable, e.Message, attempt);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
            }
        }
    }

    Process Start(string model, string systemFile, string? schema) {
        var psi = new ProcessStartInfo(_executable) {
            WorkingDirectory = _workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        // Аргументы — через ArgumentList: .NET сам экранирует пробелы и кавычки.
        psi.ArgumentList.Add("-p");
        Add(psi, "--output-format", "stream-json");
        psi.ArgumentList.Add("--verbose");                     // без него stream-json в режиме -p не работает
        psi.ArgumentList.Add("--include-partial-messages");    // текст кусками — для стриминга
        Add(psi, "--model", model);
        Add(psi, "--tools", "");                               // никаких Bash/Read/Edit: только модель
        psi.ArgumentList.Add("--strict-mcp-config");           // и никаких MCP-серверов из настроек пользователя
        Add(psi, "--system-prompt-file", systemFile);          // заменяет системный промпт Claude Code целиком
        Add(psi, "--setting-sources", "");                     // без пользовательских хуков, плагинов, CLAUDE.md
        psi.ArgumentList.Add("--no-session-persistence");      // не копить на диске сессию от каждого вызова
        if (schema is not null)
            Add(psi, "--json-schema", schema);                 // только строкой: путь к файлу CLI не принимает

        return Process.Start(psi) ?? throw new ClaudeCliException($"Не удалось запустить {_executable}");
    }

    async Task<string?> ReadLineAsync(Process process, CancellationToken timeout, CancellationToken caller) {
        try {
            return await process.StandardOutput.ReadLineAsync(timeout);
        } catch (OperationCanceledException) when (!caller.IsCancellationRequested) {
            throw new TimeoutException($"claude не ответил за {_options.TimeoutSeconds} с");
        }
    }

    void ThrowIfUnsupported(ChatOptions? options) {
        if (options?.Tools is { Count: > 0 })
            throw new NotSupportedException(
                "ClaudeCli пока не умеет вызывать инструменты: CLI не отдаёт tool_use наружу. " +
                "Для агента используйте провайдер OpenAiCompatible (эмуляция вызова функций — следующий шаг этапа 6).");
        if (options?.Temperature is not null && Interlocked.Exchange(ref _warnedAboutTemperature, 1) == 0)
            _log.LogWarning("ClaudeCli игнорирует Temperature: у claude -p нет такого параметра");
    }

    /// <summary>
    /// ChatResponseFormat.Json без схемы → любая JSON-структура ({"type":"object"});
    /// со схемой (ForJsonSchema или GetResponseAsync&lt;T&gt;) → она сама, одной строкой. Текстовый режим → null.
    /// </summary>
    public static string? JsonSchemaArgument(ChatOptions? options) =>
        options?.ResponseFormat is ChatResponseFormatJson json
            ? json.Schema is { } s ? JsonSerializer.Serialize(s) : """{"type":"object"}"""
            : null;

    static void Add(ProcessStartInfo psi, string name, string value) {
        psi.ArgumentList.Add(name);
        psi.ArgumentList.Add(value);
    }

    static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
            // файл ещё открыт — не страшно, это временная папка
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this
        : serviceType == typeof(ChatClientMetadata) ? Metadata
        : null;

    public void Dispose() => _gate.Dispose();
}

public sealed class ClaudeCliException(string message) : Exception(message);