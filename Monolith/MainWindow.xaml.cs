using Microsoft.Win32;
using Monolith.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Monolith
{
    public partial class MainWindow
    {
        private int StepCount = 0;
        CancellationTokenSource cts;
        private string ImageFile = string.Empty;
        private string UnattendXml = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            GetPartitions();
            GetBootInformation();

            Loaded += (s, e) => SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
        }

        #region Events

        private void btnRefreshDrives_Click(object sender, RoutedEventArgs e) => GetPartitions();

        private void btnBrowseImage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                List<string> validExtensions = new List<string> { ".iso", ".wim", ".esd", ".swm" };

                if (files.Length > 1)
                {
                    Utils.ShowSnackbar(SnackbarHost, "Error", "Only single file drag & drop is supported", SymbolRegular.ErrorCircle24);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                string filePath = files.First();
                string fileExtension = Path.GetExtension(filePath).ToLower();

                if (!validExtensions.Contains(fileExtension))
                {
                    Utils.ShowSnackbar(SnackbarHost, "Invalid Format", "Supported formats: .iso, .wim, .esd, .swm", SymbolRegular.DocumentError24);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                if (!File.Exists(filePath))
                {
                    Utils.ShowSnackbar(SnackbarHost, "File Not Found", "The system cannot find the file specified", SymbolRegular.DocumentSearch24);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                e.Effects = DragDropEffects.Copy;

                if (fileExtension == ".iso")
                {
                    StartExtraction(filePath);
                }
                else
                {
                    ProcessImageFile(filePath);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void btnBrowseImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Windows Image Files (*.iso;*.wim;*.esd;*.swm)|*.iso;*.wim;*.esd;*.swm",
                Title = "Select a Windows Image File",
                Multiselect = false
            };

            bool? dialogResult = openFileDialog.ShowDialog();

            if (dialogResult == true)
            {
                string selectedFile = openFileDialog.FileName;
                string extension = Path.GetExtension(selectedFile).ToLower();

                if (extension == ".iso")
                {
                    StartExtraction(selectedFile);
                }
                else
                {
                    ProcessImageFile(selectedFile);
                }
            }
        }

        private void cmBoxPartitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDrive = cmBoxPartitions.SelectedItem as DriveInfo;
            btnNext.IsEnabled = false;
            SetupInfoBar.IsOpen = false;

            if (selectedDrive == null) return;

            double totalSizeGB = (double)selectedDrive.TotalSize / (1024 * 1024 * 1024);
            if (totalSizeGB < 20)
            {
                ShowInfoBar("Warning", "Selected drive must have at least 20GB of space.", InfoBarSeverity.Error);
                return;
            }
            else if (totalSizeGB < 40)
            {
                ShowInfoBar("Warning", "A minimum of 40GB of space is required to install Windows.", InfoBarSeverity.Warning);
            }

            if (!Utils.IsDriveEmpty(selectedDrive))
            {
                ShowInfoBar("Warning", "Selected drive is not empty.", InfoBarSeverity.Error);
                return;
            }

            btnNext.IsEnabled = true;
        }

        private void tsAutoUnattendXml_Click(object sender, RoutedEventArgs e)
        {
            if (tsAutoUnattendXml.IsChecked == true)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Windows Answer File (*.xml)|*.xml;",
                    Title = "Select a Windows Answer File",
                    Multiselect = false
                };

                bool? dialogResult = openFileDialog.ShowDialog();

                if (dialogResult == true)
                {
                    UnattendXml = openFileDialog.FileName;
                    txtBlockUnattendXml.Text = $"Using file: {UnattendXml}";
                }
                else
                {
                    tsAutoUnattendXml.IsChecked = false;
                    txtBlockUnattendXml.Text = "Answer file to perform unattended installation of Windows.";
                }
            }
            else
            {
                txtBlockUnattendXml.Text = "Answer file to perform unattended installation of Windows.";
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            switch (StepCount)
            {
                case 0:
                    btnNext.Visibility = pageHome.Visibility = Visibility.Collapsed;
                    btnBack.Visibility = pageSelectImage.Visibility = Visibility.Visible;
                    string imageFile = Utils.ImageFileExists();
                    if (imageFile != null)
                    {
                        txtBlockISO.Visibility = btnNext.Visibility = Visibility.Visible;
                        txtBlockISO.Text = $"Previous image extraction found at:\n{imageFile}";
                    }
                    StepCount++;
                    break;
                case 1:
                    pageInstallConfiguration.Visibility = Visibility.Visible;
                    pageSelectImage.Visibility = Visibility.Collapsed;
                    ProcessImageFile(Utils.ImageFileExists());
                    StepCount++;
                    break;
                case 2:
                    pageInstallConfiguration.Visibility = btnNext.Visibility = btnBack.Visibility = Visibility.Collapsed;
                    pageInstallation.Visibility = btnCancel.Visibility = Visibility.Visible;
                    ShowInfoBar("Installation in progress", "Do NOT close the app!", InfoBarSeverity.Informational);
                    StartInstallation();
                    StepCount++;
                    break;
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            switch (StepCount)
            {
                case 1:
                    btnBack.Visibility = pageSelectImage.Visibility = Visibility.Collapsed;
                    pageHome.Visibility = btnNext.Visibility = Visibility.Visible;
                    btnNext.IsEnabled = true;
                    SetupInfoBar.IsOpen = false;
                    StepCount--;
                    break;
                case 2:
                    pageInstallConfiguration.Visibility = Visibility.Collapsed;
                    pageSelectImage.Visibility = Visibility.Visible;
                    btnNext.Visibility = Visibility.Collapsed;
                    string imageFile = Utils.ImageFileExists();
                    if (imageFile != null)
                    {
                        txtBlockISO.Visibility = btnNext.Visibility = Visibility.Visible;
                        btnNext.IsEnabled = true;
                        txtBlockISO.Text = $"Previous image extraction found at:\n{imageFile}";
                    }
                    StepCount--;
                    break;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();

            if (StepCount == 3)
            {
                ShowInfoBar("Installing has been cancelled!", "Restart the app", InfoBarSeverity.Error);
                txtBlockInstallInProg.Text = "Installation has been cancelled!";
                installationStatus.Visibility = iconWindows.Visibility = btnCancel.Visibility = Visibility.Collapsed;
                iconCancelled.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Methods

        private void GetBootInformation()
        {
            var bootConfig = BootConfiguration.GetBootConfiguration();

            txtBlockCurrentEntry.Text = bootConfig.CurrentEntryName ?? "Unknown";
            txtBlockBootloaderLocation.Text = bootConfig.BootloaderPath ?? "Unknown";
            txtBlockBootloaderTimeout.Text = $"{bootConfig.TimeoutSeconds} Seconds" ?? "Unknown";
            txtBlockMetroBootloader.Text = bootConfig.MetroBootEnabled ? "Enabled" : "Disabled";
        }

        private void GetPartitions()
        {
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).ToList();
            cmBoxPartitions.ItemsSource = drives;
        }

        private async void StartExtraction(string selectedFile)
        {
            btnBrowseImage.IsEnabled = false;
            btnCancel.IsEnabled = true;
            cts = new CancellationTokenSource();

            txtBlockISO.Visibility = progressBarISO.Visibility = btnCancel.Visibility = Visibility.Visible;
            btnBack.Visibility = btnNext.Visibility = Visibility.Collapsed;

            try
            {
                await IsoExtractor.ExtractIso(selectedFile,
                    status => Dispatcher.Invoke(() => txtBlockISO.Text = status),
                    cts.Token);

                if (cts.Token.IsCancellationRequested)
                {
                    txtBlockISO.Text = "Cancelling Extraction...";
                    btnCancel.IsEnabled = false;
                    await Task.Delay(3000);
                    Utils.CleanFolder(Utils.ExtractionPath);
                    return;
                }

                txtBlockISO.Visibility = progressBarISO.Visibility = btnCancel.Visibility = Visibility.Collapsed;
                btnBack.Visibility = btnNext.Visibility = Visibility.Visible;

                string foundImage = Utils.ImageFileExists();

                if (foundImage != null)
                {
                    ProcessImageFile(foundImage);
                }
                else
                {
                    Utils.ShowDialog("Error", "Not a valid Windows Image File, select a different file.");
                }
            }
            catch (Exception ex)
            {
                Utils.ShowDialog("Extraction Failed", $"An error occurred during ISO extraction:\n{ex.Message}");
                Utils.CleanFolder(Utils.ExtractionPath); // Clean in case of partial extract
            }
            finally
            {
                btnBrowseImage.IsEnabled = true;
                btnCancel.IsEnabled = false;
                txtBlockISO.Visibility = progressBarISO.Visibility = btnCancel.Visibility = Visibility.Collapsed;
                btnBack.Visibility = btnNext.Visibility = Visibility.Visible;
            }
        }

        private async void ProcessImageFile(string filePath)
        {
            cmBoxIndexes.Items.Clear();
            try
            {
                var editions = await Task.Run(() => DismHelper.GetImageIndexes(filePath));

                foreach (var edition in editions)
                {
                    cmBoxIndexes.Items.Add(edition);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowDialog("Error", ex.Message);
            }

            cmBoxIndexes.SelectedIndex = 0;
            pageSelectImage.Visibility = Visibility.Collapsed;
            pageInstallConfiguration.Visibility = btnNext.Visibility = Visibility.Visible;
            SetupInfoBar.IsOpen = btnNext.IsEnabled = false;
            StepCount = 2;

            ImageFile = filePath;

            string imageFolder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(imageFolder))
            {
                string parentFolder = Directory.GetParent(imageFolder)?.FullName;

                if (!string.IsNullOrEmpty(parentFolder))
                {
                    string unattendFile = Path.Combine(parentFolder, "autounattend.xml");

                    if (File.Exists(unattendFile))
                    {
                        UnattendXml = unattendFile;
                        tsAutoUnattendXml.IsChecked = true;
                        txtBlockUnattendXml.Text = $"Using file: {UnattendXml}";
                    }
                }
            }
        }

        private async void StartInstallation()
        {
            if (tsAutoUnattendXml.IsChecked == true) taskUnattend.Visibility = Visibility.Visible;

            if (tsCompactOS.IsChecked == true) taskCompactOS.Visibility = Visibility.Visible;

            if (tsDefaultOS.IsChecked == true) taskDefaultOS.Visibility = Visibility.Visible;

            if (tsMetroBootloader.IsChecked == true) taskMetroBootloader.Header = "Enable Metro Bootloader";

            cts = new CancellationTokenSource();

            try
            {
                await DismHelper.ApplyImageAsync(
                    ImageFile,
                    cmBoxIndexes.SelectedIndex + 1,
                    cmBoxPartitions.Text,
                    UnattendXml,
                    tsCompactOS.IsChecked == true,
                    progress =>
                    {
                        Dispatcher.Invoke(() => txtBlockInstallPercent.Text = progress);
                    },
                    status =>
                    {
                        Dispatcher.Invoke(() => txtBlockInstallStatus.Text = status);
                    },
                    cts.Token
                );

                if (!cts.IsCancellationRequested)
                {
                    AdditionalTasks();

                    ShowInfoBar("Installation has finished!", "Restart your PC", InfoBarSeverity.Success);
                    txtBlockInstallInProg.Text = "Installation has finished!";
                }
            }
            catch (Exception ex)
            {
                Utils.ShowDialog("Installation Failed", $"An error occurred while applying the image:\n\n{ex.Message}");
                txtBlockInstallStatus.Text = "Image application failed.";
            }
            finally
            {
                installationStatus.Visibility = Visibility.Collapsed;
                btnCancel.Visibility = Visibility.Collapsed;
            }
        }

        private async void AdditionalTasks()
        {
            if (Directory.Exists(Utils.ExtractionPath))
                await Task.Run(() => Utils.CleanFolder(Utils.ExtractionPath));

            Utils.RunCommand("bcdboot", $"{cmBoxPartitions.Text}\\Windows /d /addlast");

            if (tsAutoUnattendXml.IsChecked == true)
            {
                string sourceScripts = $"{Path.GetDirectoryName(ImageFile)}\\$OEM$\\$$\\Setup\\Scripts";
                string destScripts = Path.Combine(cmBoxPartitions.Text, @"Windows\Setup\Scripts");
                string destUnattend = Path.Combine(cmBoxPartitions.Text, @"Windows\System32\Sysprep\");

                Directory.CreateDirectory(destScripts);
                Directory.CreateDirectory(destUnattend);

                if (Directory.Exists(sourceScripts))
                {
                    foreach (var file in Directory.GetFiles(sourceScripts))
                    {
                        string destFile = Path.Combine(destScripts, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }

                File.Copy(UnattendXml, Path.Combine(destUnattend, "unattend.xml"), true);
            }

            Utils.ConfigureBootloader(cmBoxPartitions.Text, "New Install", tsDefaultOS.IsChecked.Value);

            string metroPolicy = tsMetroBootloader.IsChecked.Value ? "Standard" : "Legacy";
            Utils.RunCommand("bcdedit", $"/set bootmenupolicy {metroPolicy}");
            Utils.RunCommand("bcdedit", $"/timeout {numBoxTimeout.Value}");
        }

        private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
        {
            SetupInfoBar.Title = title;
            SetupInfoBar.Message = message;
            SetupInfoBar.Severity = severity;
            SetupInfoBar.IsOpen = true;
        }

        #endregion

    }
}