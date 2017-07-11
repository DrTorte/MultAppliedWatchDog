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
        public string smtpServer;
        public int smtpPort;
        public string smtpUser;
        public string smtpPass;

        public string emailFrom;
        public List<string> emailTo = new List<string>();
        public List<string> emailCC = new List<string>();

        //a list of all the alerts to send.
        public List<string> alertsToSend = new List<string>();
        public long timeUntilSend;

        //lock the thread.
        public bool emailSending { get; private set; }

        public bool Configure()
        {
            emailFrom = Properties.config.Default.fromEmail;
            if (Properties.config.Default.toEmails != "")
            {
                emailTo = Properties.config.Default.toEmails.Split(',').ToList();
            }
            if (Properties.config.Default.ccEmails != "")
            {
                emailCC = Properties.config.Default.ccEmails.Split(',').ToList();
            }

            List<string> EmailsToCheck = new List<string>();
            EmailsToCheck.Add(emailFrom);
            EmailsToCheck.AddRange(emailTo);
            EmailsToCheck.AddRange(emailCC);

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

            smtpServer = Properties.config.Default.smtp;
            smtpUser = Properties.config.Default.smtpUser;
            smtpPass = Properties.config.Default.smtpPass;
            smtpPort = Properties.config.Default.smtpPort;

            return true;
        }

        //add an entry, and start the timer, if it hasn't been started.
        public void AddEmailAlert(int bondId, int legId, string bondName)
        {
            if (alertsToSend.Count == 0)
            {
                timeUntilSend = 60000;
            }

            //to-do, add link.
            //need to clear up the API URL first though.
            alertsToSend.Add(String.Format("<a href='{0}'>{1} lost leg {2}</a>", Configuration.BondURI + bondId.ToString(), bondName, legId)); 
        }


        public bool SendAlerts()
        {
            if (emailSending)
            {
                return false; //thread is locked, go away.
            }

            emailSending = true;
            //Set up an email setup here. Again, use the conf info.
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(emailFrom);
            mail.IsBodyHtml = true;
            foreach (var e in emailTo)
            {
                mail.To.Add(e);
            }
            foreach (var e in emailCC)
            {
                mail.CC.Add(e);
            }
            SmtpClient client = new SmtpClient();
            client.Host = smtpServer;
            client.Port = smtpPort;

            if (smtpUser != "")
            {
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);
            }
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;

            mail.Subject = String.Format("{0} services lost connection", alertsToSend.Count());
            foreach (string e in alertsToSend)
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
                alertsToSend = new List<string>();
                emailSending = false;
                return true;
            }
            catch
            {
                //catch errors for emails here.
                alertsToSend = new List<string>();
                emailSending = false;
                return false;
            }
        }
    }
}
