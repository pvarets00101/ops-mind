-- Действия агента, ожидающие подтверждения человеком (human-in-the-loop).
-- Раньше жили в памяти процесса и терялись при перезапуске — вместе с заявками, которые никто не успел подтвердить.

CREATE TABLE IF NOT EXISTS agent_actions (
    id               text PRIMARY KEY,               -- A-1a2b3c4d; он же ключ идемпотентности для ITSM
    conversation_id  text,                           -- диалог агента; NULL — вызов через MCP без диалога
    kind             text NOT NULL,                  -- create_ticket
    payload          jsonb NOT NULL,                 -- параметры действия (NewTicket)
    status           text NOT NULL DEFAULT 'pending'
                     CHECK (status IN ('pending', 'approving', 'approved', 'rejected')),
    result           text,                           -- «Создан тикет T-2026-…» или текст ошибки
    created_at       timestamptz NOT NULL DEFAULT now(),
    decided_at       timestamptz
);

CREATE INDEX IF NOT EXISTS ix_agent_actions_conversation ON agent_actions (conversation_id, created_at);
CREATE INDEX IF NOT EXISTS ix_agent_actions_pending ON agent_actions (created_at) WHERE status = 'pending';

GRANT SELECT, INSERT, UPDATE ON agent_actions TO opsmind_app;
REVOKE ALL ON agent_actions FROM opsmind_agent;
