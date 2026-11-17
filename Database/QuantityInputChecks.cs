using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Materijalno.Model.EntityModels;
using Materijalno.UI;

public static class QuantityInputChecks
{
    public sealed class Data {public Mat CurrentItemMat {get;set;}=new Mat{Kolic=0m};}
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static IEnumerable<DependencyObject> Children(DependencyObject o){yield return o;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var n in Children(VisualTreeHelper.GetChild(o,i)))yield return n;}
    static void Pump(Dispatcher d)=>d.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static void Enter(TextBox t){t.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(t),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});Pump(t.Dispatcher);}
    [STAThread] public static int Main()
    {
        try{
            foreach(var view in new UserControl[]{new UlazMaterijala(),new IzlazMaterijala(),new PovratMaterijala()}){
                var data=new Data();view.DataContext=data;
                var window=new Window{Content=view,Width=1400,Height=950,ShowInTaskbar=false};
                try{
                    window.Show();window.Activate();Pump(window.Dispatcher);
                    var input=Children(view).OfType<TextBox>().Single(t=>t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="CurrentItemMat.Kolic");
                    var next=view is UlazMaterijala?(Control)view.FindName("txtFvrijed"):(Control)view.FindName("btnSnimi");
                    Check(input.Text=="0,00","Initial zero display");
                    Keyboard.Focus(next);Keyboard.Focus(input);Pump(window.Dispatcher);
                    Check(input.SelectionLength==input.Text.Length&&input.SelectionStart==0,"Keyboard focus selects all");
                    Keyboard.Focus(next);Pump(window.Dispatcher);
                    var mouse=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent};input.RaiseEvent(mouse);Pump(window.Dispatcher);
                    Check(mouse.Handled&&input.IsKeyboardFocusWithin&&input.SelectionLength==input.Text.Length,"First mouse click focuses and selects all");
                    foreach(var example in new[]{new{Text="12",Value=12m,Display="12,00"},new{Text="12,5",Value=12.5m,Display="12,50"},new{Text="1000",Value=1000m,Display="1.000,00"},new{Text="0",Value=0m,Display="0,00"}}){
                        Keyboard.Focus(input);input.SetCurrentValue(TextBox.TextProperty,example.Text);Enter(input);
                        Check(data.CurrentItemMat.Kolic==example.Value,"Parsed quantity "+example.Text);
                        Check(input.Text==example.Display,"Formatted quantity "+example.Display);
                        Check(next.IsKeyboardFocusWithin,"Enter retains intended next focus");
                    }
                    Keyboard.Focus(input);input.SetCurrentValue(TextBox.TextProperty,"abc");Enter(input);
                    Check(Validation.GetHasError(input)&&input.IsKeyboardFocusWithin&&input.Text=="abc"&&data.CurrentItemMat.Kolic==0m,"Invalid text remains selected for correction without changing model or advancing");
                    input.SetCurrentValue(TextBox.TextProperty,"8");Enter(input);Check(data.CurrentItemMat.Kolic==8m&&input.Text=="8,00","Valid correction recovers");
                    Console.WriteLine("PASS "+view.GetType().Name+": default zero; keyboard/mouse selection; integer/decimal/grouped formatting; Enter focus; invalid input; correction");
                }finally{window.Close();}
            }
            Console.WriteLine("ALL PASSED "+checks+"; isolated data contexts; no SQL/Oracle calls or business rows.");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
