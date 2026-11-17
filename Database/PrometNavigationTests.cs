using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using Materijalno.Model.EntityModels;
using Materijalno.UI;
using Materijalno.ViewModel;
using Materijalno.ViewModel.Services;

public static class PrometNavigationTests
{
    static int checks;
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static void Field(object instance,string name,object value){instance.GetType().GetField(name,Fields).SetValue(instance,value);}
    static void State(object vm,bool pending){
        if(vm is UlazMaterijalaViewModel) Field(vm,"novaStavkaUToku",pending);
        if(vm is IzlazMaterijalaViewModel) IzlazMaterijalaViewModel.isNovaKalkulacijaClicked=pending;
        if(vm is PovratMaterijalaViewModel) Field(vm,"isNovaKalkulacijaClicked",pending);
        if(vm is MedjuskladisnicaViewModel){var f=vm.GetType().GetField("_workflow",Fields);f.SetValue(vm,Enum.Parse(f.FieldType,pending?"Editing":"SavedLine"));}
    }
    static ICommand Menu(GlavniViewModel shell,Action action)=>(ICommand)typeof(GlavniViewModel).GetMethod("CreateMenuCommand",Fields).Invoke(shell,new object[]{action});
    sealed class Entry:IActivePrometEntry {public bool HasUnsavedEntry {get;set;}}
    [STAThread] public static int Main(string[] args)
    {
        try{
            XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var names=XDocument.Load(args[0]).Descendants(ns+"Button").Select(b=>(string)b.Attribute("Command"))
                .Where(s=>s!=null&&s.StartsWith("{Binding ")).Select(s=>s.Substring(9).TrimEnd('}')).Distinct().ToArray();
            Check(names.Length>=30,"All left-menu routes discovered");
            foreach(var type in new[]{typeof(UlazMaterijalaViewModel),typeof(IzlazMaterijalaViewModel),typeof(MedjuskladisnicaViewModel),typeof(PovratMaterijalaViewModel)}){
                UlazMaterijalaViewModel.isNovaKalkulacijaClicked=false;IzlazMaterijalaViewModel.isNovaKalkulacijaClicked=false;
                // Test actual workflow state without constructing data-loading VMs or creating business rows.
                var vm=FormatterServices.GetUninitializedObject(type);var entry=(IActivePrometEntry)vm;
                var draft=new Mat{Id=987654,Ident="094001",IsDraft=true};Field(vm,"currentItemMat",draft);State(vm,true);
                Check(entry.HasUnsavedEntry,type.Name+" protects active entry");
                int warnings=0;var guard=new PrometNavigationGuard(message=>{Check(message.Contains("SNIMI")&&message.Contains("ODUSTANI"),"Actionable warning");warnings++;});
                var shell=new GlavniViewModel();Field(shell,"_navigationGuard",guard);shell.OdabraniVM=vm;
                foreach(string name in names){
                    var command=(ICommand)typeof(GlavniViewModel).GetProperty(name).GetValue(shell);
                    Check(command!=null&&command.CanExecute(null),"Menu remains clickable for warning: "+name);
                    int previous=warnings;command.Execute(null);
                    Check(warnings==previous+1&&ReferenceEquals(shell.OdabraniVM,vm),"Blocked before destination constructor: "+name);
                    Check(ReferenceEquals(type.GetField("currentItemMat",Fields).GetValue(vm),draft)&&entry.HasUnsavedEntry,"Draft and input retained");
                }
                int opens=0;var route=Menu(shell,()=>opens++);
                // A failed validation leaves the same pending entry; completion of fields alone is insufficient.
                draft.Kolic=1;route.Execute(null);Check(opens==0,"Still blocked until successful save");
                State(vm,false);draft.IsDraft=false;Check(!entry.HasUnsavedEntry,"Saved-line state permits menu");
                route.Execute(null);Check(opens==1,"Menu allowed immediately after save");
                State(vm,true);draft.IsDraft=true;route.Execute(null);Check(opens==1,"Next/new line locks menu again");
                shell.OdabraniVM=new object();route.Execute(null);Check(opens==1,"Internal lookup cannot bypass pending entry");
                State(vm,false);Field(vm,"currentItemMat",null);route.Execute(null);Check(opens==2,"Completed cancel releases menu");
                shell.OdabraniVM=vm;Field(vm,"currentItemMat",draft);Check(entry.HasUnsavedEntry,"Persisted draft protected even if workflow flags reset");
                Field(vm,"currentItemMat",new Mat());Check(!entry.HasUnsavedEntry,"Idle empty screen does not lock menu");
                Console.WriteLine("PASS "+type.Name+": "+names.Length+" menu routes; save/cancel release; next line; validation failure; lookup; persisted-draft fallback");
            }
            // Exercise the real sidebar Click handler and command order, including its deferred header update.
            var view=new GlavniView();var main=(GlavniViewModel)view.DataContext;int notices=0;
            Field(main,"_navigationGuard",new PrometNavigationGuard(_=>notices++));
            var current=new Entry{HasUnsavedEntry=true};main.OdabraniVM=current;
            var title=(TextBlock)view.FindName("PageText");title.Text="Izlaz materijala";
            var selected=(Button)view.FindName("izlazMaterijala");selected.Tag="Selected";
            var target=(Button)view.FindName("dnevnik");target.Command=main.DnevnikCommand;
            target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,target));target.Command.Execute(null);
            view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
            Check(notices==1&&ReferenceEquals(main.OdabraniVM,current),"Real sidebar command blocks navigation");
            Check(title.Text=="Izlaz materijala"&&(string)selected.Tag=="Selected"&&target.Tag==null,"Blocked click preserves title and selected menu item");
            current.HasUnsavedEntry=false;target.Command=Menu(main,()=>main.OdabraniVM=new object());
            target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,target));target.Command.Execute(null);
            view.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
            Check(title.Text=="Dnevnik"&&(string)target.Tag=="Selected"&&selected.Tag==null,"Successful navigation updates title and selected item");
            Console.WriteLine("PASS Actual sidebar presentation remains on the active page when blocked");
            Console.WriteLine("ALL PASSED "+checks+"; four Promet screens; "+names.Length+" menu commands each; no SQL or Oracle calls.");return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
