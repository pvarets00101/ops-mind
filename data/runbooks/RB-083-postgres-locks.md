---
id: RB-083
title: Блокировки и deadlock в PostgreSQL
category: database
team: Database
updated: 2026-09-10
---
# Блокировки и deadlock в PostgreSQL

## Симптомы
- Запросы «висят», растёт количество активных сессий.
- В логе `ERROR: deadlock detected`.

## Кто кого блокирует
```sql
SELECT pid, pg_blocking_pids(pid) AS blocked_by, now() - query_start AS waiting, query
FROM pg_stat_activity
WHERE cardinality(pg_blocking_pids(pid)) > 0;
```
Корневой блокирующий процесс — тот, который сам ни кем не заблокирован. Часто это `idle in transaction` или длительная миграция (`ALTER TABLE`).

## Решение
1. Сначала попробовать мягко отменить запрос: `SELECT pg_cancel_backend(<pid>);`.
2. Если не помогло — `SELECT pg_terminate_backend(<pid>);` (транзакция будет откатана). На продуктиве — с согласия владельца приложения.
3. Для миграций использовать `SET lock_timeout = '5s'`, чтобы `ALTER TABLE` не выстраивал очередь из блокировок.

## Deadlock
PostgreSQL сам разрешает deadlock через `deadlock_timeout = 1s`, откатывая одну из транзакций. Регулярные deadlock — ошибка в приложении (разный порядок обновления строк): передать разработчикам запросы из лога.
