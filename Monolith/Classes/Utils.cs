using System.Windows;
using System;
using Wpf.Ui.Controls;
using System.Threading.Tasks;

namespace Monolith.Classes
{
    public class Utils
    {
        public static void ShowSnackbar(SnackbarPresenter snackbarPresenter, string title, string content, SymbolRegular symbol)
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
                Timeout = TimeSpan.FromSeconds(5)
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