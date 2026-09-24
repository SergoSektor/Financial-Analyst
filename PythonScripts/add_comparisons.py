"""
Добавляет 3 сравнения между документами в БД.
"""

import sqlite3, json, os, copy
from datetime import datetime

APPDATA = os.path.join(os.environ['APPDATA'], 'FinancialAnalyst')
DB_PATH = os.path.join(APPDATA, 'data.db')


def fmt(v):
    """FormatNumber из MainViewModel.cs"""
    if v is None:
        return "Н/Д"
    if v >= 1_000_000_000_000:
        return f"{v / 1_000_000_000_000:.2f} трлн"
    if v >= 1_000_000_000:
        return f"{v / 1_000_000_000:.2f} млрд"
    if v >= 1_000_000:
        return f"{v / 1_000_000:.2f} млн"
    return f"{v:.2f}"


def get_op(v1, v2, inverted=False):
    if v1 is None and v2 is None:
        return "=", "equal"
    if v1 is None:
        return "-", "right"
    if v2 is None:
        return "-", "left"
    cmp = (v1 > v2) - (v1 < v2)
    if inverted:
        cmp = -cmp
    if cmp > 0:
        return ">", "left"
    if cmp < 0:
        return "<", "right"
    return "=", "equal"


def build_metrics(d1, d2, r1, r2):
    """BuildComparisonMetrics из MainViewModel.cs"""
    metrics = []

    def add_pair(label, v1, v2, inverted=False):
        op, raw = get_op(v1, v2, inverted)
        val = f"{fmt(v1)} {op} {fmt(v2)}"
        metrics.append({
            "Label": label,
            "Value": val,
            "Category": "Сравнение",
            "RawText": raw
        })

    add_pair("Выручка", d1.get("revenue"), d2.get("revenue"))
    add_pair("Чистая прибыль", d1.get("net_income"), d2.get("net_income"))
    add_pair("Активы", d1.get("assets"), d2.get("assets"))
    add_pair("Обязательства", d1.get("liabilities"), d2.get("liabilities"))
    add_pair("Капитал", d1.get("equity"), d2.get("equity"))
    add_pair("ROE", r1.get("ROE"), r2.get("ROE"))
    add_pair("ROA", r1.get("ROA"), r2.get("ROA"))
    add_pair("Долг/Капитал", r1.get("DebtToEquity"), r2.get("DebtToEquity"), inverted=True)
    add_pair("NIM", r1.get("NIM"), r2.get("NIM"))
    add_pair("C/I", r1.get("CostToIncome"), r2.get("CostToIncome"), inverted=True)

    return metrics


def _winner_title(w, l, wv, lv, label, unit="%", invert=False):
    """Формирует заголовок с дельтой"""
    if invert:
        better_min = min(wv, lv)
        worse_max = max(wv, lv)
        return f"{label}: {w} лучше на {worse_max - better_min:.1f}{unit}"
    delta = abs(wv - lv)
    return f"{label}: {w} лучше на {delta:.1f}{unit}"


def build_comparison_findings(n1, n2, d1, d2, r1, r2):
    findings = []

    def get_roe(n):
        return r1.get("ROE") if n == n1 else r2.get("ROE")

    def get_ci(n):
        return r1.get("CostToIncome") if n == n1 else r2.get("CostToIncome")

    def get_nim(n):
        return r1.get("NIM") if n == n1 else r2.get("NIM")

    def get_roa(n):
        return r1.get("ROA") if n == n1 else r2.get("ROA")

    def get_de(n):
        return r1.get("DebtToEquity") if n == n1 else r2.get("DebtToEquity")

    def get_rev(n):
        return d1.get("revenue") if n == n1 else d2.get("revenue")

    def get_ni(n):
        return d1.get("net_income") if n == n1 else d2.get("net_income")

    # --- Определяем лидера по каждому показателю ---
    leaders = {}
    # Кто крупнее (по активам)
    if d1.get("assets") and d2.get("assets"):
        leaders["assets"] = n1 if d1["assets"] > d2["assets"] else n2
    # ROE
    if r1.get("ROE") and r2.get("ROE"):
        leaders["roe"] = n1 if r1["ROE"] > r2["ROE"] else n2
    # C/I (инвертирован: меньше = лучше)
    if r1.get("CostToIncome") and r2.get("CostToIncome"):
        leaders["ci"] = n1 if r1["CostToIncome"] < r2["CostToIncome"] else n2
    # NIM
    if r1.get("NIM") and r2.get("NIM"):
        leaders["nim"] = n1 if r1["NIM"] > r2["NIM"] else n2
    # ROA
    if r1.get("ROA") and r2.get("ROA"):
        leaders["roa"] = n1 if r1["ROA"] > r2["ROA"] else n2
    # D/E (инвертирован: меньше = лучше)
    if r1.get("DebtToEquity") and r2.get("DebtToEquity"):
        leaders["de"] = n1 if r1["DebtToEquity"] < r2["DebtToEquity"] else n2
    # Чистая прибыль
    if d1.get("net_income") and d2.get("net_income"):
        leaders["ni"] = n1 if d1["net_income"] > d2["net_income"] else n2

    # --- Результат противостояния ---
    if leaders:
        wins = {}
        for _, w in leaders.items():
            wins[w] = wins.get(w, 0) + 1
        overall = max(wins, key=wins.get) if wins else n1
        wincount = wins.get(overall, 0)
        total = len(leaders)
        loser = n2 if overall == n1 else n1
        findings.append({
            "Type": 2,
            "Title": f"{overall} лидирует по {wincount}/{total} метрикам",
            "Description": f"По результатам сравнения {overall} опережает {loser} по {wincount} из {total} ключевых показателей. Детальный анализ по каждой метрике приведён ниже.",
            "Source": "AI"
        })

    # --- Масштаб ---
    if d1.get("assets") and d2.get("assets"):
        bigger = n1 if d1["assets"] > d2["assets"] else n2
        smaller = n2 if d1["assets"] > d2["assets"] else n1
        bv = max(d1["assets"], d2["assets"])
        sv = min(d1["assets"], d2["assets"])
        ratio = bv / sv
        delta = bv - sv
        findings.append({
            "Type": 4,
            "Title": f"Разница в масштабе: {ratio:.0f}x",
            "Description": f"Активы {bigger} ({fmt(bv)}) превышают активы {smaller} ({fmt(sv)}) на {fmt(delta)}. Более крупному банку рекомендуется уделить внимание управлению рисками концентрации, меньшему — искать нишевые преимущества.",
            "Source": "AI"
        })

    # --- ROE ---
    if r1.get("ROE") and r2.get("ROE"):
        w = n1 if r1["ROE"] > r2["ROE"] else n2
        l = n2 if r1["ROE"] > r2["ROE"] else n1
        wv = max(r1["ROE"], r2["ROE"])
        lv = min(r1["ROE"], r2["ROE"])
        rec_winner = "Рекомендуется проанализировать, какие факторы обеспечивают более высокую доходность капитала, и рассмотреть возможность их применения." if w == get_roe(w) else ""
        rec_loser = f"Рекомендуется {l} провести декомпозицию ROE: проверить, за счёт какого компонента (прибыльность, оборачиваемость или леверидж) происходит отставание, и разработать план улучшения."
        findings.append({
            "Type": 2 if wv > 15 else 1,
            "Title": _winner_title(w, l, wv, lv, "ROE"),
            "Description": f"ROE {w} = {wv:.1f}% против {lv:.1f}% у {l}. Дельта: {wv - lv:.1f}п.п. {rec_winner} {rec_loser}",
            "Source": "AI"
        })

    # --- C/I ---
    if r1.get("CostToIncome") and r2.get("CostToIncome"):
        w = n1 if r1["CostToIncome"] < r2["CostToIncome"] else n2
        l = n2 if r1["CostToIncome"] < r2["CostToIncome"] else n1
        wv = min(r1["CostToIncome"], r2["CostToIncome"])
        lv = max(r1["CostToIncome"], r2["CostToIncome"])
        findings.append({
            "Type": 2 if wv < 50 else 0,
            "Title": _winner_title(w, l, wv, lv, "C/I", unit="п.п."),
            "Description": f"C/I у {w} ({wv:.1f}%) эффективнее, чем у {l} ({lv:.1f}%), разрыв {lv - wv:.1f}п.п. {'Рекомендуется изучить практики управления расходами лидера и внедрить аналогичные инструменты контроля затрат.' if lv - wv > 10 else 'Рекомендуется продолжить мониторинг операционной эффективности.'}",
            "Source": "AI"
        })

    # --- NIM ---
    if r1.get("NIM") and r2.get("NIM"):
        w = n1 if r1["NIM"] > r2["NIM"] else n2
        l = n2 if r1["NIM"] > r2["NIM"] else n1
        wv = max(r1["NIM"], r2["NIM"])
        lv = min(r1["NIM"], r2["NIM"])
        findings.append({
            "Type": 2 if wv > 3 else 0,
            "Title": _winner_title(w, l, wv, lv, "NIM", unit="п.п."),
            "Description": f"NIM {w} = {wv:.2f}%, у {l} = {lv:.2f}%. {'Разрыв в маржинальности существенный. Рекомендуется проанализировать структуру кредитного портфеля и депозитной базы.' if wv - lv > 1 else 'Разница в марже незначительная.'}",
            "Source": "AI"
        })

    # --- ROA ---
    if r1.get("ROA") and r2.get("ROA"):
        w = n1 if r1["ROA"] > r2["ROA"] else n2
        l = n2 if r1["ROA"] > r2["ROA"] else n1
        wv = max(r1["ROA"], r2["ROA"])
        lv = min(r1["ROA"], r2["ROA"])
        findings.append({
            "Type": 0 if lv < 2 else 2,
            "Title": _winner_title(w, l, wv, lv, "ROA", unit="п.п."),
            "Description": f"ROA {w} = {wv:.2f}%, у {l} = {lv:.2f}%. {'Отстающему банку рекомендуется пересмотреть структуру активов: выявить низкодоходные статьи, рассмотреть реструктуризацию.' if lv < 2 else 'Оба банка демонстрируют приемлемый уровень ROA.'}",
            "Source": "AI"
        })

    # --- D/E ---
    if r1.get("DebtToEquity") and r2.get("DebtToEquity"):
        w = n1 if r1["DebtToEquity"] < r2["DebtToEquity"] else n2
        l = n2 if r1["DebtToEquity"] < r2["DebtToEquity"] else n1
        wv = min(r1["DebtToEquity"], r2["DebtToEquity"])
        lv = max(r1["DebtToEquity"], r2["DebtToEquity"])
        findings.append({
            "Type": 1,
            "Title": f"Долговая нагрузка: ниже у {w}",
            "Description": f"D/E {w} = {wv:.2f} против {lv:.2f} у {l}. {'Отстающему банку рекомендуется ограничить новые заимствования и направить усилия на увеличение собственного капитала.' if lv > 10 else 'Оба банка находятся в зоне контроля.'}",
            "Source": "AI"
        })

    # --- Итоговые рекомендации ---
    overall_winner = max(set(leaders.values()), key=list(leaders.values()).count) if leaders else n1
    overall_loser = n2 if overall_winner == n1 else n1
    w_score = sum(1 for v in leaders.values() if v == overall_winner)
    l_score = sum(1 for v in leaders.values() if v == overall_loser)

    findings.append({
        "Type": 3,
        "Title": f"Что делать: {overall_loser}",
        "Description": _build_action_plan(overall_loser, overall_winner, d1, d2, r1, r2, n1, n2),
        "Source": "AI"
    })

    return findings


def _build_action_plan(loser, winner, d1, d2, r1, r2, n1, n2):
    """Строит рекомендацию для отстающего банка на основе дельт."""
    plan = []
    d = d2 if loser == n2 else d1
    r = r2 if loser == n2 else r1
    dw = d1 if loser == n2 else d2
    rw = r1 if loser == n2 else r2

    delta_roe = rw.get("ROE", 0) - r.get("ROE", 0) if r.get("ROE") and rw.get("ROE") else 0
    delta_ci = r.get("CostToIncome", 0) - rw.get("CostToIncome", 0) if r.get("CostToIncome") and rw.get("CostToIncome") else 0
    delta_nim = rw.get("NIM", 0) - r.get("NIM", 0) if r.get("NIM") and rw.get("NIM") else 0
    delta_roa = rw.get("ROA", 0) - r.get("ROA", 0) if r.get("ROA") and rw.get("ROA") else 0
    delta_ni = (dw.get("net_income", 0) - d.get("net_income", 0)) if d.get("net_income") and dw.get("net_income") else 0

    if delta_roe > 3:
        plan.append(f"ROE отстаёт на {delta_roe:.1f}п.п. Рекомендуется: провести факторный анализ ROE (DuPont), увеличить чистую прибыль за счёт оптимизации операционной деятельности.")
    if delta_ci > 5:
        plan.append(f"C/I хуже на {delta_ci:.1f}п.п. Рекомендуется: внедрить программу сокращения операционных расходов, автоматизировать рутинные процессы, пересмотреть штатное расписание.")
    if delta_nim > 1:
        plan.append(f"NIM отстаёт на {delta_nim:.2f}п.п. Рекомендуется: пересмотреть кредитную политику, увеличить долю высокомаржинальных продуктов, оптимизировать стоимость фондирования.")
    if delta_roa > 0.5:
        plan.append(f"ROA ниже на {delta_roa:.2f}п.п. Рекомендуется: провести аудит активов, выявить низкодоходные и проблемные активы, рассмотреть реструктуризацию портфеля.")
    if delta_ni > 100_000_000_000:
        delta_fmt = fmt(delta_ni)
        plan.append(f"Чистая прибыль меньше на {delta_fmt}. Рекомендуется: проанализировать структуру доходов и расходов, выявить источники утечки прибыли.")

    if not plan:
        plan.append("Показатели сопоставимы. Рекомендуется: сфокусироваться на удержании достигнутых позиций и поиске точечных улучшений по каждому коэффициенту.")

    plan.append(f"Целевой ориентир: достичь показателей {winner} в течение 1-2 отчётных периодов.")
    return " | ".join(plan)


def add_comparisons():
    conn = sqlite3.connect(DB_PATH)
    now = datetime.now().isoformat()

    # читаем данные документов
    rows = conn.execute("""
        SELECT d.Id, d.FileName, a.FinancialDataJson
        FROM Documents d
        JOIN Analyses a ON a.DocumentId = d.Id
        WHERE d.Status = 'Completed'
        ORDER BY d.Id
    """).fetchall()

    docs = {}
    for r in rows:
        docs[r[0]] = {
            "id": r[0],
            "name": r[1],
            "data": json.loads(r[2]),
        }
        # вычисляем коэффициенты (как FinancialCalculatorService)
        d = docs[r[0]]["data"]
        ni = d.get("net_income")
        eq = d.get("equity")
        a = d.get("assets")
        li = d.get("liabilities")
        nii = d.get("net_interest_income")
        rev = d.get("revenue")
        op = d.get("operating_expenses")
        docs[r[0]]["ratios"] = {
            "ROE": float(ni / eq * 100) if ni and eq else None,
            "ROA": float(ni / a * 100) if ni and a else None,
            "DebtToEquity": float(li / eq) if li and eq else None,
            "NIM": float(nii / a * 100) if nii and a else None,
            "CostToIncome": float(op / rev * 100) if op and rev else None,
        }

    print(f"Загружено документов: {len(docs)}")
    for did, doc in docs.items():
        print(f"  [{did}] {doc['data']['CompanyName']}")

    # 3 пары сравнений
    pairs = [
        ("Т-Банк vs Сбербанк", 2, 1),
        ("Альфа-Банк vs ВТБ", 4, 5),
        ("Совкомбанк vs Т-Банк", 3, 2),
    ]

    inserted = 0
    for label, id1, id2 in pairs:
        if id1 not in docs or id2 not in docs:
            print(f"  Пропускаю {label}: документы не найдены в БД")
            continue

        d1_doc = docs[id1]
        d2_doc = docs[id2]
        n1 = d1_doc["data"]["CompanyName"]
        n2 = d2_doc["data"]["CompanyName"]
        d1 = d1_doc["data"]
        d2 = d2_doc["data"]
        r1 = d1_doc["ratios"]
        r2 = d2_doc["ratios"]

        metrics = build_metrics(d1, d2, r1, r2)
        findings = build_comparison_findings(n1, n2, d1, d2, r1, r2)

        data1_json = json.dumps(d1, ensure_ascii=False)
        data2_json = json.dumps(d2, ensure_ascii=False)
        ratios1_json = json.dumps(r1, ensure_ascii=False)
        ratios2_json = json.dumps(r2, ensure_ascii=False)
        metrics_json = json.dumps(metrics, ensure_ascii=False)
        findings_json = json.dumps(findings, ensure_ascii=False)

        conn.execute("""
            INSERT INTO Comparisons (Doc1Id, Doc2Id, Doc1Name, Doc2Name,
                Data1Json, Data2Json, Ratios1Json, Ratios2Json,
                MetricsJson, FindingsJson, CreatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, (id1, id2, n1, n2,
              data1_json, data2_json, ratios1_json, ratios2_json,
              metrics_json, findings_json, now))

        inserted += 1
        print(f"\n  + Сравнение: {label}")
        for m in metrics:
            print(f"      {m['Label']}: {m['Value']}")
        print(f"      Выводов: {len(findings)}")

    conn.commit()
    conn.close()

    print(f"\nДобавлено сравнений: {inserted}")


if __name__ == "__main__":
    add_comparisons()
