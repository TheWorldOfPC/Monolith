using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace Monolith.Classes
{
    public class Utils
    {
        public static readonly string ExtractionPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Monolith", "ExtractedFiles");

        public static void ConfigureBootloader(string systemDrive, string entryName, bool defaultEntry)
        {
            string newBootGuid = null;
            var output = RunCommand("bcdedit", "/enum");

            for (int i = 0; i < output.Count; i++)
            {
                string line = output[i];

                if (line.StartsWith("device", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains($"partition={systemDrive.TrimEnd('\\')}"))
                {
                    for (int j = i; j >= 0; j--)
                    {
                        if (output[j].StartsWith("identifier", StringComparison.OrdinalIgnoreCase))
                        {
                            newBootGuid = output[j].Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                            break;
                        }
                    }

                    if (newBootGuid != null)
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(newBootGuid))
                throw new Exception("Failed to find the new boot entry GUID.");

            RunCommand("bcdedit", $"/set {newBootGuid} description \"{entryName}\"");

            if (defaultEntry)
                RunCommand("bcdedit", $"/default {newBootGuid}");
        }

        public static List<string> RunCommand(string fileName, string arguments)
        {
            var outputLines = new List<string>();

            using (Process process = new Process())
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                while (!process.StandardOutput.EndOfStream)
                {
                    outputLines.Add(process.StandardOutput.ReadLine());
                }

                string err = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(err))
                {
                    throw new Exception("BCDEdit error: " + err.Trim());
                }
            }

            return outputLines;
        }

        public static void CleanFolder(string folderPath)
        {
            try
            {
                DirectoryInfo directory = new DirectoryInfo(folderPath);
                if (directory.Exists)
                {
                    foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try { file.Delete(); }
                        catch { }
                    }

                    foreach (var dir in directory.EnumerateDirectories("*", SearchOption.AllDirectories))
                    {
                        try { dir.Delete(true); }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static string ImageFileExists()
        {
            string[] validImageNames = { "install.wim", "install.esd", "install.swm" };

            if (!Directory.Exists(ExtractionPath)) return null;

            string foundImage = Directory.EnumerateFiles(ExtractionPath, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(file =>
                    validImageNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase));

            if (foundImage != null)
            {
                return foundImage;
            }
            else
            {
                return null;
            }
        }

        public static bool IsDriveEmpty(DriveInfo drive)
        {
            try
            {
                var rootDir = drive.RootDirectory;

                if (rootDir.GetFiles().Any(f => !f.Attributes.HasFlag(FileAttributes.System)))
                    return false;

                return !rootDir.GetDirectories()
                    .Any(d => !d.Attributes.HasFlag(FileAttributes.System));
            }
            catch
            {
                return false;
            }
        }

        public static void ShowSnackbar(SnackbarPresenter snackbarPresenter, string title, string content, SymbolRegular symbol, int time = 5)
        {
            var snackbar = new Snackbar(snackbarPresenter)
            {
                Title = title,
                Content = content,
                Appearance = ControlAppearance.Secondary,
                Icon = new SymbolIcon(symbol)
                {
                    FontSize = 24,
                    Margin = new Thickness(0, 0, 8, 0)
                },
                Timeout = TimeSpan.FromSeconds(time)
            };

            snackbar.Show();
        }

        public static async Task ShowDialog(string title, string content)
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = content,
            };

            _ = await uiMessageBox.ShowDialogAsync();
        }
    }
}