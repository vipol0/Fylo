using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Fylo.Dialogs;
using Fylo.Helpers;
using Fylo.Models;
using Fylo.Services;

namespace Fylo.ViewModels
{
    public enum SearchScope { CurrentFolder, AllSubfolders }

    public sealed class TabViewModel : ViewModelBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        private readonly DirectoryReaderService _reader;
        private readonly FileSystemOperationService _operationService;
        private readonly RecycleBinService _recycleBinService = new();
        private readonly List<string> _backStack = new();
        private readonly List<string> _forwardStack = new();

        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _recursiveSearchCts;
        private CancellationTokenSource? _sizingCts;
        private SearchScope _searchScope = SearchScope.CurrentFolder;

        private string _currentPath = string.Empty;
        private string _addressBarText = string.Empty;
        private string _statusText = "Готово";
        private string _displayName = "Fast Explorer";
        private bool _isLoading;
        private FileSystemEntry? _selectedEntry;
        private string _sortColumn = "Name";
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private string _searchText = string.Empty;
        private string? _editingEntryFullPath;
        private string? _pendingRenameText;
        private bool _isSearchEmpty;
        private bool _isSearching;

        public string DisplayName
        {
            get => _displayName;
            private set => SetField(ref _displayName, value);
        }

        public bool IsDrivePanelVisible { get; set; } = true;

        private bool _isShowingRecycleBin;
        public bool IsShowingRecycleBin
        {
            get => _isShowingRecycleBin;
            set => SetField(ref _isShowingRecycleBin, value);
        }

        public int RecycleBinItemCount => Entries.Count;

        public string? EditingEntryFullPath
        {
            get => _editingEntryFullPath;
            set => SetField(ref _editingEntryFullPath, value);
        }

        public string? PendingRenameText
        {
            get => _pendingRenameText;
            set => SetField(ref _pendingRenameText, value);
        }

        public ObservableCollection<FileSystemEntry> Entries { get; } = new();

        public ICollectionView EntriesView { get; }

        public string CurrentPath
        {
            get => _currentPath;
            private set => SetField(ref _currentPath, value);
        }

        public string AddressBarText
        {
            get => _addressBarText;
            set => SetField(ref _addressBarText, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    if (_searchScope == SearchScope.CurrentFolder)
                    {
                        EntriesView.Refresh();
                        UpdateStatusAfterFilter();
                    }
                    else
                    {
                        var roots = DriveInfo.GetDrives()
                            .Where(d => d.IsReady)
                            .Select(d => d.RootDirectory.FullName)
                            .ToList();
                        _ = RunRecursiveSearchAsync(roots, value);
                    }
                    OnPropertyChanged(nameof(IsSearchActive));
                    CreateFolderCommand.RaiseCanExecuteChanged();
                    CreateFileCommand.RaiseCanExecuteChanged();
                    RenameEntryCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsSearchActive => !string.IsNullOrEmpty(_searchText);

        public bool IsSearchEmpty
        {
            get => _isSearchEmpty;
            set => SetField(ref _isSearchEmpty, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetField(ref _isSearching, value);
        }

        public SearchScope CurrentSearchScope
        {
            get => _searchScope;
            set
            {
                if (SetField(ref _searchScope, value))
                {
                    OnPropertyChanged(nameof(SearchScopeIcon));
                    OnPropertyChanged(nameof(SearchScopeTooltip));

                    _recursiveSearchCts?.Cancel();

                    if (value == SearchScope.AllSubfolders)
                    {
                        if (IsSearchActive)
                        {
                            var roots = DriveInfo.GetDrives()
                                .Where(d => d.IsReady)
                                .Select(d => d.RootDirectory.FullName)
                                .ToList();
                            _ = RunRecursiveSearchAsync(roots, _searchText);
                        }
                    }
                    else if (IsSearchActive)
                    {
                        _ = LoadDirectoryAsync(CurrentPath, pushHistory: false);
                    }
                }
            }
        }

        public string SearchScopeIcon => _searchScope == SearchScope.CurrentFolder ? "📁" : "🌐";

        public string SearchScopeTooltip => _searchScope == SearchScope.CurrentFolder
            ? "Поиск в текущей папке"
            : "Поиск во всех подпапках";

        public FileSystemEntry? SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (SetField(ref _selectedEntry, value))
                {
                    OnPropertyChanged(nameof(CanAddToFavorites));
                }
            }
        }

        public bool CanAddToFavorites =>
            SelectedEntry is { IsDirectory: true };

        public int BackStackCount => _backStack.Count;
        public int ForwardStackCount => _forwardStack.Count;

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
        public RelayCommand OpenFileLocationCommand { get; }
        public RelayCommand RestoreRecycleBinEntryCommand { get; }

        public TabViewModel(DirectoryReaderService reader, FileSystemOperationService operationService, string startPath)
        {
            _reader = reader;
            _operationService = operationService;

            EntriesView = CollectionViewSource.GetDefaultView(Entries);
            EntriesView.SortDescriptions.Add(new SortDescription(nameof(FileSystemEntry.IsDirectory), ListSortDirection.Descending));
            EntriesView.SortDescriptions.Add(new SortDescription(_sortColumn, _sortDirection));
            EntriesView.Filter = FilterPredicate;

            NavigateBackCommand = new RelayCommand(_ => NavigateBack(), _ => _backStack.Count > 0);
            NavigateForwardCommand = new RelayCommand(_ => NavigateForward(), _ => _forwardStack.Count > 0);
            NavigateUpCommand = new RelayCommand(_ => NavigateUp(), _ => CanNavigateUp());
            RefreshCommand = new RelayCommand(_ =>
            {
                if (_isShowingRecycleBin)
                    _ = LoadRecycleBinAsync();
                else
                    _ = LoadDirectoryAsync(CurrentPath, pushHistory: false);
            });
            GoToAddressCommand = new RelayCommand(_ => _ = NavigateToAddressBarAsync());
            OpenEntryCommand = new RelayCommand(param => OpenEntry(param as FileSystemEntry));
            SortByColumnCommand = new RelayCommand(param => SortByColumn(param as string));
            ClearSearchCommand = new RelayCommand(_ => ClearSearch());
            ToggleSearchScopeCommand = new RelayCommand(_ => ToggleSearchScope());

            CreateFolderCommand = new RelayCommand(_ => CreateNewFolder(), _ => !string.IsNullOrEmpty(CurrentPath) && !_isShowingRecycleBin && !IsSearchActive);
            CreateFileCommand = new RelayCommand(_ => CreateNewFile(), _ => !string.IsNullOrEmpty(CurrentPath) && !_isShowingRecycleBin && !IsSearchActive);
            DeleteEntryCommand = new RelayCommand(_ => DeleteSelectedEntry(), _ => SelectedEntry != null);
            RenameEntryCommand = new RelayCommand(_ => StartRename(), _ => SelectedEntry != null && !_isShowingRecycleBin && !IsSearchActive);
            CommitRenameCommand = new RelayCommand(_ => CommitRename());
            CancelRenameCommand = new RelayCommand(_ => CancelRename());

            RestoreRecycleBinEntryCommand = new RelayCommand(
                _ => RestoreSelectedRecycleBinEntry(),
                _ => SelectedEntry != null && _isShowingRecycleBin);

            OpenFileLocationCommand = new RelayCommand(
                _ => OpenFileLocation(),
                _ => SelectedEntry != null);

            _ = LoadDirectoryAsync(startPath, pushHistory: false);
        }

        private void UpdateDisplayName()
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                DisplayName = "Fast Explorer";
                return;
            }

            var root = Path.GetPathRoot(_currentPath);
            if (root != null && string.Equals(_currentPath, root, StringComparison.OrdinalIgnoreCase))
            {
                DisplayName = root.TrimEnd('\\');
            }
            else
            {
                DisplayName = Path.GetFileName(_currentPath.TrimEnd('\\', '/'));
            }
        }

        public bool CanNavigateUp()
        {
            if (_isShowingRecycleBin) return _backStack.Count > 0;
            if (string.IsNullOrEmpty(CurrentPath)) return false;
            var parent = Directory.GetParent(CurrentPath);
            return parent != null;
        }

        public async Task LoadDirectoryAsync(string path, bool pushHistory)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                StatusText = "Путь не найден";
                return;
            }

            _recursiveSearchCts?.Cancel();
            _sizingCts?.Cancel();
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            if (_isShowingRecycleBin)
            {
                IsShowingRecycleBin = false;
                OnPropertyChanged(nameof(RecycleBinItemCount));
            }

            if (_searchScope != SearchScope.CurrentFolder &&
                !string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                _searchScope = SearchScope.CurrentFolder;
                OnPropertyChanged(nameof(CurrentSearchScope));
                OnPropertyChanged(nameof(SearchScopeIcon));
                OnPropertyChanged(nameof(SearchScopeTooltip));
            }

            IsLoading = true;
            StatusText = "Загрузка...";

            try
            {
                var result = await _reader.ReadDirectoryAsync(path, token);

                if (token.IsCancellationRequested) return;

                if (pushHistory && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
                {
                    _backStack.Add(CurrentPath);
                    _forwardStack.Clear();
                }

                foreach (var entry in Entries)
                    if (entry.SizeBytes >= 0)
                        _reader.SetCachedSize(entry.FullPath, entry.SizeBytes);

                Entries.Clear();
                foreach (var dir in result.Directories) Entries.Add(dir);
                foreach (var file in result.Files) Entries.Add(file);

                CurrentPath = path;
                AddressBarText = path;
                UpdateDisplayName();
                UpdateStatusAfterFilter();

                NavigateBackCommand.RaiseCanExecuteChanged();
                NavigateForwardCommand.RaiseCanExecuteChanged();
                NavigateUpCommand.RaiseCanExecuteChanged();
            }
            catch (OperationCanceledException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                StatusText = "Нет доступа к папке";
            }
            catch (Exception ex)
            {
                StatusText = $"Ошибка: {ex.Message}";
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        public void NavigateBack()
        {
            if (_backStack.Count == 0) return;
            var target = _backStack[^1];
            _backStack.RemoveAt(_backStack.Count - 1);
            _forwardStack.Add(_isShowingRecycleBin ? "recyclebin:" : CurrentPath);

            if (target == "recyclebin:")
            {
                _ = LoadRecycleBinAsync(pushHistory: false);
                return;
            }

            _ = LoadDirectoryAsync(target, pushHistory: false);
        }

        public void NavigateForward()
        {
            if (_forwardStack.Count == 0) return;
            var target = _forwardStack[^1];
            _forwardStack.RemoveAt(_forwardStack.Count - 1);
            _backStack.Add(CurrentPath);

            if (target == "recyclebin:")
            {
                _ = LoadRecycleBinAsync(pushHistory: false);
                return;
            }

            _ = LoadDirectoryAsync(target, pushHistory: false);
        }

        public void NavigateUp()
        {
            if (_isShowingRecycleBin)
            {
                if (_backStack.Count > 0)
                {
                    NavigateBack();
                }
                return;
            }

            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
                _ = LoadDirectoryAsync(parent.FullName, pushHistory: true);
        }

        private async Task NavigateToAddressBarAsync()
        {
            await LoadDirectoryAsync(AddressBarText, pushHistory: true);
        }

        public void OpenEntry(FileSystemEntry? entry)
        {
            if (entry == null) return;

            if (_isShowingRecycleBin)
            {
                RestoreSelectedRecycleBinEntry();
                return;
            }

            if (entry.IsDirectory)
            {
                _ = LoadDirectoryAsync(entry.FullPath, pushHistory: true);
            }
            else
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(entry.FullPath)
                    {
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception ex)
                {
                    StatusText = $"Не удалось открыть файл: {ex.Message}";
                }
            }
        }

        private bool FilterPredicate(object obj)
        {
            if (string.IsNullOrEmpty(_searchText))
                return true;
            if (obj is FileSystemEntry entry)
                return entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        private void CreateNewFolder()
        {
            var baseName = "Новая папка";
            var path = _operationService.CreateDirectory(CurrentPath, baseName);
            var dirInfo = new DirectoryInfo(path);
            var entry = FileSystemEntry.FromDirectory(dirInfo);
            Entries.Add(entry);
            SelectedEntry = entry;
            StartRename();
        }

        private void CreateNewFile()
        {
            var baseName = "Новый файл.txt";
            var path = _operationService.CreateFile(CurrentPath, baseName);
            var fileInfo = new FileInfo(path);
            var entry = FileSystemEntry.FromFile(fileInfo);
            Entries.Add(entry);
            SelectedEntry = entry;
            StartRename();
        }

        private void DeleteSelectedEntry()
        {
            var entry = SelectedEntry;
            if (entry == null) return;

            if (_isShowingRecycleBin)
            {
                var type = entry.IsDirectory ? "папку" : "файл";
                var message = $"Вы уверены, что хотите удалить {type} \"{entry.Name}\" навсегда?\nЭто действие нельзя отменить.";
                var dialog = new ConfirmDeleteDialog(message);
                dialog.ShowDialog();

                if (!dialog.Confirmed) return;

                var success = _operationService.DeletePermanent(entry.OriginalPath, entry.IsDirectory);
                if (success)
                {
                    Entries.Remove(entry);
                    OnPropertyChanged(nameof(RecycleBinItemCount));
                    StatusText = $"Удалено навсегда: {entry.Name}";
                }
                else
                {
                    StatusText = "Не удалось удалить элемент";
                }
                return;
            }

            var type2 = entry.IsDirectory ? "папку" : "файл";
            var message2 = $"Вы уверены, что хотите удалить {type2} \"{entry.Name}\"?\nОн будет перемещён в корзину.";
            var dialog2 = new ConfirmDeleteDialog(message2);
            dialog2.ShowDialog();

            if (!dialog2.Confirmed) return;

            var success2 = _operationService.DeleteToRecycleBin(entry.FullPath, entry.IsDirectory);
            if (success2)
            {
                if (entry.IsDirectory) _reader.InvalidateSize(entry.FullPath);
                Entries.Remove(entry);
                StatusText = $"Удалено: {entry.Name}";
            }
            else
            {
                StatusText = "Не удалось удалить элемент";
            }
        }

        public async Task LoadRecycleBinAsync(bool pushHistory = true)
        {
            _recursiveSearchCts?.Cancel();
            _sizingCts?.Cancel();
            _loadCts?.Cancel();

            if (_isShowingRecycleBin) return;

            IsLoading = true;
            StatusText = "Загрузка корзины...";

            if (pushHistory && !string.IsNullOrEmpty(CurrentPath))
            {
                _backStack.Add(CurrentPath);
                _forwardStack.Clear();
            }

            IsShowingRecycleBin = true;

            try
            {
                var items = await Task.Run(() => _recycleBinService.GetItems());

                Entries.Clear();

                foreach (var item in items)
                {
                    Entries.Add(new FileSystemEntry
                    {
                        Name = item.Name,
                        FullPath = item.OriginalPath,
                        OriginalPath = item.RecycleBinPath,
                        SizeBytes = item.SizeBytes,
                        Modified = item.DateDeleted,
                        IsDirectory = item.IsDirectory,
                        Extension = item.Extension,
                        IsRecycleBinItem = true,
                        RelativePath = GetParentPath(item.OriginalPath)
                    });
                }

                CurrentPath = "Корзина";
                AddressBarText = "Корзина";
                DisplayName = "Корзина";
                StatusText = $"Элементов: {Entries.Count}";
                IsSearchEmpty = false;
                OnPropertyChanged(nameof(RecycleBinItemCount));
                NavigateBackCommand.RaiseCanExecuteChanged();
                NavigateForwardCommand.RaiseCanExecuteChanged();
                NavigateUpCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusText = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenFileLocation()
        {
            _ = OpenFileLocationAsync();
        }

        private async Task OpenFileLocationAsync()
        {
            var entry = SelectedEntry;
            if (entry == null) return;

            try
            {
                SearchText = string.Empty;

                var parentPath = Path.GetDirectoryName(entry.FullPath.TrimEnd('\\'));
                if (string.IsNullOrEmpty(parentPath)) return;

                await LoadDirectoryAsync(parentPath, pushHistory: true);

                var target = Entries.FirstOrDefault(e =>
                    string.Equals(e.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                    SelectedEntry = target;

                StatusText = $"Открыто расположение: {entry.Name}";
            }
            catch (Exception ex)
            {
                StatusText = $"Не удалось открыть расположение: {ex.Message}";
            }
        }

        private void RestoreSelectedRecycleBinEntry()
        {
            var entry = SelectedEntry;
            if (entry == null || !_isShowingRecycleBin) return;

            var success = _recycleBinService.RestoreItem(entry.FullPath);
            if (success)
            {
                Entries.Remove(entry);
                OnPropertyChanged(nameof(RecycleBinItemCount));
                StatusText = $"Восстановлено: {entry.Name}";
            }
            else
            {
                StatusText = "Не удалось восстановить элемент";
            }
        }

        public void EmptyRecycleBin()
        {
            var message = "Вы уверены, что хотите очистить корзину?\nВсе элементы будут удалены навсегда.";
            var dialog = new ConfirmDeleteDialog(message);
            dialog.ShowDialog();

            if (!dialog.Confirmed) return;

            _recycleBinService.EmptyBin(IntPtr.Zero);
            Entries.Clear();
            OnPropertyChanged(nameof(RecycleBinItemCount));
            StatusText = "Корзина очищена";
        }

        private static string GetParentPath(string fullPath)
        {
            try
            {
                var parent = Path.GetDirectoryName(fullPath.TrimEnd('\\'));
                return parent ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void StartRename()
        {
            var entry = SelectedEntry;
            if (entry == null) return;

            EditingEntryFullPath = entry.FullPath;
            PendingRenameText = entry.Name;
        }

        private void CommitRename()
        {
            if (EditingEntryFullPath == null) return;

            var newName = PendingRenameText;
            if (string.IsNullOrWhiteSpace(newName))
            {
                CancelRename();
                return;
            }

            var entry = Entries.FirstOrDefault(e =>
                string.Equals(e.FullPath, EditingEntryFullPath, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                EditingEntryFullPath = null;
                return;
            }

            var success = _operationService.Rename(EditingEntryFullPath, newName, entry.IsDirectory);
            EditingEntryFullPath = null;
            PendingRenameText = null;

            if (success)
            {
                if (entry.IsDirectory && EditingEntryFullPath != null)
                    _reader.InvalidateSize(EditingEntryFullPath);
                _ = LoadDirectoryAsync(CurrentPath, pushHistory: false);
            }
            else
            {
                StatusText = "Не удалось переименовать";
            }
        }

        private void CancelRename()
        {
            EditingEntryFullPath = null;
            PendingRenameText = null;
        }

        private void ToggleSearchScope()
        {
            CurrentSearchScope = _searchScope == SearchScope.CurrentFolder
                ? SearchScope.AllSubfolders
                : SearchScope.CurrentFolder;
        }

        private static readonly HashSet<string> SkippedDirNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "windows", "windows.old",
            "program files", "program files (x86)",
            "programdata",
            "$recycle.bin",
            "system volume information",
            "recovery",
            "winsxs",
            "installer",
            "assembly",
            "nativeimages",
            "servicepackfiles",
            "config.msi",
            "msocache",
            "$winreagent",
            "windowsapps",
            "$getcurrent",
            "$windows.~bt",
            "$windows.~ws"
        };

        private async Task RunRecursiveSearchAsync(IEnumerable<string> rootPaths, string searchText)
        {
            _recursiveSearchCts?.Cancel();
            _recursiveSearchCts = new CancellationTokenSource();
            var token = _recursiveSearchCts.Token;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                await LoadDirectoryAsync(CurrentPath, pushHistory: false);
                return;
            }

            var roots = rootPaths as IReadOnlyList<string> ?? rootPaths.ToList();
            if (roots.Count == 0)
            {
                StatusText = "Нет доступных дисков";
                return;
            }

            IsLoading = true;
            IsSearching = true;
            StatusText = "Поиск...";
            Entries.Clear();

            int totalFound = 0;
            var batch = new List<FileSystemEntry>();
            var batchLock = new object();

            await Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(roots, new ParallelOptions
                    {
                        CancellationToken = token,
                        MaxDegreeOfParallelism = Math.Min(roots.Count, 4)
                    }, root =>
                    {
                        var stack = new Stack<string>();
                        stack.Push(root);

                        while (stack.Count > 0)
                        {
                            if (token.IsCancellationRequested) return;

                            var currentDir = stack.Pop();

                            var leafName = Path.GetFileName(currentDir);
                            if (!string.IsNullOrEmpty(leafName) && SkippedDirNames.Contains(leafName))
                                continue;

                            string[] subDirs;
                            string[] files;

                            try
                            {
                                subDirs = Directory.GetDirectories(currentDir);
                            }
                            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or IOException)
                            {
                                subDirs = Array.Empty<string>();
                            }

                            try
                            {
                                files = Directory.GetFiles(currentDir);
                            }
                            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or IOException)
                            {
                                files = Array.Empty<string>();
                            }

                            foreach (var subDir in subDirs)
                            {
                                if (token.IsCancellationRequested) return;
                                stack.Push(subDir);

                                try
                                {
                                    var dirName = Path.GetFileName(subDir);
                                    if (!dirName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    var dirInfo = new DirectoryInfo(subDir);
                                    var relPath = Path.GetDirectoryName(subDir)!.TrimEnd('\\');

                                    lock (batchLock)
                                    {
                                        batch.Add(new FileSystemEntry
                                        {
                                            Name = dirName,
                                            FullPath = subDir,
                                            IsDirectory = true,
                                            Modified = FileSystemEntry.SafeGetLastWrite(dirInfo),
                                            RelativePath = relPath
                                        });
                                        totalFound++;

                                        if (batch.Count >= 100)
                                        {
                                            var copy = batch.ToList();
                                            batch.Clear();
                                            var captured = totalFound;

                                            Application.Current.Dispatcher.InvokeAsync(() =>
                                            {
                                                foreach (var e in copy) Entries.Add(e);
                                                IsSearching = false;
                                                StatusText = $"Найдено: {captured} (сканирование...)";
                                            }, System.Windows.Threading.DispatcherPriority.Background);
                                        }
                                    }
                                }
                                catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or IOException)
                                {
                                }
                            }

                            foreach (var file in files)
                            {
                                if (token.IsCancellationRequested) return;

                                try
                                {
                                    var fileName = Path.GetFileName(file);
                                    if (!fileName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    var fileInfo = new FileInfo(file);
                                    var relPath = Path.GetDirectoryName(file)!.TrimEnd('\\');

                                    lock (batchLock)
                                    {
                                        batch.Add(new FileSystemEntry
                                        {
                                            Name = fileName,
                                            FullPath = file,
                                            IsDirectory = false,
                                            SizeBytes = FileSystemEntry.SafeGetLength(fileInfo),
                                            Modified = FileSystemEntry.SafeGetLastWrite(fileInfo),
                                            Extension = fileInfo.Extension,
                                            RelativePath = relPath
                                        });
                                        totalFound++;

                                        if (batch.Count >= 100)
                                        {
                                            var copy = batch.ToList();
                                            batch.Clear();
                                            var captured = totalFound;

                                            Application.Current.Dispatcher.InvokeAsync(() =>
                                            {
                                                foreach (var e in copy) Entries.Add(e);
                                                IsSearching = false;
                                                StatusText = $"Найдено: {captured} (сканирование...)";
                                            }, System.Windows.Threading.DispatcherPriority.Background);
                                        }
                                    }
                                }
                                catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or IOException)
                                {
                                }
                            }
                        }
                    });
                }
                catch (OperationCanceledException) { }
                catch (AggregateException ae) when (token.IsCancellationRequested)
                {
                    ae.Handle(ex => ex is OperationCanceledException);
                }
            }, token);

            if (token.IsCancellationRequested) return;

            List<FileSystemEntry> remaining;
            lock (batchLock)
            {
                remaining = batch.ToList();
                totalFound += remaining.Count;
                batch.Clear();
            }

            if (remaining.Count > 0)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var e in remaining) Entries.Add(e);
                    IsSearching = false;
                }, System.Windows.Threading.DispatcherPriority.Background);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                EntriesView.Refresh();
                IsSearchEmpty = totalFound == 0;
                IsSearching = false;
                StatusText = totalFound > 0
                    ? $"Найдено: {totalFound}"
                    : "Ничего не найдено";
                IsLoading = false;
            });
        }

        private void UpdateStatusAfterFilter()
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                StatusText = $"Элементов: {Entries.Count}";
                IsSearchEmpty = false;
            }
            else
            {
                var count = EntriesView.Cast<object>().Count();
                StatusText = $"Найдено: {count} из {Entries.Count}";
                IsSearchEmpty = count == 0;
            }
        }

        public void CancelFolderSizing()
        {
            _debounceCts?.Cancel();
            _sizingCts?.Cancel();
        }

        private CancellationTokenSource? _debounceCts;

        public void ScheduleFolderSizing()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            _ = DebounceThenSizeAsync(token);
        }

        private async Task DebounceThenSizeAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(1500, token);
                if (!token.IsCancellationRequested)
                    await RunFolderSizingAsync(token);
            }
            catch (OperationCanceledException) { }
        }

        private async Task RunFolderSizingAsync(CancellationToken token)
        {
            _sizingCts?.Cancel();
            _sizingCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var linkedToken = _sizingCts.Token;

            var dirs = Entries.Where(e => e.IsDirectory).ToList();
            if (dirs.Count == 0) return;

            var semaphore = new SemaphoreSlim(3);

            var tasks = dirs.Select(async entry =>
            {
                await semaphore.WaitAsync(linkedToken);
                try
                {
                    var size = await DirectoryReaderService.CalculateFolderSizeAsync(entry.FullPath, linkedToken);
                    _reader.SetCachedSize(entry.FullPath, size);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        entry.SizeBytes = size;
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (OperationCanceledException) { }
                catch (Exception)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (entry.SizeBytes < 0) entry.SizeBytes = 0;
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
        }

        private void SortByColumn(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;

            _sortDirection = (_sortColumn == columnName && _sortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _sortColumn = columnName;

            EntriesView.SortDescriptions.Clear();
            EntriesView.SortDescriptions.Add(new SortDescription(nameof(FileSystemEntry.IsDirectory), ListSortDirection.Descending));
            EntriesView.SortDescriptions.Add(new SortDescription(_sortColumn, _sortDirection));
        }
    }
}
