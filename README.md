# OpsMind

On-prem AI-ассистент IT-поддержки: RAG по регламентам, агент с доступом к SQL, REST и S3, классификатор тикетов на LoRA.
Учебный проект по стеку LLM / RAG / агенты / fine-tuning / vLLM на C#.

План и этапы: [docs/plan.md](docs/plan.md)

## Запуск

```powershell
docker compose -f deploy/docker-compose.yml up -d
docker exec opsmind-ollama ollama pull qwen3:4b-instruct
docker exec opsmind-ollama ollama pull bge-m3
dotnet run --project src/OpsMind.Api
```

LLM-провайдер задаётся в секции `Llm` файла `src/OpsMind.Api/appsettings.json`.
Подходит любой OpenAI-совместимый endpoint: Ollama, vLLM, TGI.

## Сервисы

| Сервис | Порт | Запуск |
|---|---|---|
| API (чат, RAG, агент) | 5080 | `dotnet run --project src/OpsMind.Api --urls http://localhost:5080` |
| ITSM (имитация) | 5090 | `dotnet run --project src/OpsMind.TicketService.Mock --urls http://localhost:5090` |
| MCP-сервер | 5100 | `dotnet run --project src/OpsMind.McpServer` |
| Индексация регламентов и вложений | — | `dotnet run --project src/OpsMind.Ingestion` |

## MCP: инструменты OpsMind в Claude Code

1. Запустите docker-compose, ITSM и MCP-сервер (см. выше).
2. Откройте Claude Code в корне репозитория — он найдёт `.mcp.json` и спросит, подключать ли сервер `opsmind`.
3. `/mcp` — статус сервера и список инструментов.
4. Попробуйте:
   - «Сколько тикетов по VPN нарушили SLA?» — Claude вызовет `query_history`;
   - `@opsmind:runbook://RB-011` — подключить регламент в контекст;
   - `/mcp__opsmind__triage_ticket T-2026-02967` — готовый сценарий разбора тикета.
5. Заявки из `create_ticket` подтверждаются вручную: `POST http://localhost:5100/actions/{id}/approve`.

Проверка сервера без Claude Code: `dotnet run --project tools/OpsMind.McpCheck`.
