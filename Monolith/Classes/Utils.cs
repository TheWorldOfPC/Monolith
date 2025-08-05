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

        public static void AddFileExclusion(string filePath)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -Command \"Add-MpPreference -ExclusionProcess '{filePath}'\"",
                Verb = "runas",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = psi })
            {
                process.Start();
                process.WaitForExit();
            }
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

        public static void CreateBootEntry(string entryName, string systemDrive, bool setAsDefault = false)
        {
            var createOutput = RunCommand("bcdedit", $"/create /d \"{entryName}\" /application osloader");
            
            string guid = null;
            foreach (var line in createOutput)
            {
                int startIndex = line.IndexOf('{');
                int endIndex = line.IndexOf('}');
                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                {
                    string potentialGuid = line.Substring(startIndex + 1, endIndex - startIndex - 1);
                    if (Guid.TryParse(potentialGuid, out _))
                    {
                        guid = potentialGuid;
                        break;
                    }
                }
            }

            RunCommand("bcdedit", $"/set {{{guid}}} device partition={systemDrive.TrimEnd('\\')}");
            RunCommand("bcdedit", $"/set {{{guid}}} path \\Windows\\system32\\winload.efi");
            RunCommand("bcdedit", $"/set {{{guid}}} osdevice partition={systemDrive.TrimEnd('\\')}");
            RunCommand("bcdedit", $"/set {{{guid}}} systemroot \\Windows");
            RunCommand("bcdedit", $"/displayorder {{{guid}}} /addlast");

            if (setAsDefault)
            {
                RunCommand("bcdedit", $"/default {{{guid}}}");
            }
        }
    }
}