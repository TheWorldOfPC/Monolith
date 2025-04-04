using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Windows;
using System.IO;
using Wpf.Ui.Controls;
using Monolith.Classes;

namespace Monolith
{
    public partial class MainWindow
    {
        private int StepCount = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnBrowseImage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                List<string> validExtensions = new List<string> { ".iso", ".wim", ".esd", ".swm" };

                if (files.Length > 1)
                {
                    Utils.ShowSnackbar(SnackbarHost,"Error", "Only single file drag & drop is supported", SymbolRegular.ErrorCircle24);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                string filePath = files.First();
                string fileExtension = Path.GetExtension(filePath).ToLower();

                if (!validExtensions.Contains(fileExtension))
                {
                    Utils.ShowSnackbar(SnackbarHost,"Invalid Format", "Supported formats: .iso, .wim, .esd, .swm", SymbolRegular.DocumentError24);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                if (!File.Exists(filePath))
                {
                    Utils.ShowSnackbar(SnackbarHost,"File Not Found", "The system cannot find the file specified", SymbolRegular.DocumentSearch24);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                e.Effects = DragDropEffects.Copy;

                if (fileExtension == ".iso")
                {
                    try
                    {
                        Utils.ShowSnackbar(SnackbarHost,"Success", "ISO extracted successfully!", SymbolRegular.Archive24);
                    }
                    catch (Exception ex)
                    {
                        Utils.ShowSnackbar(SnackbarHost,"Extraction Failed", ex.Message, SymbolRegular.Warning24);
                    }
                }
                else
                {
                    Utils.ShowSnackbar(SnackbarHost,"Success", "Image file processed successfully!", SymbolRegular.Document24);
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
                Filter = "Windows Image Files (*.iso;*.wim;*.esd;*.swm)|*.iso;*.wim;*.esd;*.swm|All Files (*.*)|*.*",
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
                    ExtractISOFile(selectedFile);
                }
            }
        }

        private void ExtractISOFile(string filePath)
        {
            //MessageBox.Show(filePath);
        }
    }
}