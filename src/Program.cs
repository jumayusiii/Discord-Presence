using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;

[assembly: AssemblyTitle("Discord Presence")]
[assembly: AssemblyProduct("Discord Presence")]
[assembly: AssemblyDescription("A portable Discord Rich Presence editor")]
[assembly: AssemblyVersion("1.4.0.0")]
[assembly: AssemblyFileVersion("1.4.0.0")]

namespace DiscordPresence
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // WPF's image downloader uses the legacy WebRequest stack. Without
            // this, https image URLs can fail with a TLS channel error even
            // though the rest of .NET can reach them.
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // Older runtime without TLS 1.2 support; image previews will
                // simply fall back to the placeholder.
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Theme.Initialize(app);
            app.DispatcherUnhandledException += OnUnhandledException;

            MainWindow window = new MainWindow();
            app.MainWindow = window;
            app.Run(window);
        }

        private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discord-presence-error.log");
                string entry = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]" + Environment.NewLine +
                    e.Exception + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(path, entry, Encoding.UTF8);
            }
            catch
            {
                // Logging must never mask the original problem.
            }

            MessageBox.Show(
                "Discord Presence hit an unexpected error." + Environment.NewLine + Environment.NewLine +
                e.Exception.Message,
                "Discord Presence",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
