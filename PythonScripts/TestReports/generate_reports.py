"""
Генератор тестовых DOCX-отчётов с реальными финансовыми данными российских банков (IFRS 2023-2024).

Зачем: LLM-извлечение (AiService) плохо парсит слабоструктурированный текст.
Отчёты содержат ЧЁТКО обозначенные финансовые показатели с явными подписями на русском языке,
чтобы модель могла соотнести "Выручка: 3 284,5 млрд руб." с полем "revenue".
Чем точнее данные размечены в DOCX, тем больше полей извлечёт AI.
"""

from docx import Document
from docx.shared import Pt, Inches, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH
import os

OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))


def add_title(doc, text):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run(text)
    run.bold = True
    run.font.size = Pt(18)


def add_subtitle(doc, text):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run(text)
    run.font.size = Pt(12)
    run.font.color.rgb = None  # gray via theme


def add_header(doc, text):
    p = doc.add_paragraph()
    run = p.add_run(text)
    run.bold = True
    run.font.size = Pt(14)


def add_metric(doc, name, value):
    p = doc.add_paragraph()
    run = p.add_run(f"{name}: ")
    run.bold = True
    run.font.size = Pt(11)
    run = p.add_run(value)
    run.font.size = Pt(11)


def add_para(doc, text):
    p = doc.add_paragraph()
    run = p.add_run(text)
    run.font.size = Pt(11)


def add_line(doc):
    doc.add_paragraph()


def generate_sberbank():
    doc = Document()
    add_title(doc, "СБЕРБАНК (ПАО)")
    add_subtitle(doc, "Консолидированная финансовая отчётность по МСФО за 2023 год")
    add_para(doc, "Дата составления: 31 декабря 2023 года")
    add_para(doc, "Валюта: Российский рубль (RUB)")
    add_line(doc)

    add_header(doc, "1. ОСНОВНЫЕ ФИНАНСОВЫЕ ПОКАЗАТЕЛИ")
    add_metric(doc, "Операционные доходы (Выручка)", "3 284,5 млрд руб.")
    add_metric(doc, "Чистые процентные доходы", "2 073,2 млрд руб.")
    add_metric(doc, "Чистые комиссионные доходы", "574,6 млрд руб.")
    add_metric(doc, "Операционные расходы", "1 467,3 млрд руб.")
    add_metric(doc, "Чистая прибыль", "1 508,6 млрд руб.")
    add_metric(doc, "Итого активы", "52 345,0 млрд руб.")
    add_metric(doc, "Обязательства", "46 700,0 млрд руб.")
    add_metric(doc, "Капитал (собственные средства)", "5 645,0 млрд руб.")
    add_line(doc)

    add_header(doc, "2. КЛЮЧЕВЫЕ КОЭФФИЦИЕНТЫ")
    add_metric(doc, "ROE (Рентабельность капитала)", "28,7%")
    add_metric(doc, "ROA (Рентабельность активов)", "2,9%")
    add_metric(doc, "Достаточность капитала Н1.0", "13,2%")
    add_metric(doc, "C/I (Отношение расходов к доходам)", "44,7%")
    add_metric(doc, "NIM (Чистая процентная маржа)", "5,6%")
    add_line(doc)

    add_header(doc, "3. КАЧЕСТВО КРЕДИТНОГО ПОРТФЕЛЯ")
    add_para(doc, "Доля просроченной задолженности (NPL): 2,0%")
    add_para(doc, "Резервы под обесценение: 1 050,0 млрд руб.")
    add_para(doc, "Покрытие NPL резервами: 180%")
    add_line(doc)

    add_header(doc, "4. АНАЛИЗ ПОКАЗАТЕЛЕЙ")
    add_para(doc,
             "Сбербанк демонстрирует рекордную чистую прибыль в 1 508,6 млрд руб., что на 24% выше показателя 2022 года.")
    add_para(doc, "Рентабельность капитала (ROE) составила 28,7%, что значительно превышает порог эффективности в 15%.")
    add_para(doc, "Отношение операционных расходов к доходам (C/I) составляет 44,7%, что ниже 50% — эффективная модель.")
    add_para(doc, "Активы банка достигли 52,3 трлн руб., увеличившись на 35% за год.")
    add_para(doc, "Доля просроченной задолженности сохраняется на низком уровне 2,0%.")

    path = os.path.join(OUTPUT_DIR, "Sberbank_Annual_Report_2023.docx")
    doc.save(path)
    print(f"  Создан: {path}")
    return path


def generate_vtb():
    doc = Document()
    add_title(doc, "БАНК ВТБ (ПАО)")
    add_subtitle(doc, "Консолидированная финансовая отчётность по МСФО за 2023 год")
    add_para(doc, "Дата составления: 31 декабря 2023 года")
    add_para(doc, "Валюта: Российский рубль (RUB)")
    add_line(doc)

    add_header(doc, "1. ОСНОВНЫЕ ФИНАНСОВЫЕ ПОКАЗАТЕЛИ")
    add_metric(doc, "Операционные доходы (Выручка)", "1 284,0 млрд руб.")
    add_metric(doc, "Чистые процентные доходы", "937,0 млрд руб.")
    add_metric(doc, "Чистые комиссионные доходы", "145,0 млрд руб.")
    add_metric(doc, "Операционные расходы", "610,0 млрд руб.")
    add_metric(doc, "Чистая прибыль", "432,2 млрд руб.")
    add_metric(doc, "Итого активы", "30 578,0 млрд руб.")
    add_metric(doc, "Обязательства", "28 650,0 млрд руб.")
    add_metric(doc, "Капитал (собственные средства)", "1 928,0 млрд руб.")
    add_line(doc)

    add_header(doc, "2. КЛЮЧЕВЫЕ КОЭФФИЦИЕНТЫ")
    add_metric(doc, "ROE (Рентабельность капитала)", "22,3%")
    add_metric(doc, "ROA (Рентабельность активов)", "1,42%")
    add_metric(doc, "Достаточность капитала Н1.0", "11,5%")
    add_metric(doc, "C/I (Отношение расходов к доходам)", "47,5%")
    add_metric(doc, "NIM (Чистая процентная маржа)", "4,2%")
    add_line(doc)

    add_header(doc, "3. КАЧЕСТВО КРЕДИТНОГО ПОРТФЕЛЯ")
    add_para(doc, "Доля просроченной задолженности (NPL): 3,5%")
    add_para(doc, "Резервы под обесценение: 540,0 млрд руб.")
    add_para(doc, "Покрытие NPL резервами: 155%")
    add_line(doc)

    add_header(doc, "4. АНАЛИЗ ПОКАЗАТЕЛЕЙ")
    add_para(doc, "ВТБ показал чистую прибыль 432,2 млрд руб. после убытка 2022 года.")
    add_para(doc, "ROE 22,3% — выше порога 15%, капитал используется эффективно.")
    add_para(doc, "C/I 47,5% — ниже 50%, операционная эффективность на хорошем уровне.")
    add_para(doc, "Активы ВТБ выросли до 30,6 трлн руб. (+25% за год).")
    add_para(doc, "Доля просрочки 3,5% выше, чем у Сбербанка (2,0%), но остаётся под контролем.")

    path = os.path.join(OUTPUT_DIR, "VTB_Financial_Summary_2023.docx")
    doc.save(path)
    print(f"  Создан: {path}")
    return path


def generate_alfabank():
    doc = Document()
    add_title(doc, "АЛЬФА-БАНК (АО)")
    add_subtitle(doc, "Консолидированная финансовая отчётность по МСФО за 1 квартал 2024 года")
    add_para(doc, "Дата составления: 31 марта 2024 года")
    add_para(doc, "Валюта: Российский рубль (RUB)")
    add_line(doc)

    add_header(doc, "1. ОСНОВНЫЕ ФИНАНСОВЫЕ ПОКАЗАТЕЛИ")
    add_metric(doc, "Операционные доходы (Выручка)", "385,0 млрд руб.")
    add_metric(doc, "Чистые процентные доходы", "208,6 млрд руб.")
    add_metric(doc, "Чистые комиссионные доходы", "55,8 млрд руб.")
    add_metric(doc, "Операционные расходы", "175,1 млрд руб.")
    add_metric(doc, "Чистая прибыль", "209,9 млрд руб.")
    add_metric(doc, "Итого активы", "8 524,0 млрд руб.")
    add_metric(doc, "Обязательства", "7 645,0 млрд руб.")
    add_metric(doc, "Капитал (собственные средства)", "879,0 млрд руб.")
    add_line(doc)

    add_header(doc, "2. КЛЮЧЕВЫЕ КОЭФФИЦИЕНТЫ")
    add_metric(doc, "ROE (Рентабельность капитала)", "23,3%")
    add_metric(doc, "ROA (Рентабельность активов)", "2,46%")
    add_metric(doc, "Достаточность капитала Н1.0", "12,1%")
    add_metric(doc, "C/I (Отношение расходов к доходам)", "45,5%")
    add_metric(doc, "NIM (Чистая процентная маржа)", "4,8%")
    add_line(doc)

    add_header(doc, "3. КАЧЕСТВО КРЕДИТНОГО ПОРТФЕЛЯ")
    add_para(doc, "Доля просроченной задолженности (NPL): 2,8%")
    add_para(doc, "Резервы под обесценение: 185,0 млрд руб.")
    add_para(doc, "Покрытие NPL резервами: 165%")
    add_line(doc)

    add_header(doc, "4. АНАЛИЗ ПОКАЗАТЕЛЕЙ")
    add_para(doc, "Альфа-Банк демонстрирует ROE 23,3% — выше порога эффективности 15%.")
    add_para(doc, "C/I 45,5% — лучший показатель среди трёх банков, ниже 50% — эффективная модель.")
    add_para(doc, "Чистая прибыль за 1 кв. 2024 года составила 209,9 млрд руб.")
    add_para(doc, "NIM 4,8% — уверенная процентная маржа выше порога 3%.")
    add_para(doc, "Доля просрочки 2,8% — умеренный уровень, ниже среднеотраслевого.")

    path = os.path.join(OUTPUT_DIR, "AlfaBank_Q1_2024_Review.docx")
    doc.save(path)
    print(f"  Создан: {path}")
    return path


def generate_all():
    print("Генерация тестовых отчётов...")
    generate_sberbank()
    generate_vtb()
    generate_alfabank()
    print(f"Готово. Файлы в {OUTPUT_DIR}")


if __name__ == "__main__":
    generate_all()
