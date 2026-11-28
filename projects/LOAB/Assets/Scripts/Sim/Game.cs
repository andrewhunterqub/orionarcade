using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public enum Phase { Prep, Service, Report, GameOver }

    /// <summary>A build-mode command that can be undone (brief 14.2: every action is a command).</summary>
    public interface IBuildCommand
    {
        string Name { get; }
        string Validate(Game g);
        void Apply(Game g);
    }

    public sealed class PlaceCmd : IBuildCommand
    {
        public string Id; public int X, Y, Rot;
        public string Name => "Place " + Content.Item(Id)?.Name;
        public string Validate(Game g)
        {
            var d = Content.Item(Id);
            if (d == null) return "Unknown item";
            if (!g.IsUnlocked(d)) return $"Unlocks at reputation {d.UnlockRep}";
            if (g.S.cash < d.Cost && g.S.difficulty > 0) return "Not enough cash";
            return g.L.CanPlace(Id, X, Y, Rot);
        }
        public void Apply(Game g)
        {
            var d = Content.Item(Id);
            var it = new PlacedItem { uid = g.S.nextUid++, id = Id, x = X, y = Y, rot = Rot };
            g.S.items.Add(it);
            g.S.cash -= d.Cost;
            g.PlacedToday.Add(it.uid);
            g.SpentToday += d.Cost;
        }
    }

    public sealed class MoveCmd : IBuildCommand
    {
        public int Uid, X, Y, Rot;
        public string Name => "Move";
        public string Validate(Game g)
        {
            var it = g.S.Item(Uid);
            return it == null ? "Gone" : g.L.CanPlace(it.id, X, Y, Rot, Uid);
        }
        public void Apply(Game g) { var it = g.S.Item(Uid); it.x = X; it.y = Y; it.rot = Rot; }
    }

    public sealed class SellCmd : IBuildCommand
    {
        public int Uid;
        public string Name => "Sell";
        public string Validate(Game g)
        {
            var it = g.S.Item(Uid);
            if (it == null) return "Gone";
            var d = Content.Item(it.id);
            if (d.Fn == ItemFn.BarCounter && g.S.items.FindAll(i => Content.Item(i.id).Fn == ItemFn.BarCounter).Count <= 1)
                return "You need at least one bar counter";
            return null;
        }
        public void Apply(Game g)
        {
            var it = g.S.Item(Uid);
            g.S.cash += g.SellValue(it);
            g.S.items.Remove(it);
        }
    }

    public sealed class StyleCmd : IBuildCommand
    {
        public int Kind; public string Id; // 0 floor, 1 wall
        public string Name => Kind == 0 ? "Floor" : "Walls";
        public string Validate(Game g)
        {
            var st = Kind == 0 ? Content.Floor(Id) : Content.Wall(Id);
            if ((Kind == 0 ? g.S.floor : g.S.wall) == Id) return "Already chosen";
            if (g.S.cash < st.Cost && g.S.difficulty > 0) return "Not enough cash";
            return null;
        }
        public void Apply(Game g)
        {
            var st = Kind == 0 ? Content.Floor(Id) : Content.Wall(Id);
            g.S.cash -= st.Cost; g.SpentToday += st.Cost;
            if (Kind == 0) g.S.floor = Id; else g.S.wall = Id;
        }
    }

    public sealed class Game
    {
        public GameState S;
        public Layout L;
        public VibeReport V;
        public Service Svc;
        public Phase Phase;
        public NightRecord LastNight;
        public readonly List<string> GoalsDoneLastNight = new List<string>();
        public readonly HashSet<int> PlacedToday = new HashSet<int>();
        public float SpentToday;
        public string LastError;
        public event Action Changed;

        // Undo/redo of build commands: snapshot-based, within one Prep phase.
        sealed class Snapshot { public List<PlacedItem> items; public string floor, wall; public int nextUid; public float cashDelta; }
        readonly Stack<Snapshot> _undo = new Stack<Snapshot>();
        readonly Stack<Snapshot> _redo = new Stack<Snapshot>();
        Rng _r;

        public Game(GameState s)
        {
            S = s;
            SaveMigration.Migrate(s);
            if (string.IsNullOrEmpty(s.pendingCard)) s.pendingCard = null; // JsonUtility saves null strings as ""
            if (string.IsNullOrEmpty(s.scenario)) s.scenario = null;
            s.agency ??= new List<Act>();
            if (!s.agency.Exists(a => a.kind == 2)) Economy.AddOpenMicActs(s, new Rng(s.seed + 99)); // saves from before Open Mic
            if (!s.agency.Exists(a => a.kind == EventKinds.LiveMusic)) Economy.AddNewEventActs(s, new Rng(s.seed + 98)); // saves from before K6
            s.menu ??= new List<MenuEntry>(); s.staff ??= new List<StaffMember>(); s.events ??= new List<BookedEvent>();
            if (s.stats == null || (s.stats.nights == 0 && s.history != null && s.history.Count > 0))
            {
                // Saves from before career stats: start from what history remembers.
                s.stats = new CareerStats();
                if (s.history != null) foreach (var h in s.history) CountNight(s.stats, h);
            }
            L = new Layout(s);
            // LastNight isn't saved separately: restore it from history so a loaded game behaves like one that never left.
            LastNight = s.history != null && s.history.Count > 0 ? s.history[s.history.Count - 1] : null;
            _r = new Rng(s.seed + (ulong)s.day * 31UL + 7UL);
            Recompute();
            Phase = s.bankrupt ? Phase.GameOver : Phase.Prep;
        }

        // =====================================================================================
        //  New games
        // =====================================================================================

        public static Game NewCareer(ulong seed, int difficulty)
        {
            var s = new GameState { seed = seed, difficulty = difficulty, mode = 0, cash = difficulty == 0 ? 8000 : difficulty == 2 ? 4500 : 6000, reputation = 12, rating = 3f };
            s.awareness = new[] { 0.2f, 0.16f, 0.34f, 0.18f, 0.16f, 0.18f, 0.1f, 0.14f };
            var r = new Rng(seed);
            Item(s, "bar_counter", 6, 8, 0);
            Item(s, "toilets", 14, 9, 0);
            Item(s, "pub_table", 2, 4, 0);
            Item(s, "pub_table", 11, 4, 0);
            Item(s, "plastic_set", 4, 2, 0);
            Item(s, "plastic_set", 10, 2, 0);
            Item(s, "bar_stool", 2, 9, 0);
            Item(s, "bar_stool", 3, 9, 0);
            Item(s, "bar_stool", 11, 9, 0);
            Item(s, "chalkboard", 0, 10, 0);
            Item(s, "plant", 15, 0, 0);
            Item(s, "darts", 0, 6, 0);
            SetupMenu(s, new[] { "lager", "bitter", "stout", "cider", "house_red", "house_white", "gin_tonic", "rum_cola", "zero_lager", "soft" });
            var bartender = Economy.MakeApplicant(s, r, 0, 10);
            bartender.name = "Sam Okafor";
            bartender.traits.Clear(); bartender.traits.Add("calm");
            bartender.wage = Economy.FairWage(bartender);
            s.staff.Add(bartender);
            Economy.RefreshApplicants(s, r);
            Economy.RefreshAgency(s, r);
            s.weather = "Mild";
            return new Game(s);
        }

        public static Game NewScenario(string id, ulong seed)
        {
            var def = Scenarios.Get(id);
            var g = NewCareer(seed, 1);
            g.S.mode = 2;
            g.S.scenario = id;
            g.S.day = def?.StartDay ?? 0;
            g.S.SetFlag("start_cash", 0);
            def?.Setup(g.S);
            g.S.SetFlag("start_cash", (int)g.S.cash);
            g.Recompute();
            return g;
        }

        public ScenarioDef Scenario => S.mode == 2 ? Scenarios.Get(S.scenario) : null;
        public int ScenarioDaysLeft => Scenario == null ? 0 : Math.Max(0, Scenario.Days - (S.day - Scenario.StartDay));
        /// Days played in the current scenario (scenarios can start mid-year, task P3).
        public int ScenarioDay => Scenario == null ? S.day : S.day - Scenario.StartDay;
        public bool ScenarioJustEnded;

        public static Game NewSandbox(ulong seed)
        {
            var g = NewCareer(seed, 0);
            g.S.mode = 1;
            g.S.cash = 100000;
            g.S.reputation = 50;
            g.S.barName = "The Sandbox";
            foreach (var m in g.S.menu) { m.stock = Math.Max(m.stock, 88); }
            g.Recompute();
            return g;
        }

        static void Item(GameState s, string id, int x, int y, int rot) => s.items.Add(new PlacedItem { uid = s.nextUid++, id = id, x = x, y = y, rot = rot, wear = 40 });

        static void SetupMenu(GameState s, string[] onMenu)
        {
            foreach (var d in Content.Drinks)
            {
                bool on = Array.IndexOf(onMenu, d.Id) >= 0;
                s.menu.Add(new MenuEntry { id = d.Id, onMenu = on, price = d.DefaultPrice, stock = on ? d.CaseSize : 0 });
            }
        }

        // =====================================================================================
        //  Helpers
        // =====================================================================================

        public void Recompute()
        {
            L.Rebuild();
            V = VibeCalc.Compute(S);
            Changed?.Invoke();
        }

        /// The next reputation thresholds and what they unlock (task P4): up to `groups` thresholds above the current reputation.
        public List<(int rep, List<ItemDef> items)> UpcomingUnlocks(int groups = 3)
        {
            var result = new List<(int, List<ItemDef>)>();
            if (S.mode == 1) return result; // sandbox has everything
            var pending = Content.Items.FindAll(d => !d.Hidden && d.UnlockRep > S.reputation && !S.unlockedItems.Contains(d.Id));
            pending.Sort((a, b) => a.UnlockRep.CompareTo(b.UnlockRep));
            foreach (var d in pending)
            {
                if (result.Count > 0 && result[result.Count - 1].Item1 == d.UnlockRep) { result[result.Count - 1].Item2.Add(d); continue; }
                if (result.Count >= groups) break;
                result.Add((d.UnlockRep, new List<ItemDef> { d }));
            }
            return result;
        }

        public bool IsUnlocked(ItemDef d)
        {
            if (S.mode == 1) return !d.Hidden || S.unlockedItems.Contains(d.Id);
            if (d.Hidden) return S.unlockedItems.Contains(d.Id);
            return S.reputation >= d.UnlockRep || S.unlockedItems.Contains(d.Id);
        }

        public float SellValue(PlacedItem it)
        {
            var d = Content.Item(it.id);
            return PlacedToday.Contains(it.uid) ? d.Cost : (float)Math.Round(d.Cost * 0.5f * (1f - it.wear / 200f));
        }

        public void Spend(float amount, string what)
        {
            S.cash -= amount;
            SpentToday += amount;
        }

        public StaffMember CardStaff() => S.Staff(S.GetFlag("card_staff")) ?? (S.staff.Count > 0 ? S.staff[0] : null);

        public void TryAutoPlace(string id)
        {
            var d = Content.Item(id);
            for (int y = S.gridH - 1; y >= 0; y--)
                for (int x = 0; x < S.gridW; x++)
                    if (L.CanPlace(id, x, y, 0) == null)
                    {
                        S.items.Add(new PlacedItem { uid = S.nextUid++, id = id, x = x, y = y, rot = 0 });
                        Recompute();
                        return;
                    }
        }

        string Fail(string err) { LastError = err; return err; }

        // =====================================================================================
        //  Build commands
        // =====================================================================================

        public string Execute(IBuildCommand cmd)
        {
            if (Phase != Phase.Prep) return Fail("You can only build during Prep");
            var err = cmd.Validate(this);
            if (err != null) return Fail(err);
            var snap = Snap();
            float before = S.cash;
            _redo.Clear();
            cmd.Apply(this);
            snap.cashDelta = S.cash - before; // only this command's money moves on undo/redo
            _undo.Push(snap);
            Recompute();
            return null;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public void Undo()
        {
            if (_undo.Count == 0) return;
            var snap = _undo.Pop();
            var cur = Snap(); cur.cashDelta = snap.cashDelta;
            _redo.Push(cur);
            Restore(snap);
            S.cash -= snap.cashDelta; SpentToday += snap.cashDelta;
            Recompute();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            var snap = _redo.Pop();
            var cur = Snap(); cur.cashDelta = snap.cashDelta;
            _undo.Push(cur);
            Restore(snap);
            S.cash += snap.cashDelta; SpentToday -= snap.cashDelta;
            Recompute();
        }

        Snapshot Snap()
        {
            var list = new List<PlacedItem>();
            foreach (var i in S.items) list.Add(new PlacedItem { uid = i.uid, id = i.id, x = i.x, y = i.y, rot = i.rot, wear = i.wear });
            return new Snapshot { items = list, floor = S.floor, wall = S.wall, nextUid = S.nextUid };
        }

        void Restore(Snapshot s)
        {
            S.items = s.items; S.floor = s.floor; S.wall = s.wall; S.nextUid = s.nextUid;
        }

        // =====================================================================================
        //  Prep actions
        // =====================================================================================

        public void SetMusic(string id) { S.music = id; Recompute(); }
        public void SetLighting(int v) { S.lighting = Math.Max(0, Math.Min(100, v)); Recompute(); }
        public void SetVolume(int v) { S.volume = Math.Max(0, Math.Min(100, v)); Recompute(); }
        public void SetResponsible(bool on) { S.responsibleService = on; Changed?.Invoke(); }
        public void SetHappyHour(int hour) { S.happyHourStart = hour; Changed?.Invoke(); }

        public void SetPrice(string id, float price)
        {
            var m = S.Menu(id);
            if (m == null) return;
            m.price = (float)Math.Round(Math.Max(0.5f, Math.Min(30f, price)) * 10f) / 10f;
            Recompute();
        }

        public string ToggleMenu(string id)
        {
            var m = S.Menu(id);
            if (m == null) return Fail("Unknown drink");
            m.onMenu = !m.onMenu;
            Recompute();
            return null;
        }

        public float CaseCost(string id) { var d = Content.Drink(id); return d.CaseSize * d.UnitCost * Economy.CostMult(S); }

        public string OrderStock(string id, int cases)
        {
            var d = Content.Drink(id);
            var m = S.Menu(id);
            float cost = CaseCost(id) * cases;
            if (S.cash < cost && S.difficulty > 0) return Fail("Not enough cash");
            S.cash -= cost; SpentToday += cost;
            m.stock += d.CaseSize * cases;
            if (!m.onMenu) m.onMenu = true;
            Recompute();
            return null;
        }

        /// <summary>Suggested stock for tonight: last night's sales of this drink plus headroom.</summary>
        public int ParLevel(string id)
        {
            var d = Content.Drink(id);
            float expected = Math.Max(30, Economy.ExpectedTotal(S, V));
            // Expected share of sales by category across segments
            float share = 0, tot = 0;
            var catCount = new int[10];
            foreach (var m in S.menu) if (m.onMenu) catCount[(int)Content.Drink(m.id).Cat]++;
            for (int i = 0; i < Content.Segments.Count; i++)
            {
                var seg = Content.Segments[i];
                share += seg.DrinkPrefs[(int)d.Cat] / Math.Max(1, catCount[(int)d.Cat]) * seg.Share;
                tot += seg.Share;
            }
            share /= tot;
            int model = (int)Math.Ceiling(expected * 2.6f * share * 1.4f);
            // Once there's history, trust what actually sold (plus lost sales), with headroom for busy nights.
            var entry = S.Menu(id);
            float busy = S.DayOfWeek >= 4 ? 1.3f : 1f;
            int learned = entry != null && entry.demandAvg > 0 ? (int)Math.Ceiling(entry.demandAvg * 1.35f * busy) : 0;
            return Math.Max(model, learned);
        }

        public float RestockCost()
        {
            float cost = 0;
            foreach (var m in S.menu)
            {
                if (!m.onMenu) continue;
                int need = ParLevel(m.id) - m.stock;
                if (need <= 0) continue;
                var d = Content.Drink(m.id);
                cost += (int)Math.Ceiling(need / (float)d.CaseSize) * CaseCost(m.id);
            }
            return cost;
        }

        public string RestockAll()
        {
            float cost = RestockCost();
            if (cost <= 0) return Fail("Stock is already healthy");
            if (S.cash < cost && S.difficulty > 0) return Fail($"Restocking costs £{cost:0}. Not enough cash");
            foreach (var m in S.menu)
            {
                if (!m.onMenu) continue;
                int need = ParLevel(m.id) - m.stock;
                if (need <= 0) continue;
                var d = Content.Drink(m.id);
                int cases = (int)Math.Ceiling(need / (float)d.CaseSize);
                S.cash -= cases * CaseCost(m.id); SpentToday += cases * CaseCost(m.id);
                m.stock += cases * d.CaseSize;
            }
            Recompute();
            return null;
        }

        public float RepairCost(PlacedItem it) => (float)Math.Round(Content.Item(it.id).Cost * it.wear / 100f * 0.35f);

        public string Repair(int uid)
        {
            var it = S.Item(uid);
            if (it == null) return Fail("Gone");
            if (it.wear < 5) return Fail("It's in good nick already");
            float cost = RepairCost(it);
            if (S.cash < cost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(cost, "Repairs");
            it.wear = 0;
            Recompute();
            return null;
        }

        public const int TrainingCost = 150;

        public string Train(int uid)
        {
            var m = S.Staff(uid);
            if (m == null) return Fail("Not found");
            if (S.GetFlag("trained_" + uid) >= S.Week) return Fail("One course per person per week");
            if (S.cash < TrainingCost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(TrainingCost, "Training");
            // Improve the weakest relevant attribute.
            if (m.role == 0 && m.skill <= m.speed && m.skill <= m.charm) m.skill = Math.Min(20, m.skill + 2);
            else if (m.speed <= m.charm) m.speed = Math.Min(20, m.speed + 2);
            else m.charm = Math.Min(20, m.charm + 2);
            m.morale = Math.Min(100, m.morale + 8);
            S.SetFlag("trained_" + uid, S.Week);
            Changed?.Invoke();
            return null;
        }

        // ---- Second venue (task P5) ----------------------------------------------------------------------
        // Late career: hand the first bar to a manager (who sends back a share of its profit each week) and
        // open a new bar in another district, starting again from a bare unit with your cash, recipes and know-how.
        public const int SecondVenueCost = 30000, SecondVenueRep = 85;
        public const float ManagerShare = 0.3f, ManagedIncomeCap = 6000f;

        public bool SecondVenueUnlocked => S.mode == 0 && S.venues <= 1 && (S.GetFlag("bar_of_year") > 0 || S.reputation >= SecondVenueRep);

        /// What the first bar would send back each week under a manager: 30% of its average weekly profit over the last 4 weeks.
        public float ManagedIncomeEstimate()
        {
            float total = 0; int n = 0;
            for (int i = S.history.Count - 1; i >= 0 && n < 28; i--, n++) total += S.history[i].profit;
            if (n == 0) return 0;
            float weekly = total / n * 7f;
            return Math.Max(0f, Math.Min(ManagedIncomeCap, (float)Math.Round(weekly * ManagerShare / 10f) * 10f));
        }

        /// Opens the second bar. Returns (error, newGame): the caller swaps the running game for newGame.
        public (string error, Game next) OpenSecondVenue(string districtId, string name)
        {
            var dist = Content.District(districtId);
            if (dist == null) return ("Pick a district", null);
            if (!SecondVenueUnlocked) return ($"Win Bar of the Year or reach reputation {SecondVenueRep} first", null);
            if (Phase != Phase.Prep) return ("Wait until the bar is closed", null);
            if (S.cash < SecondVenueCost) return ($"A second bar needs £{SecondVenueCost:#,0} for the fit-out", null);
            var fresh = NewCareer(S.seed * 31 + 7, S.difficulty).S;
            fresh.mode = S.mode;
            fresh.district = dist.Id;
            fresh.floor = dist.Floor; fresh.wall = dist.Wall;
            fresh.venues = 2;
            fresh.firstBarName = S.barName;
            fresh.barName = string.IsNullOrWhiteSpace(name) ? $"The Anchor, {dist.Name}" : name.Trim();
            fresh.managedIncome = ManagedIncomeEstimate();
            // What comes with you: the calendar, the money, your recipes, what you've unlocked and learned.
            fresh.day = S.day;
            fresh.weather = S.weather;
            fresh.cash = S.cash - SecondVenueCost;
            fresh.loanBalance = S.loanBalance; fresh.loanDaily = S.loanDaily; fresh.loansTaken = S.loansTaken;
            fresh.goalIndex = S.goalIndex;
            fresh.flags = new List<Flag>(S.flags);
            fresh.seenTips = new List<string>(S.seenTips);
            fresh.unlockedItems = new List<string>(S.unlockedItems);
            fresh.cardHistory = new List<string>(S.cardHistory);
            fresh.signatures = new List<string>(S.signatures);
            fresh.stats = S.stats; // a career is a career, across both bars
            foreach (var sig in fresh.signatures)
            {
                var dd = Content.Drink(sig);
                var e = fresh.Menu(sig);
                if (dd == null) continue;
                if (e == null) { e = new MenuEntry { id = sig }; fresh.menu.Add(e); }
                e.onMenu = true; e.price = dd.DefaultPrice; e.stock = dd.CaseSize;
            }
            // A known name travels, but a new room has to earn its own reputation.
            fresh.reputation = Math.Max(12f, S.reputation * 0.4f);
            fresh.SetFlag("second_venue_day", S.day);
            var g = new Game(fresh);
            g.Recompute();
            return (null, g);
        }

        public const int ExpandCost = 15000, ExpandRep = 35, ExpandCols = 6, MaxWidth = 22;
        public bool CanExpand => S.gridW + ExpandCols <= MaxWidth;

        /// <summary>Knock through into the unit next door (brief 5.4): the bar grows by a few columns.</summary>
        public string Expand()
        {
            if (Phase != Phase.Prep) return Fail("Only during Prep");
            if (!CanExpand) return Fail("There's nowhere left to expand into");
            if (S.reputation < ExpandRep && S.mode != 1) return Fail($"The landlord wants to see reputation {ExpandRep} first");
            if (S.cash < ExpandCost && S.difficulty > 0) return Fail($"Expanding costs £{ExpandCost:#,0}");
            Spend(ExpandCost, "Expansion");
            S.gridW += ExpandCols;
            L = new Layout(S);
            _undo.Clear(); _redo.Clear();
            Recompute();
            return null;
        }

        /// <summary>Book the residency act on its next weekday, if that night is free.</summary>
        public void BookResidency()
        {
            if (S.residencyDay < 0) return;
            for (int off = 0; off < 7; off++)
            {
                int day = S.day + off;
                if (day % 7 != S.residencyDay) continue;
                if (S.events.Exists(e => e.day == day)) return;
                var a = S.residencyAct;
                Spend(a.fee, "Residency");
                S.events.Add(new BookedEvent { uid = S.nextUid++, day = day, kind = a.kind, act = a, promo = 0, ticket = EventKinds.DefaultTicket(a.kind) });
                return;
            }
        }

        public void EndResidency() { S.residencyDay = -1; S.events.RemoveAll(e => e.act != null && e.act.name == S.residencyAct.name && e.day >= S.day); Changed?.Invoke(); }

        // ---- Cocktail Lab (task K4) ---------------------------------------------------------------------
        public const int SignatureCost = 250, MaxSignatures = 3;

        /// Invent a signature cocktail from a base, a modifier and a style. Goes straight on the menu with a first batch.
        public string CreateSignature(int b, int m, int st)
        {
            if (b < 0 || b >= Content.LabBases.Length || m < 0 || m >= Content.LabMods.Length || st < 0 || st >= Content.LabStyles.Length) return Fail("Pick a base, a twist and a style");
            string id = Content.SignatureId(b, m, st);
            if (S.signatures.Contains(id)) return Fail("You already serve that one");
            if (S.signatures.Count >= MaxSignatures) return Fail($"Three signatures is plenty. Retire one first");
            if (S.cash < SignatureCost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(SignatureCost, "Cocktail Lab");
            S.signatures.Add(id);
            var e = S.Menu(id);
            e.onMenu = true;
            e.price = Content.Drink(id).DefaultPrice;
            e.stock = Math.Max(e.stock, Content.Drink(id).CaseSize);
            Recompute();
            return null;
        }

        public string RetireSignature(string id)
        {
            if (!S.signatures.Remove(id)) return Fail("Not one of yours");
            var e = S.Menu(id);
            if (e != null) e.onMenu = false;
            Recompute();
            return null;
        }

        // ---- Beer garden (task O1) ----
        public string BuyGarden()
        {
            if (S.garden) return Fail("You've already got a beer garden");
            if (S.reputation < Economy.GardenRep && S.mode != 1) return Fail($"Needs reputation {Economy.GardenRep}");
            if (S.cash < Economy.GardenCost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(Economy.GardenCost, "Beer garden");
            S.garden = true;
            Recompute();
            return null;
        }

        public string BuyGardenHeaters()
        {
            if (!S.garden) return Fail("Build the beer garden first");
            if (S.gardenHeaters) return Fail("Already toasty");
            if (S.cash < Economy.GardenHeaterCost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(Economy.GardenHeaterCost, "Patio heaters");
            S.gardenHeaters = true;
            Changed?.Invoke();
            return null;
        }

        public string CleanLines()
        {
            if (S.lineQuality >= 95) return Fail("Lines are already clean");
            if (S.cash < 40 && S.difficulty > 0) return Fail("Not enough cash");
            Spend(40, "Line clean");
            S.lineQuality = 100;
            Changed?.Invoke();
            return null;
        }

        /// A contract clean: priced by floor area so it stays a real decision as the bar grows (£256 for the starting unit).
        public int DeepCleanCost => 80 + S.gridW * S.gridH;

        public string DeepClean()
        {
            if (S.cleanliness >= 98) return Fail("It's already spotless");
            if (S.cash < DeepCleanCost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(DeepCleanCost, "Deep clean");
            S.cleanliness = 100;
            Changed?.Invoke();
            return null;
        }

        public string Hire(int uid)
        {
            var a = S.applicants.Find(x => x.uid == uid);
            if (a == null) return Fail("They've taken another job");
            if (S.staff.Count >= 10) return Fail("That's enough staff for one bar");
            S.applicants.Remove(a);
            S.staff.Add(a);
            Changed?.Invoke();
            return null;
        }

        public string Fire(int uid)
        {
            var m = S.Staff(uid);
            if (m == null) return Fail("Not found");
            S.staff.Remove(m);
            foreach (var o in S.staff) o.morale = Math.Max(0, o.morale - 5);
            Changed?.Invoke();
            return null;
        }

        /// Turn a night on or off on someone's rota (task S3).
        public string ToggleDayOff(int uid, int dayOfWeek)
        {
            var m = S.Staff(uid);
            if (m == null || dayOfWeek < 0 || dayOfWeek > 6) return Fail("Who?");
            m.daysOff ^= 1 << dayOfWeek;
            if ((m.daysOff & 0x7f) == 0x7f) { m.daysOff ^= 1 << dayOfWeek; return Fail("Everyone works at least one night"); }
            Changed?.Invoke();
            return null;
        }

        public string SetWage(int uid, int wage)
        {
            var m = S.Staff(uid);
            if (m == null) return Fail("Not found");
            m.wage = Math.Max(30, Math.Min(300, wage));
            Changed?.Invoke();
            return null;
        }

        public string BookEvent(int actUid, int dayOffset, int promo, int ticket)
        {
            var act = S.agency.Find(a => a.uid == actUid);
            if (act == null) return Fail("Act not available");
            int day = S.day + dayOffset;
            if (S.events.Exists(e => e.day == day)) return Fail("You've already got an event that night");
            var blocker = EventKinds.Requirement(act.kind, V);
            if (blocker != null) return Fail($"{Economy.EventName(act.kind)} {blocker}");
            float cost = act.fee + promo;
            if (S.cash < cost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(cost, "Event booking");
            S.events.Add(new BookedEvent { uid = S.nextUid++, day = day, kind = act.kind, act = act, promo = promo, ticket = ticket });
            if (act.kind == EventKinds.Comedy || act.kind == EventKinds.LiveMusic) S.agency.Remove(act); // comedians and bands are booked out
            Changed?.Invoke();
            return null;
        }

        public string CancelEvent(int uid)
        {
            var e = S.events.Find(x => x.uid == uid);
            if (e == null) return Fail("Not found");
            S.events.Remove(e);
            S.cash += e.act.fee * 0.5f; // half refund
            Changed?.Invoke();
            return null;
        }

        public string SocialPost()
        {
            if (S.postedToday) return Fail("You've already posted today");
            S.postedToday = true;
            // Better-looking, more coherent bars make better posts.
            float quality = V.Coherence / 100f * 0.6f + (V.ActiveTheme != null ? 0.3f : 0f) + 0.1f;
            S.socialPostBoost = Math.Min(0.25f, S.socialPostBoost + 0.04f + quality * 0.1f);
            S.awareness[(int)Seg.Students] += 0.004f * quality;
            S.awareness[(int)Seg.YoungPros] += 0.006f * quality;
            S.awareness[(int)Seg.Creatives] += 0.006f * quality;
            Changed?.Invoke();
            return null;
        }

        public string Flyers()
        {
            if (S.cash < 60 && S.difficulty > 0) return Fail("Not enough cash");
            Spend(60, "Flyers");
            S.awareness[(int)Seg.Students] += 0.03f;
            S.awareness[(int)Seg.Locals] += 0.025f;
            S.awareness[(int)Seg.SportsFans] += 0.015f;
            Changed?.Invoke();
            return null;
        }

        public string PaidAd()
        {
            if (S.cash < 180 && S.difficulty > 0) return Fail("Not enough cash");
            Spend(180, "Social ad");
            S.awareness[(int)Seg.YoungPros] += 0.04f;
            S.awareness[(int)Seg.Creatives] += 0.035f;
            S.awareness[(int)Seg.SportsFans] += 0.02f;
            Changed?.Invoke();
            return null;
        }

        public string ChooseCard(int option)
        {
            var card = Director.Get(S.pendingCard);
            if (card == null) { S.pendingCard = null; return null; }
            if (option < 0 || option >= card.Options.Count) return Fail("Pick an option");
            var o = card.Options[option];
            var err = o.CanChoose?.Invoke(this);
            if (err != null) return Fail(err);
            o.Apply(this);
            S.cardHistory.Add(card.Id);
            S.pendingCard = null;
            Recompute();
            return null;
        }

        public string TakeEmergencyLoan()
        {
            S.loansTaken++;
            S.cash += 3000;
            S.loanBalance += 3600;
            S.loanDaily = 90;
            S.pendingCard = null;
            Changed?.Invoke();
            return null;
        }

        public void Rename(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length < 2 || name.Length > 28) return;
            S.barName = name;
            Changed?.Invoke();
        }

        // =====================================================================================
        //  Day flow
        // =====================================================================================

        public string OpenDoors()
        {
            if (Phase != Phase.Prep) return Fail("Not now");
            if (S.pendingCard != null) return Fail("Deal with today's decision first");
            _undo.Clear(); _redo.Clear();
            if (S.closedTonight)
            {
                Svc = null;
                FinishNight(null);
                return null;
            }
            Svc = new Service(S, L, V);
            Phase = Phase.Service;
            Changed?.Invoke();
            return null;
        }

        public void EndServiceIfDone()
        {
            if (Phase == Phase.Service && Svc != null && Svc.Finished) FinishNight(Svc);
        }

        void FinishNight(Service svc)
        {
            // End-of-night rolls (level-ups, resignations) get their own seed, so a game loaded that morning
            // makes the same rolls as one that never left: the morning's weather and card draws don't shift them.
            _r = new Rng(S.seed + (ulong)S.day * 31UL + 19UL);
            var rec = new NightRecord { day = S.day, weather = S.weather };
            float wages = 0, fines = 0;
            foreach (var m in S.staff) if (!m.OffOn(S.DayOfWeek)) wages += m.wage; // only paid for nights on the rota
            // Rest and fatigue (task S3)
            foreach (var m in S.staff)
            {
                if (m.OffOn(S.DayOfWeek)) { m.streak = 0; m.morale = Math.Min(100, m.morale + 4); continue; }
                m.streak++;
                if (m.streak > 5 && !m.HasTrait("workhorse")) m.morale = Math.Max(0, m.morale - (m.streak - 5) * 2.5f);
                if (m.streak == 7) rec.moments.Add($"{m.name} has worked seven nights straight and is flagging. Give them a night off on the Staff tab.");
            }
            if (svc != null)
            {
                rec.customers = svc.Arrivals;
                rec.turnedAway = svc.TurnedAway;
                rec.servedDrinks = svc.Served;
                rec.walkouts = svc.Walkouts;
                rec.takings = svc.Takings + svc.TicketIncome;
                rec.cogs = svc.Cogs;
                rec.spills = svc.Spills;
                rec.rowdy = svc.RowdyCount;
                for (int i = 0; i < Content.Segments.Count; i++) rec.segCounts.Add(svc.SegCounts[i]);
                float satSum = 0; int satN = 0;
                foreach (var c in svc.Departed) { satSum += c.Satisfaction; satN++; }
                rec.satisfaction = satN > 0 ? satSum / satN : 50;
                rec.moments.AddRange(svc.Moments);
                rec.stockOuts.AddRange(svc.StockOuts);
                rec.reviews.AddRange(svc.NewReviews);
                // Top thoughts
                var order = new List<int>();
                for (int n = 0; n < (int)Note.Count; n++) if (svc.NoteCounts[n] > 0) order.Add(n);
                order.Sort((a, b) => svc.NoteCounts[b].CompareTo(svc.NoteCounts[a]));
                foreach (var n in order)
                {
                    var t = ThoughtText((Note)n, svc.NoteCounts[n], svc.Arrivals);
                    if (t != null) rec.thoughts.Add(t);
                    if (rec.thoughts.Count >= 6) break;
                }
                if (svc.Event != null)
                {
                    rec.eventName = Economy.EventName(svc.Event.kind) + " with " + svc.Event.act.name;
                    rec.eventOutcome = svc.EventOutcome ?? "Didn't happen";
                    string hostedKey = EventKinds.FlagKey[svc.Event.kind] + "_hosted";
                    S.SetFlag(hostedKey, S.GetFlag(hostedKey) + 1);
                    if (svc.EventOutcome == "Viral hit") { S.reputation += 6; for (int i = 0; i < S.awareness.Length; i++) S.awareness[i] += 0.04f; AddTag(EventKinds.ViralTag[svc.Event.kind]); }
                    else if (svc.EventOutcome == "Hit") { S.reputation += 2.5f; string hk = EventKinds.FlagKey[svc.Event.kind] + "_hits"; S.SetFlag(hk, S.GetFlag(hk) + 1); }
                    else if (svc.EventOutcome == "Flop") S.reputation -= 2;
                    bool hit = svc.EventOutcome == "Hit" || svc.EventOutcome == "Viral hit";
                    bool isResidency = S.residencyDay >= 0 && svc.Event.act.name == S.residencyAct.name;
                    if (hit && isResidency)
                    {
                        S.residencyAct.following = Math.Min(100, S.residencyAct.following + 6);
                        rec.moments.Add($"Residency night with {S.residencyAct.name} is building a real following.");
                    }
                    else if (hit && svc.Event.kind >= 1 && svc.Event.kind != EventKinds.Match && S.residencyDay < 0 && S.pendingCard == null)
                    {
                        var a = svc.Event.act;
                        S.residencyOffer = new Act { kind = a.kind, name = a.name, style = a.style, fee = a.fee, quality = a.quality, reliability = a.reliability, following = a.following, volatility = a.volatility };
                        S.residencyOfferDay = S.DayOfWeek;
                        S.pendingCard = "residency";
                    }
                    if (S.GetFlag("quiz_hits") >= 3) AddTag("Best Quiz in Town");
                }
                // Staff
                foreach (var a in svc.Staff)
                {
                    if (a.IsOwner) continue;
                    var m = a.m;
                    m.nightsWorked++;
                    float fair = Economy.FairWage(m);
                    m.morale += (m.wage - fair) / fair * 12f;
                    if (m.role == 0 && a.served > 150) m.morale -= 6;
                    if (rec.satisfaction > 62) m.morale += 2;
                    if (a.served < 15 && m.role == 0) m.morale -= 1;
                    m.morale = Math.Max(0, Math.Min(100, m.morale + (65 - m.morale) * 0.04f));
                    if (m.xp >= m.level * 120)
                    {
                        m.xp -= m.level * 120; m.level++;
                        switch (_r.Range(0, 4)) { case 0: m.speed = Math.Min(20, m.speed + 1); break; case 1: m.charm = Math.Min(20, m.charm + 1); break; case 2: m.skill = Math.Min(20, m.skill + 1); break; default: m.stamina = Math.Min(20, m.stamina + 1); break; }
                        rec.moments.Add($"{m.name} levelled up to level {m.level}. They'll want a raise soon.");
                    }
                }
                // Inspections & critic
                if (S.GetFlag("inspector") == S.day + 1)
                {
                    if (svc.CleanlinessNow < 55 || svc.RowdyCount >= 3) { S.cash -= 300; fines += 300; rec.moments.Add("The licensing inspector was NOT impressed. £300 fine."); S.reputation -= 3; }
                    else { rec.moments.Add("The inspector left happy. Clean bill of health."); S.reputation += 1.5f; }
                }
                if (S.GetFlag("critic") == S.day + 1)
                {
                    int stars = rec.satisfaction >= 70 ? 5 : rec.satisfaction >= 60 ? 4 : rec.satisfaction >= 48 ? 3 : 2;
                    S.reputation += (stars - 3) * 4f;
                    var text = stars >= 4 ? $"Fenella Crisp: \"{S.barName} has charm to spare. I'll be back.\"" : stars == 3 ? $"Fenella Crisp: \"{S.barName} is perfectly adequate. Damning, I know.\"" : $"Fenella Crisp: \"I have had better evenings at the dentist.\"";
                    rec.moments.Add(text);
                    rec.reviews.Insert(0, new Review { day = S.day, stars = stars, author = "Fenella Crisp (critic)", segment = "Critic", text = text.Substring(text.IndexOf('"')).Trim('"') });
                }
                foreach (var a in svc.Staff) { }
                rec.incidents = svc.Spills + svc.RowdyCount;
            }
            else
            {
                rec.moments.Add("Closed tonight. The TV crew filmed until 2am and left you a signed photo.");
                rec.satisfaction = 60;
                wages *= 0.5f; // paid a half shift for helping out
            }

            // Licensing (task S1): on Hardcore, a rough Friday or Saturday with nobody on the door draws a fine.
            if (svc != null && S.difficulty == 2 && (S.DayOfWeek == 4 || S.DayOfWeek == 5) && svc.RowdyCount >= 2
                && !S.staff.Exists(m => m.role == Roles.Door))
            {
                fines += 300;
                rec.moments.Add("The licensing officer fined you £300: a rowdy weekend night with no door staff.");
            }

            // Money
            rec.wages = wages;
            float utilities = Economy.UtilitiesPerNight + rec.customers * 0.4f + (S.garden ? Economy.GardenUpkeep : 0);
            float rent = S.DayOfWeek == 6 ? Economy.WeeklyRent(S) : 0;
            float dog = S.GetFlag("dog") == 1 ? 5 : 0;
            float loan = 0;
            if (S.loanBalance > 0) { loan = Math.Min(S.loanDaily, S.loanBalance); S.loanBalance -= loan; }
            float extra = fines + (svc != null ? svc.EmergencySpend : 0); // already paid during the night
            rec.rent = rent; rec.utilities = utilities; rec.loan = loan; rec.other = dog + extra;
            rec.costs = wages + utilities + rent + dog + loan + rec.cogs + extra;
            rec.profit = rec.takings - rec.costs;
            S.cash += rec.takings - (wages + utilities + rent + dog + loan);
            if (rent > 0 && S.GetFlag("food_truck") == 1) { S.cash += 50; rec.moments.Add("The street-food van paid its £50 pitch fee."); }
            if (rent > 0 && S.managedIncome > 0)
            {
                S.cash += S.managedIncome;
                rec.moments.Add($"Your manager at {S.firstBarName} sent over £{S.managedIncome:#,0}, your share of the week.");
            }
            if (rent > 0)
            {
                float week = rec.profit;
                int nights = 1;
                for (int i = S.history.Count - 1; i >= 0 && nights < 7; i--, nights++) week += S.history[i].profit;
                rec.moments.Add($"Weekly rent paid: £{rent:0}. Profit this week: £{week:#,0}.");
            }
            if ((S.day + 1) % 28 == 0) rec.moments.Add($"{Content.Seasons[(S.Season + 1) % 4]} starts tomorrow.");

            // Reviews & rating
            foreach (var rv in rec.reviews) { S.reviews.Insert(0, rv); S.reviewCount++; }
            if (S.reviews.Count > 80) S.reviews.RemoveRange(80, S.reviews.Count - 80);
            float sum = 0; int n2 = Math.Min(40, S.reviews.Count);
            for (int i = 0; i < n2; i++) sum += S.reviews[i].stars;
            S.rating = (sum + 3f * 4) / (n2 + 4);
            rec.rating = S.rating;

            // Reputation
            float repBefore = S.reputation;
            float target = (S.rating - 1f) / 4f * 100f * Math.Min(1f, (S.reviewCount + 8) / 50f);
            S.reputation += (target - S.reputation) * 0.12f;
            S.reputation = Math.Max(0, Math.Min(100, S.reputation));
            if (PlinthTier(S.reputation) > PlinthTier(repBefore))
                rec.moments.Insert(0, $"Your model has been upgraded to a {PlinthNames[PlinthTier(S.reputation)]} plinth. People are noticing.");
            if (S.mode == 0)
            {
                var unlocked = new List<string>();
                foreach (var d in Content.Items)
                    if (!d.Hidden && d.UnlockRep > repBefore && d.UnlockRep <= S.reputation) unlocked.Add(d.Name);
                if (unlocked.Count > 0) rec.moments.Insert(0, "New in the catalogue: " + string.Join(", ", unlocked) + "!");
            }

            // Tags from tonight
            if (svc != null && svc.Arrivals > 20)
            {
                float a = svc.Arrivals;
                if (svc.NoteCounts[(int)Note.Pricey] / a > 0.25f) AddTag("Overpriced"); else RemoveTag("Overpriced");
                if (svc.NoteCounts[(int)Note.WaitedLong] / a > 0.25f) AddTag("Slow Service"); else RemoveTag("Slow Service");
                if (svc.NoteCounts[(int)Note.Dirty] / a > 0.3f) AddTag("Sticky Tables"); else RemoveTag("Sticky Tables");
                if (svc.NoteCounts[(int)Note.WaitedLong] / a < 0.05f && svc.Served > 100) AddTag("Great Service");
                if (S.rating >= 4.2f && svc.Arrivals < 70) AddTag("Hidden Gem"); else if (svc.Arrivals > 120) RemoveTag("Hidden Gem");
                if (svc.RowdyCount >= 3 && S.DayOfWeek == 5) AddTag("Rough on a Saturday");
                if (S.GetFlag("dog") == 1) AddTag("Dog-Friendly");
                if (svc.NoteCounts[(int)Note.Bargain] / a > 0.3f) AddTag("Great Value");
            }

            // Learn each drink's demand (what sold plus what people wanted but couldn't get), for restocking.
            if (svc != null)
                foreach (var m in S.menu)
                {
                    if (!m.onMenu) continue;
                    float tonight = m.soldTonight + m.missedTonight;
                    m.demandAvg = m.demandAvg <= 0 ? tonight : m.demandAvg * 0.6f + tonight * 0.4f;
                }

            // Alcohol-free sales, for the Dry January scenario (task P3).
            foreach (var m in S.menu) if (Content.Drink(m.id).Cat == DrinkCat.AlcoholFree && m.soldTonight > 0) S.SetFlag("af_sold", S.GetFlag("af_sold") + m.soldTonight);

            // Signature cocktails (task K4): sell enough and they become part of the bar's reputation.
            int sigSold = 0;
            foreach (var m in S.menu) if (Content.Drink(m.id).Signature) sigSold += m.soldTonight;
            if (sigSold > 0)
            {
                S.SetFlag("sig_sold", S.GetFlag("sig_sold") + sigSold);
                if (S.GetFlag("sig_sold") >= 120 && !S.tags.Contains("Signature Cocktails")) { AddTag("Signature Cocktails"); rec.moments.Add("People are coming in just for your signature cocktails."); }
            }

            // The awards ceremony was this morning's card; announce the verdict in tonight's report (task P2).
            if (S.GetFlag("awards_tried") == S.day && S.GetFlag("awards_score") > 0)
            {
                int score = S.GetFlag("awards_score");
                rec.moments.Insert(0, S.GetFlag("bar_of_year") > 0 && S.tags.Contains("Bar of the Year") && score >= Director.AwardsWinScore
                    ? $"BAR OF THE YEAR! The judges scored you {score}. The trophy's on the bar and £5,000 is in the bank."
                    : $"Bar of the Year went elsewhere. The judges scored you {score} (the winner needed {Director.AwardsWinScore}). There's always next summer.");
                S.SetFlag("awards_score", 0);
            }

            // Tonight's verdict, judged against earlier nights (before tonight joins the history).
            if (svc != null) Economy.Verdict(S, rec, svc.NoteCounts, svc.Arrivals);

            // Wear & upkeep
            foreach (var it in S.items) it.wear = Math.Min(100, it.wear + (Content.Item(it.id).Fn == ItemFn.Games ? 0.8f : 0.35f));
            S.lineQuality = Math.Max(20, S.lineQuality - 4);
            bool hasFloorStaff = S.staff.Exists(m => m.role == Roles.Floor);
            var cleaner = S.staff.Find(m => m.role == Roles.Cleaner);
            S.cleanliness = hasFloorStaff ? 85 : 74;
            float mess = svc != null ? Math.Min(15, svc.Spills + svc.RowdyCount * 2) : 0;
            if (cleaner != null)
            {
                // A cleaner after close (task S2): a better starting point, and last night's mess mostly gone.
                S.cleanliness = Math.Min(97, S.cleanliness + 8 + cleaner.stamina * 0.3f);
                mess *= 0.4f;
                cleaner.nightsWorked++;
            }
            S.cleanliness -= mess;

            // Awareness decays slowly towards a floor
            for (int i = 0; i < S.awareness.Length; i++) S.awareness[i] = Math.Max(0.05f, Math.Min(1f, 0.12f + (S.awareness[i] - 0.12f) * 0.985f));
            S.socialPostBoost *= 0.5f;

            // Staff quitting
            for (int i = S.staff.Count - 1; i >= 0; i--)
            {
                var m = S.staff[i];
                if (m.morale < 20 && _r.Chance(0.35))
                {
                    rec.moments.Add($"{m.name} has quit. \"I can't do this any more.\"");
                    S.staff.RemoveAt(i);
                }
            }

            S.events.RemoveAll(e => e.day <= S.day);
            S.history.Add(rec);
            CountNight(S.stats, rec);
            if (S.history.Count > 42) S.history.RemoveAt(0);
            // Keep saves small (WebGL PlayerPrefs are capped): old nights keep their numbers, not their stories.
            for (int i = 0; i < S.history.Count - 7; i++)
            {
                var h = S.history[i];
                h.reviews.Clear(); h.moments.Clear(); h.thoughts.Clear(); h.stockOuts.Clear();
            }

            // Goals (after the night is in the history, so "first night" etc. count tonight)
            GoalsDoneLastNight.Clear();
            Recompute();
            CheckGoals();
            ScenarioJustEnded = false;
            var sc = Scenario;
            if (sc != null && S.scenarioResult == 0)
            {
                if (sc.Won(this)) { S.scenarioResult = 1; ScenarioJustEnded = true; }
                else if (S.day - sc.StartDay >= sc.Days - 1) { S.scenarioResult = 2; ScenarioJustEnded = true; }
            }
            LastNight = rec;
            Svc = svc;
            Phase = Phase.Report;

            if (S.difficulty > 0) CashCrisis(rec);
            Changed?.Invoke();
        }

        // ---- Cash crisis (task T8) --------------------------------------------------------------------
        // A struggling bar gets a story before game over: a warning about rent, the bank's emergency loan,
        // then a final demand with a deadline and a bailiff card offering ways out. Only then is it bankrupt.

        /// Days to get back in the black after a final demand.
        public int FinalDemandDays => S.difficulty == 2 ? 3 : 7;
        /// Debt beyond which the bar closes immediately.
        public const float HardDebtLimit = 6000f;
        /// Day the final demand runs out, or -1 if there isn't one.
        public int FinalDemandDeadline => S.GetFlag("final_demand") > 0 ? S.GetFlag("final_demand") - 1 + FinalDemandDays : -1;

        void CashCrisis(NightRecord rec)
        {
            int demand = S.GetFlag("final_demand"); // issue day + 1, 0 = none
            if (S.cash >= 0)
            {
                if (demand > 0)
                {
                    S.SetFlag("final_demand", 0);
                    rec.moments.Add("Back in the black. The bailiffs' letter goes in the bin.");
                }
                float rent = Economy.WeeklyRent(S);
                if (S.DayOfWeek >= 3 && S.DayOfWeek <= 5 && S.cash < rent)
                    rec.moments.Add($"Heads up: Sunday's rent is £{rent:#,0} and there's £{S.cash:#,0} in the bank.");
                return;
            }
            if (S.loansTaken == 0) { S.pendingCard = "loan"; return; }
            if (S.cash < -HardDebtLimit) { S.bankrupt = true; return; }
            if (demand == 0)
            {
                S.SetFlag("final_demand", S.day + 1);
                if (S.pendingCard == null) S.pendingCard = "bailiff";
                rec.moments.Add($"A final demand from the bank: get back above £0 within {FinalDemandDays} days or the bailiffs come.");
            }
            else if (S.day >= FinalDemandDeadline) S.bankrupt = true;
            else rec.moments.Add($"Still overdrawn. {FinalDemandDeadline - S.day} days left on the final demand.");
        }

        /// The most valuable thing you could sell without breaking the bar (keeps one counter and the toilets).
        public PlacedItem BestToSell()
        {
            PlacedItem best = null; float bestValue = 0;
            int counters = S.items.FindAll(i => Content.Item(i.id).Fn == ItemFn.BarCounter).Count;
            foreach (var it in S.items)
            {
                var d = Content.Item(it.id);
                if (d.Fn == ItemFn.Toilet || (d.Fn == ItemFn.BarCounter && counters <= 1)) continue;
                float v = SellValue(it);
                if (v > bestValue) { best = it; bestValue = v; }
            }
            return best;
        }

        public string SecondLoanBlocker() => S.difficulty == 2 ? "The bank won't lend twice on Hardcore" : S.loansTaken >= 2 ? "The bank has said no" : null;

        public void TakeSecondLoan()
        {
            S.loansTaken++;
            S.cash += 2000;
            S.loanBalance += 3000;
            S.loanDaily = Math.Max(S.loanDaily, 110);
        }

        static void CountNight(CareerStats st, NightRecord r)
        {
            if (st.firstDay < 0) st.firstDay = r.day;
            st.nights++;
            st.customers += r.customers; st.drinks += r.servedDrinks; st.turnedAway += r.turnedAway;
            st.takings += r.takings; st.profit += r.profit;
            if (!string.IsNullOrEmpty(r.eventName)) { st.events++; if (r.eventOutcome == "Hit" || r.eventOutcome == "Viral hit") st.hits++; }
            if (r.takings > st.bestTakings) { st.bestTakings = r.takings; st.bestTakingsDay = r.day; }
            if (r.customers > st.bestCustomers) { st.bestCustomers = r.customers; st.bestCustomersDay = r.day; }
        }

        public void CheckGoals()
        {
            if (S.mode != 0) return;
            while (S.goalIndex < Goals.Career.Count && Goals.Career[S.goalIndex].Done(this))
            {
                var goal = Goals.Career[S.goalIndex];
                S.cash += goal.Reward;
                GoalsDoneLastNight.Add(S.goalIndex.ToString());
                S.goalIndex++;
            }
        }

        public static readonly string[] PlinthNames = { "Cardboard", "Painted MDF", "Stained Oak", "Walnut & Inlay", "Marble" };
        public static int PlinthTier(float reputation) => reputation >= 80 ? 4 : reputation >= 60 ? 3 : reputation >= 40 ? 2 : reputation >= 20 ? 1 : 0;

        public GoalDef CurrentGoal => S.mode == 0 && S.goalIndex < Goals.Career.Count ? Goals.Career[S.goalIndex] : null;

        void AddTag(string t) { if (!S.tags.Contains(t)) S.tags.Add(t); }
        public void AddTagPublic(string t) => AddTag(t);
        void RemoveTag(string t) { S.tags.Remove(t); }

        static string ThoughtText(Note n, int count, int total)
        {
            string pct = total > 0 ? $"{Math.Round(100f * count / total)}%" : "";
            switch (n)
            {
                case Note.WaitedLong: return $"{pct} thought service was slow";
                case Note.GaveUpQueue: return $"{count} gave up queuing at the bar";
                case Note.LovedDrink: return $"{pct} loved their drinks";
                case Note.BadDrink: return $"{pct} said a drink tasted off (clean the lines?)";
                case Note.OutOfStock: return $"{count} couldn't get the drink they wanted";
                case Note.Pricey: return $"{pct} thought it was overpriced";
                case Note.Bargain: return $"{pct} thought prices were a bargain";
                case Note.Dirty: return $"{pct} thought it was dirty";
                case Note.NoSeat: return $"{pct} couldn't find a seat";
                case Note.ComfySeat: return $"{pct} loved the comfy seating";
                case Note.LovedVibe: return $"{pct} loved the vibe";
                case Note.HatedVibe: return $"{pct} felt it wasn't their kind of place";
                case Note.LovedEvent: return $"{count} loved tonight's event";
                case Note.EventBombed: return $"{count} sat through a painful event";
                case Note.ToiletQueue: return $"{count} complained about the toilet queue";
                case Note.NoToilet: return $"{count} were horrified there were no toilets";
                case Note.FriendlyStaff: return $"{pct} said the staff were lovely";
                case Note.GrumpyStaff: return $"{count} found the staff grumpy";
                case Note.RowdyNearby: return $"{count} were bothered by rowdy customers";
                case Note.Dog: return $"{count} fell in love with Biscuit the dog";
                case Note.LovedMusic: return $"{pct} liked the music";
                case Note.HatedMusic: return $"{pct} hated the music";
                case Note.Crowded: return $"{pct} found it too crowded";
                case Note.HappyHour: return $"{count} enjoyed the happy hour";
                case Note.Screens: return $"{count} enjoyed the screens";
                default: return null;
            }
        }

        public void NextDay()
        {
            if (Phase != Phase.Report) return;
            if (S.bankrupt) { Phase = Phase.GameOver; Changed?.Invoke(); return; }
            S.day++;
            _r = new Rng(S.seed + (ulong)S.day * 31UL + 7UL);
            PlacedToday.Clear();
            SpentToday = 0;
            S.postedToday = false;
            S.closedTonight = false;
            if (S.rivalDays > 0 && --S.rivalDays == 0) { S.rivalPressure = 0; }
            // Weather, weighted by season
            float[] w = S.Season switch
            {
                0 => new[] { 0.15f, 0.35f, 0.3f, 0.08f, 0.12f },
                1 => new[] { 0.1f, 0.35f, 0.3f, 0.15f, 0.1f },
                2 => new[] { 0.25f, 0.3f, 0.25f, 0.05f, 0.15f },
                _ => new[] { 0.45f, 0.2f, 0.12f, 0.03f, 0.2f },
            };
            S.weather = Economy.Weathers[Math.Max(0, _r.WeightedIndex(w))];
            if (S.DayOfWeek == 0)
            {
                Economy.RefreshApplicants(S, _r);
                Economy.RefreshAgency(S, _r);
            }
            if (S.residencyDay >= 0 && V != null && V.HasStage) BookResidency();
            if (S.pendingCard != null) { /* keep the loan or residency decision */ }
            else Director.Roll(this, _r);
            Svc = null;
            Phase = Phase.Prep;
            Recompute();
        }
    }
}
