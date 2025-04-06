using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Monolith.Classes
{
    public class DriveIndexer
    {
        private readonly string drivePath;
        private readonly FileSystemWatcher watcher;
        private readonly ConcurrentDictionary<string, DateTime> recentFiles;
        private readonly Timer updateTimer;
        public Action<string> OnStatusUpdate;
        private string Type;
        private int dotCount = 0;

        public DriveIndexer(string drive, string type)
        {
            drivePath = drive;
            Type = type;
            recentFiles = new ConcurrentDictionary<string, DateTime>();

            watcher = new FileSystemWatcher
            {
                Path = drivePath,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;

            updateTimer = new Timer(UpdateStatus, null, 0, 200);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (File.Exists(e.FullPath))
                {
                    DateTime lastWrite = File.GetLastWriteTime(e.FullPath);
                    recentFiles[e.FullPath] = lastWrite;
                }
            }
            catch { /* ignore access exceptions */ }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            OnChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(e.FullPath), Path.GetFileName(e.FullPath)));
        }

        private void UpdateStatus(object state)
        {
            if (recentFiles.IsEmpty) return;

            KeyValuePair<string, DateTime>[] snapshot;

            try
            {
                snapshot = recentFiles.ToArray(); // Safe snapshot
            }
            catch
            {
                return; // Skip this tick if snapshot failed
            }

            if (snapshot.Length == 0)
                return;

            var latest = snapshot.OrderByDescending(f => f.Value).FirstOrDefault();
            string filePath = latest.Key;

            dotCount = (dotCount + 1) % 4;
            string dots = new string('.', dotCount);

            OnStatusUpdate?.Invoke($"{Type}: {filePath}{dots}");
        }


        public void Stop()
        {
            watcher.Dispose();
            updateTimer.Dispose();
        }
    }
}