using Materijalno.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for GlavniView.xaml
    /// </summary>
    public partial class GlavniView : UserControl
    {
        public GlavniView()
        {
            DataContext = new GlavniViewModel();
            InitializeComponent();
            foreach (var button in MenuButtons(glavniStackPanel))
                button.Click += MenuButton_Click;
            ((GlavniViewModel)DataContext).PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "OdabraniVM")
                    UpdateWorkspace();
            };
        }

        private void UpdateWorkspace()
        {
            var hasPage = ((GlavniViewModel)DataContext).OdabraniVM != null;
            WelcomePanel.Visibility = hasPage ? Visibility.Collapsed : Visibility.Visible;
            WorkspacePanel.Visibility = hasPage ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null || button.Command == null)
                return;
            // Commands execute after the routed click. Update only the presentation afterwards.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (((GlavniViewModel)DataContext).OdabraniVM == null) return;
                foreach (var item in MenuButtons(glavniStackPanel)) item.Tag = null;
                button.Tag = "Selected";
                PageText.Text = button.Content as string ?? "Arhiva";
                var sections = new List<string>();
                DependencyObject parent = button;
                while (parent != null && parent != glavniStackPanel)
                {
                    var expander = parent as Expander;
                    var header = expander == null ? null : expander.Header as TextBlock;
                    if (header != null) sections.Insert(0, header.Text);
                    parent = LogicalTreeHelper.GetParent(parent);
                }
                SectionText.Text = sections.Count == 0 ? "RADNI PROSTOR" : string.Join("  /  ", sections);
                UpdateWorkspace();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static IEnumerable<Button> MenuButtons(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                var button = child as Button;
                if (button != null) yield return button;
                else foreach (var nested in MenuButtons(child)) yield return nested;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = WindowState.Maximized;
                window.MinWidth = 1120;
                window.MinHeight = 700;
                window.FontFamily = new FontFamily("Segoe UI");
                var navigationWindow = window as NavigationWindow;
                if (navigationWindow != null) navigationWindow.ShowsNavigationUI = false;
                var database = new System.Data.SqlClient.SqlConnectionStringBuilder(
                    Materijalno.Model.Services.DatabaseConfiguration.MaterialConnectionString).InitialCatalog;
                window.Title = database.StartsWith("materijalno_fox_test_", StringComparison.OrdinalIgnoreCase)
                    ? "Materijalno – TESTNI PROMET"
                    : "Materijalno | Materijalno knjigovodstvo";
                EnvironmentText.Text = database.StartsWith("materijalno_fox_test_", StringComparison.OrdinalIgnoreCase)
                    ? "TESTNI PODACI" : "MATERIJALNO";
            }
            TodayText.Text = DateTime.Today.ToString("dd.MM.yyyy.");
            UpdateWorkspace();
        }

    }
}
