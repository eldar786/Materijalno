using Materijalno.UI.Izvjestaji;
using Materijalno.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Materijalno.UI
{
    /// <summary>
    /// Interaction logic for Medjuskladisnica.xaml
    /// </summary>
    public partial class Medjuskladisnica : UserControl
    {
        public Medjuskladisnica()
        {
            InitializeComponent();
            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MoveFocusToNextControl(sender as TextBox);
                e.Handled = true;
            }
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Check if the DataContext is of type UlazMaterijalaViewModel
            if (e.NewValue is MedjuskladisnicaViewModel medjuskladisnicaViewModel)
            {
                // Subscribe to the print event from the ViewModel
                medjuskladisnicaViewModel.OnPrintEvent += HandlePrintRequest;
            }
        }


        private void HandlePrintRequest()
        {
            // Kreiranje Window za print
            PrintWindowMedjuskladisnica printWindow = new PrintWindowMedjuskladisnica();

            // Postavljanje DataContext za PrintWindow (uzeli smo UlazMaterijalaViewModel)
            if (DataContext is MedjuskladisnicaViewModel medjuskladisnicaViewModel)
            {
                // Dodajemo trenutni ViewModel u PrintWindow
                printWindow.DataContext = medjuskladisnicaViewModel;

                // Prikazi prozor za print
                printWindow.Show();

                // Koristimo PrintDialog za ispis zadrzaja prozora
                PrintDialog printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    // Postavljamo velicinu na A4 i vodoravna orijentacija
                    printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                    printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

                    //  Izračunamo faktor skaliranja kako bi sadržaj odgovarao formatu A4
                    double scaleX = printDialog.PrintableAreaWidth / printWindow.ActualWidth;
                    double scaleY = printDialog.PrintableAreaHeight / printWindow.ActualHeight;
                    double scale = Math.Min(scaleX, scaleY);

                    // Save the original transform
                    Transform originalTransform = printWindow.LayoutTransform;

                    // Apply scaling transform to the PrintWindow
                    printWindow.LayoutTransform = new ScaleTransform(scale, scale);

                    // Measure and arrange the page to the size of the printable area
                    Size pageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                    printWindow.Measure(pageSize);
                    printWindow.Arrange(new Rect(0, 0, pageSize.Width, pageSize.Height));

                    // Print the window's visual
                    printDialog.PrintVisual(printWindow, "Print DataGrid");

                    // Restore the original transform
                    printWindow.LayoutTransform = originalTransform;
                }

                // Close the print window after printing
                printWindow.Close();
            }
        }

        private void MoveFocusToNextControl(TextBox currentTextBox)
        {
            currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void datOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void stampa_button(object sender, RoutedEventArgs e)
        {
            if (DataContext is MedjuskladisnicaViewModel medjuskladisnicaViewModel)
            {
                MedjuskladisnicaIzvjestaj medjuskladisnicaIzvjestaj = new MedjuskladisnicaIzvjestaj(medjuskladisnicaViewModel);

                // Dodajemo trenutni ViewModel u PrintWindow
                //medjuskladisnicaIzvjestaj.DataContext = medjuskladisnicaViewModel;
                medjuskladisnicaIzvjestaj.Show();
            }
        }
    }

}
