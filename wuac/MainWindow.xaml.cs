using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wuac
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string endpointURL = "";
        string nodeIdToSubscribe = "";
        string nodeIdFile = "";
        bool autoAccept = true;
        OpcClient client;
        static ObservableCollection<MessageData> messages = new ObservableCollection<MessageData>();

        bool connected = false;

        public static RoutedCommand ConnectCmd = new RoutedCommand();
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _dataValues;
        public string DataValues {
            get { return _dataValues; }
            set { 
                if (value != _dataValues)
                {
                    _dataValues = value;
                    OnPropertyChanged(nameof(DataValues));
                }
            } 
        }

        public MainWindow()
        {
            InitializeComponent();
            dgMessage.ItemsSource = messages;
            DataValues = "Init";
        }

        private void mnuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ConnectCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var c = new Connect();
            c.Owner = this;
            c.ShowDialog();
            tbStatus.Text = c.txtURL.Text;
            nodeIdToSubscribe = "ns=2;s=0:TESTMOD2/SGGN1/OUT.CV";
            endpointURL = "opc.tcp://M1:9409/DvOpcUaServer";
            if (client is not null)
            {
                client.MessageRecieved -= OnMessage; //prevent leak
            }
            client = new OpcClient(endpointURL, nodeIdToSubscribe, nodeIdFile, autoAccept, 0);
            client.MessageRecieved += OnMessage;
            client.Notification += OnNotification;
            client.Run();
        }

        static void OnMessage(object sender, MessageEventArgs e)
        {
            messages.Add(new MessageData() { Time=e.Time, Message = e.Message, Type=e.Type });
        }

        private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                DataValues = $"{item.ResolvedNodeId}:{value.SourceTimestamp}:{value.StatusCode}:{value.Value}";
                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
            }
        }
    }

    class MessageCategory
    {
        private MessageCategory(string value) { Value = value; }

        public string Value { get; set; }

        public static MessageCategory Trace { get { return new MessageCategory("Trace"); } }
        public static MessageCategory Debug { get { return new MessageCategory("Debug"); } }
        public static MessageCategory Info { get { return new MessageCategory("Info"); } }
        public static MessageCategory Warning { get { return new MessageCategory("Warning"); } }
        public static MessageCategory Error { get { return new MessageCategory("Error"); } }
    }

    class MessageEventArgs : EventArgs
    {
        public DateTime Time { get; set; }
        public MessageCategory Type { get; set; }

        public string Message { get; set; }
    }

    class MessageData
    {
        public DateTime Time { get; set; }
        public MessageCategory Type { get; set; }
        public string Message { get; set; }
    }

}
