using System.IO;
using System.Text;
using Diplom_1.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Spire.Doc;
using Spire.Doc.Documents;

namespace Diplom_1.Services
{
    public class DocumentParserService
    {
        public DocumentInfo LoadDocument(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var docType = extension switch
            {
                ".pdf" => DocumentType.Pdf,
                ".doc" or ".docx" => DocumentType.Word,
                _ => throw new NotSupportedException($"Unsupported file format: {extension}")
            };

            var fileInfo = new FileInfo(filePath);
            var document = new DocumentInfo
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                Type = docType,
                FileSize = fileInfo.Length,
                DateAdded = DateTime.Now,
                Status = DocumentStatus.NotProcessed
            };

            document.ExtractedText = docType == DocumentType.Pdf
                ? ExtractPdfText(filePath)
                : ExtractWordText(filePath);

            return document;
        }

        private string ExtractPdfText(string filePath)
        {
            try
            {
                using var reader = new PdfReader(filePath);
                using var pdf = new PdfDocument(reader);

                int pageCount = pdf.GetNumberOfPages();
                var sb = new StringBuilder();

                for (int i = 1; i <= pageCount; i++)
                {
                    var page = pdf.GetPage(i);
                    var strategy = new LocationTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

                    if (!string.IsNullOrEmpty(pageText))
                    {
                        sb.Append(pageText);
                        sb.AppendLine();
                    }
                }

                string fullText = sb.ToString().Trim();

                if (string.IsNullOrEmpty(fullText))
                    throw new InvalidOperationException(
                        "PDF не содержит текстового слоя (возможно, это сканированный документ). " +
                        "Текст можно извлечь через OCR.");

                return fullText;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Не удалось извлечь текст из PDF: {ex.Message}", ex);
            }
        }

        private string ExtractWordText(string filePath)
        {
            var doc = new Document();
            doc.LoadFromFile(filePath);
            var sb = new StringBuilder();

            foreach (Section section in doc.Sections)
            {
                foreach (Paragraph paragraph in section.Paragraphs)
                {
                    sb.AppendLine(paragraph.Text);
                }
            }

            doc.Close();
            return sb.ToString();
        }
    }
}
