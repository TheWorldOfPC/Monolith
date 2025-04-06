using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Monolith.Classes
{
    public static class IsoExtractor
    {
        public static string Extract7ZipBinaries()
        {
            string extractPath = Path.Combine(Path.GetTempPath(), "Monolith", "7zip");
            Directory.CreateDirectory(extractPath);
            WriteResourceToFile("Monolith.Assets.7z.exe", Path.Combine(extractPath, "7z.exe"));
            WriteResourceToFile("Monolith.Assets.7z.dll", Path.Combine(extractPath, "7z.dll"));

            return Path.Combine(extractPath, "7z.exe");
        }

        private static void WriteResourceToFile(string resourceName, string filePath)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }
        }

        public static async Task ExtractIso(string isoPath, Action<string> onStatus, CancellationToken cancellationToken)
        {
            string extractPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Monolith", "ExtractedFiles");

            if (!Directory.Exists(extractPath))
                Directory.CreateDirectory(extractPath);

            var indexer = new DriveIndexer(extractPath, "Extracting ISO File")
            {
                OnStatusUpdate = onStatus
            };

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = App.SevenZipPath,
                    Arguments = $"x \"{isoPath}\" -o\"{extractPath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
                {
                    var tcs = new TaskCompletionSource<bool>();

                    process.Exited += (sender, args) => tcs.TrySetResult(true);
                    process.Start();


                    while (!process.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            try { process.Kill(); } catch { /* already exited */ }
                            onStatus?.Invoke("Extraction cancelled.");
                            indexer.Stop();
                            return;
                        }

                        await Task.Delay(200);
                    }

                    await tcs.Task;

                    if (process.ExitCode != 0)
                        throw new Exception($"7z extraction failed. Exit code: {process.ExitCode}");
                }

                onStatus?.Invoke("Extraction complete.");
            }
            finally
            {
                indexer.Stop();
            }
        }
    }
}