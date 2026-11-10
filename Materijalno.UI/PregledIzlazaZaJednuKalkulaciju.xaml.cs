using System.Windows.Controls;
namespace Materijalno.UI {
 public partial class PregledIzlazaZaJednuKalkulaciju : UserControl {
  public PregledIzlazaZaJednuKalkulaciju() { InitializeComponent(); Helpers.SummaryReportPreview.Attach(this); }
 }
}