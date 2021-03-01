﻿using System;
using System.Collections.Generic;
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

        bool connected = false;

        public static RoutedCommand ConnectCmd = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();
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
            client = new OpcClient(endpointURL, nodeIdToSubscribe, nodeIdFile, autoAccept, 0);
            client.Run();
        }
    }
}
