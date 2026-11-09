---
id: RB-093
title: Ingress отдаёт 502 или 504
category: kubernetes
team: DevOps
updated: 2026-09-18
---
# Ingress отдаёт 502 или 504

## Как устроено
- Входящий трафик в кластер принимает **ingress-nginx** (namespace `ingress-nginx`).
- Таймаут ожидания ответа от приложения по умолчанию — `proxy-read-timeout: 60` секунд.
- Максимальный размер тела запроса по умолчанию — 10 МБ (`proxy-body-size`).

## Значение кодов
| Код | Значение |
|---|---|
| 502 Bad Gateway | Нет живых подов за сервисом или под закрыл соединение |
| 503 Service Unavailable | У сервиса нет ни одного endpoint (все поды not ready) |
| 504 Gateway Timeout | Приложение не ответило за `proxy-read-timeout` |
| 413 Request Entity Too Large | Тело запроса больше `proxy-body-size` |

## Диагностика
```bash
kubectl get endpoints <svc> -n <ns>        # есть ли адреса подов
kubectl get pods -n <ns> -l app=<app>      # READY 1/1?
kubectl logs -n ingress-nginx deploy/ingress-nginx-controller | grep <host>
```

## Решение
- Нет endpoints — поды не проходят `readinessProbe`: смотреть логи приложения (RB-090).
- 504 на длинных операциях — увеличить таймаут аннотацией `nginx.ingress.kubernetes.io/proxy-read-timeout`, но больше **300 секунд** не ставить: долгие операции переводить в асинхронный режим.
