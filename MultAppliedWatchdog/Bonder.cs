using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class Bond
    {
        public int ID;
        public string Name
        {
            get
            {
                return Bonder.Name;
            }
        }
        public Bonder Bonder;
        [JsonProperty(PropertyName ="leg_list")]
        public List<Legs> Legs;
    }

    class Bonder
    {
        public int ID;
        public string Name;
    }

    class Legs
    {
        public int ID;
        public string State;
    }

    class WatchedLeg
    {
        //the id of the watched leg.
        public int ID;
        public int DownCount = 0; //how many checks it's been down for.
        public bool AlertedForConsecutiveDowns = false; //whether the alert has been sent or not.
        public DateTime AlertedForConsecutiveDownsTime; //ensure we don't send too frequently.

        public bool AlertedForFlap = false; //flap down is up/down, up/down, over a period of time. by default 5 occurances over 1 hour.
        public DateTime AlertedForFlapTime; 
         
        public List<DateTime> DownEvents = new List<DateTime>(); //time stamps for when the leg is down...
        public List<DateTime> UpEvents = new List<DateTime>(); //...and when the leg is up.

        public WatchedLeg(int _id)
        {
            ID = _id;
        }

        public bool Flapping()
        {
            DateTime now = DateTime.Now;

            //get only the entriies that are relevant to us.
            List<DateTime> checkDownEvents = DownEvents.Where(x => x > now.AddHours(-2)).ToList<DateTime>();
            List<DateTime> checkUpEvents = UpEvents.Where(x => x > now.AddHours(-2)).ToList<DateTime>();

            //need at least 30 entries to be worthwhile.
            if (checkDownEvents.Count + checkUpEvents.Count < 30)
            {
                return false;
            }

            //leg is considered unhealthy. Investigate.
            if (checkDownEvents.Count > checkUpEvents.Count / 10)
            {
                return true;
            }

            //leg is considered fine.
            return false;
        }
    }
}
