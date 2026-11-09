---
id: RB-081
title: Исчерпаны соединения к PostgreSQL
category: database
team: Database
updated: 2026-09-10
---
# Исчерпаны соединения к PostgreSQL

## Симптомы
- Ошибка приложения `FATAL: sorry, too many clients already` или `remaining connection slots are reserved`.
- Таймауты получения соединения из пула.

## Конфигурация
- `max_connections = 300` на продуктивных кластерах.
- Приложения подключаются через **PgBouncer** (порт 6432) в режиме `pool_mode = transaction`, `default_pool_size = 40`.
- Прямое подключение к порту 5432 разрешено только администраторам и сервисам миграций.

## Диагностика
```sql
SELECT count(*) FROM pg_stat_activity;
SELECT usename, application_name, state, count(*)
FROM pg_stat_activity GROUP BY 1,2,3 ORDER BY 4 DESC;
-- зависшие транзакции
SELECT pid, now() - xact_start AS duration, query
FROM pg_stat_activity WHERE state = 'idle in transaction' ORDER BY duration DESC;
```

## Решение
- Сессии в состоянии `idle in transaction` дольше **10 минут** можно завершать: `SELECT pg_terminate_backend(pid);` — с уведомлением владельца приложения.
- Приложение подключается в обход PgBouncer — исправить строку подключения.
- Увеличение `max_connections` требует перезапуска кластера и выполняется только в окно изменений.
