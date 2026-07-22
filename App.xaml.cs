using System.IO;
using System.Windows;

namespace Fylo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var startPath = GetStartPath(e.Args);

            var window = new MainWindow(startPath);
            window.Show();
        }

        private static string GetStartPath(string[] args)
        {
            if (args.Length > 0)
            {
                var path = args[0].Trim('"');
                if (Directory.Exists(path))
                    return path;
            }

            return @"C:\";
        }
    }
}
