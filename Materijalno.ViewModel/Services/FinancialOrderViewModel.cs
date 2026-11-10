using System;
using System.ComponentModel;
using System.Windows;
using Materijalno.Model.Services;

namespace Materijalno.ViewModel.Services
{
    public abstract class FinancialOrderViewModel : INotifyPropertyChanged
    {
        private string _number;
        private readonly string _status;
        public string BrojMedjuskladisnice { get => _number; set { _number = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BrojMedjuskladisnice))); } }
        public RelayCommand FormirajCommand { get; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected FinancialOrderViewModel(string status)
        { _status = status; FormirajCommand = new RelayCommand(Formiraj); }

        public Materijalno.Model.EntityModels.FinancialBatch PrepareLocal()
        {
            return new FinancialPostingService().Prepare(_status, BrojMedjuskladisnice);
        }

        public void Formiraj()
        {
            try
            {
                var service = new FinancialPostingService();
                var batch = PrepareLocal();
                if (batch.State == "Sent")
                {
                    MessageBox.Show("Nalog " + batch.Number.ToString("D4") + " je već prenesen u finansijsko knjigovodstvo.", "Finansijski nalog", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (!DatabaseConfiguration.OracleExportEnabled)
                {
                    MessageBox.Show("Nalog " + batch.Number.ToString("D4") + " je formiran i uravnotežen. Nalog je sačuvan i čeka prijenos u finansijsko knjigovodstvo.", "Finansijski nalog", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                batch = service.Export(batch.Id, new OracleFinancialGateway());
                MessageBox.Show("Nalog " + batch.Number.ToString("D4") + " je prenesen u Oracle. Ponovni poziv ne duplira stavke.", "Finansijski nalog", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            { MessageBox.Show(exception.Message, "Finansijski nalog", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}
