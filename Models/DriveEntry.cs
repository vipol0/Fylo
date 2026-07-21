namespace FastExplorer.Models
{
    /// <summary>
    /// Лёгкая модель диска для боковой панели.
    /// </summary>
    public sealed class DriveEntry
    {
        public string RootPath { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string DriveLetter { get; init; } = string.Empty;
        public long TotalSizeBytes { get; init; }
        public long FreeSizeBytes { get; init; }
        public bool IsReady { get; init; }

        /// <summary>Доля занятого места 0..1, для мини-индикатора заполненности.</summary>
        public double UsedFraction => TotalSizeBytes > 0
            ? 1.0 - (double)FreeSizeBytes / TotalSizeBytes
            : 0;

        public string CapacityDisplay => IsReady && TotalSizeBytes > 0
            ? $"{FormatSize(TotalSizeBytes - FreeSizeBytes)} / {FormatSize(TotalSizeBytes)}"
            : string.Empty;

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
