using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace wuac
{
    enum ExitCode : int
    {
        Ok = 0,
        ErrorCreateApplication = 0x11,
        ErrorDiscoverEndpoints = 0x12,
        ErrorCreateSession = 0x13,
        ErrorBrowseNamespace = 0x14,
        ErrorCreateSubscription = 0x15,
        ErrorMonitoredItem = 0x16,
        ErrorAddSubscription = 0x17,
        ErrorRunning = 0x18,
        ErrorNoKeepAlive = 0x30,
        ErrorInvalidCommandLine = 0x100
    };

    class OpcClient
    {
        const int ReconnectPeriod = 10;
        Session session;
        SessionReconnectHandler reconnectHandler;
        string endpointURL;
        string nodeIdToSubscribe;
        string nodeIdFile;
        int clientRunTime = Timeout.Infinite;
        static bool autoAccept = false;
        static ExitCode exitCode;
        static Stopwatch m_sw = new Stopwatch();
        static bool m_sw_init = true;
        //static bool is_console_out = true;
        static int count = 0;
        static int node_count = 0; //marker by number of node IDs loaded
        static ManualResetEvent quitEvent;
        static string last_node_id; //marker by node id
        static int cycle_count = 0; //number of marker counts
        static StreamWriter sw = null;
        static StringBuilder sb = new StringBuilder();

        public OpcClient(string _endpointURL, string _nodeIdToSubscribe, string _nodeIdFile, bool _autoAccept, int _stopTimeout)
        {
            endpointURL = _endpointURL;
            nodeIdToSubscribe = _nodeIdToSubscribe;
            nodeIdFile = _nodeIdFile;
            autoAccept = _autoAccept;
            clientRunTime = _stopTimeout <= 0 ? Timeout.Infinite : _stopTimeout * 1000;
        }

        public void Run()
        {
            try
            {
                ConsoleClient().Wait();
            }
            catch (Exception e)
            {
                Utils.Trace("ServiceResultException:" + e.Message);
                MessageEventArgs args = new MessageEventArgs();
                args.Message = e.Message;
                args.Time = DateTime.Now;
                args.Type = MessageCategory.Error;
                OnMessageRecieved(args);
                return;
            }

            //quitEvent = new ManualResetEvent(false);
            //try
            //{
            //    Console.CancelKeyPress += (sender, eArgs) =>
            //    {
            //        quitEvent.Set();
            //        eArgs.Cancel = true;
            //    };
            //}
            //catch
            //{
            //}

            //// wait for timeout or Ctrl-C
            //quitEvent.WaitOne(clientRunTime);

            //// return error conditions
            //if (session.KeepAliveStopped)
            //{
            //    exitCode = ExitCode.ErrorNoKeepAlive;
            //    return;
            //}

            //if (sw != null)
            //{
            //    sw.Close();
            //    sw.Dispose();
            //}

            //exitCode = ExitCode.Ok;
        }

        public static ExitCode ExitCode { get => exitCode; }

        private async Task ConsoleClient()
        {
            Console.WriteLine("1 - Create an Application Configuration.");
            exitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Core Sample Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Wicked.Opc.Ua.Client"
            };

            // load the application configuration.
            ApplicationConfiguration config = null;
            try
            {
                config = await application.LoadApplicationConfiguration(false);
            } catch (Opc.Ua.ServiceResultException e)
            {
                if (e.Message.Contains("Could not load configuration file."))
                {
                    string filePath = ApplicationConfiguration.GetFilePathFromAppConfig(application.ConfigSectionName);
                    string msg = $"{e.Message} - {filePath}";
                    MessageEventArgs args = new MessageEventArgs();
                    args.Message = msg;
                    args.Time = DateTime.Now;
                    args.Type = MessageCategory.Error;
                    OnMessageRecieved(args);
                }
                throw;
            }

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (haveAppCertificate)
            {
                config.ApplicationUri = X509Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
            else
            {
                Console.WriteLine("    WARN: missing application certificate, using unsecure connection.");
            }

            Console.WriteLine("2 - Discover endpoints of {0}.", endpointURL);
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15000);
            Console.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Console.WriteLine("3 - Create a session with OPC UA server.");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            session = await Session.Create(config, endpoint, false, "OPC UA Console Client - " + System.Environment.MachineName, 60000, new UserIdentity(new AnonymousIdentityToken()), null);
           
            // register keep alive handler
            session.KeepAlive += Client_KeepAlive;

            Console.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            exitCode = ExitCode.ErrorCreateSubscription;
            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };

            Console.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");
            exitCode = ExitCode.ErrorMonitoredItem;

            var list = new List<MonitoredItem>();

            var sw = new Stopwatch();
            if (nodeIdFile.Length > 0)
            {
                System.IO.StreamReader file = new System.IO.StreamReader(nodeIdFile);
                string line;

                sw.Start();
                Console.WriteLine("Loading node IDs...");
                //int cnt = 0;
                while ((line = file.ReadLine()) != null)
                {
                    //var nodeIds = new List<NodeId> { new NodeId(line) };
                    //var dispNames = new List<string>();
                    //var errors = new List<ServiceResult>();
                    //session.ReadDisplayName(nodeIds, out dispNames, out errors);
                    //var _displayName = dispNames[0];
                    var item = new MonitoredItem(subscription.DefaultItem)
                    {
                        //DisplayName = _displayName,
                        StartNodeId = line
                    };
                    list.Add(item);
                    //Console.WriteLine("{1}: Adding {0}", line, ++cnt);
                    node_count++;
                    last_node_id = line;
                }
                sw.Stop();
                Console.WriteLine("Loading node IDs...done in {0} for node count {1}", sw.Elapsed, node_count);
            }
            else
            {
                var nodeIds = new List<NodeId> { new NodeId(nodeIdToSubscribe) };
                var dispNames = new List<string>();
                var errors = new List<ServiceResult>();
                session.ReadDisplayName(nodeIds, out dispNames, out errors);
                var _displayName = dispNames[0];
                var item = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = _displayName,
                    StartNodeId = nodeIdToSubscribe
                };
                list.Add(item);
                node_count = 1;
                last_node_id = nodeIdToSubscribe;
            }

            //list.ForEach(i => i.Notification += OnNotification);
            list.ForEach(i => i.Notification += this.Notification);
            subscription.AddItems(list);

            Console.WriteLine("7 - Add the subscription to the session.");
            sw.Start();
            exitCode = ExitCode.ErrorAddSubscription;
            session.AddSubscription(subscription);
            subscription.Create();
            sw.Stop();
            Console.WriteLine("Create subscription took {0}", sw.Elapsed);

            Console.WriteLine("8 - Running...Press Ctrl-C to exit...");
            exitCode = ExitCode.ErrorRunning;
        }

        private void Client_KeepAlive(Session sender, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

                if (reconnectHandler == null)
                {
                    Console.WriteLine("--- RECONNECTING ---");
                    reconnectHandler = new SessionReconnectHandler();
                    reconnectHandler.BeginReconnect(sender, ReconnectPeriod * 1000, Client_ReconnectComplete);
                }
            }
        }

        private void Client_ReconnectComplete(object sender, EventArgs e)
        {
            // ignore callbacks from discarded objects.
            if (!Object.ReferenceEquals(sender, reconnectHandler))
            {
                return;
            }

            session = reconnectHandler.Session;
            reconnectHandler.Dispose();
            reconnectHandler = null;

            Console.WriteLine("--- RECONNECTED ---");
        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            if (m_sw_init) { m_sw.Start(); m_sw_init = false; }
            count++;
            foreach (var value in item.DequeueValues())
            {

                if (sw == null)
                {
                    var basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    var dataFolder = Path.Combine(basePath, "data");
                    if (!File.Exists(dataFolder))
                    {
                        Directory.CreateDirectory(dataFolder);
                    }

                    var fileName = Path.Combine(dataFolder, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
                    sw = new StreamWriter(fileName, true, Encoding.UTF8, 65536);
                }

                lock (sb)
                {
                    sb.Append(item.ResolvedNodeId);
                    sb.Append(",");
                    sb.Append(value.Value);
                    sb.Append(",");
                    sb.AppendLine(value.SourceTimestamp.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
                }

                if (item.ResolvedNodeId.ToString().Contains(last_node_id))
                {
                    var data = string.Join(",", item.ResolvedNodeId, value.Value, value.SourceTimestamp.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
                    Console.WriteLine(data);
                    cycle_count++;
                    Console.WriteLine("Elapsed time : {0}, count : {1}, cycle count : {2}", m_sw.Elapsed, count, cycle_count);
                    count = 0;
                    if (m_sw.ElapsedMilliseconds > 2500)
                    {
                        quitEvent.Set();
                    }
                    m_sw.Restart();

                    lock (sb)
                    {
                        lock (sw)
                        {
                            sw.Write(sb.ToString());
                            sb.Clear();
                        }
                    }
                    if (cycle_count % (60 * 5) == 0) //time to write the file
                    {
                        lock (sb)
                        {
                            lock (sw)
                            {
                                sw.Dispose();
                                sw = null;
                            }
                        }
                    }
                }
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }

        protected virtual  void OnMessageRecieved(MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = MessageRecieved;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        public event EventHandler<MessageEventArgs> MessageRecieved;

        protected virtual void OnNotificationRecieved(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            MonitoredItemNotificationEventHandler handler = Notification;
            if (handler != null)
            {
                handler(item, e);
            }
        }
        public event MonitoredItemNotificationEventHandler Notification;
    }
}
