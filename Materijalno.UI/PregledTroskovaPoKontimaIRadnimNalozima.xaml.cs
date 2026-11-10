using System.Windows.Controls;
namespace Materijalno.UI {
 public partial class PregledTroskovaPoKontimaIRadnimNalozima : UserControl {
  public PregledTroskovaPoKontimaIRadnimNalozima() { InitializeComponent(); Helpers.SummaryReportPreview.Attach(this); }
 }
}