using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UpdateTracker.Models;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Threading;
using System.Runtime.InteropServices;

namespace UpdateTrackerService
{
    public partial class Service1 : ServiceBase
    {
        Logger logger;
        DateTracker dataTracker;
        private string connectionString = $@"Data Source=.\ARTHURSQL;Initial Catalog=ShopDB;Integrated Security=false;User ID = UpdateTrackerService;
                                                Password = 123321; MultipleActiveResultSets=true";

        static EventWaitHandle handleMessage;
        static EventWaitHandle handleOpenReceiver;

        public Service1()
        {
            InitializeComponent();

            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            //System.Diagnostics.Debugger.Launch(); // для отладки!!!

            try 
            {
                handleMessage = EventWaitHandle.OpenExisting("Global\\OnMessage");
            }
            catch (Exception ex) 
            {
                throw new Exception("Не удалось найти OnMessage event handler.");
            }

            try
            {
                handleOpenReceiver = EventWaitHandle.OpenExisting("Global\\OnOpenReceiver");
            }
            catch (Exception ex)
            {
                throw new Exception("Не удалось найти OnOpenReceiver event handler.");
            }

            dataTracker = new DateTracker(connectionString); // Класс DateTracker отвечает за отслеживание и своевременное обновление товаров,
                                                             // которые поступили на продажу. Когда дата будет совпадать с настоящей,
                                                             // то флаг OnSale будет установлен в 1. 

            Thread dateTrackerThread = new Thread(new ThreadStart(dataTracker.Start)); // поток для работы этого трекера
            dateTrackerThread.Start();

            logger = new Logger(connectionString);             /* Ключевой класс данного сервиса Logger. Он содержит основной функционал, который будет выполняться сервисом.
                                                  Создаем объект заданного класса. */

            Thread loggerThread = new Thread(new ThreadStart(logger.Start)); // Создаем новый поток, аргумент new ThreadStart - это делегает, представляющий выполняемое в потоке действие.
            
            loggerThread.Start(); // Вызываем метод Start() класса Logger, который зацикливает данный поток до тех пор, пока заданный объект loggerThread доступен.

        }

        protected override void OnStop()
        {
            dataTracker.Stop();
            logger.Stop();
            Thread.Sleep(1000);
        }

        class Logger
        {

            object locker = new object(); // При инициализации объекта класса Logger будет создаваться объект-затычка locker,
                                          // он необходим для того, чтобы разграничить доступ к файлу log.txt, чтобы не нарушать очередь поступления данных. 
            bool enabled = true; // включен ли объект.

            string connectionString;
            private ServiceBroker serviceBroker; // создаем serviceBroker - это объект класса ServiceBroker, который будет прослушивать базу данных и обрабатывать поступление новых записей.
            private string command = $"SELECT OnSale FROM dbo.Product"; //при изменении результата данного запроса к базе данных ShopDB будет вызываться событие OnChange класса ServiceBroker. 
            private DateTime appStartTime; // время старта работы сервиса.
            private Queue<Product> Products = new Queue<Product>();

            public Logger(string connectionString)
            {
                this.connectionString = connectionString;

                FileStream fstream = null; // работа с файлом log.txt, его создание, если он отсутствует. 

                try
                {
                    fstream = new FileStream("G:\\Практика\\UpdateTrackerService\\log.txt", FileMode.Create); // файл, где хранится лог
                }
                catch (Exception ex)
                { }
                finally
                {
                    fstream?.Close();
                }

                serviceBroker = new ServiceBroker(connectionString, command); //  инициализируем serviceBorker

                appStartTime = DateTime.Now; // инициализируем время старта работы сервиса.

                serviceBroker.OnMessageSent += new ServiceBroker.MessageHandler(RecordEntry); // вешаем обработчик RecordEntry на событие OnMessageSent, которое срабатывает при вызове события OnChange

                serviceBroker.StartListen(); // начинаем прослушивать базу данных.
            }

            public void Start()
            {
                while (enabled)
                {

                    if (Products.Count > 0)
                    {
                        var product = Products.Dequeue();

                        WaitHandle.SignalAndWait(handleMessage, handleOpenReceiver);

                        handleOpenReceiver.Reset();
                    }

                    Thread.Sleep(3000);
                }
            }

            public void Stop()
            {
                SqlDependency.Stop(connectionString); // прекращаем прослушивать базу данных.
                enabled = false; // делаем объект недоступным.
            }

            private void RecordEntry(object sender, string Message)
            {
                lock (locker) // блокируем данный участок кода для остальных потоков, пока первый на очереди поток не закончит работу с файлом.
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        SqlCommand cmd = new SqlCommand($"EXEC prGetProductOnSale", connection); // процедура, которая возвращает товары, которые помечены на продажу,
                                                                                                 // и устанавливает им флаг OnSale = 2

                        connection.Open();

                        SqlDataReader reader = cmd.ExecuteReader();

                        int id;
                        string productName;
                        double productPrice;
                        DateTimeOffset date;

                        while (reader.Read()) // записываем в файл все поступившие новые объекты из базы данных. 
                        {
                            id = Convert.ToInt32(reader["Id"]);
                            productName = Convert.ToString(reader["Name"]);
                            productPrice = Convert.ToDouble(reader["Price"]);
                            date = Convert.ToDateTime(Convert.ToString(reader["Date"]));

                            Product product = new Product { Id = id, Name = productName, Price = productPrice, Date = date };

                            Products.Enqueue(product);
                        }
                    }

                    serviceBroker.StartListen(); // после срабатывания события OnChange класса ServiceBroker необходимо заново начать прослушивание бд,
                                                 // т.к. необходимо снова повесить обработчик на данное событие.

                }
            }
        }

        class DateTracker
        {
            object locker = new object();

            bool enabled = true;

            string connectionString;

            public DateTracker(string connectionString)
            {
                this.connectionString = connectionString;

            }

            public void Start()
            {
                while (enabled)
                {
                    UpdateTableData(); // каждую секунду вызывает метод, который обновляет данные, если это необходимо.
                    Thread.Sleep(1000);

                }
            }
            public void Stop()
            {
                enabled = false;
            }

            public void UpdateTableData()
            {
                lock (locker)
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        SqlCommand cmd = new SqlCommand($"EXEC prUpdateProductData", connection); // вызывает хранимую в бд процедуру, которая обновляет флаг
                                                                                                  // OnSale (0 - не в продаже, 1 - отмечен на продажу, 2 - в продаже)

                        connection.Open();

                        int count = cmd.ExecuteNonQuery();
                    }
                }
            }

        }
    }
}
