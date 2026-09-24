"""
Script to generate realistic Word financial reports for testing the FinancialAnalyst app.
Generates .docx files with Russian financial data for major banks.
"""
from docx import Document
from docx.shared import Pt, Inches, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
import datetime
import os

from pathlib import Path

OUTPUT_DIR = str(Path(__file__).resolve().parent / "TestReports")

def add_header(doc, title, subtitle):
    """Add a formal header to the document."""
    title_para = doc.add_paragraph()
    title_para.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = title_para.add_run(title)
    run.bold = True
    run.font.size = Pt(16)
    run.font.color.rgb = RGBColor(0, 51, 102)
    
    sub_para = doc.add_paragraph()
    sub_para.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = sub_para.add_run(subtitle)
    run.font.size = Pt(11)
    run.font.color.rgb = RGBColor(100, 100, 100)
    doc.add_paragraph()

def add_table(doc, data, headers):
    """Add a formatted table."""
    table = doc.add_table(rows=len(data)+1, cols=len(headers))
    table.style = 'Light Shading Accent 1'
    
    # Headers
    for i, h in enumerate(headers):
        cell = table.rows[0].cells[i]
        cell.text = h
        for p in cell.paragraphs:
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            for run in p.runs:
                run.bold = True
                run.font.size = Pt(10)
                
    # Data
    for r, row in enumerate(data):
        for c, val in enumerate(row):
            cell = table.rows[r+1].cells[c]
            cell.text = str(val)
            for p in cell.paragraphs:
                for run in p.runs:
                    run.font.size = Pt(10)
    doc.add_paragraph()

def generate_sberbank_report():
    """Generate a detailed report for Sberbank."""
    doc = Document()
    add_header(doc, 
               "Годовой отчет ПАО Сбербанк", 
               f"За финансовый год, завершившийся 31 декабря 2023 года\n(Консолидированные данные по МСФО)")
    
    doc.add_heading("1. Ключевые финансовые показатели", level=1)
    doc.add_paragraph(
        "По итогам 2023 года Группа Сбербанк продемонстрировала уверенный рост. "
        "Чистая прибыль составила рекордные 1 562,4 млрд рублей, что превышает показатели 2022 года на 132%. "
        "Рентабельность капитала (ROE) достигла 24,2%, что значительно выше целевого уровня в 18%."
    )
    
    sber_data = [
        ("Выручка", "5 240 100 млн руб."),
        ("Чистый процентный доход", "2 815 300 млн руб."),
        ("Чистый комиссионный доход", "982 400 млн руб."),
        ("Операционные расходы", "2 150 000 млн руб."),
        ("Чистая прибыль", "1 562 400 млн руб."),
        ("Активы", "48 720 000 млн руб."),
        ("Обязательства", "43 150 000 млн руб."),
        ("Капитал акционеров", "5 570 000 млн руб.")
    ]
    add_table(doc, sber_data, ("Показатель", "Значение"))
    
    doc.add_heading("2. Анализ качества активов", level=1)
    doc.add_paragraph(
        "Корпоративный кредитный портфель вырос на 18% г/г, достигнув 21,4 трлн руб. "
        "Доля просроченной задолженности (NPL 90+) остается на исторически низком уровне — 2,1%. "
        "Стоимость риска снизилась до 1,8% благодаря улучшению качества заемщиков."
    )

    doc.save(os.path.join(OUTPUT_DIR, "Sberbank_Annual_Report_2023.docx"))
    print("Generated: Sberbank_Annual_Report_2023.docx")

def generate_alfa_report():
    """Generate a text-heavy report for Alfa-Bank."""
    doc = Document()
    add_header(doc,
               "Финансовый обзор АО «Альфа-Банк»",
               "Результаты деятельности за 1 квартал 2024 года (по РСБУ)")

    doc.add_heading("Обзор результатов", level=1)
    doc.add_paragraph(
        "Чистая прибыль АО «Альфа-Банк» за 1 квартал 2024 года составила 68,5 млрд рублей. "
        "Активы кредитной организации увеличились на 4,2% и достигли отметки в 8 240 млрд рублей. "
        "Обязательства банка выросли до 7 450 млрд рублей, в то время как собственный капитал увеличился до 790 млрд рублей."
    )

    doc.add_heading("Детализация доходов", level=1)
    doc.add_paragraph(
        "Процентные доходы составили 185,4 млрд руб, при этом процентные расходы — 95,2 млрд руб. "
        "Чистый процентный доход — 90,2 млрд руб. "
        "Комиссионный доход (Fee income) зафиксирован на уровне 25,8 млрд руб. "
        "Операционные расходы, включая ФОТ и амортизацию, составили 42,1 млрд руб."
    )

    doc.save(os.path.join(OUTPUT_DIR, "AlfaBank_Q1_2024_Review.docx"))
    print("Generated: AlfaBank_Q1_2024_Review.docx")

def generate_vtb_mixed():
    """Generate a mixed format report for VTB."""
    doc = Document()
    add_header(doc,
               "Отчет Группы ВТБ",
               "Анализ финансовой устойчивости и показателей эффективности за 2023 год")

    doc.add_heading("Финансовая сводка", level=1)
    doc.add_paragraph("Ниже приведены основные показатели эффективности Группы ВТБ.")
    
    vtb_data = [
        ("Совокупные активы", "26 400 000 млн руб."),
        ("Обязательства", "23 800 000 млн руб."),
        ("Собственный капитал", "2 600 000 млн руб."),
        ("Выручка", "1 250 000 млн руб."),
        ("Чистый процентный доход", "650 000 млн руб."),
        ("Чистая прибыль", "355 000 млн руб.")
    ]
    add_table(doc, vtb_data, ("Статья", "Сумма"))

    doc.add_heading("Комментарий руководства", level=1)
    doc.add_paragraph(
        "Несмотря на высокую ключевую ставку, Группа ВТБ сохранила прибыльность. "
        "Операционные расходы были оптимизированы и составили 480 000 млн рублей. "
        "Отношение затрат к доходам (Cost-to-Income) улучшилось до 52%. "
        "Рентабельность активов (ROA) составила 1,35%."
    )

    doc.save(os.path.join(OUTPUT_DIR, "VTB_Financial_Summary_2023.docx"))
    print("Generated: VTB_Financial_Summary_2023.docx")

if __name__ == "__main__":
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)
    
    generate_sberbank_report()
    generate_alfa_report()
    generate_vtb_mixed()
    print("\nAll reports generated successfully.")
