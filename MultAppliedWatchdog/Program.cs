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
        static private Configuration Configuration = new Configuration();
        static List<Bond> CurrentBonds = new List<Bond>();
        static List<Bond> NewBonds = new List<Bond>();
        static List<WatchedLeg> WatchedLegs = new List<WatchedLeg>();

        static List<UITextEntry> UIText = new List<UITextEntry>();
        static List<string> Updates = new List<string>();
        //a few entries to the UI Text entry that we'll use.
        static UITextEntry CurrentAction = new UITextEntry(Console.WindowTop, 0, "");
        static UITextEntry Bonders = new UITextEntry(Console.WindowTop + 1, 0, "");
        static UITextEntry Ticker = new UITextEntry(Console.WindowTop + 2, 0, "");
        static UITextEntry Status = new UITextEntry(Console.WindowTop + Console.WindowHeight-2, 0, "Offline");
        static UITextEntry UserInterface = new UITextEntry(Console.WindowTop + Console.WindowHeight-1, 0, "'x' to exit. 'c' to force refresh.");

        static Email Email;

        static public bool SetTimeToZero; //use this to cause ticks to go right up, temporarily.

        static bool Stop = false;
        static bool Connected = false;

        static void Main(string[] args)
        {
            //add the proper listing items.
            Console.CursorVisible = false;
            UIText.Add(Status);
            UIText.Add(UserInterface);
            UIText.Add(Bonders);
            UIText.Add(CurrentAction);
            UIText.Add(Ticker);

            //reading config.
            if (!Configuration.ReadSettings())
            {
                Console.WriteLine("Unable to read config. Try again!");
                Console.ReadKey(false);
                return;
            }

            Email = new Email();
            Email.Configure();


            CurrentAction.setNewText("Getting initial bonder data...");

            List<Task> tasks;

            Task.Factory.StartNew(() => WriteText());

            UIText.Add(CurrentAction);
            while (!Connected)
            {
                tasks = new List<Task>();
                tasks.Add(Task.Factory.StartNew(() => { NewBonds = GetBonds(); }));

                Task.WaitAll(tasks.ToArray());
                if (Configuration.status)
                {
                    Status.setNewText("Online");

                    Connected = true;
                }
                else
                {
                    UserInterface.setNewText("Unable to connect. Press Y to try again, any other key to exit.");
                    char val = Console.ReadKey(false).KeyChar;
                    if (val != 'y') {
                        //Console.WriteLine("\nExiting...");
                        //Console.ReadKey();
                        return;
                    }
                    UserInterface.setNewText("'x' to exit. 'c' to force refresh.");
                }
            }

            //begin main loop.
            Console.CursorVisible = false;

            tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                while (Connected)
                {
                    if (Configuration.timer > 0)
                    {
                        Configuration.timer = CheckCountdownTimer(Configuration.timer);
                        Ticker.setNewText(String.Format("{0} milliseconds remaining until next check.", Configuration.timer));
                    }
                    else
                    {
                        BondLoop();
                    }
                }
            }));

            tasks.Add(Task.Factory.StartNew(() =>
            {
                while (Connected)
                {
                    if (!Email.emailSending && Email.alertsToSend.Count() > 0)
                    {
                        if (Email.timeUntilSend > 0)
                        {
                            Email.timeUntilSend = CheckCountdownTimer(Email.timeUntilSend);
                            CurrentAction.setNewText(String.Format("{0} milliseconds remaining until email sent, {1} alerts to send.", Email.timeUntilSend, Email.alertsToSend.Count()));
                        }
                        else
                        {
                            Status.setNewText("Sending email...");
                            if (Email.SendAlerts())
                            {
                                Updates.Add("Email sent!");
                                CurrentAction.setNewText(String.Format("Idle, email sent at {0}", DateTime.Now));
                            } else
                            {
                                Updates.Add("Email failed to send.");
                                CurrentAction.setNewText(String.Format("Idle, email failed sending at {0}", DateTime.Now));
                            }
                            Status.setNewText("Online");
                        }
                    }
                }
            }));

            tasks.Add(Task.Factory.StartNew(() => {
                char key = new char();
                while (Connected) {
                    key = Console.ReadKey(false).KeyChar;
                    if (key == 'x')
                    {
                        Configuration.status = false;
                        Stop = true;
                        Connected = false;
                    } else if (key == 'c') //c drops the timer to 0.
                    {
                        SetTimeToZero = true;
                        Thread.Sleep(150);
                        SetTimeToZero = false;

                    } else if (key == 'a')
                    {
                        //forcibly add an alert.
                        Email.AddEmailAlert(0, 0, "Bob!");
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
            CurrentBonds = NewBonds;
            NewBonds = GetBonds();
            foreach (Bond newBond in NewBonds)
            {
                //find the old bond, if it's present.
                Bond oldBond = CurrentBonds.FirstOrDefault(x => x.id == newBond.id);
                if (oldBond == null)
                {
                    string update = String.Format("{0}: new bond!", newBond.name);
                    Updates.Add(update);
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
                        Updates.Add(update);
                        continue;
                    }
                    if (oldLeg.state != newLeg.state)
                    {
                        string update = String.Format("{0}: leg {1} changed from {2} to {3}", newBond.name, newLeg.id, oldLeg.state, newLeg.state);
                        Updates.Add(update);
                    }

                    WatchedLeg watchLeg = WatchedLegs.FirstOrDefault(x => x.id == oldLeg.id);
                    if (oldLeg.state != "down" && newLeg.state == "down")
                    {
                        //if this was triggered after the initial fetch (don't want to be sending redundant emails), add to the watchlist.
                        string update = String.Format("{0}, leg {1} dropped!", newBond.name, newLeg.id);
                        Updates.Add(update);
                        if (watchLeg == null)
                        {
                            watchLeg = new WatchedLeg(newLeg.id);
                            WatchedLegs.Add(watchLeg);
                        }
                    }

                    //now scan for this leg in the list, again.
                    if (watchLeg == null)
                    {
                        continue;
                    }
                    if (newLeg.state != "down")
                    {
                        watchLeg.downCount = Math.Max(watchLeg.downCount - 1, 0);
                        if (watchLeg.downCount == 0)
                        {
                            string Update = String.Format("{0} detected up for {1} ticks. Clearing watch.", newLeg.id, Configuration.emailAlertThreshold);
                            watchLeg.alerted = false;
                        }
                    }
                    else
                    {
                        watchLeg.downCount = Math.Min(watchLeg.downCount +1, Configuration.emailAlertThreshold);
                        Updates.Add(String.Format("{0}, leg {1} has been down for {2} ticks", newBond.name, newLeg.id, watchLeg.downCount));
                        if (watchLeg.downCount == Configuration.emailAlertThreshold && !watchLeg.alerted)
                        {
                            Email.AddEmailAlert(newBond.id, newLeg.id, newBond.name);
                            Updates.Add(String.Format("Alert added for {0}, leg {1}", newBond.name, newLeg.id));
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
            string url = Configuration.ApiURI + "bonds/";
            WebRequest req = WebRequest.Create(url);

            //get the credentials.
            req.Credentials = Configuration.GetCredentials;

            //attempt to connect...
            try
            {
                CurrentAction.setNewText("Getting bonder data...");
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
                            Bonders.setNewText(String.Format("{0} bonders detected, {1}/{2} legs online.", bonds.Count(), onlineLegs, legs));
                            CurrentAction.setNewText("Idle, last poll at " + System.DateTime.Now);
                        }
                    }
                    catch (WebException e)
                    {
                        //if it fails, we want to mark it as failed, and then return an empty bond list. Also, potentially, we want to log specific errors.
                        CurrentAction.setNewText("");
                        Status.setNewText("Offline");
                        using (WebResponse response = e.Response)
                        {
                            Configuration.status = false; //mark it as failed.
                        }
                        CurrentAction.setNewText("Failed to connect");
                        Status.setNewText("Unable to connect.");
                    }
                });

                //add a ticker so we're sure that it's updatnig.
                Ticker.setNewText("Loading.");
                while (!getData.IsCompleted)
                {
                    Thread.Sleep(100);
                    Ticker.text += ".";
                }
                Configuration.timer = Configuration.timerTarget;
            }
            catch (WebException e)
            {

            }
            return bonds;
        }

        //check the timer.
        static long CheckCountdownTimer(long timer)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Thread.Sleep(33);
            sw.Stop();
            if (SetTimeToZero)
            {
                return 0;
            }
            timer -= sw.ElapsedMilliseconds;
            return timer;
        }

        //write the text ot the screen.
        static void WriteText()
        {
            while (!Stop)
            {
                Thread.Sleep(33);
                for (int i = 0; i < UIText.Count(); i++)
                {
                    if (UIText[i].clear)
                    {
                        //if it's set to clear, print blankspaces.
                        for (int x = UIText[i].column; x < UIText[i].column + UIText[i].text.Length; x++)
                        {
                            Console.SetCursorPosition(x, UIText[i].row);
                            Console.Write(" ");
                        }
                        UIText[i].text = UIText[i].newText;
                        UIText[i].newText = "";
                        UIText[i].clear = false;
                    }
                    Console.SetCursorPosition(UIText[i].column, UIText[i].row);
                    Console.Write(UIText[i].text);
                }

                var maxUpdates = (Console.WindowTop + 4) + (Console.WindowHeight -11);
                if (Updates.Count() > maxUpdates)
                {
                    Updates.RemoveRange(0, Updates.Count() - maxUpdates);
                }
                for (int i = 0; i < Updates.Count() && i < maxUpdates; i++)
                {
                    Console.SetCursorPosition(0, Console.WindowTop + 4 + i);
                    Console.Write(Updates[i]);
                }
            }
        }

    }
}
