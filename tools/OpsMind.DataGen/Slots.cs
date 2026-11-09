namespace OpsMind.DataGen;

/// <summary>Значения для подстановки в шаблоны обращений.</summary>
static class Slots
{
    public static readonly string[] Greetings =
        ["", "", "Добрый день! ", "Здравствуйте. ", "Коллеги, привет! ", "Доброе утро. ", "Помогите, пожалуйста. "];

    // «Срочно» в тексте намеренно не влияет на приоритет (RB-001) — модель должна этому научиться.
    public static readonly string[] Closings =
        ["", "", " Спасибо.", " Заранее спасибо!", " Буду благодарен за помощь.", " СРОЧНО!!!", " Это срочно, горит отчёт.", " Жду ответа."];

    public static readonly string[] Channels = ["portal", "portal", "portal", "email", "email", "phone", "chat"];

    public static readonly string[] Departments =
        ["Бухгалтерия", "Отдел продаж", "Колл-центр", "Маркетинг", "Отдел кадров", "Юридический отдел",
         "Разработка", "Логистика", "Финансовый отдел", "Закупки", "Аналитика", "Служба качества"];

    public static readonly string[] People =
        ["Иванова Анна", "Петров Сергей", "Смирнова Ольга", "Кузнецов Дмитрий", "Попова Мария", "Соколов Андрей",
         "Лебедева Екатерина", "Козлов Алексей", "Новикова Ирина", "Морозов Павел", "Волкова Наталья", "Алексеев Игорь",
         "Фёдорова Юлия", "Николаев Максим", "Орлова Татьяна", "Захаров Роман", "Зайцева Елена", "Павлов Виктор",
         "Семёнова Дарья", "Голубев Артём"];

    public static readonly string[] Logins =
        ["a.ivanova", "s.petrov", "o.smirnova", "d.kuznetsov", "m.popova", "a.sokolov", "e.lebedeva", "a.kozlov",
         "i.novikova", "p.morozov", "n.volkova", "i.alekseev"];

    public static readonly string[] Shares = ["Finance", "Sales", "Marketing", "Projects", "HR", "Legal", "Logistics", "Quality"];

    public static readonly string[] Partners = ["ООО «Альфа-Трейд»", "АО «СеверСтрой»", "ООО «Вектор»", "ГК «Меридиан»"];

    public static readonly string[] Contractors = ["ООО «СофтЛаб»", "ИП Громов", "ООО «ДатаПро»", "АО «ИнфоСистемы»"];

    public static readonly string[] Servers =
        ["app-prod-03", "billing-api-02", "reports01", "etl-01", "web-front-02", "test-app-05", "stage-api-01", "files-02"];

    public static readonly string[] Services = ["portal", "reports", "billing", "wiki", "jira", "grafana"];

    public static readonly string[] Rooms = ["«Байкал»", "«Эльбрус»", "«Волга»", "«Ладога»", "3-310"];

    public static readonly string[] Sites = ["docs.supplier-portal.ru", "tenders.example.org", "cad-viewer.net", "logistics-hub.io"];

    public static readonly string[] Software = ["Visual Studio Code", "Notepad++", "Figma", "DBeaver", "Power BI Desktop", "7-Zip", "draw.io"];

    public static readonly string[] Printers = ["3-214-MFP", "2-105-MFP", "4-310-COLOR", "1-020-MFP", "5-402-MFP"];

    public static readonly string[] Databases = ["billing-pg", "crm-pg", "orders-pg", "analytics-pg"];

    public static readonly string[] Apps = ["billing-api", "orders-service", "catalog", "notifications", "payments-gateway"];

    public static readonly string[] Namespaces = ["billing", "crm-prod", "payments", "catalog", "notifications"];

    public static readonly string[] Bases1C = ["Бухгалтерию (BUH)", "ЗУП (ZUP)", "Управление торговлей (UT)"];
}
