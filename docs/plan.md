# OpsMind — план проекта

**OpsMind** — корпоративный AI-ассистент для внутренней IT-поддержки, который работает в закрытом контуре компании:

- отвечает на вопросы по регламентам и runbook'ам (RAG с цитатами источников);
- ищет похожие инциденты в базе (Text-to-SQL);
- заводит и обновляет тикеты во внешней системе (REST);
- читает вложения: логи и скриншоты (S3-хранилище);
- классифицирует входящие тикеты собственной дообученной моделью (LoRA).

Цель — на практике освоить стек из вакансии: LLM, агенты, RAG, fine-tuning, эмбеддинги, on-prem инференс (vLLM/TGI), LangChain/LlamaIndex/LangGraph, REST/SQL/векторные БД, Docker, CI/CD.

**Вторая часть (этапы 6–11, с 2026-11-09)** — под вакансию «hands-on AI engineer» («production-ready AI products… LangGraph, Langfuse, MCP… evaluation, monitoring, observability… backend, frontend, cloud»). Добавляется то, чего в OpsMind ещё нет:

- Claude как основная модель через подписку (`claude -p`, как в ClaudeTelegramBot), локальные модели остаются запасным вариантом;
- Langfuse: трейсы, версии промптов, датасеты, оценки;
- графовые workflow: Microsoft Agent Framework Workflows на C# и тот же сценарий на LangGraph в py-lab;
- MCP глубже: авторизация, C#-клиент внешних серверов, Claude как агент поверх OpsMind MCP;
- фронтенд (React + TypeScript);
- оценка качества в проде и деплой в облако.

## Ограничения и стратегия

**Железо:** i7-13700K (AVX2, без AVX-512), 32 ГБ RAM, **без GPU**. Отсюда правила:

| Задача | Где выполняется |
|---|---|
| Повседневная разработка, чат, агенты | **Ollama на CPU** (Docker): Qwen3-4B Q4, около 10–20 ток/с |
| Эмбеддинги и реранкер | CPU: Ollama `bge-m3` или TEI CPU-образ |
| Изучение vLLM / TGI, бенчмарки | **Kaggle Notebooks** (бесплатный GPU, около 30 ч/нед) или Colab; API пробрасывается наружу через `cloudflared` |
| Fine-tuning (LoRA/QLoRA) | Kaggle/Colab + Unsloth |
| Сравнение с топовой моделью, LLM-as-judge (опционально) | Claude API по оплате за токены |

Код зависит только от абстракций `IChatClient` / `IEmbeddingGenerator` (Microsoft.Extensions.AI). Ollama, vLLM и TGI отдают OpenAI-совместимый API, поэтому смена провайдера сводится к правке `appsettings.json`.

### Про Claude

- **Подписка Pro/Max не даёт доступа к API.** Она покрывает claude.ai и Claude Code, то есть помощь в разработке этого проекта.
- Чтобы вызывать Claude из своего кода, нужен отдельный аккаунт на console.anthropic.com с предоплаченным балансом (pay-as-you-go).
- Самая дешёвая модель — **Claude Haiku 5.5**: $0.10 за 1M входных и $0.50 за 1M выходных токенов. Нескольких долларов хватит на весь учебный проект.
- Есть официальный C# SDK (NuGet `Anthropic`) с поддержкой `IChatClient`, так что Claude подключается как ещё один провайдер.
- Эмбеддингов у Claude нет, они остаются локальными.
- Claude API — **опция**, а не обязательное условие: весь проект проходится на бесплатных локальных моделях.

### Claude через подписку (`claude -p`) — с этапа 6

Тот же приём, что в ClaudeTelegramBot: OpsMind запускает Claude Code CLI в headless-режиме и платит лимитами подписки Pro, а не API. Обёртка — `ClaudeCliChatClient : IChatClient`, поэтому остальной код не меняется: провайдер выбирается в `appsettings.json` (`Llm:Provider = ClaudeCli | OpenAiCompatible`).

Флаги (проверены на CLI 2.1.318):

| Флаг | Зачем |
|---|---|
| `-p`, промпт через stdin | headless-режим, без проблем с экранированием |
| `--output-format stream-json --verbose` | стриминг токенов и событие `rate_limit_event` с реальной загрузкой лимитов |
| `--system-prompt` | **заменить** системный промпт Claude Code своим: меньше токенов, нет чужих инструкций |
| `--tools ""` | отключить все встроенные инструменты (Bash, Read и т. д.) — для режима «просто модель» обязательно |
| `--json-schema` | structured output (классификатор, LLM-as-judge) |
| `--mcp-config … --strict-mcp-config` | режим «Claude как агент»: только инструменты OpsMind MCP |
| `--model haiku / sonnet / opus` | выбор модели под задачу |
| `--no-session-persistence` | не копить сессии от каждого вызова |

Ограничения, которые надо помнить:

- **Это не API.** Нет `temperature`, `top_p`, `max_tokens`, нет нативного tool calling в твой код: CLI сам гоняет цикл агента. Поэтому два режима: «модель» (инструменты отключены; вызов функций эмулируется через `--json-schema`) и «агент» (Claude вызывает OpsMind MCP сам).
- **`--bare` не подходит**: в этом режиме авторизация только по `ANTHROPIC_API_KEY`, подписка не читается.
- **Лимиты общие с claude.ai и Claude Code.** Прогон 102 вопросов RAG-оценки плюс судья — заметная часть пятичасового окна. Отсюда: `MaxConcurrency = 1`, мини-наборы для частых прогонов, полный — по требованию, и учёт `rate_limit_event`.
- **Только личное использование.** Отдавать подписку другим пользователям или вытаскивать OAuth-токен для прямых вызовов Messages API нельзя (риск бана). В облаке OpsMind на подписке доступен только тебе, за авторизацией. Для публичного демо — провайдер Claude API с лимитом трат в консоли или Ollama.
- На Windows `claude` — npm-шим: запускать `claude.cmd` или полный путь.
- Эмбеддинги и reranker остаются локальными (`bge-m3`, TEI): у Claude их нет.

## Стек

| Область | Технологии |
|---|---|
| Backend | .NET 9 (переход на .NET 10 LTS — отдельная задача), ASP.NET Core Minimal API |
| AI-абстракции | Microsoft.Extensions.AI (`IChatClient`, `IEmbeddingGenerator`, function invocation) |
| Агенты | Microsoft Agent Framework (workflow-граф, мультиагентность), MCP C# SDK |
| Инференс | **Claude через `claude -p` (основной с этапа 6)**, Ollama (CPU, запасной), vLLM и TGI (GPU в Kaggle), опционально Claude API |
| Workflow-графы | Agent Framework Workflows (C#), LangGraph (py-lab) |
| Наблюдаемость и оценка | OpenTelemetry → **Langfuse** (self-hosted), Microsoft.Extensions.AI.Evaluation |
| Frontend | React + TypeScript (Vite), SSE |
| Облако | VPS + Docker Compose + Caddy (HTTPS), GHCR, GitHub Actions |
| Векторная БД | Qdrant (основная), pgvector (для сравнения) |
| SQL | PostgreSQL + EF Core |
| Файлы | S3-совместимое хранилище: SeaweedFS (MinIO перестал публиковать Docker-образы) |
| Python-лаборатория | LlamaIndex, LangChain/LangGraph, CrewAI, Unsloth/PEFT/TRL |
| CI/CD | GitHub Actions, Docker-образы в GHCR, Testcontainers |

## Архитектура

```
[OpsMind.Web (React) / Swagger] → OpsMind.Api
                               ├─ Agent (MS Agent Framework)
                               │    ├─ search_docs ──→ IEmbeddingGenerator → Qdrant (+ reranker)
                               │    ├─ query_incidents → PostgreSQL (read-only роль)
                               │    ├─ create/update_ticket → REST → OpsMind.TicketService.Mock
                               │    └─ read_attachment → S3 (SeaweedFS)
                               ├─ Classifier → base model + LoRA-адаптер
                               └─ IChatClient → ClaudeCli (claude -p) | Ollama | vLLM | TGI | Claude API
OpsMind.Ingestion (Worker): S3 → парсинг → чанкинг → эмбеддинги → Qdrant
py-lab/: LlamaIndex ingestion, LangGraph-агент, fine-tune ноутбуки
OpsMind.McpServer ← Claude Code, claude -p (режим «агент»), LangGraph-агент из py-lab
Все сервисы → OpenTelemetry → Langfuse (трейсы, промпты, датасеты, оценки)
```

## Структура репозитория

```
src/OpsMind.Api                 — HTTP API, агент, эндпоинты чата
src/OpsMind.Core                — доменные модели, LLM-абстракции, RAG, tools
src/OpsMind.Ingestion           — фоновая индексация документов
src/OpsMind.TicketService.Mock  — имитация внешней тикет-системы (REST)
tests/OpsMind.Tests             — unit и интеграционные тесты
deploy/docker-compose.yml       — Postgres, Qdrant, SeaweedFS (S3), Ollama
py-lab/                         — Python-эксперименты и fine-tuning
docs/                           — план, бенчмарки, заметки
```

---

## Этапы

### Этап 0. Фундамент ✅ каркас готов
- [x] Solution, проекты, `.editorconfig`, `.gitignore`
- [x] `IChatClient` / `IEmbeddingGenerator` через OpenAI-совместимый endpoint (`AddOpsMindLlm`)
- [x] `/api/chat` и `/api/chat/stream` (SSE)
- [x] docker-compose: Postgres+pgvector, Qdrant, SeaweedFS (S3), Ollama
- [x] GitHub Actions: build + test
- [x] Запустить compose, скачать модели, проверить `/api/chat` руками (qwen3:4b-instruct на CPU: ~1.5–4 с на короткий ответ; qwen3:4b с режимом размышлений — ~45 с)
- [x] Датасет (см. `data/README.md`): 51 runbook, 102 вопроса для оценки RAG, 3000 тикетов и 30 инцидентов (`tools/OpsMind.DataGen`), загрузка в Postgres
- [ ] Создать репозиторий на GitHub, первый push

**Изучить:** как устроен LLM (токены, контекстное окно, temperature, sampling), чем chat template отличается от сырого промпта.

### Этап 1. LLM-инференс (≈2 недели)
- [ ] Ollama: сравнить Qwen3-1.7B / 4B / 8B на CPU по скорости (ток/с, время до первого токена) и качеству ответов
- [ ] Свой C#-бенчмарк (`tools/OpsMind.Bench`): N параллельных запросов, throughput, TTFT, p50/p95
- [ ] **vLLM в Kaggle**: поднять Qwen3-8B (FP16 и AWQ), открыть API через `cloudflared`, прогнать бенчмарк из C#
  - параметры: `--max-model-len`, `--gpu-memory-utilization`, `--enable-prefix-caching`, `--max-num-seqs`
  - что изучить: PagedAttention, continuous batching, KV-cache, квантизация (AWQ, GPTQ, FP8)
- [ ] **TGI в Kaggle** с той же моделью, тот же бенчмарк
- [ ] Попробовать vLLM CPU-backend локально в Docker на маленькой модели (на AVX2 может не завестись — записать результат)
- [ ] Structured output (JSON schema) и tool calling через `IChatClient`
- [ ] *(опц.)* Провайдер Claude через NuGet `Anthropic` и сравнение качества
- [ ] Итоги — в `docs/benchmarks.md`

### Этап 2. Эмбеддинги и RAG (≈2–3 недели)
- [x] Ingestion worker (v1): папка `data/runbooks` → чанкинг по разделам → эмбеддинги `bge-m3` → Qdrant (191 фрагмент за ~30 с на CPU)
- [ ] Ingestion worker (v2): источник — S3-бакет, парсинг pdf/docx, запуск по событию загрузки
- [x] Чанкинг по заголовкам `##` с заголовком документа и раздела в каждом фрагменте
- [ ] Сравнить с фиксированным размером с перекрытием (512/1024 символа, overlap 15%)
- [ ] Модели эмбеддингов: сравнить `bge-m3` с `multilingual-e5` и др. Изучить размерность, косинусную близость, нормализацию
- [x] Dense-поиск: hit@1 94%, hit@5 100%, MRR 0.968 (история — `docs/rag-eval.md`)
- [x] Hybrid (dense + BM25, свой энкодер `Bm25Encoder`, sparse-векторы Qdrant с IDF, взвешенный RRF): чинит точные токены («169.254», «autodiscover»); при равных весах ломает другие вопросы — вес BM25 0.5
- [ ] Нормальный стеммер для BM25 (Snowball): грубая обрезка до 6 букв не связывает «релизить» и «релизы»
- [x] Reranker `bge-reranker-v2-m3` в TEI (CPU): hit@1 94% → 98%, MRR 0.990; цена — ~4.9 с на запрос и ~7 ГБ RAM. Порог отказа по оценке reranker'а (`RerankMinScore` = 0.02) разделяет вопросы из базы и вне её без ошибок
- [ ] Ускорить reranker: 10 кандидатов вместо 20, ONNX-бэкенд или reranker поменьше; сравнить качество и время
- [x] Промпт: маленькая модель на «указывай [RB-xxx]» иногда отвечала только ссылкой — добавлен пример формата ответа
- [x] Вопросы вне базы (`data/eval/rag-offtopic.jsonl`, 25 шт.) и калибровка порога: по косинусу классы перекрываются (из базы ≥ 0.50, вне базы до 0.545), `MinScore` = 0.48
- [x] Ответ с цитатами источников (`POST /api/ask`), два уровня отказа: порог `MinScore` и инструкция модели
- [x] Метрики поиска: hit@k, MRR (`tools/OpsMind.RagEval`)
- [ ] Метрики ответа: faithfulness, relevance (LLM-as-judge, Microsoft.Extensions.AI.Evaluation)
- [ ] pgvector против Qdrant: скорость, удобство, фильтрация
- [ ] **py-lab:** тот же ingestion на **LlamaIndex** и цепочка на **LangChain**, сравнение метрик с C#-версией

### Этап 3. Агенты (≈3 недели)
- [x] Tools (`src/OpsMind.Core/Agent/OpsMindTools.cs`): `search_docs` (RAG), `query_history` (Text-to-SQL: `SqlGuard` + read-only роль `opsmind_agent` с таймаутом 5 с), `get_ticket` и `create_ticket` (REST + Polly), `read_attachments` (S3)
- [ ] `update_ticket` (смена статуса, комментарий) — тоже через подтверждение
- [x] TicketService.Mock: GET/POST/PATCH, OpenAPI, случайные задержки и 20% ответов 503 — Polly их скрывает (`OnRetry … 503` в логах)
- [x] Агент на Microsoft Agent Framework (`ChatClientAgent`, сессии), `POST /api/agent`
- [x] Проверка ответа: ссылки [RB-xxx] сверяются с результатами инструментов, выдуманные — одна попытка исправиться, затем удаление
- [ ] Жёсткий workflow для «разбора тикета» (вложения → search_docs → ответ): 4B-модель сама не доводит цепочку до поиска регламента
- [x] Human-in-the-loop: `create_ticket` создаёт ожидающее действие, тикет появляется в ITSM только после `POST /api/agent/actions/{id}/approve`
- [x] Оценка агента: 17 сценариев (`data/eval/agent-scenarios.jsonl`, `tools/OpsMind.AgentEval`, история — `docs/agent-eval.md`).
  Путь: 100%/82% (инструменты/ответы) → переименование `query_incidents` → `query_history` и пояснения колонок → 94%/94%
  → temperature 0.1 и «сначала search_docs» → 100%/100% в двух прогонах подряд. Среднее время — 14 с на CPU
- [ ] Расширить сценарии (сейчас 17, и на них же подбирались исправления) и заменить проверку по ключевым словам
  на LLM-as-judge: в одном прогоне неверный ответ «в пятницу можно релизить» прошёл проверку по слову «пятниц»
- [ ] Приоритет новой заявки: модель путает срочность и масштаб — передать классификатору из этапа 4
- [x] История диалогов в Postgres: `PostgresChatHistoryProvider` (точка расширения Agent Framework) + `ConversationStore`,
  таблицы `agent_conversations` / `agent_messages` (`data/sql/004_conversations.sql`, роль `opsmind_app`), окно последних 30 сообщений
  с началом на реплике пользователя; API `GET/DELETE /api/agent/conversations[/{id}]`
  Ловушка: jsonb переставляет ключи, и `$type` перестаёт быть первым — нужен `AllowOutOfOrderMetadataProperties`.
  Служебные реплики проверки ссылок помечены `AuthorName = "verifier"` (в API — `internal: true`)
- [x] Ожидающие действия в Postgres (`agent_actions`, `data/sql/005_agent_actions.sql`): переживают перезапуск;
  подтверждение атомарно (`UPDATE … WHERE status = 'pending'`) — двойной approve даёт один тикет, второй запрос получает 404;
  при недоступной ITSM действие возвращается в pending, API отвечает 502
- [ ] Суммаризация длинного контекста вместо простого обрезания окна
- [ ] Мультиагентный сценарий: «Аналитик» + «Исполнитель» + «Ревьюер»
- [x] MCP-сервер `src/OpsMind.McpServer` (Streamable HTTP, `http://localhost:5100/mcp`): те же 5 инструментов с аннотациями
  readOnly/destructive, 51 регламент как ресурсы `runbook://RB-xxx`, промпт `triage_ticket`; подтверждение заявок — `POST /actions/{id}/approve`.
  Проверка по протоколу — `tools/OpsMind.McpCheck`, подключение к Claude Code — `.mcp.json`
- [ ] Авторизация MCP-сервера (OAuth / токен): сейчас любой, кто достучится до порта, получает доступ к данным
- [ ] **py-lab:** тот же агент на **LangGraph** и попытка на **CrewAI** или **AutoGen**; сравнение подходов в `docs/agents-comparison.md`
- [ ] Учесть, что маленькие модели на CPU плохо вызывают инструменты: проверить, с какого размера модели агент работает стабильно

### Этап 4. Fine-tuning (≈2 недели)
- [ ] Задача: классификация тикета → JSON `{category, priority, team}`
- [x] Baseline qwen3:4b-instruct (`tools/OpsMind.ClassifierEval`, `docs/classifier-eval.md`), «все три поля верны»:
  bare — 3% (test) / 7% (hard), fewshot (правила RB-001 + 12 примеров) — 63% / 52%.
  Без правил модель ставит P1/P2 почти всему: из 52 тикетов P3/P4 в hard-наборе ни одного верного
- [x] SFT-датасет в chat-формате: train 2400 / val 300 / test 300 (шаблонный) + `test_hard.jsonl` — 60 тикетов, написанных вручную
- [ ] QLoRA на Qwen3-1.7B в Kaggle через Unsloth: ноутбук готов (`py-lab/finetune/opsmind_classifier_qlora.ipynb`), запуск — за пользователем
- [ ] Изучить параметры LoRA: rank, alpha, target modules, learning rate, эпохи; разобраться с переобучением
- [ ] Отдача модели:
  - локально: merge → GGUF → Ollama, вызов из C#
  - Kaggle: vLLM `--enable-lora` (multi-LoRA поверх одной базы)
- [ ] Импорт GGUF в Ollama (`py-lab/finetune/import-to-ollama.ps1`) и сравнение с baseline на CPU
- [ ] Классификатор в `create_ticket` агента вместо приоритета «на глаз» LLM-агента

### Этап 5. Прод-готовность и CI/CD (≈2 недели)
- [ ] Dockerfile для каждого сервиса, всё поднимается одним `docker compose up`
- [ ] OpenTelemetry: трейсы шагов агента, токены, латентность, вызовы tools → перенесено в этап 7 (Langfuse)
- [ ] Guardrails: prompt injection в документах, лимиты токенов, защита SQL
- [ ] Интеграционные тесты на Testcontainers (Postgres, Qdrant, SeaweedFS)
- [ ] CI: сборка образов и push в GHCR; RAG-оценка на мини-наборе как quality gate
- [ ] README: архитектура, демо-сценарий, ссылки на бенчмарки и сравнения

---

## Вторая часть: AI-продукт в проде (этапы 6–11)

**Порядок:** 6 → 7 → 8 → 9 → 10 → 11. Этап 6 идёт первым, потому что после него все оценки пересчитываются на Claude. Этап 7 — вторым, потому что без трейсов дальше всё делается вслепую. Пункты этапа 5 (Dockerfile, Testcontainers, guardrails) закрываются по ходу, к этапу 11.

В каждом этапе: **Изучить** (разобрать до кода, коротко записать в `docs/concepts.md`), список задач и **Проверь себя** — вопросы с собеседований. Пока не можешь ответить без подсказки, этап не закрыт.

### Этап 6. Claude как основная модель (≈1 неделя)

**Изучить:** чем headless Claude Code отличается от Messages API; формат событий `stream-json`; как работает structured output; как устроен tool calling «под капотом» (модель возвращает JSON с именем функции и аргументами — это можно эмулировать схемой); семейство моделей Haiku / Sonnet / Opus и когда какую брать.

- [ ] `ClaudeCliChatClient : IChatClient` в `OpsMind.Core/Llm`: сообщения → промпт в stdin, `--system-prompt`, `--tools ""`, `--no-session-persistence`, разбор `stream-json` в `ChatResponseUpdate` (стриминг для `/api/chat/stream`)
- [ ] `ChatOptions.ResponseFormat` (JSON-схема) → `--json-schema`; `ChatOptions.ModelId` → `--model`
- [ ] Эмуляция tool calling: инструменты из `ChatOptions.Tools` описываются в промпте, ответ по схеме `{tool_calls:[…]} | {answer}` → `FunctionCallContent`. Тогда `FunctionInvokingChatClient` и `ChatClientAgent` работают с Claude без изменений
- [ ] Учёт лимитов: `rate_limit_event` → лог и метрика; `MaxConcurrency = 1` (семафор); таймаут и понятная ошибка при исчерпании лимита
- [ ] Запасной провайдер: при ошибке или исчерпании лимита — Ollama (декоратор `FallbackChatClient`)
- [ ] Unit-тесты разбора `stream-json` на записанных фикстурах (без вызова CLI)
- [ ] Перезапустить оценки на Claude (haiku и sonnet) и сравнить с qwen3:4b: RAG-ответы, агент (17 сценариев), классификатор (fewshot) → `docs/model-selection.md`: качество, латентность, расход лимита
- [ ] Измерить накладные расходы CLI: время запуска процесса, лишние токены на вызов

**Проверь себя**
- Почему у `claude -p` нет `temperature` и что это значит для воспроизводимости оценок?
- Как эмулировать tool calling у модели, которая его не поддерживает? Чем это хуже нативного?
- Когда брать Haiku, когда Sonnet? Как ты это обосновал цифрами?
- Почему эту схему нельзя использовать для продукта с чужими пользователями?

### Этап 7. Langfuse: наблюдаемость, промпты, датасеты (≈1,5 недели)

**Изучить:** модель данных Langfuse (trace → observation: span, generation, event; session, user, score); семантические соглашения OpenTelemetry для GenAI (`gen_ai.*`); prompt management (версии, метки `production`/`staging`, кэширование на клиенте); datasets и experiments.

- [ ] Langfuse v3 в `deploy/docker-compose.yml` (web, worker, ClickHouse, Redis, MinIO-совместимое хранилище — проверить, какое S3 он принимает; можно переиспользовать SeaweedFS)
- [ ] OpenTelemetry в Api, McpServer, Ingestion: `UseOpenTelemetry()` у `IChatClient`, трейсы Agent Framework, экспорт OTLP в Langfuse (`/api/public/otel`, Basic-авторизация ключами проекта)
- [ ] Проверить, что в трейсе видны: промпт, ответ, модель, токены, латентность, вызовы tools, найденные фрагменты RAG; `session.id` = сессия агента
- [ ] Цена: для `claude -p` — из события `result` (`total_cost_usd`, условная, т. к. подписка), для локальных — 0; записать в генерацию
- [ ] Промпты (RAG-ответ, агент, классификатор, судья) — в Langfuse prompt management, C#-клиент через REST API с кэшем; откат на версию из файла, если Langfuse недоступен
- [ ] Датасеты в Langfuse из `data/eval/*.jsonl`; `RagEval` / `AgentEval` пишут результаты как experiment runs — сравнение версий промптов и моделей в UI
- [ ] Перевести проверку агента по ключевым словам на LLM-as-judge (Claude sonnet + `--json-schema`), оценки — scores в Langfuse

**Проверь себя**
- Чем трейс LLM-приложения отличается от обычного APM-трейса?
- Как откатить промпт в проде без деплоя? Чем это опасно?
- Как сравнить две версии промпта, чтобы разница не оказалась шумом?

### Этап 8. Графовые workflow: Agent Framework и LangGraph (≈2 недели)

LangGraph существует только для Python и JS. На C# тот же подход дают Workflows из Microsoft Agent Framework: исполнители, рёбра, условные переходы, чекпоинты, запросы к человеку. Делаем один сценарий на обоих и сравниваем.

**Изучить:** workflow против свободного агента (где детерминированный граф надёжнее); `StateGraph`, узлы, условные рёбра, reducers, checkpointer, `interrupt()` в LangGraph; executors, edges, `RequestPort`/human-in-the-loop, checkpointing в Agent Framework; паттерны router, evaluator–optimizer, параллельные ветки.

- [ ] Сценарий «разбор тикета» (закрывает пункт этапа 3): тикет → классификатор (категория, приоритет) → вложения из S3 → `search_docs` → `query_history` (похожие инциденты) → черновик ответа ⇄ ревьюер (до 2 итераций) → **одобрение человеком** → `update_ticket`
- [ ] Реализация на Agent Framework Workflows (C#), чекпоинты в Postgres, продолжение после перезапуска процесса во время ожидания одобрения
- [ ] `update_ticket` (статус, комментарий) через то же подтверждение
- [ ] **py-lab:** тот же граф на LangGraph (Python, `uv`), инструменты — из OpsMind MCP через `langchain-mcp-adapters`, checkpointer на Postgres, трейсы в тот же Langfuse
- [ ] Модель в LangGraph: Claude через подписку тем же способом (`claude -p` из Python) или Ollama
- [ ] Оценка траектории: правильный порядок узлов, число итераций ревьюера, доля одобренных без правок
- [ ] `docs/agents-comparison.md`: свободный ReAct-агент vs граф на C# vs LangGraph — надёжность, объём кода, отладка, наблюдаемость
- [ ] Чекпоинты workflow — в той же базе, рядом с `agent_messages` (история диалогов уже в Postgres, этап 3)

**Проверь себя**
- Зачем checkpointer, если историю можно хранить списком сообщений?
- Как ограничить цикл «черновик ⇄ ревьюер»?
- Что даёт LangGraph по сравнению с Agent Framework, и наоборот?

### Этап 9. MCP глубже (≈1 неделя)

**Изучить:** авторизация в спецификации MCP (OAuth 2.1, protected resource metadata); риски: tool poisoning, prompt injection через описания и результаты инструментов, «confused deputy»; elicitation и sampling.

- [ ] Авторизация `OpsMind.McpServer` (закрывает пункт этапа 3): сначала bearer-токен, затем OAuth
- [ ] Режим «Claude как агент»: `claude -p --mcp-config <opsmind> --strict-mcp-config --tools ""` — Claude сам вызывает инструменты OpsMind. Прогнать 17 сценариев AgentEval и сравнить со своим агентом
- [ ] C#-клиент MCP: агент OpsMind подключает внешний MCP-сервер (например, GitHub или fetch) через `McpClient` → `AIFunction`; разрешённый список инструментов
- [ ] Подтверждение опасных действий через elicitation вместо отдельного HTTP-эндпоинта (если клиент поддерживает)
- [ ] Тест на prompt injection: runbook и вложение с внедрёнными инструкциями — агент не выполняет их и не вызывает destructive-инструменты

**Проверь себя**
- Чем MCP-сервер отличается от REST API с OpenAPI?
- Какие риски у подключения стороннего MCP-сервера?
- Где проходит граница доверия в твоём агенте?

### Этап 10. Фронтенд (≈2 недели)

React + TypeScript (Vite), отдельный проект `src/OpsMind.Web`. Next.js не нужен: backend уже есть на ASP.NET Core.

**Изучить:** основы React (компоненты, состояние, эффекты), TypeScript, чтение SSE из браузера (`fetch` + `ReadableStream`); UX LLM-продукта: прогресс по шагам, частичные ответы, отмена, повтор, понятные ошибки.

- [ ] API: SSE-поток событий агента и workflow (шаг начат / инструмент вызван / токены ответа / ожидание одобрения / готово)
- [ ] Чат с ассистентом: стриминг ответа, цитаты [RB-xxx] открывают регламент
- [ ] Экран «разбор тикета»: шаги графа в реальном времени, черновик, кнопки «одобрить / править / отклонить»
- [ ] Список ожидающих действий агента (create/update ticket)
- [ ] 👍/👎 и комментарий к ответу → score в Langfuse, привязанный к трейсу
- [ ] Авторизация (OpsMind на подписке — только для тебя): cookie/JWT в Api, CORS

**Проверь себя**
- Почему SSE, а не WebSocket?
- Что увидит пользователь, если модель упала на третьем шаге из пяти?

### Этап 11. Оценка в проде и облако (≈2 недели)

**Изучить:** офлайн- и онлайн-оценки; калибровка LLM-as-judge по ручной разметке (accuracy, Cohen's kappa); метрики продукта (p50/p95 латентности, доля 👎, доля ответов-отказов, расход лимита); дрейф после смены модели. Деплой: multi-stage Dockerfile для .NET, Caddy и HTTPS, секреты, миграции, откат.

Оценка
- [ ] Разметить вручную 30 ответов и сверить с судьёй; судье верить только после согласия ≥ 80%
- [ ] Quality gate в CI: изменения промптов, графа или моделей → мини-набор RAG + агент; падение ниже порога валит сборку. На CI — Ollama или записанные ответы (подписку в CI не использовать)
- [ ] Онлайн-оценка: судья по выборке продовых трейсов в Langfuse (раз в день, в пределах лимита)
- [ ] Дашборд в Langfuse: латентность, токены, ошибки, оценки, 👎 по версиям промптов
- [ ] Цикл улучшения: трейс с 👎 → в датасет → исправление → цифры до/после в `docs/evaluation.md`

Облако
- [ ] Dockerfile для Api, McpServer, Ingestion, TicketService.Mock, Web (закрывает пункт этапа 5)
- [ ] VPS (8–16 ГБ RAM: Postgres, Qdrant, Langfuse, bge-m3; reranker — урезанный или ONNX) + Docker Compose + Caddy
- [ ] Claude на сервере: `claude setup-token` → `CLAUDE_CODE_OAUTH_TOKEN` в секретах; доступ к OpsMind только у тебя
- [ ] GitHub Actions: тесты → оценки → образы в GHCR → деплой по тегу; откат на предыдущий тег
- [ ] Бэкапы Postgres, healthchecks, алерт при падении сервиса или исчерпании лимита

**Проверь себя**
- Как узнать, что после обновления модели продукт стал хуже?
- Как оценить агента, у которого нет одного правильного ответа?
- Как откатить релиз вместе с версией промпта?

---

## Соответствие требованиям вакансии

| Требование | Где закрывается |
|---|---|
| Архитектура LLM, агенты, RAG, fine-tuning, эмбеддинги | Этапы 1–4 |
| vLLM / TGI, оптимизация инференса, on-prem | Этап 1, этап 4 (multi-LoRA) |
| LangChain, LlamaIndex, LangGraph, CrewAI, AutoGen | py-lab на этапах 2–3; на C# — Agent Framework |
| REST API, SQL, векторные БД, файловые хранилища | Этапы 2–3 |
| Git, Docker, CI/CD | Этапы 0 и 5 |

Вакансия «hands-on AI engineer» (вторая часть):

| Требование | Где закрывается |
|---|---|
| Model selection | Этап 6: Claude haiku/sonnet против qwen3, `docs/model-selection.md` |
| Prompt engineering, prompt orchestration | Этапы 6–7: эмуляция tool calling, промпты в Langfuse |
| RAG, agentic workflows | Этапы 2–3, этап 8 |
| LangGraph | Этап 8: py-lab; на C# — Agent Framework Workflows |
| Langfuse | Этап 7 и дальше |
| MCP | Этап 3 (сервер), этап 9 (авторизация, клиент, Claude как агент) |
| Evaluation, monitoring, observability | Этапы 7 и 11 |
| Backend, frontend, cloud | Этапы 10–11 |
| Claude Code как основной инструмент | вся разработка; свой MCP подключён к Claude Code |

## Быстрый старт

```powershell
docker compose -f deploy/docker-compose.yml up -d
docker exec opsmind-ollama ollama pull qwen3:4b-instruct
docker exec opsmind-ollama ollama pull bge-m3
dotnet run --project src/OpsMind.Api
# POST http://localhost:<port>/api/chat  {"message": "Привет"}
```
