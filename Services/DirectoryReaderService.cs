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
