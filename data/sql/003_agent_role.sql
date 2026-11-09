-- Отдельная роль для SQL-инструмента агента: только чтение, короткий таймаут.
-- Даже если модель сгенерирует DROP TABLE или обойдёт проверку в коде, база его не выполнит.

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'opsmind_agent') THEN
        CREATE ROLE opsmind_agent LOGIN PASSWORD 'agent_ro';
    END IF;
END
$$;

REVOKE ALL ON ALL TABLES IN SCHEMA public FROM opsmind_agent;
GRANT CONNECT ON DATABASE opsmind TO opsmind_agent;
GRANT USAGE ON SCHEMA public TO opsmind_agent;
GRANT SELECT ON tickets, incidents TO opsmind_agent;

ALTER ROLE opsmind_agent SET default_transaction_read_only = on;
ALTER ROLE opsmind_agent SET statement_timeout = '5s';
