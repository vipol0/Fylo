using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastExplorer.Models;
using FastExplorer.ViewModels;

namespace FastExplorer
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel ViewModel;

        private Point _dragPoint;
        private bool _dragReady;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            StateChanged += (_, _) => UpdateMaximizeRestoreIcons();

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

        // ========== Кастомный заголовок окна ==========

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Maximized)
                    SystemCommands.RestoreWindow(this);
                else
                    SystemCommands.MaximizeWindow(this);
                return;
            }

            _dragPoint = e.GetPosition(this);
            _dragReady = true;
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragReady || e.LeftButton != MouseButtonState.Pressed)
                return;

            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragPoint.X) > 4 || Math.Abs(pos.Y - _dragPoint.Y) > 4)
            {
                _dragReady = false;
                DragMove();
            }
        }

        private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragReady = false;
        }

        private void TitleBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var point = PointToScreen(e.GetPosition(this));
            SystemCommands.ShowSystemMenu(this, point);
        }

        private void UpdateMaximizeRestoreIcons()
        {
            bool isMaximized = WindowState == WindowState.Maximized;

            if (MaximizeIcon != null)
                MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;

            if (RestoreIcon != null)
                RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;

            if (MaximizeButton != null)
                MaximizeButton.ToolTip = isMaximized ? "Восстановить" : "Развернуть";
        }
    }
}
