# Оценка классификатора тикетов

История запусков tools/OpsMind.ClassifierEval, новые сверху.

## 2026-10-16 17:08 — qwen3:4b-instruct, fewshot, набор test (150)

| Валидный JSON | category | priority | team | Все три | Macro-F1 (priority) | Время на тикет |
|---|---|---|---|---|---|---|
| 100 % | 92 % | 67 % | 89 % | 63 % | 0.604 | 1.4 с |

Матрица ошибок priority (строки — эталон, столбцы — ответ модели):

| | P1 | P2 | P3 | P4 | иное |
|---|---|---|---|---|---|
| **P1** | 3 | 1 | 0 | 0 | 0 |
| **P2** | 1 | 8 | 6 | 0 | 0 |
| **P3** | 1 | 17 | 72 | 13 | 0 |
| **P4** | 0 | 1 | 9 | 18 | 0 |

Примеры ошибок (эталон → ответ):

- T-2026-01701: «Гостевой Wi-Fi для мероприятия — Планируем мероприятие на 50 человек 25 февраля, нужна отд…» network/P3/Network → network/P4/Network
- T-2026-01440: «Блокировка учётной записи — Добрый день! С утра уже третий раз блокируется учётная запись …» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-00239: «Наши письма попадают в спам — Письма на адреса ООО «Альфа-Трейд» возвращаются с ошибкой 5.…» email/P3/Infra → email/P2/Infra
- T-2026-00800: «Заблокирован сайт — Здравствуйте. Не открывается docs.supplier-portal.ru, пишет «Доступ за…» network/P4/Security → network/P3/Network
- T-2026-00112: «Наши письма попадают в спам — Отправил клиенту письмо с вложением 40 МБ, пришёл отказ дост…» email/P3/Infra → email/P2/Infra
- T-2026-02988: «VPN медленно работает — Доброе утро. Через VPN всё очень медленно открывается, файлы с сет…» vpn/P3/Network → vpn/P4/Network
- T-2026-02454: «Слабый Wi-Fi в переговорной — Здравствуйте. После смены пароля телефон не подключается к с…» network/P3/Network → network/P4/Network
- T-2026-02880: «Почта не работает у всех — Доброе утро. Почта не отправляется и не принимается во всей ком…» email/P1/Infra → email/P1/ServiceDesk
- T-2026-01262: «Не могу пройти MFA — Здравствуйте. Потерял телефон, не могу войти в VPN и почту — требует …» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-02226: «Утёк токен в репозиторий — Помогите, пожалуйста. Случайно закоммитил токен доступа к billi…» cicd/P2/Security → kubernetes/P2/Security
- T-2026-00669: «Медленно работает компьютер — Добрый день! Ноутбук сильно тормозит после обеда, особенно п…» workstation/P3/ServiceDesk → workstation/P4/ServiceDesk
- T-2026-02245: «Почтовый ящик переполнен — Помогите, пожалуйста. Прошу увеличить размер почтового ящика, п…» email/P3/ServiceDesk → email/P4/ServiceDesk
- T-2026-00558: «Доступ к 1С — Прошу дать доступ к базе Управление торговлей (UT) с ролью «Бухгалтер».» business_app/P4/Apps → access/P3/ServiceDesk
- T-2026-00673: «Разблокировать сайт — Добрый день! Не открывается cad-viewer.net, пишет «Доступ запрещён п…» network/P4/Security → access/P3/ServiceDesk
- T-2026-01352: «Тормозит отчёт — Доброе утро. После релиза запрос к таблице orders в analytics-pg выполняе…» database/P3/Database → database/P2/Database

## 2026-10-16 17:05 — qwen3:4b-instruct, bare, набор test (150)

| Валидный JSON | category | priority | team | Все три | Macro-F1 (priority) | Время на тикет |
|---|---|---|---|---|---|---|
| 100 % | 77 % | 5 % | 81 % | 3 % | 0.047 | 1.5 с |

Матрица ошибок priority (строки — эталон, столбцы — ответ модели):

| | P1 | P2 | P3 | P4 | иное |
|---|---|---|---|---|---|
| **P1** | 4 | 0 | 0 | 0 | 0 |
| **P2** | 12 | 3 | 0 | 0 | 0 |
| **P3** | 35 | 68 | 0 | 0 | 0 |
| **P4** | 6 | 19 | 3 | 0 | 0 |

Примеры ошибок (эталон → ответ):

- T-2026-01932: «Pod в CrashLoopBackOff — Помогите, пожалуйста. В namespace catalog поды payments-gateway в…» kubernetes/P2/DevOps → kubernetes/P1/DevOps
- T-2026-01581: «Восстановить БД из бэкапа — Добрый день! Нужна копия crm-pg на вчерашний день на отдельный…» database/P3/Database → database/P2/Database
- T-2026-01268: «Outlook постоянно просит пароль — Коллеги, привет! Outlook пишет «Отключено», веб-почта пр…» email/P3/ServiceDesk → email/P2/ServiceDesk
- T-2026-00441: «Забыл пароль — Забыл пароль от учётной записи, на портале id.corp.local не приходит код по…» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-01701: «Гостевой Wi-Fi для мероприятия — Планируем мероприятие на 50 человек 25 февраля, нужна отд…» network/P3/Network → network/P1/Network
- T-2026-02971: «Истёк срок действия пароля — Здравствуйте. Забыл пароль от учётной записи, на портале id.c…» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-01440: «Блокировка учётной записи — Добрый день! С утра уже третий раз блокируется учётная запись …» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-00239: «Наши письма попадают в спам — Письма на адреса ООО «Альфа-Трейд» возвращаются с ошибкой 5.…» email/P3/Infra → email/P1/ServiceDesk
- T-2026-01905: «Компьютер долго загружается — Помогите, пожалуйста. Компьютер очень долго загружается и то…» workstation/P3/ServiceDesk → workstation/P2/ServiceDesk
- T-2026-00800: «Заблокирован сайт — Здравствуйте. Не открывается docs.supplier-portal.ru, пишет «Доступ за…» network/P4/Security → access/P2/ServiceDesk
- T-2026-00936: «Забыл пароль — Коллеги, привет! Забыл пароль от учётной записи, на портале id.corp.local н…» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-00865: «Не могу пройти MFA — Добрый день! Сменил телефон, старый сдал в магазин, теперь не могу пр…» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-02389: «Отказано в доступе к папке — Доброе утро. Вчера одобрили доступ к Marketing, но всё равно …» access/P3/ServiceDesk → access/P2/ServiceDesk
- T-2026-00015: «Истекает сертификат grafana.corp.local — Браузер ругается на сертификат wiki.corp.local.» server/P3/Infra → business_app/P2/Apps
- T-2026-02598: «Нет интернета — Коллеги, привет! Браузер пишет «прокси-сервер не отвечает». Заранее спасиб…» network/P3/Network → network/P2/Network

## 2026-10-16 17:01 — qwen3:4b-instruct, fewshot, набор hard (60)

| Валидный JSON | category | priority | team | Все три | Macro-F1 (priority) | Время на тикет |
|---|---|---|---|---|---|---|
| 100 % | 75 % | 70 % | 77 % | 52 % | 0.686 | 1.4 с |

Матрица ошибок priority (строки — эталон, столбцы — ответ модели):

| | P1 | P2 | P3 | P4 | иное |
|---|---|---|---|---|---|
| **P1** | 6 | 1 | 0 | 1 | 0 |
| **P2** | 1 | 5 | 1 | 1 | 0 |
| **P3** | 0 | 5 | 21 | 5 | 0 |
| **P4** | 0 | 0 | 3 | 10 | 0 |

Примеры ошибок (эталон → ответ):

- H01: «Не могу зайти в почту с телефона — После того как поменял пароль, на айфоне перестала прих…» access/P3/ServiceDesk → access/P2/ServiceDesk
- H03: «очень срочно!!! принтер — В 3-214 МФУ жует бумагу, распечатать договор не могу, руководите…» printing/P4/ServiceDesk → printing/P3/ServiceDesk
- H04: «Нужен доступ к папке маркетинга — Перевели в маркетинг, прошу доступ на запись к \\fs01\Ma…» access/P4/ServiceDesk → access/P3/ServiceDesk
- H06: «Сайт поставщика не открывается — При заходе на портал поставщика пишет, что доступ запрещё…» network/P4/Security → access/P3/ServiceDesk
- H07: «Упал каталог на сайте — После релиза все поды catalog в CrashLoopBackOff, клиенты на сайте…» kubernetes/P1/DevOps → server/P1/Infra
- H10: «Письма клиенту возвращаются — Отправляю коммерческое предложение в АО «СеверСтрой», приход…» email/P3/Infra → email/P3/ServiceDesk
- H12: «Тормозит комп ужасно — Компьютер грузится по 10 минут, диск всё время на 100 процентах, ра…» workstation/P3/ServiceDesk → workstation/P2/ServiceDesk
- H13: «фишинг? — пришло письмо от «службы безопасности», что мой ящик заблокируют, если не подтве…» security/P3/Security → security/P2/Security
- H14: «Ввела пароль не там — кликнула на ссылку из письма про премию и ввела логин и пароль, пото…» security/P2/Security → access/P4/ServiceDesk
- H15: «Украли ноутбук — В метро украли рабочий ноутбук вместе с рюкзаком» security/P2/Security → access/P2/ServiceDesk
- H16: «Все файлы переименовались — у меня на диске все файлы теперь с расширением .locked и какой…» security/P1/Security → workstation/P4/ServiceDesk
- H19: «Добавьте в ЗУП — Новому кадровику нужен доступ к ЗУП с ролью кадровика» business_app/P4/Apps → access/P4/ServiceDesk
- H20: «Too many clients — orders-service сыплет FATAL: sorry, too many clients already, заказы оф…» database/P2/Database → server/P2/Infra
- H22: «Восстановить справочник — Случайно удалили справочник в crm-pg вчера вечером, нужно восста…» database/P3/Database → database/P2/Database
- H24: «Релиз не стартует — новый релиз notifications не поднимается, ImagePullBackOff, unauthoriz…» kubernetes/P3/DevOps → cicd/P3/DevOps

## 2026-10-16 16:59 — qwen3:4b-instruct, bare, набор hard (60)

| Валидный JSON | category | priority | team | Все три | Macro-F1 (priority) | Время на тикет |
|---|---|---|---|---|---|---|
| 100 % | 68 % | 13 % | 68 % | 7 % | 0.132 | 1.4 с |

Матрица ошибок priority (строки — эталон, столбцы — ответ модели):

| | P1 | P2 | P3 | P4 | иное |
|---|---|---|---|---|---|
| **P1** | 7 | 1 | 0 | 0 | 0 |
| **P2** | 6 | 1 | 1 | 0 | 0 |
| **P3** | 7 | 24 | 0 | 0 | 0 |
| **P4** | 1 | 12 | 0 | 0 | 0 |

Примеры ошибок (эталон → ответ):

- H01: «Не могу зайти в почту с телефона — После того как поменял пароль, на айфоне перестала прих…» access/P3/ServiceDesk → email/P2/ServiceDesk
- H03: «очень срочно!!! принтер — В 3-214 МФУ жует бумагу, распечатать договор не могу, руководите…» printing/P4/ServiceDesk → printing/P1/ServiceDesk
- H04: «Нужен доступ к папке маркетинга — Перевели в маркетинг, прошу доступ на запись к \\fs01\Ma…» access/P4/ServiceDesk → workstation/P2/ServiceDesk
- H05: «VPN — Дома не подключается фортиклиент, пишет credential or sslvpn configuration is wrong.…» vpn/P3/Network → vpn/P2/Network
- H06: «Сайт поставщика не открывается — При заходе на портал поставщика пишет, что доступ запрещё…» network/P4/Security → business_app/P2/Apps
- H08: «Диск — на billing-api-02 /var заполнен на 97%, пришёл critical алерт» server/P2/Infra → server/P1/Infra
- H09: «Помогите с Outlook — аутлук всё время спрашивает пароль, ввожу — опять спрашивает. Веб-поч…» email/P3/ServiceDesk → email/P2/ServiceDesk
- H10: «Письма клиенту возвращаются — Отправляю коммерческое предложение в АО «СеверСтрой», приход…» email/P3/Infra → email/P2/ServiceDesk
- H11: «Новый сотрудник — С понедельника выходит дизайнер в отдел маркетинга, нужен ноутбук помощн…» workstation/P4/ServiceDesk → workstation/P2/ServiceDesk
- H12: «Тормозит комп ужасно — Компьютер грузится по 10 минут, диск всё время на 100 процентах, ра…» workstation/P3/ServiceDesk → workstation/P1/ServiceDesk
- H13: «фишинг? — пришло письмо от «службы безопасности», что мой ящик заблокируют, если не подтве…» security/P3/Security → security/P1/Security
- H14: «Ввела пароль не там — кликнула на ссылку из письма про премию и ввела логин и пароль, пото…» security/P2/Security → email/P3/ServiceDesk
- H15: «Украли ноутбук — В метро украли рабочий ноутбук вместе с рюкзаком» security/P2/Security → workstation/P1/ServiceDesk
- H16: «Все файлы переименовались — у меня на диске все файлы теперь с расширением .locked и какой…» security/P1/Security → workstation/P2/ServiceDesk
- H17: «1С ругается на лицензию — При входе в 1С: «не обнаружена лицензия». Вчера всё работало.» business_app/P3/Apps → business_app/P2/Apps

