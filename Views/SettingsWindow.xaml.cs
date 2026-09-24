using System.Windows;
using FinancialAnalyst.Models;
using FinancialAnalyst.ViewModels;

namespace FinancialAnalyst.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(AppSettings.Load());
        }
    }
}
