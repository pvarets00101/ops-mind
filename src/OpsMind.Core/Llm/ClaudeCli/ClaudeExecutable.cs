namespace OpsMind.Core.Llm.ClaudeCli;

/// <summary>Где лежит исполняемый файл Claude Code CLI.</summary>
public static class ClaudeExecutable {
    const string PackageExe = @"node_modules\@anthropic-ai\claude-code\bin\claude.exe";

    /// <summary>
    /// Явно заданный путь — как есть. Иначе на Windows ищем в PATH npm-шим claude.cmd и берём claude.exe,
    /// который он запускает (`"%dp0%\node_modules\@anthropic-ai\claude-code\bin\claude.exe" %*`).
    /// Так процесс стартует без cmd.exe, и .NET передаёт аргументы с кавычками без искажений.
    /// </summary>
    public static string Resolve(string? configured) {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        if (!OperatingSystem.IsWindows())
            return "claude";

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
            var dir = entry.Trim('"');
            var exe = Path.Combine(dir, "claude.exe");      // нативная установка
            if (File.Exists(exe))
                return exe;
            var packaged = Path.Combine(dir, PackageExe);    // npm: рядом с claude.cmd
            if (File.Exists(packaged))
                return packaged;
        }
        return "claude.cmd"; // запасной вариант: работает, но без сложных аргументов
    }
}