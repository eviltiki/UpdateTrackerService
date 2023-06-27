using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateTrackerService
{
    public class ServiceBroker
    {
        private string connectionString = "";
        private string command = "";
        private SqlConnection connection;
        private SqlDependency dep;


        public delegate void MessageHandler(object sender, string messagename);
        public event MessageHandler OnMessageSent = null;

        public ServiceBroker(string connectionString, string command)
        {

            if (connectionString == "" || command == "") throw new ApplicationException("Connection string or command was not specified.");

            this.connectionString = connectionString;
            this.command = command;

            SqlDependency.Start(connectionString);

            connection = new SqlConnection(connectionString);
        }

        public void StartListen()
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand cmd = new SqlCommand(command, connection);

                    cmd.CommandType = CommandType.Text;

                    cmd.Notification = null;

                    dep = new SqlDependency(cmd);

                    dep.OnChange += OnChange;

                    if (connection.State == ConnectionState.Closed)
                        connection.Open();

                    var sqlDataReader = cmd.ExecuteReader();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void StopListen()
        {
            SqlDependency.Stop(connectionString);
        }

        private void OnChange(object sender, SqlNotificationEventArgs e)
        {
            dep.OnChange -= OnChange;
            dep = null;

            OnMessageSent?.Invoke(this, "Message sent");
        }
    }
}
