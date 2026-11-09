# Fine-tuning классификатора тикетов (этап 4)

Задача: тикет → `{"category", "priority", "team"}`. Базовая модель не знает наших категорий и путает срочность с масштабом
(«совещание через час» → P1). Дообучаем маленькую модель на 2400 размеченных тикетах.

## Шаги

1. **Baseline на CPU** (уже сделано, `docs/classifier-eval.md`):
   `dotnet run --project tools/OpsMind.ClassifierEval -- --model qwen3:4b-instruct --mode fewshot --set hard`
2. **Обучение в Kaggle** — `opsmind_classifier_qlora.ipynb` (инструкция в первой ячейке). Бесплатный GPU T4, ~30 минут вместе с экспортом.
3. **Импорт в Ollama и оценка на CPU**:
   `.\py-lab\finetune\import-to-ollama.ps1 -Gguf <путь к скачанному .gguf>`
4. **Интеграция**: классификатор вызывается из `create_ticket` агента вместо оценки приоритета самой LLM-агентом.

## Что сравниваем

| Модель | Промпт | Где работает |
|---|---|---|
| qwen3:4b-instruct | bare / fewshot (правила + 12 примеров) | Ollama, CPU |
| Qwen3-1.7B до обучения | bare | Kaggle GPU (в ноутбуке) |
| Qwen3-1.7B + LoRA | bare | Kaggle GPU и Ollama, CPU |

Ожидание: дообученная 1.7B точнее 4B с few-shot и быстрее — короткий промпт и модель в 2 раза меньше.
Честная проверка — набор `test_hard` (написан вручную), на шаблонном `test` результат будет завышен.

## Если что-то сломалось

API Unsloth/TRL меняется часто. Ошибку из ячейки пришлите целиком — поправим ноутбук.
