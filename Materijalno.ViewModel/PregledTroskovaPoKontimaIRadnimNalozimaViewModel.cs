using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
namespace Materijalno.ViewModel {
 public class PregledTroskovaPoKontimaIRadnimNalozimaViewModel : SummaryReportViewModel {
  public PregledTroskovaPoKontimaIRadnimNalozimaViewModel(GlavniViewModel main) : base(main, SummaryReportKind.CostsByWorkOrder) { }
 }
}