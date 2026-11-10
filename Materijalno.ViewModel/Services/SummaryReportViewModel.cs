using System;
using System.ComponentModel;
using System.Windows;
using Materijalno.Model.EntityModels;
using Materijalno.Model.Services;

namespace Materijalno.ViewModel.Services
{
    public abstract class SummaryReportViewModel : INotifyPropertyChanged
    {
        private DateTime _from, _to;
        private readonly SummaryReportKind _kind;
        public DateTime Od { get => _from; set { _from = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Od))); } }
        public DateTime Do { get => _to; set { _to = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Do))); } }
        public Mat CurrentItemMat { get; } = new Mat();
        public RelayCommand StampaCommand { get; }
        public RelayCommand CancelSifarnikSkladistaCommand { get; }
        public event Action<SummaryReport> ReportRequested;
        public event PropertyChangedEventHandler PropertyChanged;
        protected SummaryReportViewModel(GlavniViewModel main, SummaryReportKind kind)
        {
            _kind = kind; int year = new MaterialPostingService().ActiveYear();
            Od = new DateTime(year, 1, 1); Do = new DateTime(year, 12, 31);
            StampaCommand = new RelayCommand(Print);
            CancelSifarnikSkladistaCommand = new RelayCommand(() => main.OdabraniVM = null);
        }
        public void Print()
        {
            try { ReportRequested?.Invoke(SummaryReports.Create(_kind, Od, Do, CurrentItemMat.Kljnaz, CurrentItemMat.Brfak)); }
            catch (Exception e) { MessageBox.Show(e.Message, "Izvještaj", MessageBoxButton.OK, MessageBoxImage.Information); }
        }
    }
}
