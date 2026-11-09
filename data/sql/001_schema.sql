-- Схема истории обращений и инцидентов OpsMind (для SQL-инструмента агента).

CREATE TABLE IF NOT EXISTS incidents (
    id              text PRIMARY KEY,           -- MI-2026-001
    title           text NOT NULL,
    service         text NOT NULL,              -- затронутый сервис: VPN, Почта, CRM, ...
    category        text NOT NULL,
    priority        text NOT NULL CHECK (priority IN ('P1', 'P2', 'P3', 'P4')),
    team            text NOT NULL,              -- команда-исполнитель
    runbook_id      text NOT NULL,              -- RB-xxx из data/runbooks
    started_at      timestamptz NOT NULL,
    resolved_at     timestamptz NOT NULL,
    root_cause      text NOT NULL,
    resolution      text NOT NULL,
    affected_users  int NOT NULL
);

CREATE TABLE IF NOT EXISTS tickets (
    id                  text PRIMARY KEY,       -- T-2026-00001
    created_at          timestamptz NOT NULL,
    channel             text NOT NULL,          -- portal | email | phone | chat
    department          text NOT NULL,
    requester           text NOT NULL,
    is_vip              boolean NOT NULL,
    subject             text NOT NULL,
    description         text NOT NULL,
    category            text NOT NULL,
    priority            text NOT NULL CHECK (priority IN ('P1', 'P2', 'P3', 'P4')),
    team                text NOT NULL,
    runbook_id          text NOT NULL,
    status              text NOT NULL,          -- new | in_progress | resolved
    resolved_at         timestamptz,
    resolution_minutes  int,
    sla_breached        boolean NOT NULL,
    incident_id         text REFERENCES incidents (id)
);

CREATE INDEX IF NOT EXISTS ix_tickets_created_at ON tickets (created_at);
CREATE INDEX IF NOT EXISTS ix_tickets_category ON tickets (category);
CREATE INDEX IF NOT EXISTS ix_tickets_incident ON tickets (incident_id);

COMMENT ON TABLE incidents IS 'Крупные инциденты (P1/P2), затронувшие много пользователей';
COMMENT ON TABLE tickets IS 'Обращения пользователей в IT-поддержку';
COMMENT ON COLUMN tickets.sla_breached IS 'Время решения превысило SLA приоритета (RB-001)';
