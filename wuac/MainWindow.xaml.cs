using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class MainWindow : Window
    {
        string endpointURL = "";
        string nodeIdToSubscribe = "";
        string nodeIdFile = "";
        bool autoAccept = true;
        OpcClient client;
        static ObservableCollection<MessageData> messages = new ObservableCollection<MessageData>();

        bool connected = false;

        public static RoutedCommand ConnectCmd = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();
            dgMessage.ItemsSource = messages;
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
            nodeIdToSubscribe = "TESTMOD2/SSGN1/OUT.CV";
            endpointURL = "opc.tcp://M1:9409/DvOpcUaServer";
            if (client is not null)
            {
                client.MessageRecieved -= OnMessage; //prevent leak
            }
            client = new OpcClient(endpointURL, nodeIdToSubscribe, nodeIdFile, autoAccept, 0);
            client.MessageRecieved += OnMessage;
            client.Run();
        }

        static void OnMessage(object sender, MessageEventArgs e)
        {
            messages.Add(new MessageData() { Time=e.Time, Message = e.Message });
        }
    }


    class MessageEventArgs : EventArgs
    {
        public DateTime Time { get; set; }

        public string Message { get; set; }
    }

    class MessageData
    {
        public DateTime Time { get; set; }

        public string Message { get; set; }
    }

}
