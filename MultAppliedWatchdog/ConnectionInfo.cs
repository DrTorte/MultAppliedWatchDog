using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class Configuration
    {
        public static string URI;
        public static string ApiURI { get { return URI + "/api/v3/"; } }
        public static string BondURI { get { return URI + "/bonds/"; } }

        private string Username;
        private string Password;

        public long Timer;
        public long TimerTarget;
        public int EmailAlertThreshold;
        public bool Status = true;

        public int UIRefreshRate = 250;

        public NetworkCredential GetCredentials {
            get {
                return new NetworkCredential(Username, Password);
            }
        }

        public bool ReadSettings()
        {
            try
            {
                Username = Properties.config.Default.username;
                Password = Properties.config.Default.password;
                URI = Properties.config.Default.server;
                if (!Uri.IsWellFormedUriString(URI, UriKind.Absolute))
                {
                    Console.WriteLine("URL {0} is invalid.", URI);
                    return false;
                }
                TimerTarget = Properties.config.Default.refreshTimer;
                EmailAlertThreshold = Properties.config.Default.emailAlertThreshold;

                return true;
            }
            catch
            {
                return false;
            }

        }
    }
}
