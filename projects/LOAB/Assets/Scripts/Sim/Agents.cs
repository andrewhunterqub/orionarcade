using System.Collections.Generic;

namespace Loab.Sim
{
    public enum CState { Entering, ToBar, Queue, Ordering, ToSeat, Seated, Standing, ToToilet, ToiletQueue, InToilet, ToGame, Playing, Leaving, Gone }

    public enum Note
    {
        WaitedLong, GaveUpQueue, LovedDrink, BadDrink, OutOfStock, Pricey, Bargain, Dirty, NoSeat, ComfySeat,
        LovedVibe, HatedVibe, LovedEvent, EventBombed, ToiletQueue, NoToilet, FriendlyStaff, GrumpyStaff,
        RowdyNearby, Slipped, Dog, Games, Screens, LovedMusic, HatedMusic, HappyHour, ShownDoor, Crowded, Regular,
        Refused, Count,
    }

    public sealed class Customer
    {
        public int id;
        public Seg seg;
        public SegmentDef def;
        public Regular regular;
        public string name;
        public uint shirt, skin, hair;
        public bool tall;

        public float x, y;                 // position in cell units (cell centre = integer coords)
        public List<Cell> path;
        public int pathIdx;
        public CState state;
        public float timer;                // generic countdown in game minutes
        public float arrivedAt, maxStay;

        public float budget, spent;
        public int drinks, alcoholic;
        public float mood = 60, moodSum, moodSamples;
        public float thirst = 0.8f, bladder, fun = 0.5f;
        public float patience;

        public int barPoint = -1;          // index into Service.BarPoints when queueing
        public int seatItem, seatSlot = -1;
        public Cell standCell;
        public int gameItem, toiletItem;
        public DrinkDef drink;             // what they're holding / ordered
        public float drinkLeft;            // minutes left on current drink
        public float queueStart;
        public bool rowdy, watching, paidTicket, refused;
        public float rowdyFor;

        public readonly bool[] notes = new bool[(int)Note.Count];
        public string thought;
        public float thoughtTime;

        public bool Inside => state != CState.Entering && state != CState.Gone;
        public bool Has(Note n) => notes[(int)n];
        public void Mark(Note n) => notes[(int)n] = true;
        public float Satisfaction => moodSamples > 0 ? moodSum / moodSamples : mood;
    }

    public sealed class StaffAgent
    {
        public StaffMember m;
        public float x, y;
        public bool present;
        public float arriveAt;
        public int barPoint = -1;          // bartender: which service point they're at
        public Customer serving;
        public float workLeft;
        public string activity = "Waiting";
        public int served;
        public float busyMinutes;
        // Floor staff
        public List<Cell> path;
        public int pathIdx;
        public Incident task;
        public float taskTimer;
        public bool IsOwner;
    }

    public enum IncidentKind { Spill, Glasses, Toilet, Rowdy }

    public sealed class Incident
    {
        public int id;
        public IncidentKind kind;
        public Cell cell;
        public float time;
        public int count;          // glasses on this spot
        public int itemUid;        // toilet / table
        public Customer customer;  // rowdy
        public bool claimed, resolved;
        public float retryAt;      // staff back off from incidents they couldn't reach
    }

    public sealed class ServiceMessage
    {
        public float time;
        public string text;
        public int kind; // 0 info, 1 good, 2 bad, 3 event, 4 mentor
        public bool hasCell;
        public Cell cell;
    }
}
