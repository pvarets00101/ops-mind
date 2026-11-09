---
id: RB-091
title: Ошибка ImagePullBackOff
category: kubernetes
team: DevOps
updated: 2026-09-18
---
# Ошибка ImagePullBackOff

## Реестр образов
- Внутренний реестр — `registry.corp.local` (Harbor).
- Публичные образы забираются через прокси-кэш `registry.corp.local/dockerhub/...`. Прямой доступ из кластеров к Docker Hub закрыт.
- Образы старше 180 дней без загрузок удаляются политикой очистки, кроме тегов вида `vX.Y.Z`.

## Диагностика
```bash
kubectl describe pod <pod> -n <ns>   # раздел Events: точный текст ошибки
```
| Текст ошибки | Причина |
|---|---|
| `manifest unknown` / `not found` | Нет такого тега (опечатка, образ не собран или удалён политикой очистки) |
| `unauthorized` / `authentication required` | Нет или устарел `imagePullSecrets` в namespace |
| `i/o timeout` / `no such host` | Узел не видит реестр: DNS или сеть |
| `toomanyrequests` | Образ тянется напрямую с Docker Hub, а не через прокси-кэш |

## Решение
- Секрет для реестра создаётся робот-аккаунтом Harbor проекта, имя секрета — `harbor-pull`. Срок действия робот-аккаунта — 1 год.
- Использовать неизменяемые теги (`v1.4.2` или digest), а не `latest`.
