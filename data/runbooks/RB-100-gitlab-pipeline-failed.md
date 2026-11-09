---
id: RB-100
title: Упал пайплайн GitLab CI
category: cicd
team: DevOps
updated: 2026-09-22
---
# Упал пайплайн GitLab CI

## Где смотреть
GitLab: https://gitlab.corp.local → проект → Build → Pipelines → упавшая задача → лог.

## Типовые причины
| Признак в логе | Причина | Решение |
|---|---|---|
| Падают тесты или сборка | Ошибка в коде | Чинит автор коммита, DevOps не привлекается |
| `This job is stuck because ... no active runners` | Нет раннера с нужными тегами | Проверить `tags` в `.gitlab-ci.yml`, см. RB-101 |
| `ERROR: Job failed: execution took longer than 1h0m0s` | Превышен таймаут задачи (по умолчанию **60 минут**) | Оптимизировать или увеличить `timeout` в задаче (максимум 3 часа) |
| `no space left on device` | Переполнен диск раннера | Сообщить DevOps в #devops-help |
| `pull access denied`, `toomanyrequests` | Образ задачи берётся не из `registry.corp.local` | Использовать прокси-кэш (RB-091) |
| Тест иногда проходит, иногда нет | Нестабильный (flaky) тест | Retry — временно; завести задачу на исправление теста |

## Повторный запуск
- Кнопка Retry в задаче. Автоматический `retry: 2` разрешён только для ошибок инфраструктуры (`when: runner_system_failure, stuck_or_timeout_failure`), не для тестов.
- Очистка кэша: Pipelines → «Clear runner caches».

## Когда обращаться в DevOps
Только если проблема в инфраструктуре CI (раннеры, реестр, общие шаблоны `ci-templates`). Канал — #devops-help, срочно — дежурный (доб. 4444).
