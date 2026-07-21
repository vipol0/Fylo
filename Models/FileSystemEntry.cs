using System;
using System.IO;

namespace FastExplorer.Models
{
    /// <summary>
    /// Лёгкая модель элемента файловой системы (файл или папка).
    /// Не хранит лишних данных, чтобы список из десятков тысяч файлов
    /// оставался быстрым (важно для скорости и легковесности).
    /// </summary>
    public sealed class FileSystemEntry
    {
        public string Name { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public bool IsDirectory { get; init; }
        public long SizeBytes { get; init; }
        public DateTime Modified { get; init; }
        public string Extension { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;

        public string DisplayName => string.IsNullOrEmpty(RelativePath) ? Name : $@"{RelativePath}\{Name}";

        public bool HasRelativePath => !string.IsNullOrEmpty(RelativePath);

        /// <summary>Человекочитаемый размер (пусто для папок).</summary>
        public string SizeDisplay => IsDirectory ? string.Empty : FormatSize(SizeBytes);

        /// <summary>Тип для колонки "Тип".</summary>
        public string TypeDisplay => IsDirectory
            ? "Папка с файлами"
            : (string.IsNullOrEmpty(Extension) ? "Файл" : $"Файл \"{Extension.TrimStart('.').ToUpperInvariant()}\"");

        public static FileSystemEntry FromDirectory(DirectoryInfo dir)
        {
            return new FileSystemEntry
            {
                Name = dir.Name,
                FullPath = dir.FullName,
                IsDirectory = true,
                SizeBytes = 0,
                Modified = SafeGetLastWrite(dir),
                Extension = string.Empty
            };
        }

        public static FileSystemEntry FromFile(FileInfo file)
        {
            return new FileSystemEntry
            {
                Name = file.Name,
                FullPath = file.FullName,
                IsDirectory = false,
                SizeBytes = SafeGetLength(file),
                Modified = SafeGetLastWrite(file),
                Extension = file.Extension
            };
        }

        internal static DateTime SafeGetLastWrite(FileSystemInfo info)
        {
            try { return info.LastWriteTime; }
            catch { return DateTime.MinValue; }
        }

        internal static long SafeGetLength(FileInfo file)
        {
            try { return file.Length; }
            catch { return 0; }
        }

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
                : $"{size:0.##} {units[unitIndex]}";
        }
    }
}
