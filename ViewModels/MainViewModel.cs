using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Fylo.Helpers;
using Fylo.Models;
using Fylo.Services;

namespace Fylo.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly DriveEnumeratorService _driveEnumerator = new();
        private readonly FavoritesService _favoritesService = new();
        private readonly DirectoryReaderService _reader = new();
        private readonly FileSystemOperationService _operationService = new();
        private readonly RecycleBinService _recycleBinService = new();

        public ObservableCollection<TabViewModel> Tabs { get; } = new();

        private TabViewModel? _selectedTab;
        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value) return;
                if (_selectedTab != null)
                {
                    _selectedTab.CancelFolderSizing();
                    _selectedTab.IsSelected = false;
                    _selectedTab.PropertyChanged -= OnSelectedTabPropertyChanged;
                }
                _selectedTab = value;
                if (_selectedTab != null)
                {
                    _selectedTab.IsSelected = true;
                    _selectedTab.PropertyChanged += OnSelectedTabPropertyChanged;
                }
                OnPropertyChanged();
                OnSelectedTabChanged();
            }
        }

        private void OnSelectedTabChanged()
        {
            OnPropertyChanged(nameof(CurrentPath));
            OnPropertyChanged(nameof(AddressBarText));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(IsSearchActive));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(IsSearching));
            OnPropertyChanged(nameof(CanAddToFavorites));
            OnPropertyChanged(nameof(EditingEntryFullPath));
            OnPropertyChanged(nameof(PendingRenameText));
            OnPropertyChanged(nameof(CurrentSearchScope));
            OnPropertyChanged(nameof(SearchScopeIcon));
            OnPropertyChanged(nameof(SearchScopeTooltip));
            OnPropertyChanged(nameof(SelectedEntry));
            OnPropertyChanged(nameof(Entries));
            OnPropertyChanged(nameof(EntriesView));
            OnPropertyChanged(nameof(IsShowingRecycleBin));
            OnPropertyChanged(nameof(RecycleBinItemCount));

            SelectedTab?.ScheduleFolderSizing();

            NavigateBackCommand.RaiseCanExecuteChanged();
            NavigateForwardCommand.RaiseCanExecuteChanged();
            NavigateUpCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();
            CreateFolderCommand.RaiseCanExecuteChanged();
            CreateFileCommand.RaiseCanExecuteChanged();
            DeleteEntryCommand.RaiseCanExecuteChanged();
            RenameEntryCommand.RaiseCanExecuteChanged();
            AddToFavoritesCommand.RaiseCanExecuteChanged();
            OpenFileLocationCommand.RaiseCanExecuteChanged();
        }

        private void OnSelectedTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(TabViewModel.SelectedEntry))
            {
                OnPropertyChanged(nameof(CanAddToFavorites));
            }
            else if (e.PropertyName == nameof(TabViewModel.CurrentPath))
            {
                UpdateSidebarSelection();
                SelectedTab?.ScheduleFolderSizing();
            }
            else if (e.PropertyName is nameof(TabViewModel.IsShowingRecycleBin) or nameof(TabViewModel.RecycleBinItemCount) or nameof(TabViewModel.IsSearchActive))
            {
                OnPropertyChanged(e.PropertyName);
                CreateFolderCommand.RaiseCanExecuteChanged();
                CreateFileCommand.RaiseCanExecuteChanged();
                RenameEntryCommand.RaiseCanExecuteChanged();
            }
        }

        // ===== Forwarded properties =====

        public string CurrentPath => SelectedTab?.CurrentPath ?? string.Empty;
        public string AddressBarText
        {
            get => SelectedTab?.AddressBarText ?? string.Empty;
            set { if (SelectedTab != null) SelectedTab.AddressBarText = value; }
        }
        public string StatusText => SelectedTab?.StatusText ?? "Готово";
        public bool IsLoading => SelectedTab?.IsLoading ?? false;
        public string SearchText
        {
            get => SelectedTab?.SearchText ?? string.Empty;
            set { if (SelectedTab != null) SelectedTab.SearchText = value; }
        }
        public bool IsSearchActive => SelectedTab?.IsSearchActive ?? false;
        public bool IsSearchEmpty => SelectedTab?.IsSearchEmpty ?? false;
        public bool IsSearching => SelectedTab?.IsSearching ?? false;
        public string? EditingEntryFullPath => SelectedTab?.EditingEntryFullPath;
        public string? PendingRenameText
        {
            get => SelectedTab?.PendingRenameText;
            set { if (SelectedTab != null) SelectedTab.PendingRenameText = value; }
        }
        public SearchScope CurrentSearchScope => SelectedTab?.CurrentSearchScope ?? SearchScope.CurrentFolder;
        public string SearchScopeIcon => SelectedTab?.SearchScopeIcon ?? "📁";
        public string SearchScopeTooltip => SelectedTab?.SearchScopeTooltip ?? "Поиск в текущей папке";
        public FileSystemEntry? SelectedEntry
        {
            get => SelectedTab?.SelectedEntry;
            set { if (SelectedTab != null) SelectedTab.SelectedEntry = value; }
        }
        public ObservableCollection<FileSystemEntry> Entries => SelectedTab?.Entries ?? new();
        public ICollectionView EntriesView => SelectedTab?.EntriesView ?? CollectionViewSource.GetDefaultView(new ObservableCollection<FileSystemEntry>());
        public bool IsShowingRecycleBin => SelectedTab?.IsShowingRecycleBin ?? false;
        public int RecycleBinItemCount => SelectedTab?.RecycleBinItemCount ?? 0;

        // ===== UI State (global) =====

        private bool _isDrivePanelVisible = true;

        public bool IsDrivePanelVisible
        {
            get => _isDrivePanelVisible;
            set
            {
                if (SetField(ref _isDrivePanelVisible, value))
                {
                    OnPropertyChanged(nameof(DrivePanelToggleTooltip));
                }
            }
        }

        public string DrivePanelToggleTooltip => _isDrivePanelVisible
            ? "Скрыть панель дисков"
            : "Показать панель дисков";

        public ObservableCollection<SidebarTreeItem> SidebarItems { get; } = new();

        // ===== Forwarded Commands =====

        public RelayCommand NavigateBackCommand { get; }
        public RelayCommand NavigateForwardCommand { get; }
        public RelayCommand NavigateUpCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand GoToAddressCommand { get; }
        public RelayCommand OpenEntryCommand { get; }
        public RelayCommand SortByColumnCommand { get; }
        public RelayCommand ClearSearchCommand { get; }
        public RelayCommand ToggleSearchScopeCommand { get; }
        public RelayCommand CreateFolderCommand { get; }
        public RelayCommand CreateFileCommand { get; }
        public RelayCommand DeleteEntryCommand { get; }
        public RelayCommand RenameEntryCommand { get; }
        public RelayCommand CommitRenameCommand { get; }
        public RelayCommand CancelRenameCommand { get; }
        public RelayCommand ToggleDrivePanelCommand { get; }
        public RelayCommand AddToFavoritesCommand { get; }
        public RelayCommand RemoveFromFavoritesCommand { get; }
        public RelayCommand OpenFileLocationCommand { get; }
        public RelayCommand OpenSidebarItemLocationCommand { get; }
        public RelayCommand RestoreRecycleBinEntryCommand { get; }
        public RelayCommand EmptyRecycleBinCommand { get; }

        // ===== Tab Management Commands =====

        public RelayCommand AddTabCommand { get; }
        public RelayCommand CloseTabCommand { get; }

        public MainViewModel()
        {
            NavigateBackCommand = new RelayCommand(
                _ => SelectedTab?.NavigateBack(),
                _ => SelectedTab?.BackStackCount > 0);

            NavigateForwardCommand = new RelayCommand(
                _ => SelectedTab?.NavigateForward(),
                _ => SelectedTab?.ForwardStackCount > 0);

            NavigateUpCommand = new RelayCommand(
                _ => SelectedTab?.NavigateUp(),
                _ => SelectedTab?.CanNavigateUp() ?? false);

            RefreshCommand = new RelayCommand(
                _ =>
                {
                    if (SelectedTab == null) return;
                    if (SelectedTab.IsShowingRecycleBin)
                        _ = SelectedTab.LoadRecycleBinAsync();
                    else
                        _ = SelectedTab.LoadDirectoryAsync(SelectedTab.CurrentPath, pushHistory: false);
                },
                _ => SelectedTab != null);

            GoToAddressCommand = new RelayCommand(
                _ => SelectedTab?.GoToAddressCommand.Execute(null));

            OpenEntryCommand = new RelayCommand(
                param => SelectedTab?.OpenEntryCommand.Execute(param));

            SortByColumnCommand = new RelayCommand(
                param => SelectedTab?.SortByColumnCommand.Execute(param));

            ClearSearchCommand = new RelayCommand(
                _ => SelectedTab?.ClearSearchCommand.Execute(null));

            ToggleSearchScopeCommand = new RelayCommand(
                _ => SelectedTab?.ToggleSearchScopeCommand.Execute(null));

            CreateFolderCommand = new RelayCommand(
                _ => SelectedTab?.CreateFolderCommand.Execute(null),
                _ => SelectedTab?.CreateFolderCommand.CanExecute(null) ?? false);

            CreateFileCommand = new RelayCommand(
                _ => SelectedTab?.CreateFileCommand.Execute(null),
                _ => SelectedTab?.CreateFileCommand.CanExecute(null) ?? false);

            DeleteEntryCommand = new RelayCommand(
                _ => SelectedTab?.DeleteEntryCommand.Execute(null),
                _ => SelectedTab?.DeleteEntryCommand.CanExecute(null) ?? false);

            RenameEntryCommand = new RelayCommand(
                _ => SelectedTab?.RenameEntryCommand.Execute(null),
                _ => SelectedTab?.RenameEntryCommand.CanExecute(null) ?? false);

            CommitRenameCommand = new RelayCommand(
                _ => SelectedTab?.CommitRenameCommand.Execute(null));

            CancelRenameCommand = new RelayCommand(
                _ => SelectedTab?.CancelRenameCommand.Execute(null));

            ToggleDrivePanelCommand = new RelayCommand(
                _ => IsDrivePanelVisible = !IsDrivePanelVisible);

            AddToFavoritesCommand = new RelayCommand(
                _ => AddCurrentToFavorites(),
                _ => CanAddToFavorites);

            RemoveFromFavoritesCommand = new RelayCommand(
                param => RemoveFromFavorites(param as SidebarTreeItem));

            RestoreRecycleBinEntryCommand = new RelayCommand(
                _ => SelectedTab?.RestoreRecycleBinEntryCommand.Execute(null),
                _ => SelectedTab?.RestoreRecycleBinEntryCommand.CanExecute(null) ?? false);

            OpenFileLocationCommand = new RelayCommand(
                _ => SelectedTab?.OpenFileLocationCommand.Execute(null),
                _ => SelectedTab?.OpenFileLocationCommand.CanExecute(null) ?? false);

            OpenSidebarItemLocationCommand = new RelayCommand(param =>
            {
                if (param is SidebarTreeItem item && !string.IsNullOrEmpty(item.Path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Path) { UseShellExecute = true });
                    }
                    catch { }
                }
            });

            EmptyRecycleBinCommand = new RelayCommand(
                _ => SelectedTab?.EmptyRecycleBin());

            AddTabCommand = new RelayCommand(_ => AddTab());
            CloseTabCommand = new RelayCommand(param => CloseTab(param as TabViewModel));

            LoadSidebarItems();

            AddTab();
        }

        public bool CanAddToFavorites
        {
            get
            {
                var entry = SelectedTab?.SelectedEntry;
                return entry is { IsDirectory: true } &&
                       !_favoritesService.Contains(entry.FullPath);
            }
        }

        // ===== Tab Management =====

        public void AddTab()
        {
            AddTabAt(Tabs.Count);
        }

        public void AddTabAt(int index)
        {
            var startPath = @"C:\";
            var tab = new TabViewModel(_reader, _operationService, startPath);
            Tabs.Insert(index, tab);
            SelectedTab = tab;
            UpdateSidebarSelection();
        }

        public void CloseTab(TabViewModel? tab)
        {
            if (tab == null) return;
            if (Tabs.Count == 1)
            {
                Application.Current.Shutdown();
                return;
            }

            var oldIndex = Tabs.IndexOf(tab);

            tab.PropertyChanged -= OnSelectedTabPropertyChanged;
            Tabs.Remove(tab);

            if (tab == _selectedTab)
            {
                var newIndex = Math.Min(oldIndex, Tabs.Count - 1);
                SelectedTab = Tabs[newIndex];
            }
        }

        public void SelectTab(TabViewModel tab)
        {
            SelectedTab = tab;
            UpdateSidebarSelection();
        }

        // ===== Sidebar =====

        public void NavigateToSidebarItem(SidebarTreeItem? item)
        {
            if (item == null) return;

            SelectedTab?.ClearSearchCommand.Execute(null);

            if (item.ItemType == SidebarItemType.RecycleBin)
            {
                _ = SelectedTab?.LoadRecycleBinAsync();
                return;
            }

            if (string.IsNullOrEmpty(item.Path)) return;
            if (item.ItemType == SidebarItemType.Drive && !item.IsReady) return;
            _ = SelectedTab?.LoadDirectoryAsync(item.Path, pushHistory: true);
        }

        private void UpdateSidebarSelection()
        {
            if (SelectedTab == null) return;

            var isRecycleBin = SelectedTab.IsShowingRecycleBin;
            var currentPath = SelectedTab.CurrentPath;

            foreach (var item in SidebarItems)
            {
                if (item.Children.Count > 0)
                {
                    foreach (var child in item.Children)
                    {
                        child.IsSelected = !isRecycleBin &&
                            string.Equals(child.Path, currentPath, StringComparison.OrdinalIgnoreCase);
                    }
                }

                item.IsSelected = isRecycleBin
                    ? item.ItemType == SidebarItemType.RecycleBin
                    : string.Equals(item.Path, currentPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void LoadSidebarItems()
        {
            SidebarItems.Clear();

            var drives = _driveEnumerator.GetDrives();

            var drivesSection = new SidebarTreeItem
            {
                DisplayName = "Диски",
                Icon = "💾",
                ItemType = SidebarItemType.Section,
                IsSelectable = false,
                IsExpanded = true
            };
            foreach (var drive in drives)
            {
                drivesSection.Children.Add(new SidebarTreeItem
                {
                    DisplayName = drive.DisplayName,
                    Icon = "💾",
                    ItemType = SidebarItemType.Drive,
                    Path = drive.RootPath,
                    IsSelectable = true,
                    DriveLetter = drive.DriveLetter,
                    TotalSizeBytes = drive.TotalSizeBytes,
                    FreeSizeBytes = drive.FreeSizeBytes,
                    IsReady = drive.IsReady
                });
            }
            SidebarItems.Add(drivesSection);

            var favoritesSection = new SidebarTreeItem
            {
                DisplayName = "Избранное",
                Icon = "📁",
                ItemType = SidebarItemType.Section,
                IsSelectable = false,
                IsExpanded = true
            };
            foreach (var path in _favoritesService.GetAll())
            {
                var displayName = GetDisplayNameForPath(path);
                AddFolderChild(favoritesSection, displayName, path, isFavorite: true);
            }
            SidebarItems.Add(favoritesSection);

            var thisPc = new SidebarTreeItem
            {
                DisplayName = "Этот компьютер",
                Icon = "🖥️",
                ItemType = SidebarItemType.ThisPc,
                IsSelectable = false,
                IsExpanded = false
            };
            AddFolderChild(thisPc, "Загрузки", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            AddFolderChild(thisPc, "Рабочий стол", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            AddFolderChild(thisPc, "Видео", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            AddFolderChild(thisPc, "Документы", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddFolderChild(thisPc, "Музыка", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            AddFolderChild(thisPc, "Изображения", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            SidebarItems.Add(thisPc);

            var recycleBin = new SidebarTreeItem
            {
                DisplayName = "Корзина",
                Icon = "🗑️",
                ItemType = SidebarItemType.RecycleBin,
                IsSelectable = true,
                Path = "recyclebin:"
            };
            SidebarItems.Add(recycleBin);
        }

        private static void AddFolderChild(SidebarTreeItem parent, string displayName, string path, bool isFavorite = false)
        {
            parent.Children.Add(new SidebarTreeItem
            {
                DisplayName = displayName,
                Icon = "📁",
                ItemType = SidebarItemType.Folder,
                Path = path,
                IsSelectable = true,
                IsFavorite = isFavorite
            });
        }

        private static string GetDisplayNameForPath(string path)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (string.Equals(path, desktop, StringComparison.OrdinalIgnoreCase))
                return "Рабочий стол";
            if (string.Equals(path, downloads, StringComparison.OrdinalIgnoreCase))
                return "Загрузки";
            if (string.Equals(path, documents, StringComparison.OrdinalIgnoreCase))
                return "Документы";

            return Path.GetFileName(path.TrimEnd('\\', '/'));
        }

        private void AddCurrentToFavorites()
        {
            var entry = SelectedTab?.SelectedEntry;
            if (entry == null || !entry.IsDirectory) return;
            _favoritesService.Add(entry.FullPath);
            RebuildFavoritesSection();
            OnPropertyChanged(nameof(CanAddToFavorites));
        }

        private void RemoveFromFavorites(SidebarTreeItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.Path)) return;
            _favoritesService.Remove(item.Path);
            RebuildFavoritesSection();
            OnPropertyChanged(nameof(CanAddToFavorites));
        }

        private void RebuildFavoritesSection()
        {
            var section = SidebarItems.FirstOrDefault(s =>
                s.ItemType == SidebarItemType.Section && s.DisplayName == "Избранное");
            if (section == null) return;
            section.Children.Clear();
            foreach (var path in _favoritesService.GetAll())
            {
                var displayName = GetDisplayNameForPath(path);
                section.Children.Add(new SidebarTreeItem
                {
                    DisplayName = displayName,
                    Icon = "📁",
                    ItemType = SidebarItemType.Folder,
                    Path = path,
                    IsSelectable = true,
                    IsFavorite = true
                });
            }
        }
    }
}
