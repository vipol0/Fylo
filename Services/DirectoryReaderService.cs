using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastExplorer.Models;

namespace FastExplorer.Services
{
    /// <summary>
    /// Читает содержимое папки в фоновом потоке.
    /// Используется EnumerateFileSystemInfos вместо GetFiles/GetDirectories —
    /// это даёт результаты потоково и быстрее на больших папках.
    /// </summary>
    public sealed class DirectoryReaderService
    {
        /// <summary>Известные системные папки, которые пропускаем при подсчёте размера.</summary>
        private static readonly HashSet<string> SkippedSystemDirs = new(StringComparer.OrdinalIgnoreCase)
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

        /// <summary>
        /// Рекурсивно вычисляет размер папки (сумму всех файлов внутри).
        /// Пропускает системные папки и недоступные директории.
        /// </summary>
        public static async Task<long> CalculateFolderSizeAsync(string path, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                long totalSize = 0;
                var stack = new Stack<string>();
                stack.Push(path);

                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var dir = stack.Pop();
                    var dirName = Path.GetFileName(dir);

                    if (!string.IsNullOrEmpty(dirName) && SkippedSystemDirs.Contains(dirName))
                        continue;

                    string[] files;
                    string[] subDirs;

                    try
                    {
                        files = Directory.GetFiles(dir);
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (DirectoryNotFoundException) { continue; }
                    catch (IOException) { continue; }

                    try
                    {
                        subDirs = Directory.GetDirectories(dir);
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (DirectoryNotFoundException) { continue; }
                    catch (IOException) { continue; }

                    foreach (var file in files)
                    {
                        try
                        {
                            totalSize += new FileInfo(file).Length;
                        }
                        catch { }
                    }

                    foreach (var subDir in subDirs)
                    {
                        stack.Push(subDir);
                    }
                }

                return totalSize;
            }, token);
        }

        public async Task<DirectoryReadResult> ReadDirectoryAsync(string path, CancellationToken token)
        {
            var directories = new List<FileSystemEntry>();
            var files = new List<FileSystemEntry>();

            await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(path);

                foreach (var entry in dirInfo.EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();

                    // Пропускаем скрытые и системные файлы — как в File Pilot по умолчанию
                    if ((entry.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                    if ((entry.Attributes & FileAttributes.System) == FileAttributes.System)
                        continue;

                    try
                    {
                        if (entry is DirectoryInfo di)
                        {
                            directories.Add(FileSystemEntry.FromDirectory(di));
                        }
                        else if (entry is FileInfo fi)
                        {
                            files.Add(FileSystemEntry.FromFile(fi));
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Пропускаем недоступные элементы, не роняем весь список
                    }
                    catch (IOException)
                    {
                        // Файл мог быть удалён/заблокирован во время чтения — пропускаем
                    }
                }
            }, token);

            return new DirectoryReadResult(directories, files);
        }
    }

    public sealed record DirectoryReadResult(
        List<FileSystemEntry> Directories,
        List<FileSystemEntry> Files);
}
