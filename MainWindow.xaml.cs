using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastExplorer.Models;
using FastExplorer.ViewModels;

namespace FastExplorer
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            SourceInitialized += (_, _) => Helpers.DarkTitleBarHelper.Apply(this);

            // Горячие клавиши
            InputBindings.Add(new KeyBinding(ViewModel.RefreshCommand, Key.F5, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(new Helpers.RelayCommand(_ => GoBackAlt()), Key.Back, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(new Helpers.RelayCommand(_ => GoUpAlt()), Key.Up, ModifierKeys.Alt));
        }

        private void GoBackAlt()
        {
            if (ViewModel.NavigateBackCommand.CanExecute(null))
                ViewModel.NavigateBackCommand.Execute(null);
        }

        private void GoUpAlt()
        {
            if (ViewModel.NavigateUpCommand.CanExecute(null))
                ViewModel.NavigateUpCommand.Execute(null);
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem { DataContext: FileSystemEntry entry })
            {
                ViewModel.OpenEntryCommand.Execute(entry);
            }
        }

        private void ListViewItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is ListViewItem { DataContext: FileSystemEntry entry })
            {
                ViewModel.OpenEntryCommand.Execute(entry);
                e.Handled = true;
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader { Tag: string columnName })
            {
                ViewModel.SortByColumnCommand.Execute(columnName);
            }
        }

        private void DriveItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Models.DriveEntry drive })
            {
                ViewModel.NavigateToDriveCommand.Execute(drive);
            }
        }
    }
}
