using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using FastExplorer.Helpers;
using FastExplorer.Models;
using FastExplorer.Services;

namespace FastExplorer.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly DirectoryReaderService _reader = new();
        private readonly DriveEnumeratorService _driveEnumerator = new();
        private readonly List<string> _backStack = new();
        private readonly List<string> _forwardStack = new();

        private CancellationTokenSource? _loadCts;

        private string _currentPath = string.Empty;
        private string _addressBarText = string.Empty;
        private string _statusText = "Готово";
        private bool _isLoading;
        private FileSystemEntry? _selectedEntry;
        private string _sortColumn = "Name";
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        public ObservableCollection<FileSystemEntry> Entries { get; } = new();
        public ObservableCollection<DriveEntry> Drives { get; } = new();

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
            private set => SetField(ref _statusText, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        public FileSystemEntry? SelectedEntry
        {
            get => _selectedEntry;
            set => SetField(ref _selectedEntry, value);
        }

        public RelayCommand NavigateBackCommand { get; }
        public RelayCommand NavigateForwardCommand { get; }
        public RelayCommand NavigateUpCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand GoToAddressCommand { get; }
        public RelayCommand OpenEntryCommand { get; }
        public RelayCommand SortByColumnCommand { get; }
        public RelayCommand NavigateToDriveCommand { get; }

        public MainViewModel()
        {
            EntriesView = CollectionViewSource.GetDefaultView(Entries);
            EntriesView.SortDescriptions.Add(new SortDescription(nameof(FileSystemEntry.IsDirectory), ListSortDirection.Descending));
            EntriesView.SortDescriptions.Add(new SortDescription(_sortColumn, _sortDirection));

            NavigateBackCommand = new RelayCommand(_ => NavigateBack(), _ => _backStack.Count > 0);
            NavigateForwardCommand = new RelayCommand(_ => NavigateForward(), _ => _forwardStack.Count > 0);
            NavigateUpCommand = new RelayCommand(_ => NavigateUp(), _ => CanNavigateUp());
            RefreshCommand = new RelayCommand(_ => _ = LoadDirectoryAsync(CurrentPath, pushHistory: false));
            GoToAddressCommand = new RelayCommand(_ => _ = NavigateToAddressBarAsync());
            OpenEntryCommand = new RelayCommand(param => OpenEntry(param as FileSystemEntry));
            SortByColumnCommand = new RelayCommand(param => SortByColumn(param as string));
            NavigateToDriveCommand = new RelayCommand(param => NavigateToDrive(param as DriveEntry));

            LoadDrives();

            var startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _ = LoadDirectoryAsync(startPath, pushHistory: false);
        }

        private void LoadDrives()
        {
            Drives.Clear();
            foreach (var drive in _driveEnumerator.GetDrives())
                Drives.Add(drive);
        }

        private void NavigateToDrive(DriveEntry? drive)
        {
            if (drive == null || !drive.IsReady) return;
            _ = LoadDirectoryAsync(drive.RootPath, pushHistory: true);
        }

        public async Task LoadDirectoryAsync(string path, bool pushHistory)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                StatusText = "Путь не найден";
                return;
            }

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

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

                Entries.Clear();
                foreach (var dir in result.Directories) Entries.Add(dir);
                foreach (var file in result.Files) Entries.Add(file);

                CurrentPath = path;
                AddressBarText = path;
                StatusText = $"Элементов: {Entries.Count}";

                NavigateBackCommand.RaiseCanExecuteChanged();
                NavigateForwardCommand.RaiseCanExecuteChanged();
                NavigateUpCommand.RaiseCanExecuteChanged();
            }
            catch (OperationCanceledException)
            {
                // Навигация была прервана более новым запросом — это нормально
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

        private void NavigateBack()
        {
            if (_backStack.Count == 0) return;
            var target = _backStack[^1];
            _backStack.RemoveAt(_backStack.Count - 1);
            _forwardStack.Add(CurrentPath);
            _ = LoadDirectoryAsync(target, pushHistory: false);
        }

        private void NavigateForward()
        {
            if (_forwardStack.Count == 0) return;
            var target = _forwardStack[^1];
            _forwardStack.RemoveAt(_forwardStack.Count - 1);
            _backStack.Add(CurrentPath);
            _ = LoadDirectoryAsync(target, pushHistory: false);
        }

        private bool CanNavigateUp()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return false;
            var parent = Directory.GetParent(CurrentPath);
            return parent != null;
        }

        private void NavigateUp()
        {
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
                _ = LoadDirectoryAsync(parent.FullName, pushHistory: true);
        }

        private async Task NavigateToAddressBarAsync()
        {
            await LoadDirectoryAsync(AddressBarText, pushHistory: true);
        }

        private void OpenEntry(FileSystemEntry? entry)
        {
            if (entry == null) return;

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
