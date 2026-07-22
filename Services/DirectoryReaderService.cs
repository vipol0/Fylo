using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fylo.Models;

namespace Fylo.Services
{
    public sealed class DirectoryReaderService
    {
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

        private sealed record CacheEntry(DirectoryReadResult Result, DateTime CachedAt)
        {
            private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

            public bool IsExpired => DateTime.UtcNow - CachedAt > Ttl;
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, long> _sizeCache = new(StringComparer.OrdinalIgnoreCase);

        public void SetCachedSize(string path, long size) => _sizeCache[path] = size;

        public void SetCachedSizes(IEnumerable<KeyValuePair<string, long>> entries)
        {
            foreach (var kvp in entries)
                _sizeCache[kvp.Key] = kvp.Value;
        }

        public long? TryGetCachedSize(string path) =>
            _sizeCache.TryGetValue(path, out var size) ? size : null;

        public void InvalidateSize(string path) => _sizeCache.TryRemove(path, out _);

        public void InvalidateAllSizes() => _sizeCache.Clear();

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
            if (_cache.TryGetValue(path, out var cached) && !cached.IsExpired)
            {
                return CloneResult(cached.Result);
            }

            var result = await ReadDirectoryInternalAsync(path, token);

            _cache[path] = new CacheEntry(result, DateTime.UtcNow);

            return CloneResult(result);
        }

        private static async Task<DirectoryReadResult> ReadDirectoryInternalAsync(string path, CancellationToken token)
        {
            var directories = new List<FileSystemEntry>();
            var files = new List<FileSystemEntry>();

            await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(path);

                foreach (var entry in dirInfo.EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();

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
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }, token);

            return new DirectoryReadResult(directories, files);
        }

        private DirectoryReadResult CloneResult(DirectoryReadResult source)
        {
            var dirs = new List<FileSystemEntry>(source.Directories.Count);
            foreach (var e in source.Directories)
                dirs.Add(CloneEntry(e));

            var files = new List<FileSystemEntry>(source.Files.Count);
            foreach (var e in source.Files)
                files.Add(CloneEntry(e));

            return new DirectoryReadResult(dirs, files);
        }

        private FileSystemEntry CloneEntry(FileSystemEntry e)
        {
            var sizeBytes = e.SizeBytes;
            if (sizeBytes < 0 && _sizeCache.TryGetValue(e.FullPath, out var cachedSize))
                sizeBytes = cachedSize;

            return new FileSystemEntry
            {
                Name = e.Name,
                FullPath = e.FullPath,
                IsDirectory = e.IsDirectory,
                SizeBytes = sizeBytes,
                Modified = e.Modified,
                Extension = e.Extension,
                RelativePath = e.RelativePath
            };
        }

        public void InvalidateCache(string path)
        {
            _cache.TryRemove(path, out _);
        }

        public void InvalidateAll()
        {
            _cache.Clear();
        }
    }

    public sealed record DirectoryReadResult(
        List<FileSystemEntry> Directories,
        List<FileSystemEntry> Files);
}
