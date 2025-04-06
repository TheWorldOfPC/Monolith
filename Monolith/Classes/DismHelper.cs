using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Monolith.Classes
{
    public static class DismHelper
    {
        public class WimImageInfo
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public override string ToString() => $"Index {Index}: {Name}";
        }

        public static List<WimImageInfo> GetImageIndexes(string imagePath)
        {
            var editions = new List<WimImageInfo>();

            using (var process = new Process())
            {
                process.StartInfo.FileName = "dism.exe";
                process.StartInfo.Arguments = $"/get-wiminfo /wimfile:\"{imagePath}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error) || output.Contains("Error"))
                {
                    throw new InvalidOperationException("Invalid image file or DISM error:\n" + error);
                }

                var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                WimImageInfo current = null;

                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("Index :"))
                    {
                        current = new WimImageInfo
                        {
                            Index = int.Parse(line.Split(':')[1].Trim())
                        };
                        editions.Add(current);
                    }
                    else if (line.TrimStart().StartsWith("Name :") && current != null)
                    {
                        current.Name = line.Split(':')[1].Trim();
                    }
                }

                return editions;
            }
        }

        public static async Task ApplyImageAsync(string imagePath, int imageIndex, string targetDrive, string unattendXml, bool compactOS, 
            Action<string> onProgressChanged, Action<string> onStatusTextChanged, CancellationToken cancellationToken)
        {

            string parameter = $"/apply-image /imagefile:\"{imagePath}\"";

            if (imagePath.EndsWith(".swm", StringComparison.OrdinalIgnoreCase))
            {
                string swmWildcard = Path.Combine(Path.GetDirectoryName(imagePath),
                    Path.GetFileNameWithoutExtension(imagePath) + "*.swm");
                parameter += $" /SWMFile:\"{swmWildcard}\"";
            }

            if (!string.IsNullOrEmpty(unattendXml))
            {
                parameter += $" /Apply-Unattend:\"{unattendXml}\"";
            }

            parameter += $" /index:{imageIndex} /applydir:{targetDrive}";

            if (compactOS)
            {
                parameter += " /compact";
            }

            if (!Directory.Exists(targetDrive))
                throw new DirectoryNotFoundException($"Target drive '{targetDrive}' not found.");

            var indexer = new DriveIndexer(targetDrive, "Applying Image")
            {
                OnStatusUpdate = onStatusTextChanged
            };

            await Task.Run(() =>
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "dism.exe";
                    process.StartInfo.Arguments = parameter;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    var outputBuilder = new StringBuilder();

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);

                            var match = Regex.Match(e.Data, @"\[\=* *(\d{1,3}(?:\.\d+)?)%");
                            if (match.Success && double.TryParse(match.Groups[1].Value, out double rawPercent))
                            {
                                int percent = (int)Math.Round(rawPercent);
                                onProgressChanged?.Invoke($"{percent}%");
                            }
                        }
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            outputBuilder.AppendLine("ERR: " + e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                process.Kill();
                                onStatusTextChanged?.Invoke("Image application cancelled.");
                            }
                            catch { }
                            indexer.Stop();
                            return;
                        }

                        Thread.Sleep(200); // Avoid busy waiting
                    }

                    process.WaitForExit();

                    indexer.Stop();
                    onProgressChanged?.Invoke("100%");
                    onStatusTextChanged?.Invoke("Image applied successfully.");

                    if (process.ExitCode != 0)
                    {
                        throw new Exception("DISM failed:\n\n" + outputBuilder.ToString());
                    }
                }
            }, cancellationToken);
        }
    }
}