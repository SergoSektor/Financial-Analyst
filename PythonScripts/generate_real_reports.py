"""
Генерация 5 тестовых отчётов (2 PDF + 3 DOCX) с реальными данными
Т-Банка, Совкомбанка и Альфа-Банка по МСФО за 2024 год.

PDF содержат встроенные изображения (диаграммы matplotlib) для увеличения
объёма файлов до 2-4 МБ.
"""

import os, io, tempfile

# ============================================================
#   БИБЛИОТЕКИ
# ============================================================
try:
    from docx import Document
    from docx.shared import Pt, RGBColor, Inches, Emu
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.enum.table import WD_TABLE_ALIGNMENT
    DOCX_OK = True
except ImportError:
    DOCX_OK = False

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import numpy as np

# ============================================================
#   РЕАЛЬНЫЕ ФИНАНСОВЫЕ ДАННЫЕ (МСФО 2024)
# ============================================================

REPORTS = [
    {
        "id": "tbank",
        "bank": "МКПАО «Т-Банк» (Т-Технологии)",
        "name_short": "Т-Банк",
        "period": "2024 год",
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
        "bank": "ПАО «Совкомбанк»",
        "name_short": "Совкомбанк",
        "period": "2024 год",
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
        "bank": "Альфа-Банк (АО)",
        "name_short": "Альфа-Банк",
        "period": "2024 год",
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
]

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "TestReports")
os.makedirs(OUTPUT_DIR, exist_ok=True)

TEMP_DIR = os.path.join(OUTPUT_DIR, "_img_tmp")
os.makedirs(TEMP_DIR, exist_ok=True)


def fmt(v):
    if v >= 1_000_000_000_000:
        return f"{v / 1_000_000_000_000:.2f} трлн руб."
    if v >= 1_000_000_000:
        return f"{v / 1_000_000_000:.2f} млрд руб."
    if v >= 1_000_000:
        return f"{v / 1_000_000:.2f} млн руб."
    return f"{v:.2f} руб."


# ============================================================
#   ГЕНЕРАЦИЯ ИЗОБРАЖЕНИЙ (matplotlib)
# ============================================================

def _fig_to_png(fig, dpi=200):
    """Сохраняет фигуру matplotlib в PNG bytes."""
    buf = io.BytesIO()
    fig.savefig(buf, format='png', dpi=dpi, bbox_inches='tight',
                facecolor='white', edgecolor='none')
    plt.close(fig)
    buf.seek(0)
    return buf.read()


def _fmt_trln(v):
    if v >= 1_000_000_000_000:
        return f"{v/1_000_000_000_000:.1f} трлн"
    return f"{v/1_000_000_000:.0f} млрд"


def generate_charts(report):
    """Генерирует 6 изображений для отчёта. Возвращает список (filename, png_bytes)."""
    plt.close('all')
    d = report["data"]
    name = report["name_short"]
    images = []

    # Цвета банков
    colors_bank = {
        "Т-Банк": ("#FFDD00", "#333333"),
        "Совкомбанк": ("#003A70", "#00A650"),
        "Альфа-Банк": ("#EF3124", "#000000"),
    }
    c1, c2 = colors_bank.get(name, ("#0078D4", "#50B8E0"))

    # 1. PIE: Структура доходов
    fig = plt.figure(figsize=(7, 5))
    ax = fig.add_subplot(111)
    labels = ['Процентный доход', 'Комиссионный доход', 'Прочие доходы']
    vals = [d['net_interest_income'], d['fee_income'],
            d['revenue'] - d['net_interest_income'] - d['fee_income']]
    vals = [max(v, 0) for v in vals]
    colors = ['#003399', '#FF8800', '#66BB6A']
    ax.pie(vals, labels=labels, autopct='%1.1f%%',
           colors=colors, startangle=90,
           textprops={'fontsize': 11})
    ax.set_title(f'Структура доходов {name} за 2024 год', fontsize=14, fontweight='bold')
    images.append(('chart_income.png', _fig_to_png(fig)))

    # 2. BAR: Основные метрики
    fig = plt.figure(figsize=(8, 5))
    ax = fig.add_subplot(111)
    metrics_names = ['Выручка', 'Чистая прибыль', 'Активы', 'Капитал']
    metrics_vals = [d['revenue'], d['net_income'], d['assets'], d['equity']]
    bar_colors = [c1, '#FF6B35', '#1565C0', '#2E7D32']
    bars = ax.bar(metrics_names, [v/1e9 for v in metrics_vals],
                  color=bar_colors, width=0.6, edgecolor='white', linewidth=1.5)
    ax.set_title(f'Ключевые финансовые показатели {name}', fontsize=14, fontweight='bold')
    ax.set_ylabel('млрд руб.', fontsize=12)
    for bar, v in zip(bars, metrics_vals):
        ax.text(bar.get_x() + bar.get_width()/2, bar.get_height() + max(metrics_vals)/100,
                _fmt_trln(v), ha='center', va='bottom', fontsize=10, fontweight='bold')
    ax.spines['top'].set_visible(False)
    ax.spines['right'].set_visible(False)
    images.append(('chart_metrics.png', _fig_to_png(fig)))

    # 3. BAR: Коэффициенты
    fig = plt.figure(figsize=(10, 4))
    axes = [fig.add_subplot(1, 3, i+1) for i in range(3)]
    ratio_data = [
        ('ROE %', report['roe'], 15, 'зелёный'),
        ('NIM %', report['nim'], 3, 'синий'),
        ('C/I %', report['ci'], 50, 'красный'),
    ]
    for ax, (label, val, threshold, _) in zip(axes, ratio_data):
        color = '#2E7D32' if (label != 'C/I %' and val > threshold) or (label == 'C/I %' and val < threshold) else '#E53935'
        ax.barh([''], [val], color=color, height=0.5)
        ax.axvline(threshold, color='gray', linestyle='--', linewidth=1, label=f'Порог {threshold}')
        ax.set_xlim(0, max(val * 1.4, threshold * 1.4))
        ax.set_title(f'{label}', fontsize=12, fontweight='bold')
        ax.text(val + max(val, threshold)/20, 0, f'{val:.1f}',
                va='center', fontsize=11, fontweight='bold')
        ax.legend(fontsize=7, loc='upper right')
        ax.set_yticks([])
    fig.suptitle(f'Ключевые коэффициенты {name}', fontsize=14, fontweight='bold')
    fig.tight_layout()
    images.append(('chart_ratios.png', _fig_to_png(fig)))

    # 4. BIG GRADIENT HEADER
    fig = plt.figure(figsize=(8, 2))
    ax = fig.add_subplot(111)
    gradient = np.linspace(0, 1, 256).reshape(1, -1)
    gradient = np.vstack([gradient, gradient])
    ax.imshow(gradient, aspect='auto', cmap='Blues',
              extent=[0, 1, 0, 1])
    ax.text(0.5, 0.6, report['bank'], transform=ax.transAxes,
            fontsize=20, fontweight='bold', color='white',
            ha='center', va='center')
    ax.text(0.5, 0.3, 'Консолидированная отчётность по МСФО',
            transform=ax.transAxes, fontsize=12, color='white',
            ha='center', va='center')
    ax.axis('off')
    images.append(('header.png', _fig_to_png(fig)))

    # 5. PIE: Структура капитала
    fig = plt.figure(figsize=(6, 5))
    ax = fig.add_subplot(111)
    cap_labels = ['Обязательства', 'Капитал']
    cap_vals = [d['liabilities'], d['equity']]
    cap_colors = ['#E53935', '#2E7D32']
    ax.pie(cap_vals, labels=cap_labels, autopct='%1.1f%%',
           colors=cap_colors, startangle=90,
           textprops={'fontsize': 11})
    ax.set_title(f'Структура капитала {name}', fontsize=14, fontweight='bold')
    images.append(('chart_capital.png', _fig_to_png(fig)))

    # 6. FAKE TABLE IMAGE (текстура/градиент для увеличения объёма)
    fig, ax = plt.subplots(figsize=(8, 6))
    ax.axis('off')
    data_rows = [
        ['Показатель', 'Значение', 'Динамика'],
        ['Выручка', _fmt_trln(d['revenue']), '+82%'],
        ['Чистая прибыль', _fmt_trln(d['net_income']), '+51%'],
        ['Активы', _fmt_trln(d['assets']), '+27%'],
        ['Капитал', _fmt_trln(d['equity']), '+31%'],
        ['ROE', f"{report['roe']:.1f}%", '-'],
        ['NIM', f"{report['nim']:.2f}%", '-'],
        ['C/I', f"{report['ci']:.1f}%", '-'],
    ]
    table = ax.table(cellText=data_rows, loc='center', cellLoc='center')
    table.auto_set_font_size(False)
    table.set_fontsize(11)
    table.scale(1, 2)
    for j in range(3):
        table[0, j].set_facecolor('#003399')
        table[0, j].set_text_props(color='white', fontweight='bold')
    for i in range(1, len(data_rows)):
        bg = '#E8F0FE' if i % 2 == 0 else 'white'
        for j in range(3):
            table[i, j].set_facecolor(bg)
    ax.set_title(f'Сводка показателей {name} — {report["period"]}',
                 fontsize=14, fontweight='bold', pad=20)
    images.append(('summary_table.png', _fig_to_png(fig, dpi=250)))

    return images


# ============================================================
#   ГЕНЕРАЦИЯ DOCX (с изображениями)
# ============================================================

def gen_docx(report, path):
    if not DOCX_OK:
        print("  python-docx not installed, skipping")
        return

    doc = Document()
    d = report["data"]

    def _title():
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(report["bank"])
        r.bold = True
        r.font.size = Pt(18)
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run("Консолидированная финансовая отчётность по МСФО")
        r.font.size = Pt(13)
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(f"за {report['period']}")
        r.font.size = Pt(12)
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run("Все суммы указаны в рублях РФ (RUB), если не указано иное")
        r.font.size = Pt(9)
        r.font.color.rgb = RGBColor(100, 100, 100)
        doc.add_paragraph()

    def _hdr(text):
        p = doc.add_paragraph()
        r = p.add_run(text)
        r.bold = True
        r.font.size = Pt(14)
        r.font.color.rgb = RGBColor(0, 0, 140)

    def _para(text):
        p = doc.add_paragraph()
        r = p.add_run(text)
        r.font.size = Pt(10)

    def _table(headers, rows):
        tbl = doc.add_table(rows=len(rows) + 1, cols=len(headers))
        tbl.style = 'Light Grid Accent 1'
        tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
        for j, h in enumerate(headers):
            tbl.rows[0].cells[j].text = h
        for i, row in enumerate(rows, 1):
            for j, val in enumerate(row):
                tbl.rows[i].cells[j].text = val
        doc.add_paragraph()

    def _add_image(png_bytes, width_inches=5.5):
        """Вставляет PNG-изображение в документ."""
        tmp = tempfile.NamedTemporaryFile(suffix='.png', delete=False, dir=TEMP_DIR)
        tmp.write(png_bytes)
        tmp.close()
        run = doc.add_paragraph().add_run()
        run.add_picture(tmp.name, width=Inches(width_inches))
        os.unlink(tmp.name)

    # --- Генерируем изображения ---
    charts = generate_charts(report)

    # --- Title ---
    _title()

    # --- HEADER IMAGE ---
    for name, png in charts:
        if name == 'header.png':
            _add_image(png, 6.0)
            break

    # --- 1. Key metrics ---
    _hdr("1. Ключевые финансовые показатели")
    _table(["Показатель", "Значение"], [
        ("Выручка (операционные доходы)", fmt(d["revenue"])),
        ("Чистая прибыль", fmt(d["net_income"])),
        ("Итого активы", fmt(d["assets"])),
        ("Обязательства", fmt(d["liabilities"])),
        ("Капитал (собственные средства)", fmt(d["equity"])),
        ("Чистый процентный доход", fmt(d["net_interest_income"])),
        ("Комиссионный доход", fmt(d["fee_income"])),
        ("Операционные расходы", fmt(d["operating_expenses"])),
    ])
    _para(f"Выручка: {fmt(d['revenue'])}")
    _para(f"Чистая прибыль: {fmt(d['net_income'])}")
    _para(f"Итого активы: {fmt(d['assets'])}")
    _para(f"Обязательства: {fmt(d['liabilities'])}")
    _para(f"Собственный капитал: {fmt(d['equity'])}")
    _para(f"Чистый процентный доход: {fmt(d['net_interest_income'])}")
    _para(f"Комиссионный доход: {fmt(d['fee_income'])}")
    _para(f"Операционные расходы: {fmt(d['operating_expenses'])}")

    # CHART: metrics bar
    for name, png in charts:
        if name == 'chart_metrics.png':
            _add_image(png, 5.8)
            break

    # --- 2. Ratios ---
    _hdr("2. Ключевые коэффициенты")
    _table(["Коэффициент", "Значение"], [
        ("ROE (рентабельность капитала)", f"{report['roe']:.1f}%"),
        ("ROA (рентабельность активов)", f"{report['roa']:.2f}%"),
        ("D/E (долг / капитал)", f"{report['de']:.2f}"),
        ("NIM (чистая процентная маржа)", f"{report['nim']:.2f}%"),
        ("C/I (расходы / доходы)", f"{report['ci']:.1f}%"),
    ])

    # CHART: ratios
    for name, png in charts:
        if name == 'chart_ratios.png':
            _add_image(png, 6.0)
            break

    # CHART: income structure
    for name, png in charts:
        if name == 'chart_income.png':
            _add_image(png, 5.0)
            break

    # --- 3. Income statement ---
    _hdr("3. Отчёт о прибылях и убытках")
    _para(f"За отчётный период {report['period']} операционные доходы "
          f"{report['name_short']} составили {fmt(d['revenue'])}. "
          f"Чистый процентный доход достиг {fmt(d['net_interest_income'])}, "
          f"что отражает устойчивую процентную маржу банка.")
    _para(f"Комиссионный доход составил {fmt(d['fee_income'])}, "
          f"демонстрируя рост за счёт увеличения объёмов расчётно-кассового "
          f"обслуживания и комиссионных операций.")
    _para(f"Операционные расходы составили {fmt(d['operating_expenses'])}. "
          f"Отношение расходов к доходам (C/I) равно {report['ci']:.1f}%.")
    _para(f"Чистая прибыль за {report['period']} составила "
          f"{fmt(d['net_income'])}. Рентабельность капитала (ROE) "
          f"достигла {report['roe']:.1f}%.")

    # --- 4. Balance sheet ---
    _hdr("4. Отчёт о финансовом положении")
    _para(f"Активы {report['name_short']} по состоянию на конец "
          f"{report['period']} составили {fmt(d['assets'])}. "
          f"В структуре активов преобладают кредитный портфель и ценные бумаги.")
    _para(f"Обязательства достигли {fmt(d['liabilities'])}. "
          f"Основную долю составляют средства клиентов.")
    _para(f"Собственный капитал составил {fmt(d['equity'])}. "
          f"Достаточность капитала поддерживается на уровне, превышающем "
          f"регуляторные требования.")

    # CHART: capital structure
    for name, png in charts:
        if name == 'chart_capital.png':
            _add_image(png, 5.0)
            break

    # --- 5. Analysis ---
    _hdr("5. Анализ финансовых результатов")
    _para(f"По итогам {report['period']} {report['bank']} демонстрирует "
          f"устойчивые финансовые результаты. Выручка составила "
          f"{fmt(d['revenue'])}, чистая прибыль - {fmt(d['net_income'])}.")
    _para(f"Рентабельность капитала (ROE) на уровне {report['roe']:.1f}% "
          f"подтверждает эффективность использования собственных средств.")
    _para(f"Рентабельность активов (ROA) составила {report['roa']:.2f}%. "
          f"Чистая процентная маржа (NIM) - {report['nim']:.2f}%.")
    _para(f"Долговая нагрузка (D/E) составляет {report['de']:.2f}, "
          f"что характерно для банковского сектора.")
    _para(f"Отношение операционных расходов к доходам (C/I) на уровне "
          f"{report['ci']:.1f}%.")
    _para(f"В целом, финансовое положение {report['name_short']} "
          f"оценивается как устойчивое.")

    # CHART: summary table
    for name, png in charts:
        if name == 'summary_table.png':
            _add_image(png, 6.0)
            break

    # --- 6. Additional sections (with more charts embedded as repeats) ---
    for i in range(2, 7):
        _hdr(f"6.{i} Дополнительная информация")
        _para(f"Настоящий раздел содержит дополнительные сведения о "
              f"деятельности {report['bank']} в {report['period']}. "
              f"Финансовые показатели подтверждают устойчивое положение "
              f"банка на рынке банковских услуг.")
        _para(f"Выручка банка за отчётный период составила "
              f"{fmt(d['revenue'])}, чистая прибыль - {fmt(d['net_income'])}. "
              f"Активы достигли {fmt(d['assets'])}.")
        _para(f"Обязательства составили {fmt(d['liabilities'])}, "
              f"собственный капитал - {fmt(d['equity'])}. "
              f"Чистый процентный доход - {fmt(d['net_interest_income'])}. "
              f"Комиссионный доход - {fmt(d['fee_income'])}. "
              f"Операционные расходы - {fmt(d['operating_expenses'])}.")
        _para(f"Коэффициенты: ROE {report['roe']:.1f}%, "
              f"ROA {report['roa']:.2f}%, D/E {report['de']:.2f}, "
              f"NIM {report['nim']:.2f}%, C/I {report['ci']:.1f}%.")

        # Вставляем изображения в дополнительные секции для увеличения объёма
        if i % 2 == 0:
            for name, png in charts:
                if name == 'chart_income.png':
                    _add_image(png, 5.0)
                    break
        else:
            for name, png in charts:
                if name == 'chart_capital.png':
                    _add_image(png, 5.0)
                    break

    doc.save(path)
    print(f"  DOCX: {path}")


# ============================================================
#   ГЕНЕРАЦИЯ PDF (через docx2pdf / MS Word)
# ============================================================

def gen_pdf(report, path):
    docx_path = path.replace('.pdf', '.docx')
    if not os.path.exists(docx_path):
        print(f"  PDF:  DOCX не найден: {docx_path}")
        return
    try:
        from docx2pdf import convert as docx2pdf_convert
        docx2pdf_convert(docx_path, path)
        sz = os.path.getsize(path)
        print(f"  PDF:  {path}  ({sz/1024:.0f} КБ)")
    except Exception as e:
        print(f"  PDF:  Ошибка конвертации (требуется MS Word): {e}")


# ============================================================
#   MAIN
# ============================================================

def generate_all():
    # Очистка временной папки
    for f in os.listdir(TEMP_DIR):
        try:
            os.unlink(os.path.join(TEMP_DIR, f))
        except:
            pass

    for report in REPORTS:
        rid = report["id"]
        gen_docx(report, os.path.join(OUTPUT_DIR, f"{rid}_2024.docx"))
        if rid in ("tbank", "sovcombank"):
            gen_pdf(report, os.path.join(OUTPUT_DIR, f"{rid}_2024.pdf"))

    print(f"\nФайлы в {OUTPUT_DIR}:")
    for fname in sorted(os.listdir(OUTPUT_DIR)):
        if fname.endswith((".pdf", ".docx")) and not fname.startswith("_"):
            sz = os.path.getsize(os.path.join(OUTPUT_DIR, fname))
            print(f"  {fname}: {sz/1024:.0f} КБ")

    # Очистка
    for f in os.listdir(TEMP_DIR):
        try:
            os.unlink(os.path.join(TEMP_DIR, f))
        except:
            pass
    try:
        os.rmdir(TEMP_DIR)
    except:
        pass


if __name__ == "__main__":
    generate_all()
