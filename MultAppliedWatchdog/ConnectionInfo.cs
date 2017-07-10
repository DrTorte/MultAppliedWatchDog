using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class ConnectionInfo
    {
        private string Username;
        private string Password;

        public long Timer;
        public long TimerTarget;
        public int EmailAlertThreshold;

        public string URL;
        public string EmailFrom;
        public List<string> EmailTo = new List<string>();
        public List<string> EmailCC = new List<string>();

        public string SMTPServer;
        public int SMTPPort;
        public string SMTPUser;
        public string SMTPPass;

        public bool Status = true;

        public NetworkCredential GetCredentials {
            get {
                return new NetworkCredential(Username, Password);
            }
        }

        public bool readSettings()
        {
            try
            {
                Username = Properties.config.Default.username;
                Password = Properties.config.Default.password;
                URL = Properties.config.Default.server;
                if (!Uri.IsWellFormedUriString(URL, UriKind.Absolute))
                {
                    Console.WriteLine("URL {0} is invalid.", URL);
                    return false;
                }
                TimerTarget = Properties.config.Default.refreshTimer;
                EmailFrom = Properties.config.Default.fromEmail;
                EmailTo = Properties.config.Default.toEmails.Split(',').ToList();
                EmailCC = Properties.config.Default.ccEmails.Split(',').ToList();
                EmailAlertThreshold = Properties.config.Default.emailAlertThreshold;

                List<string> EmailsToCheck = new List<string>();
                EmailsToCheck.Add(EmailFrom);
                EmailsToCheck.AddRange(EmailTo);
                EmailsToCheck.AddRange(EmailCC);

                foreach (string e in EmailsToCheck) {
                    try
                    {
                        System.Net.Mail.MailAddress addr = new System.Net.Mail.MailAddress(e);
                        if (addr.Address != e)
                        {
                            Console.WriteLine("Email {0} is invalid.", e);
                            return false;
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Email {0} is invalid.", e);
                        return false;
                    }
                }

                SMTPServer = Properties.config.Default.smtp;
                SMTPUser = Properties.config.Default.smtpUser;
                SMTPPass = Properties.config.Default.smtpPass;
                SMTPPort = Properties.config.Default.smtpPort;
                return true;
            }
            catch
            {
                return false;
            }

        }
    }
}
