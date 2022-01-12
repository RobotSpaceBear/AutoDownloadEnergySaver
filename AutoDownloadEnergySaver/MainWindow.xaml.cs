using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Configuration;

namespace AutoDownloadEnergySaver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int NETWORK_POOL_RATE_MILLISECONDS; // amount of time between network pool loops
        private int TIME_SECONDS_USED_FOR_AVERAGE; // amount of seconds over which the moving average download speed is calculated
        private long MIN_KILOBYTES_THRESHOLD; // traffic under which computer shutdown is considered
        private int TIME_SECONDS_BEFORE_SHUTDOWN; // time spent under traffic threshold before shutdown is triggered
        private int SHUTDOWN_MODE; // shutdown mode : 0=suspend, 1=hibernate, 2=power off
        private bool SIMULATION_MODE; // "true" allows for the app to really hibernate the PC

        private readonly DispatcherTimer dispatcherTimer = new DispatcherTimer();
        private Dictionary<string, List<DownloadSample>> _netInterfacesReadingsDict;
        private Dictionary<string, long> _averages;
        private DateTime _timeForShutdown;
        private bool _canShutdown = true;

        public MainWindow()
        {
            InitializeComponent();

            NETWORK_POOL_RATE_MILLISECONDS = Convert.ToInt32(ConfigurationManager.AppSettings.Get("NETWORK_POOL_RATE_MILLISECONDS"));
            TIME_SECONDS_USED_FOR_AVERAGE = Convert.ToInt32(ConfigurationManager.AppSettings.Get("TIME_SECONDS_USED_FOR_AVERAGE"));
            MIN_KILOBYTES_THRESHOLD = Convert.ToInt64(ConfigurationManager.AppSettings.Get("MIN_KILOBYTES_THRESHOLD"));
            TIME_SECONDS_BEFORE_SHUTDOWN = Convert.ToInt32(ConfigurationManager.AppSettings.Get("TIME_SECONDS_BEFORE_SHUTDOWN"));
            SHUTDOWN_MODE = Convert.ToInt32(ConfigurationManager.AppSettings.Get("SHUTDOWN_MODE"));
            SIMULATION_MODE = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("SIMULATION_MODE"));

#if DEBUG
            //dumps de dictionaries' contents in the output debug console
            SIMULATION_MODE=true;
#endif

            _timeForShutdown = DateTime.Now.AddSeconds(TIME_SECONDS_BEFORE_SHUTDOWN);
            _netInterfacesReadingsDict = new Dictionary<string, List<DownloadSample>>();

            switch (SHUTDOWN_MODE)
            {
                case 0: // suspend
                    lblActionTime.Content = "Suspend time :";
                    lblActionIn.Content = "Suspend in :";
                    break;
                case 1: // hibernate
                    lblActionTime.Content = "Hibernate time :";
                    lblActionIn.Content = "Hibernate in :";
                    break;
                case 2: // power off
                    lblActionTime.Content = "Power off time :";
                    lblActionIn.Content = "Power off in :";
                    break;
                default:
                    break;
            }


            dispatcherTimer.Tick += DoWorkLoop;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(NETWORK_POOL_RATE_MILLISECONDS);
            dispatcherTimer.Start();

        }

        private void DoWorkLoop(object sender, EventArgs e)
        {
            //reads the data from network interface and adds them to a dictionary
            ReadNetworkBytesTraffic();

            //purges the least to keep only what will be used for average calculations
            CleanReadingsList(_netInterfacesReadingsDict, TIME_SECONDS_USED_FOR_AVERAGE);

#if DEBUG
            //dumps de dictionaries' contents in the output debug console
            LogReadings();
#endif
            _averages = CalculateAverage();

            //updating the UI
            foreach (var average in _averages)
            {
                txtAverages.Text = $"Interface '{average.Key}' : average '{ConvertBytesToKiloBytes(average.Value)}' kB/s";
            }

            //if highest average is over the threshold, we bump the scheduled shutdown time by the set amount of time
            if (_averages.Count > 0 &&
                ConvertBytesToKiloBytes(_averages.Max(x => x.Value)) > MIN_KILOBYTES_THRESHOLD)
            {
                _timeForShutdown = DateTime.Now.AddSeconds(TIME_SECONDS_BEFORE_SHUTDOWN);
            }

            // if we're past the scheduled shutdown time, we trigger the shutdown/hibernate
            if (_timeForShutdown < DateTime.Now)
            {
                if (SIMULATION_MODE)
                {
                    Debug.WriteLine("SIMULATION MODE TRUE => ordered pc shutdown");
                }
                else if (_canShutdown)
                {
                    _canShutdown = false;
                    dispatcherTimer.Stop();

                    switch (SHUTDOWN_MODE)
                    {
                        case 0: // suspend
                            Debug.WriteLine("=> ordered pc suspend");
                            SuspendPc();
                            break;
                        case 1: // hibernate
                            Debug.WriteLine("=> ordered pc hibernate");
                            HibernatePc();
                            break;
                        case 2: // power off
                            Debug.WriteLine("=> ordered pc shut down");
                            PowerOffPc();
                            break;
                        default:
                            break;
                    }
                    
                }
            }



            txtHibernateTime.Text = _timeForShutdown.ToString("HH:MM:ss");
            txtHibernateRemaining.Text = $"{(int)((_timeForShutdown - DateTime.Now).TotalSeconds)} sec";
            if (_averages.Any())
            {
                txtAverages.Text = ConvertBytesToKiloBytes(_averages.Max(x => x.Value)) + "kB/s";
            }
        }

        private Dictionary<string, long> CalculateAverage()
        {
            Dictionary<string, long> result = new Dictionary<string, long>();

            foreach (string netInterfaceName in _netInterfacesReadingsDict.Keys)
            {
                if (_netInterfacesReadingsDict[netInterfaceName].Count < 2) break;

                var totalBytes = _netInterfacesReadingsDict[netInterfaceName][_netInterfacesReadingsDict[netInterfaceName].Count - 1].BytesValue - _netInterfacesReadingsDict[netInterfaceName][0].BytesValue;

                var average = totalBytes / _netInterfacesReadingsDict.Count;

                Debug.WriteLine(" ==> {0} \taverage {1} bytes", netInterfaceName, average);

                result.Add(netInterfaceName, average);
            }

            return result;
        }


        private void ReadNetworkBytesTraffic()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return;


            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces().Where(x => !x.Name.Contains("Loopback")))
            {
                long bytesReceived = netInterface.GetIPStatistics().BytesReceived;

                if (!_netInterfacesReadingsDict.ContainsKey(netInterface.Name))
                {
                    _netInterfacesReadingsDict.Add(netInterface.Name, new List<DownloadSample>());
                }

                _netInterfacesReadingsDict[netInterface.Name].Add(new DownloadSample() { Timestamp = DateTime.Now, BytesValue = bytesReceived });

            }
        }

        private void LogReadings()
        {
            Debug.WriteLine("===================");
            foreach (string netInterfaceName in _netInterfacesReadingsDict.Keys)
            {
                foreach (var reading in _netInterfacesReadingsDict[netInterfaceName])
                {
                    Debug.WriteLine(" ==> {0} \t{1} \t{2}", netInterfaceName, reading.Timestamp, reading.BytesValue);
                }
            }
        }

        private void CleanReadingsList(Dictionary<string, List<DownloadSample>> netInterfacesReadingsDict, int historySecondsToKeep)
        {
            foreach (string netInterfaceName in netInterfacesReadingsDict.Keys)
            {
                netInterfacesReadingsDict[netInterfaceName].RemoveAll(x => x.Timestamp < DateTime.Now.AddSeconds(-1 * historySecondsToKeep));
            }
        }

        private long GetMovingAverageDownload(List<DownloadSample> downloadSamples, int movingAverageDurationSeconds)
        {
            List<DownloadSample> samples = downloadSamples.Where(x => x.Timestamp > DateTime.Now.AddSeconds(-movingAverageDurationSeconds)).ToList();

            if (!samples.Any())
            {
                return 0;
            }

            var samplesSum = samples.Sum(x => x.BytesValue);
            var average = samplesSum / samples.Count();

            return average;
        }

        public class DownloadSample
        {
            public DateTime Timestamp { get; set; }
            public long BytesValue { get; set; }
        }

        private long ConvertBytesToKiloBytes(long bytes)
        {
            return bytes / (1024);
        }

        private long ConvertBytesToMegaBytes(long bytes)
        {
            return bytes / (1024 * 1024);
        }

        private void HibernatePc()
        {
            System.Windows.Forms.Application.SetSuspendState(PowerState.Hibernate, false, true);
        }

        private void SuspendPc()
        {
            System.Windows.Forms.Application.SetSuspendState(PowerState.Suspend, false, true);
        }

        private void PowerOffPc()
        {
            var psi = new ProcessStartInfo("shutdown", "/s /f");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        private void Reset_Button_Click(object sender, RoutedEventArgs e)
        {
            _canShutdown = true;
            _timeForShutdown = DateTime.Now.AddSeconds(TIME_SECONDS_BEFORE_SHUTDOWN);
            _netInterfacesReadingsDict = new Dictionary<string, List<DownloadSample>>();
            
            dispatcherTimer.Stop(); 
            dispatcherTimer.Start(); 
        }
        private void Exit_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
