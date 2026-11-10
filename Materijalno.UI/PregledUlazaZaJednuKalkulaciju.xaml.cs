using System.Windows.Controls;
namespace Materijalno.UI {
 public partial class PregledUlazaZaJednuKalkulaciju : UserControl {
  public PregledUlazaZaJednuKalkulaciju() { InitializeComponent(); Helpers.SummaryReportPreview.Attach(this); }
 }
}