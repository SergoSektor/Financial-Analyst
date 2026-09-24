using System.Windows;
using System.Windows.Controls;
using Diplom_1.Models;

namespace Diplom_1.Views
{
    public partial class CompareSelectionWindow : Window
    {
        public DocumentInfo? SelectedDoc1 { get; private set; }
        public DocumentInfo? SelectedDoc2 { get; private set; }
        public bool Confirmed { get; private set; }

        public CompareSelectionWindow(System.Collections.Generic.List<DocumentInfo> completedDocs)
        {
            InitializeComponent();

            foreach (var doc in completedDocs)
            {
                Doc1Combo.Items.Add(doc);
                Doc2Combo.Items.Add(doc);
            }

            if (completedDocs.Count >= 2)
            {
                Doc1Combo.SelectedIndex = 0;
                Doc2Combo.SelectedIndex = 1;
                OkButton.IsEnabled = true;
            }

            Doc1Combo.SelectionChanged += OnSelectionChanged;
            Doc2Combo.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = Doc1Combo.SelectedItem != null
                && Doc2Combo.SelectedItem != null
                && Doc1Combo.SelectedItem != Doc2Combo.SelectedItem;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (Doc1Combo.SelectedItem is DocumentInfo doc1
                && Doc2Combo.SelectedItem is DocumentInfo doc2
                && doc1 != doc2)
            {
                SelectedDoc1 = doc1;
                SelectedDoc2 = doc2;
                Confirmed = true;
                DialogResult = true;
                Close();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
