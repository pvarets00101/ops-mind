using System.Text.Json;
using Microsoft.Extensions.AI;
using Npgsql;
using NpgsqlTypes;

namespace OpsMind.Core.Agent;

public sealed record ConversationSummary(string Id, string Title, int Messages, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record StoredMessage(long Id, string Role, string Text, ChatMessage Message, DateTimeOffset CreatedAt);

/// <summary>
/// Диалоги агента в Postgres (data/sql/004_conversations.sql), под ролью opsmind_app.
/// Каждое сообщение лежит отдельной строкой: ChatMessage целиком в jsonb (с вызовами инструментов и их результатами)
/// и отдельно роль и текст — чтобы смотреть историю SQL-запросом и показывать её в UI без разбора JSON.
/// </summary>
public sealed class ConversationStore(NpgsqlDataSource db) {
    const int TitleLength = 80;

    readonly NpgsqlDataSource _db = db;

    /// <summary>
    /// Сериализация ChatMessage. Настройки из Microsoft.Extensions.AI знают все виды содержимого
    /// (TextContent, FunctionCallContent, FunctionResultContent) и пишут поле $type, чтобы прочитать их обратно.
    /// AllowOutOfOrderMetadataProperties: jsonb в Postgres хранит ключи в своём порядке (короткие раньше),
    /// и $type перестаёт быть первым — без этой опции System.Text.Json отказывается читать такой объект.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(AIJsonUtilities.DefaultOptions) {
        AllowOutOfOrderMetadataProperties = true,
    };

    public async Task<IReadOnlyList<ChatMessage>> LoadAsync(string conversationId, CancellationToken ct = default) {
        await using var command = _db.CreateCommand(
            "SELECT content FROM agent_messages WHERE conversation_id = $1 ORDER BY id");
        command.Parameters.AddWithValue(conversationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var messages = new List<ChatMessage>();
        while (await reader.ReadAsync(ct))
            messages.Add(Deserialize(reader.GetString(0)));
        return messages;
    }

    /// <summary>Дописывает сообщения в конец диалога; диалог создаётся при первой записи. Всё в одной транзакции.</summary>
    public async Task AppendAsync(string conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken ct = default) {
        if (messages.Count == 0)
            return;

        await using var connection = await _db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var title = messages.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? "Новый диалог";
        await using (var upsert = new NpgsqlCommand(
            """
            INSERT INTO agent_conversations (id, title) VALUES ($1, $2)
            ON CONFLICT (id) DO UPDATE SET updated_at = now()
            """, connection, transaction)) {
            upsert.Parameters.AddWithValue(conversationId);
            upsert.Parameters.AddWithValue(Truncate(title.ReplaceLineEndings(" "), TitleLength));
            await upsert.ExecuteNonQueryAsync(ct);
        }

        // Пакет: все INSERT уходят на сервер одним сетевым обменом.
        await using var batch = new NpgsqlBatch(connection, transaction);
        foreach (var message in messages) {
            var insert = new NpgsqlBatchCommand(
                "INSERT INTO agent_messages (conversation_id, role, text, content) VALUES ($1, $2, $3, $4)");
            insert.Parameters.AddWithValue(conversationId);
            insert.Parameters.AddWithValue(message.Role.Value);
            insert.Parameters.AddWithValue(message.Text);
            insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = Serialize(message) });
            batch.BatchCommands.Add(insert);
        }
        await batch.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(int limit = 50, CancellationToken ct = default) {
        await using var command = _db.CreateCommand(
            """
            SELECT c.id, c.title, (SELECT count(*) FROM agent_messages m WHERE m.conversation_id = c.id), c.created_at, c.updated_at
            FROM agent_conversations c
            ORDER BY c.updated_at DESC
            LIMIT $1
            """);
        command.Parameters.AddWithValue(limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<ConversationSummary>();
        while (await reader.ReadAsync(ct)) {
            list.Add(new ConversationSummary(
                reader.GetString(0), reader.GetString(1), (int)reader.GetInt64(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return list;
    }

    public async Task<IReadOnlyList<StoredMessage>?> GetMessagesAsync(string conversationId, CancellationToken ct = default) {
        await using var command = _db.CreateCommand(
            """
            SELECT m.id, m.role, m.text, m.content, m.created_at
            FROM agent_conversations c
            LEFT JOIN agent_messages m ON m.conversation_id = c.id
            WHERE c.id = $1
            ORDER BY m.id
            """);
        command.Parameters.AddWithValue(conversationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<StoredMessage>? messages = null;
        while (await reader.ReadAsync(ct)) {
            messages ??= [];
            if (reader.IsDBNull(0))
                continue; // диалог есть, сообщений нет (LEFT JOIN)
            messages.Add(new StoredMessage(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                Deserialize(reader.GetString(3)), reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return messages; // null — такого диалога нет
    }

    public async Task<bool> DeleteAsync(string conversationId, CancellationToken ct = default) {
        await using var command = _db.CreateCommand("DELETE FROM agent_conversations WHERE id = $1");
        command.Parameters.AddWithValue(conversationId);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public static string Serialize(ChatMessage message) => JsonSerializer.Serialize(message, JsonOptions);

    public static ChatMessage Deserialize(string json) =>
        JsonSerializer.Deserialize<ChatMessage>(json, JsonOptions)
        ?? throw new InvalidDataException("Пустое сообщение в agent_messages");

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}