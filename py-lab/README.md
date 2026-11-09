# py-lab

Python-эксперименты, которые нельзя или неудобно делать на .NET:

- `rag-llamaindex/` — ingestion и RAG на LlamaIndex (этап 2)
- `agent-langgraph/` — агент OpsMind на LangGraph и CrewAI для сравнения (этап 3)
- `notebooks/` — Kaggle/Colab ноутбуки: vLLM и TGI бенчмарк-сервер, QLoRA fine-tuning (этапы 1, 4)

Все эксперименты используют те же сервисы из `deploy/docker-compose.yml` (Qdrant, Postgres, Ollama).
