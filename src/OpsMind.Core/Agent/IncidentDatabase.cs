using System.Globalization;
using System.Text;
using Npgsql;

namespace OpsMind.Core.Agent;

/// <summary>Выполняет проверенный SELECT под read-only ролью и возвращает результат текстовой таблицей для модели.</summary>
public sealed class IncidentDatabase(AgentOptions options) {
    public async Task<string> QueryAsync(string sql, CancellationToken ct = default) {
        var check = SqlGuard.Check(sql, options.MaxSqlRows);
        if (!check.IsValid)
            return $"Ошибка: {check.Error} Исправь запрос.";

        try {
            await using var connection = new NpgsqlConnection(options.ReadOnlyConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(check.Sql, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));
            var rows = 0;
            while (await reader.ReadAsync(ct)) {
                var values = Enumerable.Range(0, reader.FieldCount).Select(i => Format(reader.GetValue(i)));
                sb.AppendLine(string.Join(" | ", values));
                rows++;
            }
            sb.AppendLine(rows == 0 ? "(нет строк)" : $"({rows} строк{(rows == options.MaxSqlRows ? ", выдача обрезана" : "")})");
            return sb.ToString();
        } catch (PostgresException e) {
            // Текст ошибки возвращаем модели: по нему она может исправить запрос (например, имя колонки).
            return $"Ошибка SQL: {e.MessageText}. Исправь запрос.";
        }
    }

    static string Format(object value) => value switch {
        DBNull => "NULL",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
        double x => x.ToString("0.##", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}