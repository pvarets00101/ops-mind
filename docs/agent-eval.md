# Оценка агента

История запусков tools/OpsMind.AgentEval, новые сверху.

## 2026-11-09 18:12 — claude sonnet: эмуляция вызова функций через --json-schema

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 100 % | 100 % | 100 % | 2/2 | 12 с | 1 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 14 с | search_docs({"query":"потерян телефон с аутентификатором MFA, нет резервных кодов, сброс двухфакторной аутентификации"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✓ | ✓ | 14 с | search_docs({"query":"можно ли выкатывать релиз в продуктив в пятницу, правила и окна релизов"}) |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 7 с | query_history({"sql":"SELECT count(*) AS cnt FROM incidents WHERE priority='P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT team, COUNT(*) AS cnt FROM tickets GROUP BY team ORDER BY cnt DESC"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✓ | 6 с | query_history({"sql":"SELECT count(*) FILTER (WHERE sla_breached) AS breached, count(*) AS total FROM tickets WHERE category='vpn'"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✓ | 7 с | query_history({"sql":"SELECT department, COUNT(*) AS cnt FROM tickets WHERE category = 'printing' GROUP BY department ORDER BY cnt DESC LIMIT 10"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 14 с | query_history({"sql":"SELECT id, title, service, priority, started_at, root_cause, resolution, affected_users FROM incidents WHERE category='vpn' OR service ILIKE '%vpn%' OR …) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 7 с | query_history({"sql":"SELECT ROUND(AVG(resolution_minutes),1) AS avg_min, COUNT(resolution_minutes) AS n FROM tickets WHERE priority='P2' AND resolution_minutes IS NOT NULL"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 7 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 9 с | get_ticket({"ticketId":"T-2026-00002"}); query_history({"sql":"SELECT id, requester, department, subject, description FROM tickets WHERE id = 'T-2026-00002'"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 24 с | read_attachments({"ticketId":"T-2026-02535"}); get_ticket({"ticketId":"T-2026-02535"}); search_docs({"query":"под в CrashLoopBackOff OOMKilled нехватка памяти, лимиты памяти, что делать"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 18 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}); search_docs({"query":"1С не обнаружена лицензия, все лицензии заняты неактивными сеансами"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 8 с | create_ticket({"subject":"Не работает 1С у всей бухгалтерии — никто не может войти","description":"У всех сотрудников отдела бухгалтерии не работает 1С, вход в систему невозм…); приоритет P2 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 7 с | create_ticket({"subject":"Сломалась мышь, требуется замена","description":"У сотрудника вышла из строя компьютерная мышь. Просьба выдать новую мышь взамен неисправной.","cate…); приоритет P4 (ожидался P3\|P4) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 4 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✓ | 12 с | search_docs({"query":"Когда выплачивают зарплату в компании"}) |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✓ | 31 с | query_history({"sql":"SELECT count(*) FROM tickets WHERE category='vpn' AND created_at >= '2026-09-01' AND created_at < '2026-10-01'"}); search_docs({"query":"ошибка FortiClient -12 при подключении VPN"}); search_docs({"query":"сброс пароля и разблокировка заблокированной доменной учётной записи"}); search_docs({"query":"ошибка FortiClient -12 неверный пароль учётная запись заблокирована"}); эталон: 26 |

## 2026-11-09 18:09 — claude haiku: эмуляция вызова функций через --json-schema

Сценариев: 17. Модель: qwen3:4b-instruct.

| Выбор инструментов | Ответ верный | Оба верны | Приоритет заявки | Среднее время | Исправлений после проверки |
|---|---|---|---|---|---|
| 100 % | 100 % | 100 % | 2/2 | 15 с | 3 |

| Сценарий | Тип | Инструменты | Ответ | Время | Вызовы и детали |
|---|---|---|---|---|---|
| A01: Как войти, если я потерял телефон с аутентификатором и резервных кодов нет? | docs | ✓ | ✓ | 28 с | search_docs({"query":"потерял телефон с аутентификатором, нет резервных кодов, восстановление доступа MFA"}); search_docs({"query":"утеря телефона с корпоративными данными процедура"}) |
| A02: Можно ли выкатывать релиз в продуктив в пятницу? | docs | ✓ | ✓ | 16 с | search_docs({"query":"выкатка релиза в продуктив в пятницу запрет окна деплоя"}) |
| A03: Сколько P1-инцидентов было в августе 2026? | sql | ✓ | ✓ | 10 с | query_history({"sql":"SELECT COUNT(*) AS p1_incidents_aug_2026 FROM incidents WHERE priority = 'P1' AND started_at >= '2026-08-01' AND started_at < '2026-09-01'"}); эталон: 2 |
| A04: Какая команда получила больше всего тикетов за год? | sql | ✓ | ✓ | 9 с | query_history({"sql":"SELECT team, COUNT(*) AS tickets FROM tickets WHERE created_at >= '2026-01-01' AND created_at < '2027-01-01' GROUP BY team ORDER BY tickets DESC;"}); эталон: ServiceDesk |
| A05: Сколько тикетов по VPN нарушили SLA? | sql | ✓ | ✓ | 7 с | query_history({"sql":"SELECT COUNT(*) AS total_vpn, COUNT(*) FILTER (WHERE sla_breached) AS sla_breached_count FROM tickets WHERE category = 'vpn'"}); эталон: 22 |
| A06: Какой отдел чаще всего обращался по проблемам с печатью? | sql | ✓ | ✓ | 9 с | query_history({"sql":"SELECT department, COUNT(*) AS cnt FROM tickets WHERE category = 'printing' GROUP BY department ORDER BY cnt DESC"}); эталон: Отдел кадров |
| A07: Какие были корневые причины крупных инцидентов с VPN? | sql | ✓ | ✓ | 11 с | query_history({"sql":"SELECT id, title, service, priority, started_at, resolved_at, root_cause, resolution, affected_users FROM incidents WHERE category ILIKE 'vpn' OR servic…) |
| A08: Сколько в среднем минут решались тикеты с приоритетом P2? | sql | ✓ | ✓ | 9 с | query_history({"sql":"SELECT ROUND(AVG(resolution_minutes), 1) AS avg_minutes, COUNT(*) AS cnt FROM tickets WHERE priority = 'P2' AND resolution_minutes IS NOT NULL"}); эталон: 284 |
| A09: Какой статус у тикета T-2026-02994? | ticket | ✓ | ✓ | 10 с | get_ticket({"ticketId":"T-2026-02994"}) |
| A10: Кто автор тикета T-2026-00002 и о чём он? | ticket | ✓ | ✓ | 9 с | get_ticket({"ticketId":"T-2026-00002"}) |
| A11: Почему падает под из тикета T-2026-02535? Посмотри вложения. | attachments | ✓ | ✓ | 40 с | get_ticket({"ticketId":"T-2026-02535"}); read_attachments({"ticketId":"T-2026-02535"}); search_docs({"query":"OOMKilled CrashLoopBackOff под Kubernetes лимит памяти прогрев кэша CACHE_WARMUP_ON_START"}); search_docs({"query":"откат релиза helm rollback Kubernetes CrashLoopBackOff OOMKilled"}) |
| A12: Что за ошибка в тикете T-2026-02994? Посмотри вложения и подскажи решение. | attachments | ✓ | ✓ | 22 с | get_ticket({"ticketId":"T-2026-02994"}); read_attachments({"ticketId":"T-2026-02994"}); search_docs({"query":"1С не обнаружена лицензия, лимит лицензий исчерпан, сеансы без активности, отключение неактивных сеансов RDS"}) |
| A13: Заведи заявку: у всей бухгалтерии не работает 1С, никто не может войти | create | ✓ | ✓ | 11 с | create_ticket({"subject":"1С: не работает вход у всего отдела бухгалтерии","description":"У всех сотрудников бухгалтерии не работает 1С — никто не может войти в систему. Затр…); приоритет P2 (ожидался P2) |
| A14: Создай заявку: у меня сломалась мышка, нужна новая | create | ✓ | ✓ | 10 с | create_ticket({"subject":"Сломалась компьютерная мышь, нужна замена","description":"У сотрудника сломалась компьютерная мышь. Просьба выдать новую мышь взамен неисправной. За…); приоритет P3 (ожидался P3\|P4) |
| A15: Привет! Что ты умеешь? | chat | ✓ | ✓ | 5 с |  |
| A16: Когда в компании выплачивают зарплату? | offtopic | ✓ | ✓ | 21 с | search_docs({"query":"когда выплачивается зарплата, сроки выплаты заработной платы"}); search_docs({"query":"зарплата расчет дата выплаты аванс"}) |
| A17: Сколько тикетов по VPN было в сентябре 2026 и что делать при ошибке FortiClient -12? | multi | ✓ | ✓ | 29 с | query_history({"sql":"SELECT COUNT(*) AS vpn_tickets_sep_2026 FROM tickets WHERE category = 'vpn' AND created_at >= '2026-09-01' AND created_at < '2026-10-01'"}); search_docs({"query":"FortiClient ошибка -12 VPN что делать"}); search_docs({"query":"RB-010 сброс пароля учётной записи доменная учётка блокировка VPN"}); search_docs({"query":"RB-011 разблокировка учётной записи VPN SSLVPN configuration wrong"}); эталон: 26 |

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

