# Оценка агента

История запусков tools/OpsMind.AgentEval, новые сверху.

## 2026-11-09 17:03 — история диалогов и действия в Postgres

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 100 % | 100 % | 100 % | 1/2 | 14 с | 4 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 26 с | search_docs({"query":"как войти в систему если потерял телефон с аутентификатором и нет резервных кодов"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✓ | ✓ | 22 с | search_docs({"query":"можно ли выкатывать релиз в пятницу, правила по выкатке релизов, рабочие дни"}) |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE priority = 'P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT team, COUNT(*) as count FROM tickets GROUP BY team ORDER BY count DESC LIMIT 1"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND sla_breached = true"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT department, COUNT(*) as count FROM tickets WHERE category = 'printing' GROUP BY department ORDER BY count DESC"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 12 с | query_history({"sql":"SELECT i.root_cause FROM incidents i WHERE i.service ILIKE '%vpn%' AND i.priority IN ('P1', 'P2')"}) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 5 с | query_history({"sql":"SELECT AVG(resolution_minutes) AS avg_resolution_minutes FROM tickets WHERE priority = 'P2'"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 6 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 8 с | get_ticket({"ticketId":"T-2026-00002"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 25 с | read_attachments({"ticketId":"T-2026-02535"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 46 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 8 с | create_ticket({"category":"access","description":"У всей бухгалтерии не работает 1С, никто не может войти в систему.","priority":"P1","subject":"Не работает 1С для всей бухга…); приоритет P1 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 9 с | create_ticket({"category":"workstation","description":"У меня сломалась мышка, нужна новая. Проблема не затрагивает сервисы или работу, только персональное устройство.","prio…); приоритет P4 (ожидался P3\|P4) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 7 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✓ | 3 с |  |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✓ | 35 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND created_at >= '2026-09-01' AND created_at < '2026-10-01'"}); search_docs({"query":"что делать при ошибке FortiClient -12"}); эталон: 26 |

## 2026-10-16 16:41 — temperature 0.1, «сначала search_docs», прогон 2 из 2

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 100 % | 100 % | 100 % | 1/2 | 14 с | 6 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 24 с | search_docs({"query":"как войти в систему если потерял телефон с аутентификатором и нет резервных кодов"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✓ | ✓ | 21 с | search_docs({"query":"можно ли выкатывать релиз в пятницу, правила по выкатке релизов, рабочие дни"}) |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE priority = 'P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT team, COUNT(*) as count FROM tickets GROUP BY team ORDER BY count DESC LIMIT 1"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND sla_breached = true"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT department, COUNT(*) as count FROM tickets WHERE category = 'printing' GROUP BY department ORDER BY count DESC"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 12 с | query_history({"sql":"SELECT i.root_cause FROM incidents i WHERE i.service ILIKE '%vpn%' AND i.priority IN ('P1', 'P2')"}) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 5 с | query_history({"sql":"SELECT AVG(resolution_minutes) AS avg_resolution_minutes FROM tickets WHERE priority = 'P2'"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 6 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 8 с | get_ticket({"ticketId":"T-2026-00002"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 42 с | read_attachments({"ticketId":"T-2026-02535"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 44 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 8 с | create_ticket({"category":"access","description":"У всей бухгалтерии не работает 1С, никто не может войти в систему.","priority":"P1","subject":"Не работает 1С для всей бухга…); приоритет P1 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 8 с | create_ticket({"category":"workstation","description":"У меня сломалась мышка, нужна новая. Проблема не влияет на работу сервисов или оборудование.","priority":"P4","subject"…); приоритет P4 (ожидался P3\|P4) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 6 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✓ | 3 с |  |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✓ | 38 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND created_at >= '2026-09-01' AND created_at < '2026-10-01'"}); search_docs({"query":"что делать при ошибке FortiClient -12"}); эталон: 26 |

## 2026-10-16 16:37 — temperature 0.1, «сначала search_docs», прогон 1 из 2

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 100 % | 100 % | 100 % | 1/2 | 14 с | 3 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 41 с | search_docs({"query":"как войти в систему если потерял телефон с аутентификатором и нет резервных кодов"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✓ | ✓ | 19 с | search_docs({"query":"можно ли выкатывать релиз в пятницу, правила по выкатке релизов, рабочие дни"}) |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE priority = 'P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT team, COUNT(*) as count FROM tickets GROUP BY team ORDER BY count DESC LIMIT 1"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND sla_breached = true"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT department, COUNT(*) as count FROM tickets WHERE category = 'printing' GROUP BY department ORDER BY count DESC"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 12 с | query_history({"sql":"SELECT i.root_cause FROM incidents i WHERE i.service ILIKE '%vpn%' AND i.priority IN ('P1', 'P2')"}) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 5 с | query_history({"sql":"SELECT AVG(resolution_minutes) AS avg_resolution_minutes FROM tickets WHERE priority = 'P2'"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 5 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 9 с | get_ticket({"ticketId":"T-2026-00002"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 24 с | read_attachments({"ticketId":"T-2026-02535"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 50 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 8 с | create_ticket({"category":"access","description":"У всей бухгалтерии не работает 1С, никто не может войти в систему.","priority":"P1","subject":"Не работает 1С для всей бухга…); приоритет P1 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 9 с | create_ticket({"category":"workstation","description":"У меня сломалась мышка, нужна новая. Проблема не влияет на работу сервисов или оборудование.","priority":"P4","subject"…); приоритет P4 (ожидался P3\|P4) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 6 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✓ | 3 с |  |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✓ | 31 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND created_at >= '2026-09-01' AND created_at < '2026-10-01'"}); search_docs({"query":"что делать при ошибке FortiClient -12"}); эталон: 26 |

## 2026-10-16 16:33 — инструмент query_history, пояснения department/team в схеме

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 94 % | 94 % | 88 % | 1/2 | 14 с | 5 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 40 с | search_docs({"query":"как войти в систему, если потерял телефон с аутентификатором и нет резервных кодов"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✗ | ✓ | 17 с |  |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE priority = 'P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT team, COUNT(*) as count FROM tickets GROUP BY team ORDER BY count DESC LIMIT 1"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✓ | 4 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND sla_breached = true"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT department, COUNT(*) as count FROM tickets WHERE category = 'printing' GROUP BY department ORDER BY count DESC"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 17 с | query_history({"sql":"SELECT i.root_cause FROM incidents i WHERE i.service ILIKE '%vpn%' AND i.priority IN ('P1', 'P2')"}) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 5 с | query_history({"sql":"SELECT AVG(resolution_minutes) AS avg_resolution_minutes FROM tickets WHERE priority = 'P2'"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 5 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 9 с | get_ticket({"ticketId":"T-2026-00002"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 28 с | read_attachments({"ticketId":"T-2026-02535"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 47 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 7 с | create_ticket({"category":"business_app","description":"У всей бухгалтерии не работает 1С, никто не может войти в систему.","priority":"P1","subject":"Не работает 1С для всей…); приоритет P1 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 8 с | create_ticket({"category":"workstation","description":"У меня сломалась мышка, нужна новая. Время на обмен или замену не указано, но предполагается быстрое решение.","priorit…); приоритет P4 (ожидался P3\|P4) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 8 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✓ | 3 с |  |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✗ | 30 с | query_history({"sql":"SELECT COUNT(*) AS count FROM tickets WHERE category = 'vpn' AND created_at >= '2026-09-01' AND created_at < '2026-10-01'"}); search_docs({"query":"что делать при ошибке FortiClient -12"}); эталон: 26 |

Ответы в неудачных сценариях:

- **A02**: Пятница может быть использована для выката релиза в продуктив, если это не нарушает регламенты по стабильности сервисов. Релизы в продуктив допускаются в рабочие дни с 10:00 до 18:00, включая пятницу, при условии, что релиз не затрагивает критически важные сервисы и не увеличивает риски сбоев.    Перед выкатом рекомендуется провести тестирование в стабильной среде и согласование с командой DevOps.
- **A17**: При ошибке FortiClient -12 («Credential or SSLVPN configuration is wrong») нужно: - проверить правильность пароля и состояние учётной записи (заблокирована ли она).  Решение описано в регламенте [RB-030].

## 2026-10-16 16:28 — qwen3:4b-instruct (CPU), 5 инструментов, проверка ссылок

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 100 % | 82 % | 82 % | 0/2 | 15 с | 2 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 23 с | search_docs({"query":"как войти в систему если потерял телефон с аутентификатором и нет резервных кодов"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✓ | ✓ | 18 с | search_docs({"query":"правила выката релиза в продуктив, дни недели, запреты"}) |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 6 с | query_incidents({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE priority = 'P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 5 с | query_incidents({"sql":"SELECT team, COUNT(*) as count FROM tickets GROUP BY team ORDER BY count DESC"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✗ | 9 с | query_incidents({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE category ILIKE '%vpn%' AND sla_breached IS TRUE"}); query_incidents({"sql":"SELECT COUNT(*) AS count FROM incidents WHERE category ILIKE '%vpn%' AND sla_breached = true"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✗ | 4 с | query_incidents({"sql":"SELECT team, COUNT(*) as count FROM tickets WHERE category ILIKE '%printing%' GROUP BY team ORDER BY count DESC"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 38 с | query_incidents({"sql":"SELECT i.root_cause, i.service, i.affected_users, i.priority FROM incidents i JOIN tickets t ON i.id = t.incident_id WHERE i.category ILIKE '%vpn%' AND …) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 5 с | query_incidents({"sql":"SELECT AVG(resolution_minutes) AS avg_resolution_minutes FROM tickets WHERE priority = 'P2' AND status = 'resolved'"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 6 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 9 с | get_ticket({"ticketId":"T-2026-00002"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 25 с | read_attachments({"ticketId":"T-2026-02535"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 40 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 7 с | create_ticket({"category":"access","description":"У всей бухгалтерии не работает 1С, никто не может войти в систему.","priority":"P1","subject":"Не работает 1С для всей бухга…); приоритет P1 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 8 с | create_ticket({"category":"workstation","description":"У меня сломалась мышка, нужна новая. Проблема не затрагивает сервисы или других пользователей.","priority":"P4","subjec…); приоритет P4 (ожидался P3) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 14 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✗ | 2 с |  |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✓ | 29 с | query_incidents({"sql":"SELECT COUNT(*) as count, category FROM tickets WHERE category = 'vpn' AND EXTRACT(MONTH FROM created_at) = 9 GROUP BY category"}); search_docs({"query":"что делать при ошибке FortiClient -12"}); эталон: 26 |

Ответы в неудачных сценариях:

- **A05**: В таблице `incidents` отсутствует столбец `sla_breached`. Вероятно, в данных нет информации о нарушении SLA для инцидентов по VPN. Просьба уточнить, возможно, имеется в виду другой параметр или источник данных.
- **A06**: Чаще всего по проблемам с печатью обращался отдел ServiceDesk — 205 раз.
- **A16**: Запрос о графике выплаты зарплаты не относится к области IT-поддержки. Просмотрите внутренние HR-документы или спросите в HR-отделе.

