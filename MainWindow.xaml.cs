using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private bool _isAnimating;
        private bool _pendingToggle;

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
            SyncIconState();
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

        private void SidebarTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SidebarTreeItem item)
            {
                ViewModel.NavigateToSidebarItem(item);
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

        private void FilesListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Shift) return;

            var scrollViewer = FindVisualChild<ScrollViewer>(FilesListView);
            if (scrollViewer == null) return;

            e.Handled = true;
            if (e.Delta > 0)
                scrollViewer.LineLeft();
            else
                scrollViewer.LineRight();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.EditingEntryFullPath) && ViewModel.EditingEntryFullPath != null)
            {
                Dispatcher.BeginInvoke(new Action(FocusRenameTextBox));
            }
            else if (e.PropertyName == nameof(MainViewModel.IsDrivePanelVisible))
            {
                AnimateDrivePanel();
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

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel.EditingEntryFullPath != null)
                ViewModel.CancelRenameCommand.Execute(null);
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

        private void SyncIconState()
        {
            HidePanelIcon.Opacity = ViewModel.IsDrivePanelVisible ? 1 : 0;
            ShowPanelIcon.Opacity = ViewModel.IsDrivePanelVisible ? 0 : 1;
        }

        private void AnimateDrivePanel()
        {
            if (_isAnimating)
            {
                _pendingToggle = true;
                return;
            }

            bool shouldBeVisible = ViewModel.IsDrivePanelVisible;
            bool isVisible = LeftPanelBorder.Visibility == Visibility.Visible;
            if (shouldBeVisible == isVisible)
                return;

            _isAnimating = true;
            _pendingToggle = false;

            double currentWidth = LeftPanelColumn.ActualWidth;
            double currentOpacity = LeftPanelBorder.Opacity;

            LeftPanelColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            LeftPanelBorder.BeginAnimation(UIElement.OpacityProperty, null);
            HidePanelIcon.BeginAnimation(UIElement.OpacityProperty, null);
            ShowPanelIcon.BeginAnimation(UIElement.OpacityProperty, null);

            LeftPanelColumn.Width = new GridLength(Math.Max(0, currentWidth));
            LeftPanelBorder.Opacity = currentOpacity;

            if (shouldBeVisible)
                AnimateDrivePanelShow();
            else
                AnimateDrivePanelHide();
        }

        private void AnimateDrivePanelHide()
        {
            LeftPanelColumn.MinWidth = 0;

            var widthAnim = new GridLengthAnimation
            {
                From = LeftPanelColumn.Width.Value,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            widthAnim.Completed += (_, _) =>
            {
                LeftPanelBorder.Visibility = Visibility.Collapsed;
                _isAnimating = false;
                if (_pendingToggle)
                    AnimateDrivePanel();
            };
            LeftPanelColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnim);

            var opacityAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            LeftPanelBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

            HidePanelIcon.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)));
            ShowPanelIcon.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        private void AnimateDrivePanelShow()
        {
            LeftPanelBorder.Visibility = Visibility.Visible;

            LeftPanelColumn.MinWidth = 0;
            var widthAnim = new GridLengthAnimation
            {
                From = LeftPanelColumn.Width.Value,
                To = 200,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            widthAnim.Completed += (_, _) =>
            {
                LeftPanelColumn.MinWidth = 200;
                SidebarTree.InvalidateVisual();
                _isAnimating = false;
                if (_pendingToggle)
                    AnimateDrivePanel();
            };
            LeftPanelColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnim);

            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            LeftPanelBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

            HidePanelIcon.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            ShowPanelIcon.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)));
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
