using System.Windows;
using Diplom_1.Models;
using Diplom_1.ViewModels;

namespace Diplom_1.Views
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
