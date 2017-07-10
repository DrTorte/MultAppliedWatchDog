using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultAppliedWatchdog
{
    class Bond
    {
        public int id;
        public string name
        {
            get
            {
                return bonder.name;
            }
        }
        public Bonder bonder;
        public List<Legs> leg_list;
    }

    class Bonder
    {
        public int id;
        public string name;
    }

    class Legs
    {
        public int id;
        public string state;
    }

    class WatchedLeg
    {
        //the id of the watched leg.
        public int id;
        public int downCount = 0; //how many checks it's been down for.
        public bool alerted = false; //whether the alert has been sent or not.

        public WatchedLeg(int _id)
        {
            id = _id;
        }
    }
}
