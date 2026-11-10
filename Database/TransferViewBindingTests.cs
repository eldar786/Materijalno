using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.ViewModel;

public static class TransferViewBindingTests
{
    static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Children(child)) yield return descendant;
    }

    [STAThread]
    public static int Main()
    {
        var app = new Application();
        var errors = new List<Exception>();
        app.DispatcherUnhandledException += (s, e) => { errors.Add(e.Exception); e.Handled = true; };
        try
        {
            var vm = new MedjuskladisnicaViewModel
            {
                CurrentItemMat = new Mat { Status = "M", Medus = "1", Kljnaz = 1001, Kljnaz1 = 1000 },
                CurrentItemTabSkladista = new SifarnikSkladista { Kljnaz = 1001, NazivOrg = "Odredište" },
                CurrentItemTabSkladistaUlaza = new SifarnikSkladista { Kljnaz = 1000, NazivOrg = "Izvor" }
            };
            var view = new Materijalno.UI.Medjuskladisnica { DataContext = vm };
            view.Measure(new Size(1100, 800));
            view.Arrange(new Rect(0, 0, 1100, 800));
            view.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            if (errors.Count != 0) throw new Exception("WPF binding failed: " + errors[0].Message);
            foreach (string property in new[] { "SourceWarehouseName", "DestinationWarehouseName" })
            {
                var field = Children(view).OfType<TextBox>().Single(t => BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path?.Path == property);
                if (BindingOperations.GetBinding(field, TextBox.TextProperty).Mode != BindingMode.OneWay)
                    throw new Exception(property + " must use OneWay binding.");
                string expected = property == "SourceWarehouseName" ? "Izvor" : "Odredište";
                if (field.Text != expected) throw new Exception(property + " was not displayed.");
                Console.WriteLine("PASS " + property + " displayed without writing a read-only property");
            }
            foreach (UserControl screen in new UserControl[] { view, new Materijalno.UI.IzlazMaterijala(), new Materijalno.UI.PovratMaterijala() })
            {
                var button = (Button)screen.FindName("btnStampa");
                button.Measure(new Size(1100, double.PositiveInfinity));
                if (button.DesiredSize.Height > 48 || button.DesiredSize.Width > 120)
                    throw new Exception(screen.GetType().Name + " print button is oversized.");
                Console.WriteLine("PASS " + screen.GetType().Name + " compact print button");
            }
            Console.WriteLine("ALL PASSED 5; no SQL or Oracle connections used.");
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
        finally { app.Shutdown(); }
    }
}
