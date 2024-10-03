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

        private void HandlePrintRequest()
        {
            // Create the PrintWindow
            PrintWindow printWindow = new PrintWindow();

            // Set the DataContext for the PrintWindow (it could be the same ViewModel or a different one)
            if (DataContext is UlazMaterijalaViewModel ulazMaterijalaViewModel)
            {
                // Pass the current ViewModel to the PrintWindow
                printWindow.DataContext = ulazMaterijalaViewModel;

                // Show the window to print (it can be hidden or shown as preview)
                printWindow.Show();

                // Use the PrintDialog to print the window content
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintVisual(printWindow, "Printed Report");
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
