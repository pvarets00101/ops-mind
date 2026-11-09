namespace OpsMind.Core.Llm.ClaudeCli;

/// <summary>
/// Claude через Claude Code CLI (`claude -p`) — по подписке, без API-ключа. Только для личного использования:
/// отдавать подписку другим пользователям или вытаскивать из неё токен для прямых вызовов API нельзя.
/// </summary>
public sealed class ClaudeCliOptions {
    /// <summary>На Windows `claude` — npm-шим claude.cmd: Process.Start без оболочки находит только его.</summary>
    public string ExecutablePath { get; set; } = OperatingSystem.IsWindows() ? "claude.cmd" : "claude";

    /// <summary>Алиас (haiku, sonnet, opus) или полное имя модели.</summary>
    public string Model { get; set; } = "haiku";

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Сколько процессов claude одновременно. Лимиты подписки общие с claude.ai и Claude Code,
    /// поэтому по умолчанию 1: параллельные прогоны оценок быстро съедают пятичасовое окно.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Рабочая папка процесса claude. Пустая и вне репозитория: в системный контекст попадает путь к ней,
    /// а из папки проекта CLI мог бы подхватить CLAUDE.md. По умолчанию — %TEMP%/opsmind-claude.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}