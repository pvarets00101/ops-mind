---
id: RB-092
title: Pod в состоянии Pending — не хватает ресурсов
category: kubernetes
team: DevOps
updated: 2026-09-18
---
# Pod в состоянии Pending — не хватает ресурсов

## Диагностика
```bash
kubectl describe pod <pod> -n <ns>                 # Events: FailedScheduling
kubectl describe resourcequota -n <ns>              # квота namespace
kubectl top nodes
kubectl describe node <node> | grep -A5 "Allocated resources"
```

## Причины
| Сообщение | Причина |
|---|---|
| `Insufficient cpu` / `Insufficient memory` | Свободных ресурсов на узлах меньше, чем `requests` пода |
| `exceeded quota` | Исчерпана квота namespace |
| `node(s) didn't match node selector` / taints | Под требует узлы с меткой, которых нет или они заняты |
| `pod has unbound immediate PersistentVolumeClaims` | Не создан том (StorageClass, ёмкость СХД) |

## Квоты
- Квоты namespace по умолчанию: 8 vCPU и 16 ГиБ памяти в `requests`.
- Увеличение квоты — заявка в DevOps с обоснованием, согласование руководителя команды-владельца, срок — 2 рабочих дня.

## Ресурсы кластера
Кластеры развёрнуты на собственном оборудовании, автомасштабирования узлов нет. Добавление узлов планируется ежеквартально; срочное расширение — через руководителя DevOps.

## Хорошая практика
Задавать `requests` по реальному потреблению (смотреть в Grafana за 7 дней), а не «с запасом» — завышенные requests блокируют ресурсы для других.
