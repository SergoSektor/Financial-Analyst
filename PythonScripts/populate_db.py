"""
Наполняет SQLite БД тестовыми данными: 5 документов с идеальными
результатами анализа. Запускать после первого билда проекта.
"""

import sqlite3, json, os
from datetime import datetime

APPDATA = os.path.join(os.environ['APPDATA'], 'FinancialAnalyst')
DB_PATH = os.path.join(APPDATA, 'data.db')
os.makedirs(APPDATA, exist_ok=True)

# ============================================================
#   ДАННЫЕ 5 БАНКОВ (МСФО 2024)
# ============================================================

DOCUMENTS = [
    {
        "id": "sberbank",
        "name": "sberbank_2024.docx",
        "path": os.path.join(os.path.expanduser("~"), "Documents", "sberbank_2024.docx"),
        "type": "Word",
        "size": 4_300_000,
        "company": "ПАО «Сбербанк»",
        "period": "2024",
        "data": {
            "revenue": 3_200_000_000_000,
            "net_income": 1_508_600_000_000,
            "assets": 55_000_000_000_000,
            "liabilities": 50_000_000_000_000,
            "equity": 5_000_000_000_000,
            "net_interest_income": 1_900_000_000_000,
            "fee_income": 600_000_000_000,
            "operating_expenses": 900_000_000_000,
        },
        "roe": 30.2, "roa": 2.74, "de": 10.00, "nim": 3.45, "ci": 28.1,
    },
    {
        "id": "tbank",
        "name": "tbank_2024.pdf",
        "path": os.path.join(os.path.expanduser("~"), "Documents", "tbank_2024.pdf"),
        "type": "Pdf",
        "size": 2_340_000,
        "company": "МКПАО «Т-Банк» (Т-Технологии)",
        "period": "2024",
        "data": {
            "revenue": 962_000_000_000,
            "net_income": 122_000_000_000,
            "assets": 5_110_000_000_000,
            "liabilities": 4_589_000_000_000,
            "equity": 521_000_000_000,
            "net_interest_income": 380_000_000_000,
            "fee_income": 106_000_000_000,
            "operating_expenses": 520_000_000_000,
        },
        "roe": 23.4, "roa": 2.39, "de": 8.81, "nim": 7.44, "ci": 54.1,
    },
    {
        "id": "sovcombank",
        "name": "sovcombank_2024.pdf",
        "path": os.path.join(os.path.expanduser("~"), "Documents", "sovcombank_2024.pdf"),
        "type": "Pdf",
        "size": 1_890_000,
        "company": "ПАО «Совкомбанк»",
        "period": "2024",
        "data": {
            "revenue": 722_000_000_000,
            "net_income": 77_000_000_000,
            "assets": 4_000_000_000_000,
            "liabilities": 3_610_000_000_000,
            "equity": 390_000_000_000,
            "net_interest_income": 158_000_000_000,
            "fee_income": 39_000_000_000,
            "operating_expenses": 340_000_000_000,
        },
        "roe": 19.7, "roa": 1.93, "de": 9.26, "nim": 3.95, "ci": 47.1,
    },
    {
        "id": "alfabank",
        "name": "alfabank_2024.docx",
        "path": os.path.join(os.path.expanduser("~"), "Documents", "alfabank_2024.docx"),
        "type": "Word",
        "size": 1_560_000,
        "company": "Альфа-Банк (АО)",
        "period": "2024",
        "data": {
            "revenue": 515_060_000_000,
            "net_income": 209_900_000_000,
            "assets": 11_299_500_000_000,
            "liabilities": 10_303_200_000_000,
            "equity": 996_300_000_000,
            "net_interest_income": 350_000_000_000,
            "fee_income": 148_700_000_000,
            "operating_expenses": 234_000_000_000,
        },
        "roe": 21.1, "roa": 1.86, "de": 10.34, "nim": 3.10, "ci": 45.4,
    },
    {
        "id": "vtb",
        "name": "vtb_2024.docx",
        "path": os.path.join(os.path.expanduser("~"), "Documents", "vtb_2024.docx"),
        "type": "Word",
        "size": 2_450_000,
        "company": "ПАО «Банк ВТБ»",
        "period": "2024",
        "data": {
            "revenue": 1_200_000_000_000,
            "net_income": 435_000_000_000,
            "assets": 30_000_000_000_000,
            "liabilities": 27_500_000_000_000,
            "equity": 2_500_000_000_000,
            "net_interest_income": 700_000_000_000,
            "fee_income": 200_000_000_000,
            "operating_expenses": 500_000_000_000,
        },
        "roe": 17.4, "roa": 1.45, "de": 11.00, "nim": 2.33, "ci": 41.7,
    },
]

SOURCE_FIELDS = [
    "revenue", "net_income", "assets", "liabilities", "equity",
    "net_interest_income", "fee_income", "operating_expenses"
]


def build_financial_data(doc):
    return {
        "revenue": doc["data"]["revenue"],
        "net_income": doc["data"]["net_income"],
        "assets": doc["data"]["assets"],
        "liabilities": doc["data"]["liabilities"],
        "equity": doc["data"]["equity"],
        "net_interest_income": doc["data"]["net_interest_income"],
        "fee_income": doc["data"]["fee_income"],
        "operating_expenses": doc["data"]["operating_expenses"],
        "Currency": "RUB",
        "Period": doc["period"],
        "CompanyName": doc["company"],
        "SourceFields": SOURCE_FIELDS,
    }


def _action(val, threshold_good, threshold_bad, label, unit="%", invert=False):
    """Генерирует рекомендацию на основе порогов"""
    if invert:
        if val < threshold_good:
            return f"{label} {val:.1f}{unit} — отлично (лучше {threshold_good}{unit}). Рекомендуется удерживать текущий уровень."
        if val < threshold_bad:
            return f"{label} {val:.1f}{unit} — в пределах нормы ({threshold_good}-{threshold_bad}{unit}). Рекомендуется мониторинг."
        return f"{label} {val:.1f}{unit} — превышает порог {threshold_bad}{unit}. Рекомендуется разработать план снижения."
    else:
        if val > threshold_good:
            return f"{label} {val:.1f}{unit} — выше порога {threshold_good}{unit}. Рекомендуется удерживать достигнутый уровень."
        if val > threshold_bad:
            return f"{label} {val:.1f}{unit} — в пределах нормы ({threshold_bad}-{threshold_good}{unit}). Рекомендуется стремиться к улучшению."
        return f"{label} {val:.1f}{unit} — ниже порога {threshold_bad}{unit}. Рекомендуется провести анализ причин и разработать план мероприятий."


def build_findings(doc):
    d = doc["data"]
    roe, roa, de = doc["roe"], doc["roa"], doc["de"]
    nim, ci = doc["nim"], doc["ci"]
    findings = [
        {
            "Type": 2 if roe > 15 else (1 if roe > 8 else 0),
            "Title": "Рентабельность капитала (ROE)",
            "Description": f"ROE = {roe:.1f}% (порог «Хорошо»: >15%). Каждый рубль собственного капитала приносит {roe/100:.3f} руб. чистой прибыли. {'Показатель выше рынка — ключевая задача удержать планку. Источники: рост чистой прибыли и контроль за достаточностью капитала.' if roe > 20 else 'Показатель в норме. Цель на следующий период: довести ROE до 20% за счёт увеличения операционной эффективности.'}",
            "Source": "AI"
        },
        {
            "Type": 0 if roa < 2 else (1 if roa < 5 else 2),
            "Title": "Рентабельность активов (ROA)",
            "Description": f"ROA = {roa:.2f}% (порог «Хорошо»: >5%, «Норма»: 2-5%, «Проблема»: <2%). {'Каждый рубль активов приносит лишь {:.3f} руб. прибыли — это ниже отраслевого ориентира 2%. Рекомендуется пересмотреть структуру активов: выявить статьи с доходностью ниже средней, рассмотреть реструктуризацию кредитного портфеля и продажу непрофильных активов.'.format(roa/100) if roa < 2 else 'Каждый рубль активов приносит {:.3f} руб. прибыли — приемлемый уровень. Цель: удерживать ROA >2%, контролировать качество кредитного портфеля.'.format(roa/100)}",
            "Source": "AI"
        },
        {
            "Type": 1,
            "Title": "Долговая нагрузка (D/E)",
            "Description": f"D/E = {de:.2f}. На каждый рубль собственного капитала приходится {de:.1f} руб. обязательств. {'Для банковского сектора уровень выше 5 является нормой, однако рекомендуется ежеквартально отслеживать динамику: резкий рост D/E может сигнализировать о наращивании рискованного кредитования.' if de < 8 else 'Уровень выше 8 — требуется контроль. Рекомендуется: (1) ограничить новые крупные заимствования, (2) увеличить капитал за счёт прибыли, (3) мониторить ковенанты по кредитным соглашениям.'}",
            "Source": "AI"
        },
        {
            "Type": 2 if nim > 3 else (1 if nim > 1.5 else 0),
            "Title": "Чистая процентная маржа (NIM)",
            "Description": f"NIM = {nim:.2f}% (порог «Хорошо»: >3%, «Проблема»: <1.5%). {'Маржа выше 3% — хороший запас прочности. Рекомендуется: проанализировать, какие кредитные продукты дают наибольший спред, и усилить их продвижение; контролировать стоимость фондирования.' if nim > 3 else 'Маржа ниже 3%. Рекомендуется: пересмотреть ставки по кредитам и депозитам, увеличить долю высокомаржинальных продуктов (потребительское кредитование, МСБ), снизить стоимость пассивов.'}",
            "Source": "AI"
        },
        {
            "Type": 2 if ci < 50 else (1 if ci < 70 else 0),
            "Title": "Операционная эффективность (C/I)",
            "Description": f"C/I = {ci:.1f}% (порог «Хорошо»: <50%, «Проблема»: >70%). Из каждого рубля выручки {ci:.1f} коп. уходит на операционные расходы. {'Отличный показатель. Рекомендуется: провести бенчмаркинг с аналогами, выявить лучшие практики и зафиксировать их в регламентах.' if ci < 50 else 'Показатель выше 50%. Рекомендуется: проанализировать структуру затрат по статьям (ФОТ, аренда, ИТ, маркетинг), выявить статьи с ростом >10% год к году, разработать программу оптимизации сроком на 6 месяцев.'}",
            "Source": "AI"
        },
        {
            "Type": 3,
            "Title": "Приоритетные действия",
            "Description": _priorities(doc),
            "Source": "AI"
        },
    ]
    return findings


def _priorities(doc):
    items = []
    i = 0
    if doc["roa"] < 2:
        i += 1
        items.append(f"{i}. Повысить ROA с {doc['roa']:.2f}% до >2% — пересмотреть структуру активов, выявить низкодоходные статьи (кредиты, ценные бумаги), рассмотреть реструктуризацию.")
    if doc["ci"] >= 50:
        i += 1
        items.append(f"{i}. Снизить C/I с {doc['ci']:.1f}% до <50% — провести аудит операционных расходов, автоматизировать рутинные процессы, оптимизировать штатную численность.")
    if doc["de"] > 10:
        i += 1
        items.append(f"{i}. Снизить D/E с {doc['de']:.2f} до <10 — ограничить новые заимствования, увеличить собственный капитал за счёт нераспределённой прибыли.")
    if doc["nim"] < 3:
        i += 1
        items.append(f"{i}. Повысить NIM с {doc['nim']:.2f}% до >3% — пересмотреть ставки по кредитам и депозитам, увеличить долю высокомаржинальных продуктов.")
    if doc["roe"] < 15:
        i += 1
        items.append(f"{i}. Повысить ROE с {doc['roe']:.1f}% до >15% — наращивать чистую прибыль при контроле за уровнем капитала.")
    if not items:
        items.append(f"1. Удерживать ROE {doc['roe']:.1f}% — показатель выше порога 15%, ключевая задача не снижать планку.")
        items.append(f"2. Удерживать C/I {doc['ci']:.1f}% — отличный показатель ниже 50%, тиражировать успешные практики управления расходами.")
        if doc["de"] < 8:
            items.append(f"3. Мониторить D/E {doc['de']:.2f} — умеренный уровень долговой нагрузки, контролировать ежеквартально.")
    return "\n".join(items)


def fmt(v):
    if v >= 1_000_000_000_000:
        return f"{v / 1_000_000_000_000:.2f} трлн руб."
    if v >= 1_000_000_000:
        return f"{v / 1_000_000_000:.2f} млрд руб."
    if v >= 1_000_000:
        return f"{v / 1_000_000:.2f} млн руб."
    return f"{v:.2f} руб."


# ============================================================
#   СОЗДАНИЕ БД
# ============================================================

def create_schema(conn):
    conn.executescript("""
        CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
            MigrationId TEXT NOT NULL PRIMARY KEY,
            ProductVersion TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Documents (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FileName TEXT NOT NULL DEFAULT '',
            FilePath TEXT NOT NULL DEFAULT '',
            DocumentType TEXT NOT NULL DEFAULT '',
            FileSize INTEGER NOT NULL DEFAULT 0,
            DateAdded TEXT NOT NULL,
            Status TEXT NOT NULL DEFAULT 'NotProcessed'
        );

        CREATE TABLE IF NOT EXISTS Analyses (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL,
            FinancialDataJson TEXT NOT NULL DEFAULT '',
            FindingsJson TEXT NOT NULL DEFAULT '',
            AnalyzedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Comparisons (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Doc1Id INTEGER NOT NULL,
            Doc2Id INTEGER NOT NULL,
            Doc1Name TEXT NOT NULL DEFAULT '',
            Doc2Name TEXT NOT NULL DEFAULT '',
            Data1Json TEXT NOT NULL DEFAULT '',
            Data2Json TEXT NOT NULL DEFAULT '',
            Ratios1Json TEXT NOT NULL DEFAULT '',
            Ratios2Json TEXT NOT NULL DEFAULT '',
            MetricsJson TEXT NOT NULL DEFAULT '',
            FindingsJson TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Chunks (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL,
            ChunkIndex INTEGER NOT NULL,
            Content TEXT NOT NULL DEFAULT '',
            Embedding TEXT NOT NULL DEFAULT ''
        );

        PRAGMA journal_mode=WAL;
        PRAGMA wal_autocheckpoint=1000;
        PRAGMA synchronous=NORMAL;
        PRAGMA cache_size=-2000;
    """)


def populate():
    now = datetime.now().isoformat()

    conn = sqlite3.connect(DB_PATH)
    # пересоздаём таблицы с нуля (сбрасывает автоинкремент)
    for t in ('Comparisons', 'Chunks', 'Analyses', 'Documents', '__EFMigrationsHistory'):
        conn.execute(f'DROP TABLE IF EXISTS {t}')
    create_schema(conn)

    # вставляем документы
    doc_ids = {}
    for doc in DOCUMENTS:
        cur = conn.execute(
            "INSERT INTO Documents (FileName, FilePath, DocumentType, FileSize, DateAdded, Status) VALUES (?, ?, ?, ?, ?, ?)",
            (doc["name"], doc["path"], doc["type"], doc["size"], now, "Completed")
        )
        doc_ids[doc["id"]] = cur.lastrowid
        print(f"  + Документ [{cur.lastrowid}]: {doc['name']}")

    # вставляем анализы
    for doc in DOCUMENTS:
        data_json = json.dumps(build_financial_data(doc), ensure_ascii=False, indent=2)
        findings_json = json.dumps(build_findings(doc), ensure_ascii=False, indent=2)
        did = doc_ids[doc["id"]]
        conn.execute(
            "INSERT INTO Analyses (DocumentId, FinancialDataJson, FindingsJson, AnalyzedAt) VALUES (?, ?, ?, ?)",
            (did, data_json, findings_json, now)
        )
        print(f"  + Анализ для документа [{did}]: {doc['company']}")

    conn.commit()
    conn.close()

    # проверяем
    verify()


def verify():
    conn = sqlite3.connect(DB_PATH)
    docs = conn.execute("SELECT Id, FileName, Status FROM Documents").fetchall()
    analyses = conn.execute("SELECT Id, DocumentId FROM Analyses").fetchall()

    print(f"\n  БД: {DB_PATH}")
    print(f"  Документов: {len(docs)}")
    print(f"  Анализов:   {len(analyses)}")
    print(f"  Размер:     {os.path.getsize(DB_PATH) / 1024:.0f} КБ")
    print()

    for d in docs:
        a = next((a for a in analyses if a[1] == d[0]), None)
        has = "есть анализ" if a else "НЕТ АНАЛИЗА"
        print(f"  [{d[0]}] {d[1]} ({d[2]}) - {has}")
    conn.close()


# ============================================================
#   ЗАПУСК
# ============================================================

if __name__ == "__main__":
    print("Наполнение БД тестовыми данными...")
    populate()
    print("\nГотово! Запускайте приложение.")
