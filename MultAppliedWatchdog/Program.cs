using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;

namespace MultAppliedWatchdog
{
    class Program
    {
        static private ConnectionInfo conn = new ConnectionInfo();
        static List<Bond> currBonds = new List<Bond>();
        static List<Bond> newBonds = new List<Bond>();
        static List<WatchedLeg> watchedLegs = new List<WatchedLeg>();

        static List<UITextEntry> uiText = new List<UITextEntry>();
        static List<string> updates = new List<string>();
        //a few entries to the UI Text entry that we'll use.
        static UITextEntry currentAction = new UITextEntry(Console.WindowTop, 0, "");
        static UITextEntry bonders = new UITextEntry(Console.WindowTop + 1, 0, "");
        static UITextEntry ticker = new UITextEntry(Console.WindowTop + 2, 0, "");
        static UITextEntry status = new UITextEntry(Console.WindowTop + Console.WindowHeight-2, 0, "Offline");
        static UITextEntry userInterface = new UITextEntry(Console.WindowTop + Console.WindowHeight-1, 0, "'x' to exit. 'c' to force refresh.");


        static bool stop = false;
        static bool connected = false;

        static void Main(string[] args)
        {
            //add the proper listing items.
            Console.CursorVisible = false;
            uiText.Add(status);
            uiText.Add(userInterface);
            uiText.Add(bonders);
            uiText.Add(currentAction);
            uiText.Add(ticker);

            //reading config.
            if (!conn.readSettings())
            {
                Console.WriteLine("Unable to read config. Try again!");
                Console.ReadKey(false);
                return;
            }


            currentAction.setNewText("Getting initial bonder data...");

            List<Task> tasks;

            Task.Factory.StartNew(() => WriteText());

            uiText.Add(currentAction);
            while (!connected)
            {
                tasks = new List<Task>();
                tasks.Add(Task.Factory.StartNew(() => { newBonds = GetBonds(); }));

                Task.WaitAll(tasks.ToArray());
                if (conn.Status)
                {
                    status.setNewText("Online");

                    connected = true;
                }
                else
                {
                    userInterface.setNewText("Unable to connect. Press Y to try again, any other key to exit.");
                    char val = Console.ReadKey(false).KeyChar;
                    if (val != 'y') {
                        //Console.WriteLine("\nExiting...");
                        //Console.ReadKey();
                        return;
                    }
                    userInterface.setNewText("'x' to exit. 'c' to force refresh.");
                }
            }

            //begin main loop.
            Console.CursorVisible = false;

            tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                while (connected)
                {
                    if (conn.Timer > 0)
                    {
                        CheckCountdownTimer();
                    }
                    else
                    {
                        BondLoop();
                    }
                }
            }));

            tasks.Add(Task.Factory.StartNew(() => {
                char key = new char();
                while (connected) {
                    key = Console.ReadKey(false).KeyChar;
                    if (key == 'x')
                    {
                        conn.Status = false;
                        stop = true;
                        connected = false;
                    } else if (key == 'c') //c drops the timer to 0.
                    {
                        conn.Timer = 0;
                    }
                }
            }));

            Task.WaitAll(tasks.ToArray());

            Console.Clear();
            Console.WriteLine("Exiting...");
            Console.ReadKey(false);

        }

        static void BondLoop()
        {
            //fetch the data.
            currBonds = newBonds;
            newBonds = GetBonds();
            foreach (Bond newBond in newBonds)
            {
                //find the old bond, if it's present.
                Bond oldBond = currBonds.FirstOrDefault(x => x.id == newBond.id);
                if (oldBond == null)
                {
                    string update = String.Format("{0}: new bond!", newBond.name);
                    updates.Add(update);
                    continue;
                }

                //compare the legs.
                foreach (Legs newLeg in newBond.leg_list)
                {
                    //find the entry in the current list.
                    Legs oldLeg = oldBond.leg_list.FirstOrDefault(x => x.id == newLeg.id);
                    //if empty, continue, means it was brand new.
                    if (oldLeg == null)
                    {
                        string update = String.Format("{0}: new leg {1}", newBond.name, newLeg.id);
                        updates.Add(update);
                        continue;
                    }
                    if (oldLeg.state != newLeg.state)
                    {
                        string update = String.Format("{0}: leg {1} changed from {2} to {3}", newBond.name, newLeg.id, oldLeg.state, newLeg.state);
                        updates.Add(update);
                    }
                    if (oldLeg.state != "down" && newLeg.state == "down")
                    {
                        //if this was triggered after the initial fetch (don't want to be sending redundant emails), add to the watchlist.
                        string update = String.Format("{0}, leg {1} dropped!", newBond.name, newLeg.id);
                        updates.Add(update);
                        watchedLegs.Add(new WatchedLeg(oldLeg.id));
                    }

                    //now scan for this leg in the list.
                    WatchedLeg watchLeg = watchedLegs.FirstOrDefault(x => x.id == oldLeg.id);
                    if (watchLeg == null)
                    {
                        continue;
                    }
                    if (newLeg.state != "down")
                    {
                        watchLeg.downCount = Math.Max(watchLeg.downCount - 1, 0);
                        if (watchLeg.downCount == 0)
                        {
                            string Update = String.Format("{0} detected up for 5 consequtive polls. Clearing watch.", newLeg.id);
                            watchLeg.alerted = false;
                        }
                    }
                    else
                    {
                        watchLeg.downCount = Math.Min(watchLeg.downCount +1, conn.EmailAlertThreshold);
                        if (watchLeg.downCount == conn.EmailAlertThreshold && !watchLeg.alerted)
                        {
                            SendAlert(newLeg.id, newBond.id, newBond.name);
                            watchLeg.alerted = true;
                        }
                    }
                }
            }
        }

        static List<Bond> GetBonds()
        {
            List<Bond> bonds = new List<Bond>();
            //get the URL from our connection class.
            string url = conn.URL + "bonds/";
            WebRequest req = WebRequest.Create(url);

            //get the credentials.
            req.Credentials = conn.GetCredentials;

            //attempt to connect...
            try
            {
                currentAction.setNewText("Getting bonder data...");
                Task getData;
                getData = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (WebResponse resp = req.GetResponse())
                        {
                            StreamReader readStream = new StreamReader(resp.GetResponseStream());
                            //return the results.

                            bonds = JsonConvert.DeserializeObject<List<Bond>>(readStream.ReadLine());
                            //count the legs.
                            int legs = 0;
                            int onlineLegs = 0;
                            foreach (var b in bonds)
                            {
                                legs += b.leg_list.Count();
                                onlineLegs += b.leg_list.Where(x => x.state != "down").Count();
                            }
                            bonders.setNewText(String.Format("{0} bonders detected, {1}/{2} legs online.", bonds.Count(), onlineLegs, legs));
                            currentAction.setNewText("Idle, last poll at " + System.DateTime.Now);
                        }
                    }
                    catch (WebException e)
                    {
                        //if it fails, we want to mark it as failed, and then return an empty bond list. Also, potentially, we want to log specific errors.
                        currentAction.setNewText("");
                        status.setNewText("Offline");
                        using (WebResponse response = e.Response)
                        {
                            conn.Status = false; //mark it as failed.
                        }
                        currentAction.setNewText("Failed to connect");
                        status.setNewText("Unable to connect.");
                    }
                });

                //add a ticker so we're sure that it's updatnig.
                ticker.setNewText("Loading.");
                while (!getData.IsCompleted)
                {
                    Thread.Sleep(100);
                    ticker.text += ".";
                }
                conn.Timer = conn.TimerTarget;
            }
            catch (WebException e)
            {

            }
            return bonds;
        }

        static bool SendAlert(int legId, int bondId, string bondName)
        {
            //Set up an email setup here. Again, use the conf info.
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(conn.EmailFrom);
            foreach (var e in conn.EmailTo)
            {
                mail.To.Add(e);
            }
            foreach (var e in conn.EmailCC)
            {
                mail.CC.Add(e);
            }
            SmtpClient client = new SmtpClient();
            client.Host = conn.SMTPServer;
            client.Port = conn.SMTPPort;

            if (conn.SMTPUser != "")
            {
                client.Credentials = new NetworkCredential(conn.SMTPUser, conn.SMTPPass);
            }
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;

            mail.Subject = String.Format("{0} leg disconnected", bondName);
            mail.Body = String.Format("{0} lost leg {1}", bondName, legId);
            try
            {
                client.Send(mail);
                updates.Add("Alert email sent!");
                return true;
            } catch
            {
                //catch errors for emails here.
                updates.Add("OH NO! Error on email. :(");
                return false;
            }
        }

        //check the timer.
        static void CheckCountdownTimer()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Thread.Sleep(100);
            sw.Stop();
            conn.Timer -= sw.ElapsedMilliseconds;
            ticker.setNewText(String.Format("{0} milliseconds remaining until next check.", conn.Timer));
        }

        //write the text ot the screen.
        static void WriteText()
        {
            while (!stop)
            {
                Thread.Sleep(100);
                for (int i = 0; i < uiText.Count(); i++)
                {
                    if (uiText[i].clear)
                    {
                        //if it's set to clear, print blankspaces.
                        for (int x = uiText[i].column; x < uiText[i].column + uiText[i].text.Length; x++)
                        {
                            Console.SetCursorPosition(x, uiText[i].row);
                            Console.Write(" ");
                        }
                        uiText[i].text = uiText[i].newText;
                        uiText[i].newText = "";
                        uiText[i].clear = false;
                    }
                    Console.SetCursorPosition(uiText[i].column, uiText[i].row);
                    Console.Write(uiText[i].text);
                }

                var maxUpdates = (Console.WindowTop + 4) + (Console.WindowHeight -11);
                if (updates.Count() > maxUpdates)
                {
                    updates.RemoveRange(0, updates.Count() - maxUpdates);
                }
                for (int i = 0; i < updates.Count() && i < maxUpdates; i++)
                {
                    Console.SetCursorPosition(0, Console.WindowTop + 4 + i);
                    Console.Write(updates[i]);
                }
            }
        }

    }
}
