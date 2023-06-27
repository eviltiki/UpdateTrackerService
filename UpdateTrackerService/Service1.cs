using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UpdateTracker.Models;

namespace UpdateTrackerService
{
    public partial class Service1 : ServiceBase
    {
        Logger logger;

        public Service1()
        {
            InitializeComponent();

            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            System.Diagnostics.Debugger.Launch();
            logger = new Logger();
            Thread loggerThread = new Thread(new ThreadStart(logger.Start));
            loggerThread.Start();
        }

        protected override void OnStop()
        {
            logger.Stop();
            Thread.Sleep(1000);
        }

        class Logger
        {

            object locker = new object();
            bool enabled = true;

            private ServiceBroker serviceBroker;
            private string connectionString = $@"Data Source=.\ARTHURSQL;Initial Catalog=ShopDB;Integrated Security=false;User ID = UpdateTrackerService;Password = 123321; MultipleActiveResultSets=true";
            private string command = $"SELECT Id FROM dbo.Product";
            private DateTime appStartTime;

            public Logger()
            {
                FileStream fstream = null;

                try
                {
                    fstream = new FileStream("G:\\Практика\\UpdateTrackerService\\log.txt", FileMode.Create);
                }
                catch (Exception ex)
                { }
                finally
                {
                    fstream?.Close();
                }

                serviceBroker = new ServiceBroker(connectionString, command);

                appStartTime = DateTime.Now;

                serviceBroker.OnMessageSent += new ServiceBroker.MessageHandler(RecordEntry);

                serviceBroker.StartListen();
            }

            public void Start()
            {
                while (enabled)
                {
                    Thread.Sleep(1000);
                }
            }
            public void Stop()
            {
                SqlDependency.Stop(connectionString);
                enabled = false;
            }

            private void RecordEntry(object sender, string Message)
            {
                lock (locker)
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        SqlCommand cmd = new SqlCommand($"EXEC prGetTrackerTable '{appStartTime}'", connection);

                        connection.Open();

                        SqlDataReader reader = cmd.ExecuteReader();

                        int id;
                        string productName;
                        double productPrice;
                        DateTimeOffset date;

                        while (reader.Read())
                        {
                            id = Convert.ToInt32(reader["Id"]);
                            productName = Convert.ToString(reader["Name"]);
                            productPrice = Convert.ToDouble(reader["Price"]);
                            date = Convert.ToDateTime(Convert.ToString(reader["Date"]));

                            Product product = new Product { Id = id, Name = productName, Price = productPrice, Date = date };

                            using (StreamWriter writer = new StreamWriter("G:\\Практика\\UpdateTrackerService\\log.txt", true))
                            {
                                writer.WriteLine(String.Format("{0}", product));
                                writer.Flush();
                            }

                        }
                    }

                    serviceBroker.StartListen();
                }
            }
        }
    }
}
