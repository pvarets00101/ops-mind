# Датасет OpsMind

Вымышленная компания, вымышленные системы и данные. Любые совпадения случайны.

| Путь | Что внутри | Источник |
|---|---|---|
| `runbooks/*.md` | 51 регламент IT-поддержки (front matter: id, title, category, team) — корпус для RAG | написаны вручную |
| `eval/rag-questions.jsonl` | 102 вопроса с эталонным документом и ответом — оценка поиска и ответа | написаны вручную |
| `tickets/tickets.jsonl` | 3000 обращений с разметкой category / priority / team | `tools/OpsMind.DataGen` |
| `incidents/incidents.jsonl` | 30 крупных инцидентов (P1/P2) с причинами | `tools/OpsMind.DataGen` |
| `finetune/{train,val,test}.jsonl` | Тикеты в chat-формате для SFT, разбиение 80/10/10 | `tools/OpsMind.DataGen` |
| `sql/001_schema.sql`, `sql/002_seed.sql` | Схема и данные для PostgreSQL (SQL-инструмент агента) | схема вручную, данные — генератор |

## Перегенерация

```powershell
dotnet run --project tools/OpsMind.DataGen -- 3000 42   # количество тикетов, seed
```

## Загрузка в PostgreSQL

```powershell
Get-Content -Raw -Encoding utf8 data/sql/001_schema.sql | docker exec -i opsmind-postgres psql -q -U opsmind -d opsmind
Get-Content -Raw -Encoding utf8 data/sql/002_seed.sql   | docker exec -i opsmind-postgres psql -q -U opsmind -d opsmind
Get-Content -Raw -Encoding utf8 data/sql/003_agent_role.sql    | docker exec -i opsmind-postgres psql -q -U opsmind -d opsmind
Get-Content -Raw -Encoding utf8 data/sql/004_conversations.sql | docker exec -i opsmind-postgres psql -q -U opsmind -d opsmind
Get-Content -Raw -Encoding utf8 data/sql/005_agent_actions.sql | docker exec -i opsmind-postgres psql -q -U opsmind -d opsmind
```

- `003` — read-only роль `opsmind_agent` для SQL-инструмента: видит только `tickets` и `incidents`.
- `004` — таблицы диалогов агента и роль приложения `opsmind_app`.
- `005` — действия агента, ожидающие подтверждения.

`opsmind_app` работает только с таблицами `agent_*` и не видит данные поддержки; `opsmind_agent` — наоборот. Скрипты можно запускать повторно.

Postgres снаружи доступен на порту **5433** (`deploy/docker-compose.yml`): 5432 на этой машине занят другой базой.

## Особенности разметки (важно для fine-tuning)

- Приоритет определяется масштабом проблемы по правилам RB-001, а **не** словом «срочно» — в часть тикетов намеренно добавлено «СРОЧНО!!!» без повышения приоритета.
- VIP-заявители (около 5%) получают приоритет не ниже P3.
- Тикеты, привязанные к крупному инциденту, имеют `incident_id`.
- Шаблонов ограниченное число, поэтому формулировки в train и test пересекаются. Для честной оценки на этапе 4 стоит добавить тикеты, переписанные LLM (парафраз), и проверять на них.
