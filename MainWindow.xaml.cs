using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Diplom_1.ViewModels;

namespace Diplom_1
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void DebugPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is false && RootGrid.RowDefinitions.Count >= 4)
            {
                RootGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
                RootGrid.RowDefinitions[3].Height = GridLength.Auto;
            }
        }

        private static readonly Color StageDoneColor = Color.FromRgb(0, 120, 212);
        private static readonly Color ActiveColor = Color.FromRgb(0, 120, 212);
        private static readonly Color InactiveColor = Color.FromRgb(224, 224, 224);

        private void UpdateStages(double progress)
        {
            var stages = new[] {
                (Bar: Stage1Bar, Done: progress >= 20, Active: progress is > 0 and < 20),
                (Bar: Stage2Bar, Done: progress >= 60, Active: progress is >= 20 and < 60),
                (Bar: Stage3Bar, Done: progress >= 80, Active: progress is >= 60 and < 80),
                (Bar: Stage4Bar, Done: progress >= 100, Active: progress is >= 80 and < 100)
            };

            foreach (var (bar, done, active) in stages)
            {
                if (bar == null) continue;
                var target = done ? StageDoneColor : active ? ActiveColor : InactiveColor;
                if (bar.Background is SolidColorBrush brush && brush.Color == target) continue;
                bar.Background = new SolidColorBrush(target);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
            {
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.IsOpen = true;
            }
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e.OldValue == e.NewValue) return;

            var bar = (ProgressBar)sender;
            if (bar.ActualWidth <= 0) return;

            var indicator = bar.Template.FindName("PART_Indicator", bar) as FrameworkElement;
            if (indicator == null) return;

            var ratio = bar.Maximum > 0 ? bar.ActualWidth / bar.Maximum : 0;
            var oldWidth = ratio * e.OldValue;
            var newWidth = ratio * e.NewValue;

            if (Math.Abs(oldWidth - newWidth) < 0.5) return;

            var anim = new DoubleAnimation(oldWidth, newWidth, new Duration(TimeSpan.FromSeconds(0.3)))
            {
                DecelerationRatio = 0.2
            };
            indicator.BeginAnimation(FrameworkElement.WidthProperty, anim);

            UpdateStages(e.NewValue);
        }
    }
}
