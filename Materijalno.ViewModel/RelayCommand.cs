using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Materijalno.ViewModel
{
    public class RelayCommand : ICommand
    {
        private Action _action;
        private readonly Func<bool> _canExecute;
        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action action, Func<bool> canExecute = null)
        {
            //_action = action;
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _canExecute = canExecute ?? (() => true);  // Default to always true if no canExecute function is provided.
        }
        public bool CanExecute(object parameter)
        {
            return _canExecute();
        }

        public void Execute(object parameter)
        {
            _action();
        }

        // Call this method when the result of CanExecute() may change.
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
