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

        private string username;
        private string password;

        public long timer;
        public long timerTarget;
        public int emailAlertThreshold;
        public bool status = true;

        public NetworkCredential GetCredentials {
            get {
                return new NetworkCredential(username, password);
            }
        }

        public bool ReadSettings()
        {
            try
            {
                username = Properties.config.Default.username;
                password = Properties.config.Default.password;
                URI = Properties.config.Default.server;
                if (!Uri.IsWellFormedUriString(URI, UriKind.Absolute))
                {
                    Console.WriteLine("URL {0} is invalid.", URI);
                    return false;
                }
                timerTarget = Properties.config.Default.refreshTimer;
                emailAlertThreshold = Properties.config.Default.emailAlertThreshold;

                return true;
            }
            catch
            {
                return false;
            }

        }
    }
}
