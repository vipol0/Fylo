using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Fylo.Services
{
    public sealed class RecycleBinItem
    {
        public string Name { get; init; } = string.Empty;
        public string OriginalPath { get; init; } = string.Empty;
        public string RecycleBinPath { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public DateTime DateDeleted { get; init; }
        public bool IsDirectory { get; init; }
        public string Extension { get; init; } = string.Empty;
    }

    public sealed class RecycleBinService
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public List<RecycleBinItem> GetItems()
        {
            var items = new List<RecycleBinItem>();

            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return items;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic bin = shell.NameSpace(10);
                dynamic folderItems = bin.Items();
                int count = folderItems.Count;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic item = folderItems.Item(i);

                        string name = bin.GetDetailsOf(item, 0) ?? string.Empty;
                        string originalLocation = bin.GetDetailsOf(item, 1) ?? string.Empty;
                        string dateDeletedStr = bin.GetDetailsOf(item, 2) ?? string.Empty;
                        string sizeStr = bin.GetDetailsOf(item, 3) ?? string.Empty;
                        string typeStr = bin.GetDetailsOf(item, 4) ?? string.Empty;

                        if (string.IsNullOrEmpty(name)) continue;

                        string recyclePath = item.Path ?? string.Empty;

                        bool isDirectory = typeStr.IndexOf("папк", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           typeStr.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           typeStr.IndexOf("directory", StringComparison.OrdinalIgnoreCase) >= 0;

                        items.Add(new RecycleBinItem
                        {
                            Name = name,
                            OriginalPath = originalLocation,
                            RecycleBinPath = recyclePath,
                            SizeBytes = ParseSize(sizeStr),
                            DateDeleted = ParseDate(dateDeletedStr),
                            IsDirectory = isDirectory,
                            Extension = Path.GetExtension(name)
                        });
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return items;
        }

        public bool RestoreItem(string originalPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic bin = shell.NameSpace(10);
                dynamic folderItems = bin.Items();
                int count = folderItems.Count;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic item = folderItems.Item(i);
                        string itemPath = bin.GetDetailsOf(item, 1) ?? string.Empty;

                        if (string.Equals(
                                itemPath.TrimEnd('\\'),
                                originalPath.TrimEnd('\\'),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            object? result = item.InvokeVerb("restore");
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public void EmptyBin(IntPtr ownerHandle)
        {
            SHEmptyRecycleBin(ownerHandle, null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        }

        private static long ParseSize(string sizeStr)
        {
            if (string.IsNullOrWhiteSpace(sizeStr)) return 0;

            sizeStr = sizeStr.Trim();
            double multiplier = 1;

            int kbIdx = sizeStr.IndexOfAny(new[] { 'К', 'к', 'K' });
            int mbIdx = sizeStr.IndexOfAny(new[] { 'М', 'м', 'M' });
            int gbIdx = sizeStr.IndexOfAny(new[] { 'Г', 'г', 'G' });

            if (kbIdx >= 0)
            {
                multiplier = 1024;
                sizeStr = sizeStr[..kbIdx];
            }
            else if (mbIdx >= 0)
            {
                multiplier = 1024 * 1024;
                sizeStr = sizeStr[..mbIdx];
            }
            else if (gbIdx >= 0)
            {
                multiplier = 1024L * 1024 * 1024;
                sizeStr = sizeStr[..gbIdx];
            }
            else if (sizeStr.IndexOfAny(new[] { 'б', 'Б', 'b', 'B' }) >= 0)
            {
                sizeStr = sizeStr[..^1];
            }

            sizeStr = sizeStr.Trim().Replace(" ", "").Replace(",", ".");
            if (double.TryParse(sizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return (long)(value * multiplier);

            return 0;
        }

        private static DateTime ParseDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return DateTime.MinValue;

            if (DateTime.TryParse(dateStr, CultureInfo.CurrentCulture, DateTimeStyles.None, out var result))
                return result;

            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;

            return DateTime.MinValue;
        }
    }
}
