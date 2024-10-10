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
    /// Interaction logic for UlazMaterijala.xaml
    /// </summary>
    public partial class UlazMaterijala : UserControl
    {
        public UlazMaterijala()
        {
            InitializeComponent();

            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Check if the DataContext is of type UlazMaterijalaViewModel
            if (e.NewValue is UlazMaterijalaViewModel ulazMaterijalaViewModel)
            {
                // Subscribe to the print event from the ViewModel
                ulazMaterijalaViewModel.OnPrintEvent += HandlePrintRequest;
            }
        }

        //private void HandlePrintRequest()
        //{
        //    // Create the PrintWindow
        //    PrintWindow printWindow = new PrintWindow();

        //    // Set the DataContext for the PrintWindow (it could be the same ViewModel or a different one)
        //    if (DataContext is UlazMaterijalaViewModel ulazMaterijalaViewModel)
        //    {
        //        // Pass the current ViewModel to the PrintWindow
        //        printWindow.DataContext = ulazMaterijalaViewModel;

        //        //if (printWindow.Content is UIElement content)
        //        //{
        //        //    content.UpdateLayout();
        //        //}

        //        // Show the window to print (it can be hidden or shown as preview)
        //        printWindow.Show();

        //        // Use the PrintDialog to print the window content
        //        PrintDialog printDialog = new PrintDialog();
        //        if (printDialog.ShowDialog() == true)
        //        {
        //            //printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        //            printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        //            printDialog.PrintVisual(printWindow, "Print DataGrid");
        //        }

        //        // Close the print window after printing
        //        printWindow.Close();
        //    }
        //}

        private void HandlePrintRequest()
        {
            // Kreiranje Window za print
            PrintWindow printWindow = new PrintWindow();

            // Postavljanje DataContext za PrintWindow (uzeli smo UlazMaterijalaViewModel)
            if (DataContext is UlazMaterijalaViewModel ulazMaterijalaViewModel)
            {
                // Dodajemo trenutni ViewModel u PrintWindow
                printWindow.DataContext = ulazMaterijalaViewModel;

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


        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MoveFocusToNextControl(sender as TextBox);
                e.Handled = true;
            }
        }

        private void MoveFocusToNextControl(TextBox currentTextBox)
        {
            currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }
}
