using Materijalno.Model.Services;
using Materijalno.ViewModel.Services;
namespace Materijalno.ViewModel {
 public class RekapitulacijaTroskovaViewModel : SummaryReportViewModel {
  public RekapitulacijaTroskovaViewModel(GlavniViewModel main) : base(main, SummaryReportKind.Costs) { }
 }
}