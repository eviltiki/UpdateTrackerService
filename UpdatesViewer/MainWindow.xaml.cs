using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ServiceProcess;
using System.ComponentModel;
using System.Threading;
using System.Security.AccessControl;
using System.IO.MemoryMappedFiles;
using System.Drawing;
using System.Runtime.InteropServices;
using UpdatesViewer.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using System.Xml.Linq;

namespace UpdatesViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly NotifyIcon _notifyIcon;
        ServiceController service;
        bool enabled = true;
        NotifyViewModel viewModel;

        static EventWaitHandle handleMessage;

        static EventWaitHandle handleOpenReceiver;

        static MemoryMappedFile memoryMapped;

        string user;

        public MainWindow()
        {
            InitializeComponent();

            if (EventWaitHandle.TryOpenExisting("Global\\OnMessage", out handleMessage) == false)
            {
                handleMessage = new EventWaitHandle
                (
                    false,
                    EventResetMode.AutoReset,
                    "Global\\OnMessage"
                );

                EventWaitHandleSecurity handleMessageSec = new EventWaitHandleSecurity();

                EventWaitHandleAccessRule rule = new EventWaitHandleAccessRule("ARTHUR-PC\\Everyone",
                    EventWaitHandleRights.Synchronize |
                    EventWaitHandleRights.Modify,
                    AccessControlType.Allow);

                handleMessageSec.AddAccessRule(rule);

                handleMessage.SetAccessControl(handleMessageSec);
            }

            if (EventWaitHandle.TryOpenExisting("Global\\OnOpenReceiver", out handleOpenReceiver) == false)
            {
                handleOpenReceiver = new EventWaitHandle
                (
                    false,
                    EventResetMode.ManualReset,
                    "Global\\OnOpenReceiver"
                );

                EventWaitHandleSecurity handleMessageSec = new EventWaitHandleSecurity();

                EventWaitHandleAccessRule rule = new EventWaitHandleAccessRule("ARTHUR-PC\\Everyone",
                    EventWaitHandleRights.Synchronize |
                    EventWaitHandleRights.Modify,
                    AccessControlType.Allow);

                handleMessageSec.AddAccessRule(rule);

                handleOpenReceiver.SetAccessControl(handleMessageSec);
            }

            this.Hide();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = new System.Drawing.Icon("Resources/icon.ico");
            _notifyIcon.Visible = true;
            _notifyIcon.BalloonTipTitle = "OK";
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Выключить", null, OnClose);

            this.DataContext = new NotifyViewModel(_notifyIcon);

            viewModel = (NotifyViewModel)DataContext;

            user = Environment.UserDomainName + "\\"
                + Environment.UserName;

            string serviceName = "UpdateTrackerService";

            service = new ServiceController(serviceName);

            ServiceStart();

            memoryMapped = MemoryMappedFile.OpenExisting("Global\\mapmemory");

            Thread thread = new Thread(ServerStart);
            thread.Name = $"Server";
            thread.Start();
        }

        private void ServerStart()
        {
            string pId;
            string pName;
            string pPrice;
            string pDate;

            while (enabled)
            {
                handleMessage.WaitOne();

                if (viewModel.NotifyCommand.CanExecute(null))
                {
                    using (var accessor = memoryMapped.CreateViewAccessor(0, 255, MemoryMappedFileAccess.Read))
                    {
                        int size = 255;
                        var readOut = new byte[size];

                        accessor.ReadArray(0, readOut, 0, size);
                        var finalValue = Encoding.UTF8.GetString(readOut);
                        string[] strArr = finalValue.Split(";"); //$"Код={id};Название={name};Цена={price};Дата={date}";

                        pId = strArr[0].Split("=")[1];
                        pName = strArr[1].Split("=")[1];
                        pPrice = strArr[2].Split("=")[1];
                        pDate = strArr[3].Split("=")[1];

                    }
                    viewModel.NotifyCommand.Execute($"Товар {pName} поступил в продажу.");
                }

                handleOpenReceiver.Set();
            }
        }

        private void OnClose(object sender, EventArgs e)
        {
            enabled = false;
            _notifyIcon.Dispose();
            ServiceStop();
            Environment.Exit(0);
        }

        private void ServiceStart()
        {
            double timeoutMilliseconds = 10_000;
            TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

            if (service.Status == ServiceControllerStatus.Running || service.Status == ServiceControllerStatus.StartPending)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }

            try
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch (InvalidOperationException exc)
            {
                System.Windows.MessageBox.Show($"Не удалось запустить сервис.\n\n{exc}");
            }
        }

        private void ServiceStop()
        {
            double timeoutMilliseconds = 10_000;
            TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

            try
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch (InvalidOperationException exc)
            {
                System.Windows.MessageBox.Show($"Произошла ошибка во время прекращения работы сервиса.\n\n{exc}");
            }

        }
    }

}
