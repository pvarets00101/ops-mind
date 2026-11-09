namespace OpsMind.Core.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Строка подключения под read-only ролью opsmind_agent (data/sql/003_agent_role.sql).</summary>
    public string ReadOnlyConnectionString { get; set; } =
        "Host=localhost;Port=5433;Database=opsmind;Username=opsmind_agent;Password=agent_ro";

    /// <summary>Строка подключения приложения: диалоги агента (data/sql/004_conversations.sql).</summary>
    public string AppConnectionString { get; set; } =
        "Host=localhost;Port=5433;Database=opsmind;Username=opsmind_app;Password=app_rw";

    /// <summary>Сколько последних сообщений диалога отдавать модели (окно начинается с сообщения пользователя).</summary>
    public int MaxHistoryMessages { get; set; } = 30;

    /// <summary>Адрес ITSM (src/OpsMind.TicketService.Mock).</summary>
    public string TicketServiceUrl { get; set; } = "http://localhost:5090/";

    public string S3Endpoint { get; set; } = "http://localhost:8333";

    public string S3AccessKey { get; set; } = "opsmind";

    public string S3SecretKey { get; set; } = "opsmind123";

    public string AttachmentsBucket { get; set; } = "attachments";

    /// <summary>Сколько строк максимум возвращает SQL-инструмент — защита контекста модели от переполнения.</summary>
    public int MaxSqlRows { get; set; } = 30;
}
