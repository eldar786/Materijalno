using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "Materijalno", "logs");

        static string NewLogPath() =>
            Path.Combine(LogDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.CreateDirectory(LogDir);

            // 1) UI thread exceptions
            this.DispatcherUnhandledException += (s, ev) =>
            {
                var path = NewLogPath();
                File.WriteAllText(path, FormatException("DispatcherUnhandledException", ev.Exception));
                MessageBox.Show($"An error occurred.\nDetails saved to:\n{path}", "Materijalno",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ev.Handled = true; // prevent silent crash; optionally exit here
                // Current.Shutdown(); // uncomment if you prefer to exit on fatal error
            };

            // 2) Non-UI threads
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                var path = NewLogPath();
                File.WriteAllText(path, FormatException("AppDomain.UnhandledException",
                    ev.ExceptionObject as Exception));
                // Avoid UI here; this may be on a non-UI thread
            };

            // 3) Task exceptions not observed
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                var path = NewLogPath();
                File.WriteAllText(path, FormatException("TaskScheduler.UnobservedTaskException", ev.Exception));
                ev.SetObserved();
            };

            base.OnStartup(e);
        }

        static string FormatException(string source, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:O}] {source}");
            if (ex == null)
            {
                sb.AppendLine("Exception object was null.");
                return sb.ToString();
            }

            // Walk inner exceptions
            int level = 0;
            for (var cur = ex; cur != null; cur = cur.InnerException, level++)
            {
                sb.AppendLine($"-- Exception level {level} --");
                sb.AppendLine(cur.GetType().FullName);
                sb.AppendLine(cur.Message);
                sb.AppendLine(cur.StackTrace);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
