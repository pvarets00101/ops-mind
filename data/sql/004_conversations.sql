-- Диалоги агента. Хранятся отдельно от данных, которые читает SQL-инструмент.
-- Роль opsmind_app пишет только в эти таблицы. Роль агента opsmind_agent их не видит:
-- иначе модель могла бы через query_history прочитать чужие диалоги.

CREATE TABLE IF NOT EXISTS agent_conversations (
    id          text PRIMARY KEY,                    -- sessionId из API
    title       text NOT NULL,                       -- начало первого сообщения пользователя
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS agent_messages (
    id               bigserial PRIMARY KEY,          -- порядок сообщений внутри диалога
    conversation_id  text NOT NULL REFERENCES agent_conversations (id) ON DELETE CASCADE,
    role             text NOT NULL,                  -- user | assistant | tool | system
    text             text NOT NULL,                  -- текст для просмотра и поиска
    content          jsonb NOT NULL,                 -- ChatMessage целиком: вызовы инструментов, их результаты
    created_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_agent_messages_conversation ON agent_messages (conversation_id, id);
CREATE INDEX IF NOT EXISTS ix_agent_conversations_updated ON agent_conversations (updated_at DESC);

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'opsmind_app') THEN
        CREATE ROLE opsmind_app LOGIN PASSWORD 'app_rw';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE opsmind TO opsmind_app;
GRANT USAGE ON SCHEMA public TO opsmind_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON agent_conversations, agent_messages TO opsmind_app;
GRANT USAGE ON SEQUENCE agent_messages_id_seq TO opsmind_app;

REVOKE ALL ON agent_conversations, agent_messages FROM opsmind_agent;
