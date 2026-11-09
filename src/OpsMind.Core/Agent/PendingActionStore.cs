using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace OpsMind.Core.Agent;

/// <summary>
/// Human-in-the-loop: изменяющие действия агента (создание тикета) не выполняются сразу,
/// а сохраняются как «ожидающие подтверждения». Выполняет их человек через API.
/// Так ошибка модели или prompt injection во вложении не приведёт к реальным изменениям без проверки.
/// Хранятся в Postgres (data/sql/005_agent_actions.sql), поэтому переживают перезапуск.
/// </summary>
public sealed class PendingActionStore(NpgsqlDataSource db, TicketClient tickets) {
    const string Columns = "id, conversation_id, kind, payload, status, result, created_at, decided_at";

    /// <summary>Сессия текущего запуска агента — чтобы инструмент знал, к какому диалогу привязать действие.</summary>
    public static readonly AsyncLocal<string?> CurrentSession = new();

    public async Task<PendingAction> AddAsync(NewTicket ticket, CancellationToken ct = default) {
        await using var command = db.CreateCommand(
            $"INSERT INTO agent_actions (id, conversation_id, kind, payload) VALUES ($1, $2, $3, $4) RETURNING {Columns}");
        command.Parameters.AddWithValue($"A-{Guid.NewGuid().ToString("N")[..8]}");
        command.Parameters.AddWithValue((object?)CurrentSession.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("create_ticket");
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(ticket) });
        return (await ReadAsync(command, ct))[0];
    }

    public async Task<IReadOnlyList<PendingAction>> ListAsync(string? sessionId = null, CancellationToken ct = default) {
        await using var command = db.CreateCommand(
            $"SELECT {Columns} FROM agent_actions WHERE $1::text IS NULL OR conversation_id = $1 ORDER BY created_at");
        command.Parameters.AddWithValue((object?)sessionId ?? DBNull.Value);
        return await ReadAsync(command, ct);
    }

    /// <summary>
    /// Подтверждение. Шаг 1 — атомарно забрать действие: UPDATE … WHERE status = 'pending' сработает только у одного
    /// из двух одновременных запросов, второй получит null. Без этого двойной клик по «Подтвердить» создал бы два тикета.
    /// Шаг 2 — вызвать ITSM (с ключом идемпотентности). Шаг 3 — записать итог.
    /// </summary>
    public async Task<PendingAction?> ApproveAsync(string id, CancellationToken ct = default) {
        await using (var claim = db.CreateCommand(
            "UPDATE agent_actions SET status = 'approving' WHERE id = $1 AND status = 'pending'")) {
            claim.Parameters.AddWithValue(id);
            if (await claim.ExecuteNonQueryAsync(ct) == 0)
                return null; // нет такого действия или его уже обработали
        }

        var action = await GetAsync(id, ct) ?? throw new InvalidOperationException($"Действие {id} пропало");
        try {
            var created = await tickets.CreateAsync(action.Ticket, idempotencyKey: action.Id, ct);
            return await FinishAsync(id, "approved", $"Создан тикет {created.GetProperty("id").GetString()}", ct);
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
            // ITSM недоступна и после повторов Polly. Возвращаем в pending: человек сможет подтвердить ещё раз,
            // а ключ идемпотентности не даст создать дубликат, если первый запрос всё-таки дошёл.
            await using var revert = db.CreateCommand("UPDATE agent_actions SET status = 'pending', result = $2 WHERE id = $1");
            revert.Parameters.AddWithValue(id);
            revert.Parameters.AddWithValue($"ITSM недоступна: {e.Message}");
            await revert.ExecuteNonQueryAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PendingAction?> RejectAsync(string id, CancellationToken ct = default) {
        await using var command = db.CreateCommand(
            $"UPDATE agent_actions SET status = 'rejected', decided_at = now() WHERE id = $1 AND status = 'pending' RETURNING {Columns}");
        command.Parameters.AddWithValue(id);
        return (await ReadAsync(command, ct)).FirstOrDefault();
    }

    async Task<PendingAction?> GetAsync(string id, CancellationToken ct) {
        await using var command = db.CreateCommand($"SELECT {Columns} FROM agent_actions WHERE id = $1");
        command.Parameters.AddWithValue(id);
        return (await ReadAsync(command, ct)).FirstOrDefault();
    }

    async Task<PendingAction> FinishAsync(string id, string status, string result, CancellationToken ct) {
        await using var command = db.CreateCommand(
            $"UPDATE agent_actions SET status = $2, result = $3, decided_at = now() WHERE id = $1 RETURNING {Columns}");
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(result);
        return (await ReadAsync(command, ct))[0];
    }

    static async Task<List<PendingAction>> ReadAsync(NpgsqlCommand command, CancellationToken ct) {
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<PendingAction>();
        while (await reader.ReadAsync(ct)) {
            list.Add(new PendingAction(
                reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.GetString(2),
                JsonSerializer.Deserialize<NewTicket>(reader.GetString(3))!,
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }
        return list;
    }
}

public sealed record PendingAction(
    string Id, string SessionId, string Kind, NewTicket Ticket, string Status, string? Result, DateTimeOffset CreatedAt);