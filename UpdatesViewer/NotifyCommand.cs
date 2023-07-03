using System;
using System.Windows.Forms;
using System.Windows.Input;

namespace UpdatesViewer
{
    public class NotifyCommand : ICommand
    {
        private readonly NotifyIcon _notifyIcon;
        public NotifyCommand(NotifyIcon notifyIcon) 
        {
            _notifyIcon = notifyIcon;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {

            _notifyIcon.ShowBalloonTip(3000, "Журнал товаров", $"{parameter}", ToolTipIcon.Info);
        }
    }
}
