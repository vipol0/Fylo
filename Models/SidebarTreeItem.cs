using System.Collections.ObjectModel;
using FastExplorer.ViewModels;

namespace FastExplorer.Models
{
    public enum SidebarItemType
    {
        Section,
        ThisPc,
        Drive,
        Folder
    }

    public sealed class SidebarTreeItem : ViewModelBase
    {
        private bool _isExpanded = true;
        private bool _isSelected;

        public string DisplayName { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public SidebarItemType ItemType { get; init; }
        public string? Path { get; init; }
        public bool IsSelectable { get; init; } = true;
        public bool IsFavorite { get; init; }

        public long TotalSizeBytes { get; init; }
        public long FreeSizeBytes { get; init; }
        public bool IsReady { get; init; }
        public string? DriveLetter { get; init; }

        public double UsedFraction => TotalSizeBytes > 0
            ? 1.0 - (double)FreeSizeBytes / TotalSizeBytes
            : 0;

        public string CapacityDisplay => IsReady && TotalSizeBytes > 0
            ? $"{FormatSize(TotalSizeBytes - FreeSizeBytes)} / {FormatSize(TotalSizeBytes)} · {FormatSize(FreeSizeBytes)} свободно"
            : string.Empty;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public ObservableCollection<SidebarTreeItem> Children { get; } = new();

        private static string FormatSize(long bytes)
        {
            string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return unitIndex == 0
                ? $"{size:0} {units[unitIndex]}"
                : $"{size:0.#} {units[unitIndex]}";
        }
    }
}
