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

        static List<UiTextEntry> UIText = new List<UiTextEntry>();
        static List<UiUpdateEntry> Updates = new List<UiUpdateEntry>();
        //a few entries to the UI Text entry that we'll use.
        static UiTextEntry CurrentAction = new UiTextEntry(Console.WindowTop, 0, "");
        static UiTextEntry Bonders = new UiTextEntry(Console.WindowTop + 1, 0, "");
        static UiTextEntry Ticker = new UiTextEntry(Console.WindowTop + 2, 0, "");
        static UiTextEntry Status = new UiTextEntry(Console.WindowTop + Console.WindowHeight-2, 0, "Offline");
        static UiTextEntry UserInterface = new UiTextEntry(Console.WindowTop + Console.WindowHeight-1, 0, "'x' to exit. 'c' to force refresh.");

        static Email Email;

        static public bool SetTimeToZero; //use this to cause ticks to go right up, temporarily.

        static bool Stop = false;
        static bool Connected = false;
        static bool ShowUI = true;

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


            CurrentAction.Text = "Getting initial bonder data...";

            List<Task> tasks;

            Task.Factory.StartNew(() => WriteText());

            UIText.Add(CurrentAction);
            while (!Connected)
            {
                tasks = new List<Task>();
                tasks.Add(Task.Factory.StartNew(() => { NewBonds = GetBonds(); }));

                Task.WaitAll(tasks.ToArray());
                if (Configuration.Status)
                {
                    Status.Text = "Online";

                    Connected = true;
                }
                else
                {
                    UserInterface.Text = "Unable to connect. Press Y to try again, any other key to exit.";
                    char val = Console.ReadKey(false).KeyChar;
                    if (val != 'y') {
                        //Console.WriteLine("\nExiting...");
                        //Console.ReadKey();
                        return;
                    }
                    UserInterface.Text = "'x' to exit. 'c' to force refresh.";
                }
            }

            //begin main loop.
            Console.CursorVisible = false;

            tasks = new List<Task>();

            tasks.Add(Task.Factory.StartNew(() =>
            {
                while (Connected)
                {
                    if (Configuration.Timer > 0)
                    {
                        Configuration.Timer = CheckCountdownTimer(Configuration.Timer);
                        Ticker.Text = String.Format("{0} milliseconds remaining until next check.", Configuration.Timer);
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
                    if (!Email.EmailSending && Email.DownAlertsToSend.Count() > 0)
                    {
                        if (Email.TimeUntilSend > 0)
                        {
                            Email.TimeUntilSend = CheckCountdownTimer(Email.TimeUntilSend);
                            CurrentAction.Text = String.Format("{0} milliseconds remaining until email sent, {1} alerts to send.", Email.TimeUntilSend, Email.DownAlertsToSend.Count());
                        }
                        else
                        {
                            Status.Text = "Sending email...";
                            if (Email.SendAlerts())
                            {
                                Updates.Add(new UiUpdateEntry("Email sent!"));
                                CurrentAction.Text = String.Format("Idle, email sent at {0}", DateTime.Now);
                            } else
                            {
                                Updates.Add(new UiUpdateEntry("Email failed to send."));
                                CurrentAction.Text = String.Format("Idle, email failed sending at {0}", DateTime.Now);
                            }
                            Status.Text = "Online";
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
                        Configuration.Status = false;
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
                        Email.AddDownAlert(0, 0, "Bob!");
                    } else if (key == 'b')
                    {
                        //print an update.
                        Random rng = new Random();
                        int length = rng.Next(5, 60);
                        const string allowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$?_-";

                        //pick random chars and random length.
                        char[] chars = new char[length];
                        for (int i = 0; i < length; i++)
                        {
                            chars[i] = allowedChars[rng.Next(0, allowedChars.Length)];
                        }
                        Updates.Add(new UiUpdateEntry(chars.ToString()));
                    } else if (key == 'u')
                    {
                        //toggle ui.
                        ShowUI = !ShowUI;
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
                Bond oldBond = CurrentBonds.FirstOrDefault(x => x.ID == newBond.ID);
                if (oldBond == null)
                {
                    string update = String.Format("{0}: new bond!", newBond.Name);
                    Updates.Add(new UiUpdateEntry(update));
                    continue;
                }

                //compare the legs.
                foreach (Legs newLeg in newBond.Legs)
                {
                    //find the entry in the current list.
                    Legs oldLeg = oldBond.Legs.FirstOrDefault(x => x.ID == newLeg.ID);
                    //if empty, continue, means it was brand new.
                    if (oldLeg == null)
                    {
                        string update = String.Format("{0}: new leg {1}", newBond.Name, newLeg.ID);
                        Updates.Add(new UiUpdateEntry(update));
                        continue;
                    }
                    if (oldLeg.State != newLeg.State)
                    {
                        string update = String.Format("{0}: leg {1} changed from {2} to {3}", newBond.Name, newLeg.ID, oldLeg.State, newLeg.State);
                        Updates.Add(new UiUpdateEntry(update));
                    }

                    WatchedLeg watchLeg = WatchedLegs.FirstOrDefault(x => x.ID == oldLeg.ID);
                    if (oldLeg.State != "down" && newLeg.State == "down")
                    {
                        //if this was triggered after the initial fetch (don't want to be sending redundant emails), add to the watchlist.
                        string update = String.Format("{0}, leg {1} dropped!", newBond.Name, newLeg.ID);
                        Updates.Add(new UiUpdateEntry(update));
                        if (watchLeg == null)
                        {
                            watchLeg = new WatchedLeg(newLeg.ID);
                            WatchedLegs.Add(watchLeg);
                        }
                    }

                    //now scan for this leg in the list, again.
                    if (watchLeg == null)
                    {
                        continue;
                    }

                    //if the leg is not down, add an up event.
                    if (newLeg.State != "down")
                    {
                        watchLeg.UpEvents.Add(DateTime.Now);
                        watchLeg.DownCount = Math.Max(watchLeg.DownCount - 1, 0);
                        if (watchLeg.DownCount == 0)
                        {
                            string Update = String.Format("{0} detected up for {1} ticks. Clearing alert.", newLeg.ID, Configuration.EmailAlertThreshold);
                            watchLeg.AlertedForConsecutiveDowns = false;
                        }
                    }

                    //if the leg is down, add a down event and check for consecutive downs.
                    else
                    {
                        watchLeg.DownEvents.Add(DateTime.Now);
                        watchLeg.DownCount = Math.Min(watchLeg.DownCount +1, Configuration.EmailAlertThreshold);
                        Updates.Add(new UiUpdateEntry(String.Format("{0}, leg {1} has been down for {2} ticks", newBond.Name, newLeg.ID, watchLeg.DownCount)));
                    }

                    //if there are enough consecutive downs, send an email, unless one was sent.
                    if (watchLeg.DownCount == Configuration.EmailAlertThreshold && !watchLeg.AlertedForConsecutiveDowns)
                    {
                        Email.AddDownAlert(newBond.ID, newLeg.ID, newBond.Name);
                        Updates.Add(new UiUpdateEntry(String.Format("Down alert added for {0}, leg {1}", newBond.Name, newLeg.ID)));
                        watchLeg.AlertedForConsecutiveDowns = true;
                        watchLeg.AlertedForConsecutiveDownsTime = DateTime.Now;
                    }

                    //check for health status of leg
                    if (watchLeg.Flapping() && !watchLeg.AlertedForFlap)
                    {
                        Email.AddFlapAlert(newBond.ID, newLeg.ID, newBond.Name);
                        Updates.Add(new UiUpdateEntry(String.Format("Flap alert added for {0}, leg {1}", newBond.Name, newLeg.ID)));
                        watchLeg.AlertedForFlap = true;
                        watchLeg.AlertedForFlapTime = DateTime.Now;
                    }

                    if (watchLeg.AlertedForFlap && !watchLeg.Flapping())
                    {
                        //if there are none in the last two hours at all, remove alert for flap - new alert will need to be sent.
                        if (watchLeg.DownEvents.Where(x=> x > DateTime.Now.AddHours(-2)).Count() == 0){
                            watchLeg.AlertedForFlap = false; 
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
                CurrentAction.Text = "Getting bonder data...";
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
                                legs += b.Legs.Count();
                                onlineLegs += b.Legs.Where(x => x.State != "down").Count();
                            }
                            Bonders.Text = String.Format("{0} bonders detected, {1}/{2} legs online. {3} legs being watched.", bonds.Count(), onlineLegs, legs, WatchedLegs.Count());
                            CurrentAction.Text = "Idle, last poll at " + System.DateTime.Now;
                        }
                    }
                    catch (WebException e)
                    {
                        //if it fails, we want to mark it as failed, and then return an empty bond list. Also, potentially, we want to log specific errors.
                        CurrentAction.Text = "";
                        Status.Text = "Offline";
                        using (WebResponse response = e.Response)
                        {
                            Configuration.Status = false; //mark it as failed.
                        }
                        CurrentAction.Text = "Failed to connect";
                        Status.Text = "Unable to connect.";
                    }
                });

                //add a ticker so we're sure that it's updatnig.
                Ticker.Text = "Loading.";
                while (!getData.IsCompleted)
                {
                    Thread.Sleep(100);
                    Ticker.Text += ".";
                }
                Configuration.Timer = Configuration.TimerTarget;
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
                Thread.Sleep(250);
                if (!ShowUI)
                {
                    //stuff.
                }
                else
                {
                    for (int i = 0; i < UIText.Count(); i++)
                    {
                        if (UIText[i].Clear)
                        {
                            int clearWidth = Console.WindowWidth;

                            //if it's set to clear, print blanks to override old stuff.
                            Console.SetCursorPosition(0, UIText[i].Row);
                            Console.Write(String.Concat(Enumerable.Repeat(" ", Console.WindowWidth)));
                            UIText[i].Clear = false;

                            Console.SetCursorPosition(UIText[i].Column, UIText[i].Row);
                            Console.Write(UIText[i].Text);
                        }
                    }

                    var maxUpdates = (Console.WindowTop + 4) + (Console.WindowHeight - 11);
                    while (Updates.Count() > maxUpdates)
                    {
                        Updates.RemoveRange(0, maxUpdates / 2);
                        for (int i = Updates.Count(); i < (Console.WindowTop + 4) + (Console.WindowHeight - 6); i++)
                        {
                            Console.SetCursorPosition(0, i);
                            Console.Write(string.Concat(Enumerable.Repeat(" ", Console.WindowWidth)));
                        }
                        for (int i = 0; i < Updates.Count(); i++)
                        {
                            Updates[i].Update = true;
                        }
                    }
                    for (int i = 0; i < Updates.Count() && i < maxUpdates; i++)
                    {
                        if (Updates[i].Update)
                        {
                            Console.SetCursorPosition(0, Console.WindowTop + 4 + i);
                            Console.Write(Updates[i].Text);
                            if (Updates[i].Text.Length < Console.WindowWidth)
                            {
                                Console.Write(string.Concat(Enumerable.Repeat(" ", Console.WindowWidth - Updates[i].Text.Length)));
                            }
                        }
                    }
                }
            }
        }

    }
}
