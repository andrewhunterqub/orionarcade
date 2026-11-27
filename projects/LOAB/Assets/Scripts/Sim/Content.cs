using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    // Vibe axes, each 0..100. Item/setting contributions are signed offsets.
    public static class Axis
    {
        public const int Energy = 0, Polish = 1, Era = 2, Intimacy = 3, Quirk = 4, Count = 5;
        public static readonly string[] Names = { "Energy", "Polish", "Era", "Intimacy", "Quirk" };
        public static readonly string[] Low = { "Chilled", "Rough & ready", "Classic", "Big & open", "Conventional" };
        public static readonly string[] High = { "Lively", "Upmarket", "Modern", "Cosy", "Weird & wonderful" };
    }

    public enum ItemFn { Decor, BarCounter, Seat, Toilet, Games, Stage, Screen, Jukebox, Speaker }

    public enum DrinkCat { Lager, Ale, Stout, Craft, Cider, Wine, Spirit, Cocktail, AlcoholFree, Hot }

    public enum Seg { Students, YoungPros, Locals, SportsFans, Creatives, AfterWork, Tourists, Couples }

    /// Staff roles (StaffMember.role). Door security added in task S1.
    public static class Roles
    {
        public const int Bartender = 0, Floor = 1, Door = 2, Cleaner = 3, Count = 4;
        public static readonly string[] Names = { "Bartender", "Floor staff", "Door security", "Cleaner" };
        /// Cleaners work after closing (task S2), so they don't appear during service.
        public static bool WorksService(int role) => role != Cleaner;
        public static int Next(int role) => (role + 1) % Count;
    }

    public sealed class ItemDef
    {
        public string Id, Name, Model, Blurb;
        public ItemFn Fn;
        public int W, H, Cost, Capacity, Comfort, Ent, UnlockRep;
        public float[] Vibe;
        public string[] Themes;
        public uint Color, Color2;
        public bool Walkable, Hidden, Glow;
        public string Category => Fn switch
        {
            ItemFn.BarCounter => "Bar",
            ItemFn.Seat => "Seating",
            ItemFn.Toilet => "Toilets",
            ItemFn.Games => "Games",
            ItemFn.Stage or ItemFn.Speaker or ItemFn.Jukebox => "Stage & Music",
            ItemFn.Screen => "Screens",
            _ => Glow ? "Lighting" : "Decor",
        };
    }

    public sealed class StyleDef
    {
        public string Id, Name;
        public int Cost, UnlockRep;
        public float[] Vibe;
        public string[] Themes;
        public uint Color, Color2;
    }

    public sealed class MusicDef
    {
        public string Id, Name;
        public float[] Vibe;
        public string[] Themes;
        public float[] SegLikes; // per Seg, -1..1
    }

    public sealed class SupplierDef
    {
        public string Id, Name, Blurb;
        public float Quality, Reliability;
    }

    public sealed class DrinkDef
    {
        public string Id, Name, Supplier;
        public DrinkCat Cat;
        public float UnitCost, DefaultPrice, PrepMinutes;
        public string[] Themes;
        /// Hot drinks are soft by default; Boozy marks the ones that aren't (mulled wine, hot toddy), so
        /// responsible service still applies to them.
        public bool Boozy;
        public bool Alcoholic => Cat != DrinkCat.AlcoholFree && (Cat != DrinkCat.Hot || Boozy);
        public int CaseSize => Cat switch
        {
            DrinkCat.Lager or DrinkCat.Ale or DrinkCat.Stout or DrinkCat.Craft or DrinkCat.Cider => 88, // keg ~ 88 pints
            DrinkCat.Wine => 24,
            DrinkCat.Spirit or DrinkCat.Cocktail => 28,
            _ => 24,
        };
    }

    public sealed class SegmentDef
    {
        public Seg Id;
        public string Name, Plural, Blurb;
        public float[] VibePref;
        public float VibeTolerance;     // larger = less fussy
        public float BudgetMin, BudgetMax;
        public float PriceSensitivity;  // 0..1
        public float Patience;          // minutes willing to queue
        public float[] DrinkPrefs;      // per DrinkCat
        public float[] DayWeights;      // Mon..Sun
        public float Share;             // share of district footfall
        public float Games, Screens, Stage, Jukebox; // appetite for entertainment types
        public float Quiz, Comedy;      // event affinity
        public float RowdyBase;
        public Dictionary<string, float> ThemeAffinity;
        public uint Color;
    }

    public sealed class TraitDef
    {
        public string Id, Name, Blurb;
    }

    public sealed class ThemeDef
    {
        public string Id, Name, Blurb;
    }

    public static class Content
    {
        public static readonly List<ItemDef> Items = new List<ItemDef>();
        public static readonly List<StyleDef> Floors = new List<StyleDef>();
        public static readonly List<StyleDef> Walls = new List<StyleDef>();
        public static readonly List<MusicDef> Music = new List<MusicDef>();
        public static readonly List<SupplierDef> Suppliers = new List<SupplierDef>();
        public static readonly List<DrinkDef> Drinks = new List<DrinkDef>();
        public static readonly List<SegmentDef> Segments = new List<SegmentDef>();
        public static readonly List<TraitDef> Traits = new List<TraitDef>();
        public static readonly List<ThemeDef> Themes = new List<ThemeDef>();

        static readonly Dictionary<string, ItemDef> _items = new Dictionary<string, ItemDef>();
        static readonly Dictionary<string, DrinkDef> _drinks = new Dictionary<string, DrinkDef>();

        public static ItemDef Item(string id) => _items.TryGetValue(id, out var d) ? d : null;
        public static DrinkDef Drink(string id) => _drinks.TryGetValue(id, out var d) ? d : null;
        public static StyleDef Floor(string id) => Floors.Find(f => f.Id == id) ?? Floors[0];
        public static StyleDef Wall(string id) => Walls.Find(f => f.Id == id) ?? Walls[0];
        public static MusicDef Genre(string id) => Music.Find(f => f.Id == id) ?? Music[0];
        public static SupplierDef Supplier(string id) => Suppliers.Find(s => s.Id == id);
        public static SegmentDef Segment(Seg s) => Segments[(int)s];
        public static TraitDef Trait(string id) => Traits.Find(t => t.Id == id);
        public static ThemeDef Theme(string id) => Themes.Find(t => t.Id == id);

        public static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        public static readonly string[] DayShort = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        public static readonly string[] Seasons = { "Autumn", "Winter", "Spring", "Summer" };

        static float[] V(float e, float p, float r, float i, float q) => new[] { e, p, r, i, q };
        static string[] T(params string[] t) => t;

        static void Add(string id, string name, ItemFn fn, int w, int h, int cost, int cap, int comfort, int ent,
            float[] vibe, string[] themes, string model, uint color, uint color2 = 0, int unlock = 0, string blurb = "",
            bool walkable = false, bool glow = false, bool hidden = false)
        {
            var d = new ItemDef
            {
                Id = id, Name = name, Fn = fn, W = w, H = h, Cost = cost, Capacity = cap, Comfort = comfort, Ent = ent,
                Vibe = vibe, Themes = themes, Model = model, Color = color, Color2 = color2 == 0 ? color : color2,
                UnlockRep = unlock, Blurb = blurb, Walkable = walkable, Glow = glow, Hidden = hidden,
            };
            Items.Add(d);
            _items[id] = d;
        }

        /// Wholesale cost multiplier on the base costs below: gives about 55-65% gross margin at normal prices,
        /// in line with UK pub "wet" margins (was 1.25, about 70%, which made money too easy; task T5).
        public const float WholesaleMarkup = 1.55f;

        static void Drink(string id, string name, DrinkCat cat, string sup, float cost, float price, float prep, params string[] themes)
        {
            var d = new DrinkDef { Id = id, Name = name, Cat = cat, Supplier = sup, UnitCost = cost * WholesaleMarkup, DefaultPrice = price, PrepMinutes = prep, Themes = themes };
            Drinks.Add(d);
            _drinks[id] = d;
        }

        static Content()
        {
            // ---- Themes -------------------------------------------------------------------------------
            Themes.Add(new ThemeDef { Id = "pub", Name = "Traditional Pub", Blurb = "Wood, real ale, darts and a crackling fire." });
            Themes.Add(new ThemeDef { Id = "speakeasy", Name = "Speakeasy", Blurb = "Low light, leather, jazz and serious cocktails." });
            Themes.Add(new ThemeDef { Id = "sports", Name = "Sports Bar", Blurb = "Big screens, pints and match-day noise." });
            Themes.Add(new ThemeDef { Id = "dive", Name = "Dive Bar", Blurb = "Sticky floors, neon, a jukebox and zero pretension." });
            Themes.Add(new ThemeDef { Id = "arcade", Name = "Retro Arcade Bar", Blurb = "Cabinets, chiptunes and neon flamingos." });
            Themes.Add(new ThemeDef { Id = "craft", Name = "Craft Beer Hall", Blurb = "Bare brick, long benches, a wall of taps and beer that tastes of grapefruit." });
            Themes.Add(new ThemeDef { Id = "lounge", Name = "Wine & Cocktail Lounge", Blurb = "Low velvet, candlelight, a serious wine list and a bartender who shakes." });

            // ---- Bar counters -------------------------------------------------------------------------
            Add("bar_counter", "Bar Counter", ItemFn.BarCounter, 3, 2, 1500, 3, 0, 0, V(0, 0, 0, 0, 0), T(), "bar", 0xA8573A, 0x5a2a1a,
                blurb: "Three serving points. Your bartenders work behind it.");
            Add("bar_neon", "Neon Dive Bar", ItemFn.BarCounter, 3, 2, 1900, 3, 0, 0, V(5, -4, 4, -2, 6), T("dive", "arcade"), "bar", 0x2a2a35, 0xff3fa4,
                unlock: 10, blurb: "A battered counter with a pink neon underglow.");
            Add("bar_deco", "Art Deco Bar", ItemFn.BarCounter, 3, 2, 3400, 3, 0, 0, V(0, 9, -3, 2, 2), T("speakeasy", "lounge"), "bar", 0x2b1d14, 0xd4a64a,
                unlock: 30, blurb: "Brass rails and a mirrored back bar. Very 1920s.");

            // ---- Seating ------------------------------------------------------------------------------
            Add("pub_table", "Pub Table & Stools", ItemFn.Seat, 2, 2, 260, 4, 4, 0, V(0, 0, -4, 2, 0), T("pub", "sports"), "pubtable", 0x7a4e2c, 0x4a2e18,
                blurb: "Seats 4. Solid, sensible, slightly wobbly.");
            Add("long_table", "Long Bench Table", ItemFn.Seat, 3, 1, 380, 6, 3, 0, V(3, -1, -2, -3, 0), T("pub", "sports", "craft"), "longtable", 0x8a5a33, 0x5a3a22,
                blurb: "Seats 6. Great for groups and quiz teams.");
            Add("bar_stool", "Bar Stool", ItemFn.Seat, 1, 1, 60, 1, 2, 0, V(1, 0, 0, 0, 0), T(), "stool", 0x3a3a3a, 0xa33a2a,
                blurb: "Seats 1. Cheap perch.");
            Add("high_table", "Poseur Table", ItemFn.Seat, 1, 1, 140, 2, 2, 0, V(3, 1, 3, -1, 0), T("sports", "craft"), "hightable", 0x222222, 0xbbbbbb,
                blurb: "Seats 2. Standing-room chic.");
            Add("barrel_table", "Barrel Table", ItemFn.Seat, 1, 1, 120, 2, 2, 0, V(1, -1, -4, 1, 3), T("pub", "craft"), "barrel", 0x6b4226, 0x333333,
                blurb: "Seats 2. An old ale cask with ambitions.");
            Add("plastic_set", "Plastic Garden Set", ItemFn.Seat, 1, 1, 40, 2, 1, 0, V(0, -6, 0, -2, 2), T("dive"), "plastic", 0xf2f2f2, 0x2e86de,
                blurb: "Seats 2. Nobody has ever chosen these on purpose.");
            Add("beanbags", "Beanbags", ItemFn.Seat, 1, 1, 90, 2, 6, 0, V(-2, -3, 4, 2, 6), T("arcade"), "beanbag", 0xff9f1c, 0x9b5de5,
                blurb: "Seats 2. Getting up is the hard part.");
            Add("booth", "Leather Booth", ItemFn.Seat, 2, 2, 650, 4, 8, 0, V(-2, 4, -2, 7, 0), T("speakeasy", "pub", "lounge"), "booth", 0xFF6B6B, 0x6b3a26,
                unlock: 5, blurb: "Seats 4. Date-night magnet.");
            Add("armchair", "Velvet Armchair", ItemFn.Seat, 1, 1, 300, 1, 8, 0, V(-3, 6, -3, 5, 1), T("speakeasy", "lounge"), "armchair", 0x1f5f4a, 0x2b1b10,
                unlock: 10, blurb: "Seats 1. Sink in and stay a while.");
            Add("chesterfield", "Chesterfield Sofa", ItemFn.Seat, 2, 1, 850, 3, 9, 0, V(-3, 6, -5, 5, 0), T("speakeasy", "lounge"), "sofa", 0x5a2a16, 0x3a1a0c,
                unlock: 20, blurb: "Seats 3. Buttoned leather, very serious.");

            // ---- Toilets ------------------------------------------------------------------------------
            Add("toilets", "Toilet Block", ItemFn.Toilet, 2, 2, 900, 2, 0, 0, V(0, 0, 0, 0, 0), T(), "toilets", 0xd8d8d8, 0x2e86de,
                blurb: "Two cubicles. Customers notice if there aren't enough.");
            Add("toilets_lux", "Fancy Washrooms", ItemFn.Toilet, 2, 2, 2200, 3, 0, 0, V(0, 5, 2, 0, 1), T(), "toilets", 0x2b2b2b, 0xd4a64a,
                unlock: 25, blurb: "Three cubicles, nice soap, people take photos.");

            // ---- Games --------------------------------------------------------------------------------
            Add("darts", "Dartboard", ItemFn.Games, 1, 1, 90, 2, 0, 6, V(2, -2, -4, 0, 0), T("pub", "sports"), "darts", 0x222222, 0xd62828,
                blurb: "Locals love it. Nobody hits the bullseye.");
            Add("pool", "Pool Table", ItemFn.Games, 2, 1, 700, 2, 0, 8, V(4, -2, 0, -2, 0), T("sports", "dive"), "pool", 0x1b6b3a, 0x4a2e18,
                blurb: "Keeps people (and their wallets) around longer.");
            Add("foosball", "Table Football", ItemFn.Games, 2, 1, 400, 4, 0, 7, V(5, -2, 0, -2, 2), T("sports", "dive"), "foosball", 0x2e7d32, 0xeeeeee,
                blurb: "Loud, fast, spinning is cheating.");
            Add("boardgames", "Board Game Shelf", ItemFn.Games, 1, 1, 250, 4, 0, 5, V(-2, 1, -1, 4, 4), T("arcade"), "boardgames", 0x8a5a33, 0xffd166,
                blurb: "Long dwell time and very long arguments.");
            Add("arcade_cab", "Arcade Cabinet", ItemFn.Games, 1, 1, 600, 1, 0, 7, V(4, -1, 3, -1, 6), T("arcade", "dive"), "arcade", 0x1a1a2e, 0x4cc9f0,
                unlock: 5, blurb: "Insert coin.");
            Add("pinball", "Pinball Machine", ItemFn.Games, 1, 1, 800, 1, 0, 7, V(4, -2, -1, -1, 5), T("dive", "arcade"), "pinball", 0x7b2cbf, 0xffd166,
                unlock: 12, blurb: "Flashing lights, satisfying thunks.");

            // ---- Stage & music ------------------------------------------------------------------------
            Add("stage", "Small Stage", ItemFn.Stage, 3, 2, 1200, 0, 0, 4, V(4, 0, 0, -2, 2), T(), "stage", 0x2a2a2a, 0x8a5a33,
                blurb: "Required for Comedy Nights. Makes any room an event.");
            Add("speakers", "PA Speakers", ItemFn.Speaker, 1, 1, 350, 0, 0, 0, V(3, 0, 1, -1, 0), T(), "speaker", 0x111111, 0x444444,
                blurb: "Better sound for quizzes and comedy (+event quality).");
            Add("jukebox", "Jukebox", ItemFn.Jukebox, 1, 1, 450, 0, 0, 3, V(3, -2, -3, 0, 4), T("dive", "pub"), "jukebox", 0xc0392b, 0xffd166,
                blurb: "Customers pick the music. What could go wrong?", glow: true);
            Add("piano", "Old Upright Piano", ItemFn.Decor, 2, 1, 900, 0, 0, 2, V(0, 3, -6, 3, 3), T("speakeasy", "pub"), "piano", 0x2b1b10, 0xeeeeee,
                unlock: 8, blurb: "Slightly out of tune, which is the point.");

            // ---- Screens ------------------------------------------------------------------------------
            Add("tv", "Big Screen TV", ItemFn.Screen, 1, 1, 500, 0, 0, 6, V(3, 0, 3, -4, -1), T("sports"), "tv", 0x111111, 0x4cc9f0,
                blurb: "Essential for sports fans. Kills intimacy.", glow: true);
            Add("projector", "Projector Screen", ItemFn.Screen, 2, 1, 1100, 0, 0, 9, V(4, 0, 3, -5, 0), T("sports"), "projector", 0xeeeeee, 0x4cc9f0,
                unlock: 15, blurb: "Match day, but massive.", glow: true);

            // ---- Lighting -----------------------------------------------------------------------------
            Add("edison", "Edison Bulbs", ItemFn.Decor, 1, 1, 120, 0, 0, 0, V(0, 2, -1, 4, 2), T("speakeasy", "craft"), "edison", 0xFFD27A, 0xFFD27A,
                blurb: "Warm filament glow.", glow: true);
            Add("candles", "Candle Cluster", ItemFn.Decor, 1, 1, 40, 0, 0, 0, V(-4, 3, -2, 7, 0), T("speakeasy", "lounge"), "candles", 0xfff1c1, 0xffb347,
                blurb: "Instant intimacy.", glow: true);
            Add("neon_open", "Neon 'OPEN' Sign", ItemFn.Decor, 1, 1, 180, 0, 0, 0, V(3, -2, 3, -1, 5), T("dive", "arcade"), "neon", 0xff3fa4, 0xff3fa4,
                blurb: "Buzzes faintly.", glow: true);
            Add("festoon", "Festoon Lights", ItemFn.Decor, 1, 1, 100, 0, 0, 0, V(2, 1, 1, 2, 2), T(), "festoon", 0xFFD27A, 0xFFD27A,
                blurb: "Strings of bulbs. Everyone likes these.", glow: true);
            Add("discoball", "Disco Ball", ItemFn.Decor, 1, 1, 250, 0, 0, 1, V(8, -1, -2, -3, 7), T("arcade"), "discoball", 0xdddddd, 0xffffff,
                blurb: "Ironically or not.", glow: true);
            Add("chandelier", "Chandelier", ItemFn.Decor, 1, 1, 900, 0, 0, 0, V(0, 8, -4, 2, 2), T("speakeasy", "lounge"), "chandelier", 0xd4a64a, 0xfff1c1,
                unlock: 20, blurb: "Crystal drops and delusions of grandeur.", glow: true);

            // ---- Decor --------------------------------------------------------------------------------
            Add("plant", "Potted Palm", ItemFn.Decor, 1, 1, 80, 0, 0, 0, V(0, 2, 1, 2, 1), T(), "plant", 0x6CC070, 0xD98A63, blurb: "Green and pleasant.");
            Add("fern", "Hanging Fern", ItemFn.Decor, 1, 1, 60, 0, 0, 0, V(-1, 1, 0, 3, 1), T(), "fern", 0x40916c, 0x5a3a22, blurb: "Softens any corner.");
            Add("photos", "Old Local Photos", ItemFn.Decor, 1, 1, 50, 0, 0, 0, V(0, 0, -5, 2, 1), T("pub"), "frames", 0x5a3a22, 0xd9c9a3, blurb: "Sepia history of the street.");
            Add("mirror", "Brewery Mirror", ItemFn.Decor, 1, 1, 160, 0, 0, 0, V(0, 2, -5, 1, 1), T("pub"), "frames", 0xd4a64a, 0x9ec5d6, blurb: "Gilt lettering, fictional brewery.");
            Add("fish", "Mounted Fish", ItemFn.Decor, 1, 1, 70, 0, 0, 0, V(0, -2, -4, 0, 5), T("pub", "dive"), "frames", 0x5a3a22, 0x6c9a8b, blurb: "It's called Gerald.");
            Add("pennants", "Sports Pennants", ItemFn.Decor, 1, 1, 60, 0, 0, 0, V(3, -2, 1, -1, 1), T("sports"), "frames", 0x1d3557, 0xe63946, blurb: "Teams you've never heard of.");
            Add("records", "Vinyl Wall", ItemFn.Decor, 1, 1, 180, 0, 0, 0, V(2, 1, 1, 1, 4), T(), "frames", 0x111111, 0xffd166, blurb: "Crate-digger credibility.");
            Add("flamingo", "Neon Flamingo", ItemFn.Decor, 1, 1, 220, 0, 0, 0, V(3, -1, 3, 0, 8), T("arcade", "dive"), "flamingo", 0xff6fb5, 0xff6fb5, blurb: "Tasteful? No. Iconic? Yes.", glow: true);
            Add("bookshelf", "Bookshelf", ItemFn.Decor, 1, 1, 200, 0, 0, 0, V(-3, 4, -3, 5, 2), T("speakeasy"), "bookshelf", 0x4a2e18, 0x9b2226, blurb: "Mostly unread. Very cosy.");
            Add("curtains", "Velvet Curtains", ItemFn.Decor, 1, 1, 220, 0, 0, 0, V(-2, 5, -3, 5, 1), T("speakeasy", "lounge"), "curtains", 0x7b1e3a, 0xd4a64a, blurb: "Theatrical and plush.");
            Add("pipes", "Exposed Pipe Art", ItemFn.Decor, 1, 1, 150, 0, 0, 0, V(1, 0, 7, -1, 3), T("craft"), "pipes", 0x9a9a9a, 0xb87333, blurb: "Industrial chic.");
            Add("chalkboard", "Chalkboard Specials", ItemFn.Decor, 1, 1, 50, 0, 0, 0, V(1, 0, -1, 1, 1), T("pub", "craft"), "chalkboard", 0x222222, 0xffffff, blurb: "Today: ambition.");
            Add("fishtank", "Fish Tank", ItemFn.Decor, 2, 1, 700, 0, 0, 1, V(-2, 3, 2, 3, 6), T(), "fishtank", 0x3a86ff, 0x222222, unlock: 8, blurb: "Hypnotic. The fish have opinions.", glow: true);
            Add("fireplace", "Fireplace", ItemFn.Decor, 2, 1, 1400, 0, 6, 0, V(-3, 4, -6, 8, 1), T("pub", "speakeasy"), "fireplace", 0x6b4a3a, 0xff7b00, unlock: 10, blurb: "The cosiest thing you can buy.", glow: true);
            Add("rug", "Persian Rug", ItemFn.Decor, 2, 2, 300, 0, 1, 0, V(-1, 3, -3, 3, 1), T("speakeasy"), "rug", 0x9b2226, 0xe9c46a, blurb: "Walkable. Hides a multitude of sins.", walkable: true);

            // ---- Craft Beer Hall (task K1) -------------------------------------------------------------
            Add("bar_craft", "Steel & Ply Bar", ItemFn.BarCounter, 3, 2, 2600, 3, 0, 0, V(2, 2, 6, -1, 3), T("craft"), "bar", 0x6b6f73, 0xc89b5c,
                unlock: 15, blurb: "Brushed steel, birch ply and a row of shiny taps.");
            Add("beer_bench", "Beer Hall Bench", ItemFn.Seat, 3, 1, 420, 6, 3, 0, V(4, -1, 3, -3, 1), T("craft"), "longtable", 0xc9a26b, 0x8a6a42,
                blurb: "Seats 6. Pale pine, made for sharing a table with strangers.");
            Add("tap_wall", "Tap Wall", ItemFn.Decor, 2, 1, 1600, 0, 0, 0, V(2, 3, 6, -1, 4), T("craft"), "tapwall", 0x2b2b2b, 0xc0c0c0,
                unlock: 15, blurb: "Twenty taps, twenty chalk labels, one very proud bar manager.");
            Add("keg_stack", "Keg Stack", ItemFn.Decor, 1, 1, 240, 0, 0, 0, V(1, -2, 3, -1, 3), T("craft"), "kegs", 0xb8bcc2, 0x2a6f97,
                blurb: "Empty kegs, artfully piled. Mostly empty.");
            Add("hop_garland", "Hop Garland", ItemFn.Decor, 1, 1, 90, 0, 0, 0, V(0, 1, -1, 2, 3), T("craft", "pub"), "festoon", 0x7fb069, 0x7fb069,
                blurb: "Dried hops strung across the beams. Smells like a brewery.");
            Add("growler_shelf", "Growler Shelf", ItemFn.Decor, 1, 1, 260, 0, 0, 0, V(0, 2, 4, 0, 4), T("craft"), "bookshelf", 0x8a6a42, 0xb5651d,
                blurb: "Glass growlers for takeaway refills. Very on trend.");

            // ---- Wine & Cocktail Lounge (task K1) ------------------------------------------------------
            Add("bar_marble", "Marble Cocktail Bar", ItemFn.BarCounter, 3, 2, 3800, 3, 0, 0, V(-1, 10, 1, 4, 2), T("lounge"), "bar", 0xe8e4dc, 0xc9a24a,
                unlock: 35, blurb: "White marble, brass footrail and a back bar lit like a jewellery shop.");
            Add("banquette", "Velvet Banquette", ItemFn.Seat, 3, 1, 980, 3, 9, 0, V(-3, 7, -1, 7, 1), T("lounge"), "sofa", 0x4a1f3d, 0x2b1b10,
                unlock: 20, blurb: "Seats 3. Deep plum velvet along the wall.");
            Add("cocktail_table", "Marble Cocktail Table", ItemFn.Seat, 1, 1, 220, 2, 4, 0, V(-1, 6, 1, 4, 0), T("lounge"), "hightable", 0xe8e4dc, 0xc9a24a,
                blurb: "Seats 2. Small, round and very elegant.");
            Add("wine_wall", "Wine Wall", ItemFn.Decor, 2, 1, 1500, 0, 0, 0, V(-2, 8, -1, 5, 2), T("lounge"), "winerack", 0x3a2418, 0x6b1a2a,
                unlock: 20, blurb: "Floor-to-ceiling bottles. Customers assume you know what they all are.");
            Add("brass_lamps", "Brass Table Lamps", ItemFn.Decor, 1, 1, 180, 0, 0, 0, V(-3, 5, -2, 6, 1), T("lounge", "speakeasy"), "candles", 0xffc46b, 0xc9a24a,
                blurb: "Warm pools of light. Everyone looks better.");
            Add("gallery_prints", "Gallery Prints", ItemFn.Decor, 1, 1, 200, 0, 0, 0, V(-1, 5, 3, 2, 4), T("lounge"), "frames", 0x222222, 0xe76f51,
                blurb: "Abstract prints from a local artist. Prices on request.");
            Add("dog_bed", "Biscuit's Bed", ItemFn.Decor, 1, 1, 0, 0, 0, 1, V(0, 0, 0, 3, 6), T("pub"), "dogbed", 0x8a5a33, 0xd9a066, blurb: "Home of the bar's unofficial mascot.", hidden: true);

            // ---- Floors & walls -----------------------------------------------------------------------
            Floors.Add(new StyleDef { Id = "boards", Name = "Honey Oak Boards", Cost = 0, Vibe = V(0, -3, -3, 1, 0), Themes = T("pub", "dive"), Color = 0xB9805A, Color2 = 0xAE7650 });
            Floors.Add(new StyleDef { Id = "oak", Name = "Polished Oak", Cost = 1200, Vibe = V(0, 5, -2, 2, 0), Themes = T("pub", "speakeasy", "lounge"), Color = 0x9a6a3b, Color2 = 0x8a5d33 });
            Floors.Add(new StyleDef { Id = "checker", Name = "Checker Tile", Cost = 900, Vibe = V(2, 2, 1, -1, 3), Themes = T("dive", "arcade"), Color = 0xe8e8e8, Color2 = 0x222222 });
            Floors.Add(new StyleDef { Id = "concrete", Name = "Polished Concrete", Cost = 800, Vibe = V(1, 2, 7, -3, 1), Themes = T("sports", "craft"), Color = 0x8c8c8c, Color2 = 0x828282 });
            Floors.Add(new StyleDef { Id = "carpet", Name = "Plush Carpet", Cost = 1000, Vibe = V(-3, 3, -3, 5, 0), Themes = T("speakeasy", "lounge"), Color = 0x6a1e2a, Color2 = 0x601a26 });
            Floors.Add(new StyleDef { Id = "parquet", Name = "Herringbone Parquet", Cost = 1400, Vibe = V(-2, 7, -2, 4, 1), Themes = T("lounge"), Color = 0x7a4a2a, Color2 = 0x6a3f22 });
            Floors.Add(new StyleDef { Id = "sticky", Name = "Sticky Carpet", Cost = 300, Vibe = V(1, -8, -4, 1, 4), Themes = T("dive", "pub"), Color = 0x5a2a4a, Color2 = 0x4e2440 });

            Walls.Add(new StyleDef { Id = "cream", Name = "Peach Plaster", Cost = 0, Vibe = V(0, -4, -3, 0, 0), Themes = T("pub", "dive"), Color = 0xF2B38A });
            Walls.Add(new StyleDef { Id = "panelling", Name = "Terracotta Panelling", Cost = 1500, Vibe = V(-2, 5, -4, 5, 0), Themes = T("pub", "speakeasy"), Color = 0xD98A63 });
            Walls.Add(new StyleDef { Id = "brick", Name = "Exposed Brick", Cost = 1000, Vibe = V(1, 1, 4, 1, 2), Themes = T("craft"), Color = 0x9c4a32 });
            Walls.Add(new StyleDef { Id = "green", Name = "Midnight Green", Cost = 700, Vibe = V(-2, 4, 1, 4, 1), Themes = T("speakeasy", "lounge"), Color = 0x1b3b33 });
            Walls.Add(new StyleDef { Id = "pink", Name = "Hot Pink & Neon", Cost = 700, Vibe = V(6, -1, 4, -2, 7), Themes = T("arcade", "dive"), Color = 0xc2185b });
            Walls.Add(new StyleDef { Id = "plum", Name = "Velvet Plum", Cost = 900, Vibe = V(-3, 6, 0, 6, 2), Themes = T("lounge"), Color = 0x4a1f3d });
            Walls.Add(new StyleDef { Id = "navy", Name = "Club Navy", Cost = 700, Vibe = V(3, 1, 2, -1, -1), Themes = T("sports"), Color = 0x1d3557 });

            // ---- Music ------------------------------------------------------------------------------
            //                                                                          Stu   YP    Loc   Spt   Cre
            Music.Add(new MusicDef { Id = "radio", Name = "Telly & Radio", Vibe = V(0, -1, 0, 0, -2), Themes = T("sports"), SegLikes = new[] { -0.3f, -0.3f, 0.2f, 0.4f, -0.4f , 0.4f, 0.0f, -0.3f } });
            Music.Add(new MusicDef { Id = "jazz", Name = "Jazz", Vibe = V(-3, 6, -5, 5, 2), Themes = T("speakeasy", "lounge"), SegLikes = new[] { -0.3f, 0.5f, 0.1f, -0.4f, 0.4f , 0.6f, 0.3f, 0.7f } });
            Music.Add(new MusicDef { Id = "indie", Name = "Indie", Vibe = V(3, 0, 3, 0, 3), Themes = T("craft"), SegLikes = new[] { 0.6f, 0.2f, -0.2f, 0.0f, 0.6f , -0.1f, 0.1f, 0.3f } });
            Music.Add(new MusicDef { Id = "rock", Name = "Classic Rock", Vibe = V(5, -3, -3, -1, 1), Themes = T("dive", "sports"), SegLikes = new[] { 0.3f, -0.1f, 0.3f, 0.5f, 0.0f , 0.1f, 0.3f, -0.3f } });
            Music.Add(new MusicDef { Id = "pop", Name = "Pop Hits", Vibe = V(6, 0, 4, -3, -2), Themes = T(), SegLikes = new[] { 0.6f, 0.3f, -0.3f, 0.2f, -0.4f , 0.0f, 0.2f, 0.1f } });
            Music.Add(new MusicDef { Id = "electronic", Name = "Electronic", Vibe = V(7, 2, 7, -3, 3), Themes = T("arcade"), SegLikes = new[] { 0.5f, 0.4f, -0.6f, -0.2f, 0.4f , -0.5f, -0.2f, -0.4f } });
            Music.Add(new MusicDef { Id = "folk", Name = "Folk & Trad", Vibe = V(-2, 0, -7, 4, 2), Themes = T("pub"), SegLikes = new[] { -0.4f, -0.2f, 0.6f, 0.0f, 0.3f , 0.1f, 0.8f, 0.2f } });
            Music.Add(new MusicDef { Id = "soul", Name = "Soul & Funk", Vibe = V(4, 3, -3, 1, 2), Themes = T("lounge"), SegLikes = new[] { 0.2f, 0.5f, 0.3f, 0.1f, 0.4f , 0.5f, 0.3f, 0.6f } });
            Music.Add(new MusicDef { Id = "chiptune", Name = "Retro Chiptunes", Vibe = V(3, -1, 1, 0, 8), Themes = T("arcade"), SegLikes = new[] { 0.3f, -0.2f, -0.3f, -0.2f, 0.6f , -0.5f, -0.2f, -0.3f } });

            // ---- Suppliers & drinks -----------------------------------------------------------------
            Suppliers.Add(new SupplierDef { Id = "barrel", Name = "Big Barrel Wholesale", Quality = 52, Reliability = 0.97f, Blurb = "Cheap, cheerful, always turns up." });
            Suppliers.Add(new SupplierDef { Id = "docklands", Name = "Docklands Brewing Co.", Quality = 82, Reliability = 0.86f, Blurb = "Local craft brewers. Brilliant beer, occasionally late." });
            Suppliers.Add(new SupplierDef { Id = "hopfield", Name = "Hopfield Craft Collective", Quality = 88, Reliability = 0.82f, Blurb = "Small-batch brewers and makers. Superb, pricey, sometimes sold out." });
            Suppliers.Add(new SupplierDef { Id = "chateau", Name = "Chateau Direct", Quality = 90, Reliability = 0.9f, Blurb = "Fine wines and premium spirits, imported with a flourish." });
            Suppliers.Add(new SupplierDef { Id = "cashcarry", Name = "Cut-Price Cash & Carry", Quality = 38, Reliability = 0.99f, Blurb = "Rock-bottom prices. You get what you pay for." });
            Suppliers.Add(new SupplierDef { Id = "velvet", Name = "Velvet & Vine Merchants", Quality = 76, Reliability = 0.93f, Blurb = "Wines, spirits and cocktail kit." });

            Drink("lager", "Hopkins Lager", DrinkCat.Lager, "barrel", 1.10f, 4.60f, 1.1f, "sports", "dive");
            Drink("pils", "Kessler Pils", DrinkCat.Lager, "docklands", 1.55f, 5.40f, 1.1f, "craft");
            Drink("bitter", "Old Anchor Bitter", DrinkCat.Ale, "barrel", 1.15f, 4.40f, 1.3f, "pub");
            Drink("stout", "Midnight Stout", DrinkCat.Stout, "barrel", 1.30f, 4.90f, 1.6f, "pub");
            Drink("ipa", "Docklands IPA", DrinkCat.Craft, "docklands", 1.90f, 6.20f, 1.2f, "craft");
            Drink("sour", "Wonky Sour", DrinkCat.Craft, "docklands", 2.20f, 6.50f, 1.2f, "arcade", "craft");
            Drink("cider", "Orchard Gold Cider", DrinkCat.Cider, "barrel", 1.20f, 4.70f, 1.1f, "pub");
            Drink("house_red", "House Red", DrinkCat.Wine, "barrel", 1.40f, 5.50f, 0.8f, "lounge");
            Drink("house_white", "House White", DrinkCat.Wine, "barrel", 1.40f, 5.50f, 0.8f, "lounge");
            Drink("fizz", "Fizz (glass)", DrinkCat.Wine, "velvet", 2.10f, 7.20f, 0.9f, "speakeasy", "lounge");
            Drink("gin_tonic", "Gin & Tonic", DrinkCat.Spirit, "velvet", 1.60f, 7.00f, 1.4f, "speakeasy", "lounge");
            Drink("rum_cola", "Rum & Cola", DrinkCat.Spirit, "barrel", 1.30f, 5.80f, 1.2f, "dive");
            Drink("whisky", "Single Malt", DrinkCat.Spirit, "velvet", 2.80f, 7.50f, 0.9f, "speakeasy", "pub");
            Drink("espresso_martini", "Espresso Martini", DrinkCat.Cocktail, "velvet", 2.40f, 10.00f, 3.0f, "speakeasy", "lounge");
            Drink("mojito", "Mojito", DrinkCat.Cocktail, "velvet", 2.10f, 9.00f, 3.2f, "lounge");
            Drink("old_fashioned", "Old Fashioned", DrinkCat.Cocktail, "velvet", 2.60f, 10.50f, 2.6f, "speakeasy", "lounge");
            Drink("zero_lager", "Zero Lager", DrinkCat.AlcoholFree, "barrel", 0.90f, 4.20f, 0.8f);
            Drink("mocktail", "Virgin Colada", DrinkCat.AlcoholFree, "velvet", 1.10f, 6.00f, 2.4f);
            Drink("soft", "Cola & Lemonade", DrinkCat.AlcoholFree, "barrel", 0.40f, 2.80f, 0.5f);
            Drink("coffee", "Coffee", DrinkCat.Hot, "barrel", 0.30f, 3.00f, 1.0f);

            // ---- More drinks (task K3) ------------------------------------------------------------------
            Drink("helles", "Bavarian-Style Helles", DrinkCat.Lager, "hopfield", 1.60f, 5.60f, 1.1f, "craft");
            Drink("value_lager", "Value Lager", DrinkCat.Lager, "cashcarry", 0.75f, 3.60f, 1.0f, "dive", "sports");
            Drink("golden_ale", "Golden Hour Ale", DrinkCat.Ale, "docklands", 1.20f, 4.70f, 1.3f, "pub", "craft");
            Drink("mild", "Dark Mild", DrinkCat.Ale, "barrel", 1.00f, 4.20f, 1.3f, "pub");
            Drink("oyster_stout", "Oyster Stout", DrinkCat.Stout, "hopfield", 1.80f, 6.00f, 1.6f, "craft", "pub");
            Drink("pastry_stout", "Pastry Stout", DrinkCat.Stout, "hopfield", 2.30f, 6.80f, 1.6f, "craft");
            Drink("neipa", "Hazy Pale", DrinkCat.Craft, "hopfield", 2.40f, 7.00f, 1.2f, "craft");
            Drink("dipa", "Double IPA", DrinkCat.Craft, "hopfield", 2.60f, 7.50f, 1.2f, "craft");
            Drink("can_of_week", "Craft Can of the Week", DrinkCat.Craft, "hopfield", 2.00f, 6.50f, 0.6f, "craft", "arcade");
            Drink("farm_cider", "Farmhouse Cider", DrinkCat.Cider, "hopfield", 1.60f, 5.60f, 1.1f, "pub", "craft");
            Drink("fruit_cider", "Fruit Cider", DrinkCat.Cider, "cashcarry", 1.00f, 4.80f, 1.0f, "dive");
            Drink("orange_wine", "Natural Orange Wine", DrinkCat.Wine, "chateau", 3.00f, 8.50f, 0.8f, "lounge", "craft");
            Drink("rose", "Provence-Style Rose", DrinkCat.Wine, "chateau", 2.40f, 7.00f, 0.8f, "lounge");
            Drink("grand_fizz", "Grand Cru Fizz (glass)", DrinkCat.Wine, "chateau", 4.00f, 11.00f, 0.9f, "lounge", "speakeasy");
            Drink("smallbatch_gin", "Small-Batch Gin & Tonic", DrinkCat.Spirit, "chateau", 2.40f, 9.00f, 1.4f, "lounge", "speakeasy");
            Drink("aged_rum", "Aged Rum", DrinkCat.Spirit, "chateau", 3.20f, 8.50f, 0.9f, "lounge", "speakeasy");
            Drink("vodka_mixer", "Vodka & Mixer", DrinkCat.Spirit, "cashcarry", 0.90f, 5.00f, 1.1f, "dive", "sports");
            Drink("spiced_rum", "Spiced Rum & Ginger", DrinkCat.Spirit, "cashcarry", 1.00f, 5.20f, 1.2f, "dive", "arcade");
            Drink("negroni", "Negroni", DrinkCat.Cocktail, "velvet", 2.40f, 10.50f, 2.2f, "speakeasy", "lounge");
            Drink("margarita", "Margarita", DrinkCat.Cocktail, "velvet", 2.20f, 9.50f, 2.8f, "arcade");
            Drink("spritz", "Aperitivo Spritz", DrinkCat.Cocktail, "chateau", 2.00f, 9.00f, 2.0f, "lounge");
            Drink("summer_cup", "Summer Fruit Cup", DrinkCat.Cocktail, "velvet", 1.60f, 7.50f, 2.2f, "pub");
            Drink("af_ipa", "Alcohol-Free IPA", DrinkCat.AlcoholFree, "hopfield", 1.00f, 5.20f, 0.8f, "craft");
            Drink("kombucha", "Kombucha", DrinkCat.AlcoholFree, "hopfield", 1.00f, 4.80f, 0.6f, "craft", "lounge");
            Drink("zero_gin", "Zero-Proof G&T", DrinkCat.AlcoholFree, "velvet", 1.20f, 6.50f, 1.2f, "lounge", "speakeasy");
            Drink("mulled_wine", "Mulled Wine", DrinkCat.Hot, "barrel", 0.90f, 5.00f, 1.2f, "pub");
            Drink("hot_toddy", "Hot Toddy", DrinkCat.Hot, "velvet", 1.20f, 6.50f, 1.8f, "pub", "speakeasy");
            _drinks["mulled_wine"].Boozy = true;
            _drinks["hot_toddy"].Boozy = true;

            // ---- Customer segments ------------------------------------------------------------------
            //                                       Lager Ale  Stout Craft Cider Wine Spirit Cockt AF   Hot
            Segments.Add(new SegmentDef
            {
                Id = Seg.Students, Name = "Student", Plural = "Students", Blurb = "Cheap drinks, loud music, quizzes, games.",
                VibePref = V(80, 25, 62, 35, 60), VibeTolerance = 34, BudgetMin = 12, BudgetMax = 26, PriceSensitivity = 0.95f, Patience = 9,
                DrinkPrefs = new[] { .30f, .03f, .03f, .06f, .18f, .05f, .17f, .09f, .08f, .01f },
                DayWeights = new[] { .9f, .8f, 1.0f, 1.3f, 1.0f, .9f, .5f }, Share = .24f,
                Games = 1.0f, Screens = 0.4f, Stage = 0.5f, Jukebox = 0.7f, Quiz = 0.9f, Comedy = 0.6f, RowdyBase = 1.4f,
                ThemeAffinity = new Dictionary<string, float> { { "dive", .8f }, { "arcade", .9f }, { "sports", .3f }, { "craft", .4f } }, Color = 0x2ec4b6,
            });
            Segments.Add(new SegmentDef
            {
                Id = Seg.YoungPros, Name = "Young Professional", Plural = "Young Professionals", Blurb = "Cocktails, style and after-work deals.",
                VibePref = V(58, 75, 70, 58, 40), VibeTolerance = 28, BudgetMin = 28, BudgetMax = 60, PriceSensitivity = 0.35f, Patience = 7,
                DrinkPrefs = new[] { .08f, .02f, .03f, .12f, .04f, .25f, .12f, .27f, .06f, .01f },
                DayWeights = new[] { .5f, .7f, .9f, 1.3f, 1.5f, 1.0f, .4f }, Share = .22f,
                Games = 0.3f, Screens = 0.2f, Stage = 0.5f, Jukebox = 0.1f, Quiz = 0.4f, Comedy = 0.85f, RowdyBase = 0.6f,
                ThemeAffinity = new Dictionary<string, float> { { "speakeasy", 1.0f }, { "lounge", 1.0f }, { "craft", .6f } }, Color = 0x3a4fa0,
            });
            Segments.Add(new SegmentDef
            {
                Id = Seg.Locals, Name = "Local", Plural = "Locals", Blurb = "Familiarity, real ale, darts and friendly staff.",
                VibePref = V(40, 40, 22, 62, 30), VibeTolerance = 32, BudgetMin = 16, BudgetMax = 34, PriceSensitivity = 0.7f, Patience = 11,
                DrinkPrefs = new[] { .25f, .28f, .14f, .04f, .08f, .08f, .07f, .01f, .04f, .01f },
                DayWeights = new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.1f, 1.1f, 1.2f }, Share = .24f,
                Games = 0.7f, Screens = 0.5f, Stage = 0.2f, Jukebox = 0.6f, Quiz = 0.85f, Comedy = 0.3f, RowdyBase = 0.7f,
                ThemeAffinity = new Dictionary<string, float> { { "pub", 1.0f }, { "craft", .3f } }, Color = 0x7a6a3a,
            });
            Segments.Add(new SegmentDef
            {
                Id = Seg.SportsFans, Name = "Sports Fan", Plural = "Sports Fans", Blurb = "Screens, pints and atmosphere on match days.",
                VibePref = V(78, 35, 50, 30, 25), VibeTolerance = 34, BudgetMin = 18, BudgetMax = 38, PriceSensitivity = 0.6f, Patience = 8,
                DrinkPrefs = new[] { .48f, .12f, .08f, .05f, .10f, .02f, .08f, .01f, .05f, .01f },
                DayWeights = new[] { .6f, .7f, 1.3f, .8f, 1.0f, 1.6f, 1.2f }, Share = .14f,
                Games = 0.6f, Screens = 1.6f, Stage = 0.1f, Jukebox = 0.3f, Quiz = 0.5f, Comedy = 0.3f, RowdyBase = 1.2f,
                ThemeAffinity = new Dictionary<string, float> { { "sports", 1.2f }, { "pub", .4f }, { "craft", .2f } }, Color = 0xd62828,
            });
            Segments.Add(new SegmentDef
            {
                Id = Seg.Creatives, Name = "Creative", Plural = "Creatives", Blurb = "Quirk, art, events and an independent feel.",
                VibePref = V(55, 52, 75, 55, 85), VibeTolerance = 30, BudgetMin = 18, BudgetMax = 42, PriceSensitivity = 0.5f, Patience = 9,
                DrinkPrefs = new[] { .06f, .04f, .04f, .25f, .08f, .15f, .08f, .18f, .11f, .01f },
                DayWeights = new[] { .6f, .7f, .9f, 1.3f, 1.3f, 1.1f, .8f }, Share = .16f,
                Games = 0.6f, Screens = 0.0f, Stage = 1.0f, Jukebox = 0.5f, Quiz = 0.6f, Comedy = 0.9f, RowdyBase = 0.6f,
                ThemeAffinity = new Dictionary<string, float> { { "arcade", .6f }, { "speakeasy", .4f }, { "dive", .4f }, { "craft", .7f }, { "lounge", .3f } }, Color = 0x9b5de5,
            });

            // ---- More customer segments (task K5) ----------------------------------------------------
            // Added on top of the original five (shares above sum to 1): extra trade for bars that suit them.
            Segments.Add(new SegmentDef
            {
                Id = Seg.AfterWork, Name = "After-Work Drinker", Plural = "After-Work Crowd", Blurb = "One quick one after the office. Fast service and somewhere to put a laptop bag.",
                VibePref = V(50, 68, 55, 45, 30), VibeTolerance = 32, BudgetMin = 20, BudgetMax = 40, PriceSensitivity = 0.4f, Patience = 6,
                DrinkPrefs = new[] { .26f, .10f, .03f, .10f, .05f, .22f, .14f, .06f, .03f, .01f },
                DayWeights = new[] { 1.1f, 1.2f, 1.2f, 1.4f, 1.3f, .3f, .2f }, Share = .09f,
                Games = 0.2f, Screens = 0.5f, Stage = 0.2f, Jukebox = 0.2f, Quiz = 0.3f, Comedy = 0.4f, RowdyBase = 0.5f,
                ThemeAffinity = new Dictionary<string, float> { { "lounge", .6f }, { "pub", .4f }, { "sports", .4f }, { "craft", .4f } }, Color = 0x4a6670,
            });
            Segments.Add(new SegmentDef
            {
                Id = Seg.Tourists, Name = "Tourist", Plural = "Tourists", Blurb = "Here for local character and a story to tell. Not fussed about prices.",
                VibePref = V(55, 45, 30, 60, 55), VibeTolerance = 38, BudgetMin = 25, BudgetMax = 55, PriceSensitivity = 0.2f, Patience = 10,
                DrinkPrefs = new[] { .16f, .22f, .14f, .12f, .10f, .07f, .08f, .06f, .04f, .01f },
                DayWeights = new[] { .8f, .8f, .9f, 1.0f, 1.2f, 1.4f, 1.3f }, Share = .06f,
                Games = 0.4f, Screens = 0.3f, Stage = 0.8f, Jukebox = 0.5f, Quiz = 0.3f, Comedy = 0.5f, RowdyBase = 0.6f,
                ThemeAffinity = new Dictionary<string, float> { { "pub", 1.2f }, { "speakeasy", .5f }, { "craft", .5f } }, Color = 0xf4a259,
            });
            Segments.Add(new SegmentDef
            {
                Id = Seg.Couples, Name = "Couple", Plural = "Couples", Blurb = "Date night: somewhere intimate, a good bottle and a quiet corner.",
                VibePref = V(35, 70, 45, 80, 45), VibeTolerance = 28, BudgetMin = 30, BudgetMax = 65, PriceSensitivity = 0.35f, Patience = 9,
                DrinkPrefs = new[] { .05f, .03f, .02f, .06f, .05f, .38f, .10f, .25f, .05f, .01f },
                DayWeights = new[] { .5f, .6f, .8f, 1.1f, 1.4f, 1.5f, 1.0f }, Share = .07f,
                Games = 0.2f, Screens = 0.0f, Stage = 0.5f, Jukebox = 0.3f, Quiz = 0.2f, Comedy = 0.7f, RowdyBase = 0.3f,
                ThemeAffinity = new Dictionary<string, float> { { "lounge", 1.0f }, { "speakeasy", .8f }, { "pub", .2f } }, Color = 0xe5989b,
            });

            // ---- Staff traits -------------------------------------------------------------------------
            Traits.Add(new TraitDef { Id = "life_soul", Name = "Life and Soul", Blurb = "Customers nearby cheer up." });
            Traits.Add(new TraitDef { Id = "calm", Name = "Calm Under Pressure", Blurb = "Doesn't slow down when it's rammed." });
            Traits.Add(new TraitDef { Id = "cocktail_nerd", Name = "Cocktail Nerd", Blurb = "Fast, excellent cocktails." });
            Traits.Add(new TraitDef { Id = "always_late", Name = "Always Late", Blurb = "Sometimes turns up 45 minutes into the shift." });
            Traits.Add(new TraitDef { Id = "grumpy", Name = "Grumpy but Brilliant", Blurb = "Very quick, not very nice." });
            Traits.Add(new TraitDef { Id = "butterfingers", Name = "Butterfingers", Blurb = "Spills and broken glasses follow them around." });
            Traits.Add(new TraitDef { Id = "secret_comic", Name = "Secretly a Comedian", Blurb = "Event nights go better when they're on shift." });
            Traits.Add(new TraitDef { Id = "gossip", Name = "Gossip", Blurb = "Chats a lot. Slower, but regulars love them." });
        }

        // ---- Name generation -----------------------------------------------------------------------
        public static readonly string[] FirstNames =
        {
            "Aoife", "Ben", "Chloe", "Dev", "Erin", "Femi", "Grace", "Hamish", "Isla", "Jamal", "Kezia", "Liam", "Maya", "Niall",
            "Orla", "Priya", "Quinn", "Rosa", "Sam", "Tariq", "Una", "Vik", "Wren", "Xander", "Yusuf", "Zara", "Ciaran", "Leah",
            "Marcus", "Nell", "Owen", "Poppy", "Rhys", "Saoirse", "Tom", "Amara", "Bex", "Callum", "Darcy", "Eoin", "Fran", "Gus",
        };
        public static readonly string[] LastNames =
        {
            "Adebayo", "Brennan", "Chen", "Doherty", "Evans", "Fairweather", "Gallagher", "Hughes", "Iqbal", "Jones", "Kowalski",
            "Lennox", "McAllister", "Nakamura", "O'Neill", "Patel", "Quigley", "Robinson", "Singh", "Thompson", "Underwood",
            "Valdez", "Whyte", "Young", "Zielinski", "Murphy", "Okafor", "Byrne", "Campbell", "Ferreira",
        };
        public static readonly string[] Nicknames = { "Big", "Wee", "Old", "Young", "Lucky", "Quiet", "Loud" };
        public static readonly string[] QuizTeams =
        {
            "The Quizzly Bears", "Les Quizerables", "Quiztopher Columbus", "Agatha Quiztie", "The Usual Suspects",
            "Universally Challenged", "Tequila Mockingbird", "The Fact Hunt", "Norfolk 'n' Chance", "E=MC Hammered",
        };
        public static readonly string[] ComedyStyles = { "observational", "surreal", "storytelling", "one-liners", "musical", "deadpan", "political", "character" };

        public static string RandomName(Rng r) => $"{r.Pick(FirstNames)} {r.Pick(LastNames)}";
    }
}
