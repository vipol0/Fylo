using System;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace Fylo.Services
{
    public sealed class FileSystemOperationService
    {
        public string CreateFile(string parentPath, string baseName)
        {
            var name = ResolveUniqueName(parentPath, baseName);
            var fullPath = Path.Combine(parentPath, name);
            File.WriteAllBytes(fullPath, Array.Empty<byte>());
            return fullPath;
        }

        public string CreateDirectory(string parentPath, string baseName)
        {
            var name = ResolveUniqueName(parentPath, baseName);
            var fullPath = Path.Combine(parentPath, name);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public bool DeleteToRecycleBin(string fullPath, bool isDirectory)
        {
            try
            {
                if (isDirectory)
                {
                    FileSystem.DeleteDirectory(
                        fullPath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                else
                {
                    FileSystem.DeleteFile(
                        fullPath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Rename(string sourcePath, string newName, bool isDirectory)
        {
            try
            {
                var parent = Path.GetDirectoryName(sourcePath);
                if (parent == null) return false;
                var dest = Path.Combine(parent, newName);
                if (string.Equals(sourcePath, dest, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (isDirectory)
                    Directory.Move(sourcePath, dest);
                else
                    File.Move(sourcePath, dest);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeletePermanent(string fullPath, bool isDirectory)
        {
            try
            {
                if (isDirectory)
                    Directory.Delete(fullPath, recursive: true);
                else
                    File.Delete(fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveUniqueName(string parentPath, string baseName)
        {
            var fullPath = Path.Combine(parentPath, baseName);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                return baseName;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
            var ext = Path.GetExtension(baseName);

            for (int i = 1; i < 1000; i++)
            {
                var candidate = $"{nameWithoutExt} ({i}){ext}";
                var candidatePath = Path.Combine(parentPath, candidate);
                if (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
                    return candidate;
            }

            return $"{nameWithoutExt} ({Guid.NewGuid():N}){ext}";
        }
    }
}
