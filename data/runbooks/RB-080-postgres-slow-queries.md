---
id: RB-080
title: Медленные запросы PostgreSQL
category: database
team: Database
updated: 2026-09-10
---
# Медленные запросы PostgreSQL

## Настройки наблюдения
- Включено расширение `pg_stat_statements` на всех кластерах.
- `log_min_duration_statement = 1000` — запросы дольше **1 секунды** пишутся в лог.
- Дашборд «PostgreSQL Overview» в Grafana: https://grafana.corp.local.

## Диагностика
```sql
-- топ запросов по суммарному времени
SELECT queryid, calls, round(total_exec_time) AS total_ms, round(mean_exec_time) AS mean_ms, query
FROM pg_stat_statements ORDER BY total_exec_time DESC LIMIT 10;

-- план конкретного запроса
EXPLAIN (ANALYZE, BUFFERS) <запрос>;
```
Признаки проблем в плане: `Seq Scan` по большой таблице, большая разница между оценкой и фактом строк (устаревшая статистика), `external merge` при сортировке (мало `work_mem`).

## Типовые решения
- Нет подходящего индекса — создавать **только** `CREATE INDEX CONCURRENTLY`, чтобы не блокировать таблицу; на продуктиве — через change request.
- Устаревшая статистика — `ANALYZE <таблица>`.
- Раздувание таблиц — проверить работу autovacuum (`pg_stat_user_tables.last_autovacuum`, `n_dead_tup`).
- Запрос из приложения генерирует N+1 — передать команде приложения с примером из pg_stat_statements.

## Эскалация
Изменение параметров сервера (`shared_buffers`, `work_mem` глобально) выполняет только команда Database.
