using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class Email
    {
        public string SmtpServer;
        public int SmtpPort;
        public string SmtpUser;
        public string SmtpPass;

        public string EmailFrom;
        public List<string> EmailTo = new List<string>();
        public List<string> EmailCc = new List<string>();

        //a list of all the alerts to send.
        public List<string> DownAlertsToSend = new List<string>();
        public List<string> FlapAlertsToSend = new List<string>();
        public long TimeUntilSend;

        //lock the thread.
        public bool EmailSending { get; private set; }

        public bool Configure()
        {
            EmailFrom = Properties.config.Default.fromEmail;
            if (Properties.config.Default.toEmails != "")
            {
                EmailTo = Properties.config.Default.toEmails.Split(',').ToList();
            }
            if (Properties.config.Default.ccEmails != "")
            {
                EmailCc = Properties.config.Default.ccEmails.Split(',').ToList();
            }

            List<string> EmailsToCheck = new List<string>();
            EmailsToCheck.Add(EmailFrom);
            EmailsToCheck.AddRange(EmailTo);
            EmailsToCheck.AddRange(EmailCc);

            foreach (string e in EmailsToCheck)
            {
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

            SmtpServer = Properties.config.Default.smtp;
            SmtpUser = Properties.config.Default.smtpUser;
            SmtpPass = Properties.config.Default.smtpPass;
            SmtpPort = Properties.config.Default.smtpPort;

            return true;
        }

        //add an entry, and start the timer, if it hasn't been started.
        public void AddDownAlert(int bondId, int legId, string bondName)
        {
            PrepSend();


            DownAlertsToSend.Add(String.Format("<a href='{0}'>{1}: leg {2} is down.</a>", Configuration.BondURI + bondId.ToString(), bondName, legId)); 
        }

        public void AddFlapAlert(int bondId, int legId, string bondName)
        {
            PrepSend();

            FlapAlertsToSend.Add(String.Format("<a href='{0}'>{1}: leg {2} is flapping.</a>", Configuration.BondURI + bondId.ToString(), bondName, legId));
        }

        private void PrepSend()
        {
            if (DownAlertsToSend.Count() + FlapAlertsToSend.Count() == 0)
            {
                TimeUntilSend = 60000;
            }
        }


        public bool SendAlerts()
        {
            if (EmailSending)
            {
                return false; //thread is locked, go away.
            }

            EmailSending = true;
            //Set up an email setup here. Again, use the conf info.
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(EmailFrom);
            mail.IsBodyHtml = true;
            foreach (var e in EmailTo)
            {
                mail.To.Add(e);
            }
            foreach (var e in EmailCc)
            {
                mail.CC.Add(e);
            }
            SmtpClient client = new SmtpClient();
            client.Host = SmtpServer;
            client.Port = SmtpPort;

            if (SmtpUser != "")
            {
                client.Credentials = new NetworkCredential(SmtpUser, SmtpPass);
            }
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;

            mail.Subject = String.Format("ML Watchdog: {0} legs down, {1} legs flapping", DownAlertsToSend.Count(), FlapAlertsToSend.Count());
            foreach (string e in DownAlertsToSend)
            {
                if (mail.Body != "")
                {
                    mail.Body += "<br />";
                }
                mail.Body += e;
            }
            foreach (string e in FlapAlertsToSend)
            {
                if (mail.Body != "")
                {
                    mail.Body += "<br />";
                }
                mail.Body += e;
            }

            try
            {
                client.Send(mail);
                DownAlertsToSend = new List<string>();
                EmailSending = false;
                return true;
            }
            catch
            {
                //catch errors for emails here.
                DownAlertsToSend = new List<string>();
                EmailSending = false;
                return false;
            }
        }
    }
}
