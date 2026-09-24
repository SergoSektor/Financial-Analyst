using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FinancialAnalyst.Models
{
    public enum DocumentStatus
    {
        NotProcessed,
        Processing,
        Completed
    }

    public enum DocumentType
    {
        Pdf,
        Word
    }

    public class DocumentInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public long FileSize { get; set; }
        public double FileSizeMb => FileSize / 1048576.0;
        public DateTime DateAdded { get; set; }

        private DocumentStatus _status;
        public DocumentStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? StoredId { get; set; }

        public bool IsComparison { get; set; }
        public DocumentComparison? ComparisonReference { get; set; }

        public string ExtractedText { get; set; } = string.Empty;

        public FinancialData? CachedFinancialData { get; set; }
        public FinancialRatios? CachedFinancialRatios { get; set; }
        public List<AiFinding>? CachedFindings { get; set; }

        public void ClearExtractedText() => ExtractedText = string.Empty;

        public void ClearCache()
        {
            CachedFinancialData = null;
            CachedFinancialRatios = null;
            CachedFindings = null;
        }
    }
}
