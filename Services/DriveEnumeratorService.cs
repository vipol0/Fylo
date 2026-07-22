using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fylo.Models;

namespace Fylo.Services
{
    public sealed class DriveEnumeratorService
    {
        public List<DriveEntry> GetDrives()
        {
            var result = new List<DriveEntry>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    bool isReady = drive.IsReady;

                    result.Add(new DriveEntry
                    {
                        RootPath = drive.RootDirectory.FullName,
                        DriveLetter = drive.Name.TrimEnd('\\'),
                        DisplayName = isReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel)
                            ? drive.VolumeLabel
                            : "Локальный диск",
                        TotalSizeBytes = isReady ? SafeGet(() => drive.TotalSize) : 0,
                        FreeSizeBytes = isReady ? SafeGet(() => drive.AvailableFreeSpace) : 0,
                        IsReady = isReady
                    });
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return result;
        }

        private static long SafeGet(Func<long> getter)
        {
            try { return getter(); }
            catch { return 0; }
        }
    }
}
