using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FastExplorer.Helpers;
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
            InputBindings.Add(new KeyBinding(new RelayCommand(_ => FocusSearchBox()), Key.F, ModifierKeys.Control));

            InputBindings.Add(new KeyBinding(new RelayCommand(_ => ViewModel.CreateFolderCommand.Execute(null)), Key.N, ModifierKeys.Control | ModifierKeys.Shift));
            InputBindings.Add(new KeyBinding(new RelayCommand(_ => ViewModel.CreateFileCommand.Execute(null)), Key.N, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(_ => ViewModel.RenameEntryCommand.Execute(null)), Key.F2, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(new RelayCommand(_ => ViewModel.DeleteEntryCommand.Execute(null)), Key.Delete, ModifierKeys.None));

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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

        private void FilesListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var pos = Mouse.GetPosition(FilesListView);
            var result = VisualTreeHelper.HitTest(FilesListView, pos);
            if (result?.VisualHit != null)
            {
                var item = FindVisualParent<ListViewItem>(result.VisualHit);
                if (item != null)
                {
                    item.IsSelected = true;
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.EditingEntryFullPath) && ViewModel.EditingEntryFullPath != null)
            {
                Dispatcher.BeginInvoke(new Action(FocusRenameTextBox));
            }
        }

        private void FocusRenameTextBox()
        {
            var entry = FilesListView.Items.OfType<FileSystemEntry>()
                .FirstOrDefault(e => string.Equals(e.FullPath, ViewModel.EditingEntryFullPath, StringComparison.OrdinalIgnoreCase));

            if (entry == null) return;

            FilesListView.ScrollIntoView(entry);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = FilesListView.ItemContainerGenerator.ContainerFromItem(entry) as ListViewItem;
                if (container == null) return;

                var textBox = FindVisualChild<TextBox>(container);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var found = FindVisualChild<T>(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void FocusSearchBox()
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ViewModel.ClearSearchCommand.Execute(null);
                FilesListView.Focus();
                e.Handled = true;
            }
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
