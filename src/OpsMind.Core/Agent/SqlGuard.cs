using System.Text.RegularExpressions;

namespace OpsMind.Core.Agent;

/// <summary>
/// Проверка SQL, сгенерированного моделью. Это первый рубеж; второй — read-only роль в Postgres
/// с таймаутом (data/sql/003_agent_role.sql). Полагаться только на проверку текста нельзя:
/// запрещённое можно спрятать, поэтому права в базе — обязательны.
/// </summary>
public static partial class SqlGuard {
    public static SqlCheck Check(string sql, int maxRows) {
        var trimmed = sql.Trim().TrimEnd(';').Trim();
        if (trimmed.Length == 0)
            return SqlCheck.Fail("Пустой запрос.");
        if (trimmed.Contains(';'))
            return SqlCheck.Fail("Разрешён только один запрос, без ';' внутри.");
        if (!StartsWithSelect().IsMatch(trimmed))
            return SqlCheck.Fail("Разрешены только запросы SELECT (или WITH ... SELECT).");
        if (Forbidden().Match(trimmed) is { Success: true } m)
            return SqlCheck.Fail($"Запрещённая конструкция: {m.Value}.");

        // Ограничиваем выдачу снаружи: модель может забыть LIMIT или поставить LIMIT 100000.
        return SqlCheck.Ok($"SELECT * FROM ({trimmed}) AS q LIMIT {maxRows}");
    }

    [GeneratedRegex(@"^(select|with)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StartsWithSelect();

    [GeneratedRegex(
        @"\b(insert|update|delete|merge|drop|alter|create|grant|revoke|truncate|copy|call|execute|vacuum|analyze|lock|set|reset|listen|notify|pg_sleep|pg_read_file|pg_read_binary_file|pg_ls_dir|lo_import|lo_export|dblink)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Forbidden();
}

public sealed record SqlCheck(bool IsValid, string Sql, string? Error) {
    public static SqlCheck Ok(string sql) => new(true, sql, null);
    public static SqlCheck Fail(string error) => new(false, "", error);
}