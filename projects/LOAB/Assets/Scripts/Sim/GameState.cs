using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    // Everything persistent lives here. Public fields + Lists only so Unity's JsonUtility can save it.

    [Serializable]
    public class PlacedItem
    {
        public int uid;
        public string id;
        public int x, y, rot; // rot 0..3, footprint swaps W/H on odd rotations
        public float wear;    // 0 = new, 100 = knackered
    }

    [Serializable]
    public class MenuEntry
    {
        public string id;
        public bool onMenu;
        public float price;
        public int stock;
        // Demand tracking for restocking (task T4): served + wanted-but-out tonight, and a running average.
        public int soldTonight, missedTonight;
        public float demandAvg;
    }

    [Serializable]
    public class StaffMember
    {
        public int uid;
        public string name;
        public int role; // 0 bartender, 1 floor
        public int speed, charm, skill, stamina; // 1..20
        public List<string> traits = new List<string>();
        public int wage;      // per night
        public float morale;  // 0..100
        public int xp, level;
        public int nightsWorked;
        public uint shirt, skin, hair;
        // Rota (task S3): bit n set = not working on day-of-week n (0 = Monday). streak = nights worked in a row.
        public int daysOff, streak;
        public bool HasTrait(string t) => traits.Contains(t);
        public bool OffOn(int dayOfWeek) => (daysOff & (1 << dayOfWeek)) != 0;
    }

    [Serializable]
    public class Act
    {
        public int uid;
        public int kind;       // 0 quizmaster, 1 comedian
        public string name, style;
        public int fee, quality, reliability, following;
        public float volatility;
    }

    [Serializable]
    public class BookedEvent
    {
        public int uid;
        public int day;        // absolute day index
        public int kind;       // 0 quiz, 1 comedy
        public Act act;
        public int promo;      // £ spent
        public int ticket;     // £ per head (comedy) / entry (quiz)
    }

    [Serializable]
    public class Review
    {
        public int day;
        public int stars;
        public string author, segment, text;
    }

    [Serializable]
    public class Regular
    {
        public int uid;
        public string name;
        public int seg;
        public string favDrink;
        public int visits;
        public float loyalty; // 0..1
        public string lastVisitNote;
    }

    [Serializable]
    public class NightRecord
    {
        public int day;
        public int customers, turnedAway, servedDrinks, walkouts;
        public float takings, costs, wages, cogs, profit, satisfaction;
        public float rent, utilities, loan, other; // cost breakdown (task T6); other = fines, emergency deliveries, the dog
        // Tonight's verdict (task L5): the best thing, the biggest problem and what would fix it.
        public string highlight, problem, fix;
        public float[] vibe = new float[Axis.Count];
        public float rating;
        public string eventName, eventOutcome;
        public List<string> moments = new List<string>();
        public List<string> thoughts = new List<string>();
        public List<int> segCounts = new List<int>();
        public List<Review> reviews = new List<Review>();
        public List<string> stockOuts = new List<string>();
        public string weather;
        public int incidents, spills, rowdy;
    }

    [Serializable]
    public class DirectorCardState
    {
        public string id;
        public int day;
    }

    [Serializable]
    public class Flag
    {
        public string key;
        public int value;
    }

    [Serializable]
    public class GameState
    {
        public int version = SaveMigration.Current;
        public int mode;            // 0 career, 1 sandbox, 2 scenario
        public string scenario;     // scenario id when mode == 2
        public int scenarioResult;  // 0 running, 1 won, 2 out of time
        public int difficulty = 1;  // 0 relaxed, 1 standard, 2 hardcore
        public ulong seed;
        public string barName = "The Crooked Anchor";

        public int day;             // 0 = Monday week 1, autumn
        public float cash;
        public float reputation;    // 0..100
        public float rating;        // stars 1..5 (rolling)
        public int reviewCount;
        public float[] awareness = new float[Content.Segments.Count]; // per segment 0..1

        // Layout & style
        public int gridW = 16, gridH = 11;
        public int doorX = 7;       // door is on the y = 0 edge
        public List<PlacedItem> items = new List<PlacedItem>();
        public int nextUid = 1;
        public string floor = "boards";
        public string wall = "cream";
        public string music = "radio";
        public int lighting = 60;   // 0 dim .. 100 bright
        public int volume = 50;
        public bool responsibleService = true;
        public bool garden, gardenHeaters; // beer garden behind the bar (task O1)

        // Menu & stock
        public List<MenuEntry> menu = new List<MenuEntry>();
        public List<string> signatures = new List<string>(); // Cocktail Lab creations on the books (task K4)
        public int cleanGlasses = 80;
        public float lineQuality = 80; // beer line cleanliness 0..100
        public float cleanliness = 85;  // start-of-night cleanliness

        // People
        public List<StaffMember> staff = new List<StaffMember>();
        public List<StaffMember> applicants = new List<StaffMember>();
        public List<Regular> regulars = new List<Regular>();

        // Events
        public List<Act> agency = new List<Act>();
        public List<BookedEvent> events = new List<BookedEvent>();
        public int happyHourStart = -1; // hour (17..22), -1 = none
        // Residencies (brief 7.4): a hit act can become a weekly fixture.
        public Act residencyAct = new Act();
        public int residencyDay = -1;   // day of week, -1 = none
        public Act residencyOffer = new Act();
        public int residencyOfferDay = -1;
        public int happyHourDiscount = 30;

        // Reputation
        public List<Review> reviews = new List<Review>();
        public List<string> tags = new List<string>();
        public List<NightRecord> history = new List<NightRecord>();

        // Progression
        public int goalIndex;
        public List<string> unlockedItems = new List<string>();
        public List<Flag> flags = new List<Flag>();
        public List<string> seenTips = new List<string>();
        public int loansTaken;
        public float loanBalance;
        public float loanDaily;
        public bool bankrupt;

        // Today
        public string weather = "Cloudy";
        public string pendingCard;  // director card awaiting a choice
        public List<string> cardHistory = new List<string>();
        public float rivalPressure; // 0..1, reduces footfall
        public int rivalDays;
        public float socialPostBoost;
        public bool postedToday;
        public bool closedTonight;
        public float todaysExtraCosts;

        public int Week => day / 7 + 1;
        public int DayOfWeek => day % 7;
        public int Season => (day / 28) % 4;

        public int GetFlag(string key)
        {
            foreach (var f in flags) if (f.key == key) return f.value;
            return 0;
        }

        public void SetFlag(string key, int value)
        {
            foreach (var f in flags) if (f.key == key) { f.value = value; return; }
            flags.Add(new Flag { key = key, value = value });
        }

        public MenuEntry Menu(string drinkId) => menu.Find(m => m.id == drinkId);
        public PlacedItem Item(int uid) => items.Find(i => i.uid == uid);
        public StaffMember Staff(int uid) => staff.Find(s => s.uid == uid);
    }
}
