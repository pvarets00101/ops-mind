using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using OpsMind.Core.Rag;

namespace OpsMind.Core.Agent;

/// <summary>
/// Инструменты агента. Модель видит только имя, описание и параметры каждого метода —
/// поэтому описания написаны как инструкция: когда вызывать и что передавать.
/// </summary>
public sealed class OpsMindTools(
    KnowledgeBase knowledgeBase,
    IncidentDatabase incidents,
    TicketClient tickets,
    AttachmentStore attachments,
    PendingActionStore pendingActions)
{
    public IList<AITool> All() =>
    [
        AIFunctionFactory.Create(SearchDocsAsync, "search_docs"),
        AIFunctionFactory.Create(QueryHistoryAsync, "query_history"),
        AIFunctionFactory.Create(GetTicketAsync, "get_ticket"),
        AIFunctionFactory.Create(ReadAttachmentsAsync, "read_attachments"),
        AIFunctionFactory.Create(CreateTicketAsync, "create_ticket"),
    ];

    [Description("Поиск по регламентам IT-поддержки (runbooks). Используй для вопросов «как сделать», «что делать, если», " +
                 "о правилах, сроках, лимитах, настройках. Возвращает фрагменты с идентификаторами [RB-xxx].")]
    async Task<string> SearchDocsAsync(
        [Description("Поисковый запрос на русском, своими словами: суть проблемы или вопроса")] string query,
        CancellationToken ct)
    {
        var hits = await knowledgeBase.SearchAsync(query, 3, ct);
        if (hits.Count == 0 || hits[0].RerankScore is < 0.02f)
            return "В регламентах ничего не найдено по этому запросу.";

        var sb = new StringBuilder();
        foreach (var hit in hits)
            sb.AppendLine($"[{hit.DocId}] {hit.Text}").AppendLine();
        return sb.ToString();
    }

    [Description("""
        SQL-запрос (только SELECT, PostgreSQL) к истории IT-поддержки. Используй для вопросов «сколько», «какие были»,
        статистики, трендов, поиска похожих случаев. Две таблицы:
        1) tickets — ВСЕ обращения пользователей (3000 строк). Почти все вопросы — про неё. Колонки:
          id 'T-2026-00001', created_at timestamptz, channel [portal|email|phone|chat],
          department — отдел ЗАЯВИТЕЛЯ (Бухгалтерия, Отдел продаж, Отдел кадров, ...),
          requester — ФИО заявителя, is_vip bool, subject, description,
          category, priority [P1|P2|P3|P4],
          team — команда поддержки-ИСПОЛНИТЕЛЬ [ServiceDesk|Network|Infra|Database|DevOps|Security|Apps],
          runbook_id, status [new|in_progress|resolved], resolved_at timestamptz, resolution_minutes int,
          sla_breached bool — нарушен ли SLA, incident_id -> incidents.id.
        2) incidents — только 30 КРУПНЫХ инцидентов (P1/P2). Колонки: id 'MI-2026-001', title, service, category, priority, team,
          runbook_id, started_at, resolved_at, root_cause — корневая причина, resolution, affected_users.
          В incidents НЕТ колонок sla_breached, department, status.
        category: access, email, vpn, network, workstation, printing, server, database, kubernetes, cicd, security, business_app.
        Данные за 2026 год. Для дат используй date_trunc/extract, для текста — ILIKE.
        """)]
    Task<string> QueryHistoryAsync(
        [Description("Один SELECT-запрос к таблицам tickets и incidents")] string sql,
        CancellationToken ct) =>
        incidents.QueryAsync(sql, ct);

    [Description("Текущее состояние тикета во внешней ITSM-системе по номеру вида T-2026-00123: статус, приоритет, команда, комментарии.")]
    async Task<string> GetTicketAsync(
        [Description("Номер тикета, например T-2026-02967")] string ticketId,
        CancellationToken ct)
    {
        try
        {
            return await tickets.GetAsync(ticketId, ct) is { } t ? TicketClient.Format(t) : $"Тикет {ticketId} не найден.";
        }
        catch (HttpRequestException e)
        {
            return $"ITSM недоступна после повторных попыток: {e.Message}";
        }
    }

    [Description("Читает вложения тикета (логи, скриншоты в виде текста, выводы команд). " +
                 "Используй, когда нужно разобраться в причине проблемы по конкретному тикету.")]
    Task<string> ReadAttachmentsAsync(
        [Description("Номер тикета, например T-2026-02967")] string ticketId,
        CancellationToken ct) =>
        attachments.ReadForTicketAsync(ticketId, ct);

    [Description("Готовит новую заявку в ITSM. Заявка НЕ создаётся сразу: она ждёт подтверждения пользователя. " +
                 "Вызывай только когда пользователь явно просит создать или завести заявку.")]
    async Task<string> CreateTicketAsync(
        [Description("Краткая тема заявки")] string subject,
        [Description("Подробное описание проблемы")] string description,
        [Description("Категория: access, email, vpn, network, workstation, printing, server, database, kubernetes, cicd, security, business_app")] string category,
        [Description("Приоритет P1–P4 по масштабу проблемы: P1 — остановлен сервис для многих, P3 — проблема у одного сотрудника, P4 — запрос")] string priority,
        [Description("Команда: ServiceDesk, Network, Infra, Database, DevOps, Security, Apps")] string team,
        CancellationToken ct)
    {
        var action = await pendingActions.AddAsync(new NewTicket(subject, description, category, priority, team, "OpsMind Agent"), ct);
        return $"Заявка подготовлена и ожидает подтверждения пользователя (действие {action.Id}). " +
               "Сообщи пользователю, что заявку нужно подтвердить.";
    }
}
