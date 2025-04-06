using Monolith.Classes;
using System.Windows;

namespace Monolith
{
    public partial class App : Application
    {
        public static string SevenZipPath;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SevenZipPath = IsoExtractor.Extract7ZipBinaries();
        }
    }
}