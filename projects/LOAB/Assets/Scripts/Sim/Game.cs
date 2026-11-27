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
            if (string.IsNullOrEmpty(s.pendingCard)) s.pendingCard = null; // JsonUtility saves null strings as ""
            if (string.IsNullOrEmpty(s.scenario)) s.scenario = null;
            s.menu ??= new List<MenuEntry>(); s.staff ??= new List<StaffMember>(); s.events ??= new List<BookedEvent>();
            L = new Layout(s);
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
            s.awareness = new[] { 0.2f, 0.16f, 0.34f, 0.18f, 0.16f };
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
            def?.Setup(g.S);
            g.Recompute();
            return g;
        }

        public ScenarioDef Scenario => S.mode == 2 ? Scenarios.Get(S.scenario) : null;
        public int ScenarioDaysLeft => Scenario == null ? 0 : Math.Max(0, Scenario.Days - S.day);
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

        public float CaseCost(string id) { var d = Content.Drink(id); return d.CaseSize * d.UnitCost; }

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
            for (int i = 0; i < 5; i++)
            {
                var seg = Content.Segments[i];
                share += seg.DrinkPrefs[(int)d.Cat] / Math.Max(1, catCount[(int)d.Cat]) * seg.Share;
                tot += seg.Share;
            }
            share /= tot;
            return (int)Math.Ceiling(expected * 2.6f * share * 1.4f);
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

        public string CleanLines()
        {
            if (S.lineQuality >= 95) return Fail("Lines are already clean");
            if (S.cash < 40 && S.difficulty > 0) return Fail("Not enough cash");
            Spend(40, "Line clean");
            S.lineQuality = 100;
            Changed?.Invoke();
            return null;
        }

        public string DeepClean()
        {
            if (S.cleanliness >= 98) return Fail("It's already spotless");
            if (S.cash < 60 && S.difficulty > 0) return Fail("Not enough cash");
            Spend(60, "Deep clean");
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
            if (act.kind >= 1 && !V.HasStage) return Fail($"{Economy.EventName(act.kind)} needs a Small Stage. Build one first");
            float cost = act.fee + promo;
            if (S.cash < cost && S.difficulty > 0) return Fail("Not enough cash");
            Spend(cost, "Event booking");
            S.events.Add(new BookedEvent { uid = S.nextUid++, day = day, kind = act.kind, act = act, promo = promo, ticket = ticket });
            if (act.kind == 1) S.agency.Remove(act); // comedians are booked out
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
            var rec = new NightRecord { day = S.day, weather = S.weather };
            float wages = 0, fines = 0;
            foreach (var m in S.staff) wages += m.wage;
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
                for (int i = 0; i < 5; i++) rec.segCounts.Add(svc.SegCounts[i]);
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
                    if (svc.Event.kind == 0) S.SetFlag("quiz_hosted", S.GetFlag("quiz_hosted") + 1);
                    else if (svc.Event.kind == 1) S.SetFlag("comedy_hosted", S.GetFlag("comedy_hosted") + 1);
                    else S.SetFlag("openmic_hosted", S.GetFlag("openmic_hosted") + 1);
                    if (svc.EventOutcome == "Viral hit") { S.reputation += 6; for (int i = 0; i < 5; i++) S.awareness[i] += 0.04f; AddTag(svc.Event.kind == 0 ? "Best Quiz in Town" : svc.Event.kind == 1 ? "Best Comedy Night" : "Legendary Open Mic"); }
                    else if (svc.EventOutcome == "Hit") { S.reputation += 2.5f; S.SetFlag(svc.Event.kind == 0 ? "quiz_hits" : "comedy_hits", S.GetFlag(svc.Event.kind == 0 ? "quiz_hits" : "comedy_hits") + 1); }
                    else if (svc.EventOutcome == "Flop") S.reputation -= 2;
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

            // Money
            rec.wages = wages;
            float utilities = Economy.UtilitiesPerNight + rec.customers * 0.4f;
            float rent = S.DayOfWeek == 6 ? Economy.RentPerWeek * (S.difficulty == 0 ? 0.8f : S.difficulty == 2 ? 1.3f : 1f) : 0;
            float dog = S.GetFlag("dog") == 1 ? 5 : 0;
            float loan = 0;
            if (S.loanBalance > 0) { loan = Math.Min(S.loanDaily, S.loanBalance); S.loanBalance -= loan; }
            float extra = fines + (svc != null ? svc.EmergencySpend : 0); // already paid during the night
            rec.costs = wages + utilities + rent + dog + loan + rec.cogs + extra;
            rec.profit = rec.takings - rec.costs;
            S.cash += rec.takings - (wages + utilities + rent + dog + loan);
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

            // Wear & upkeep
            foreach (var it in S.items) it.wear = Math.Min(100, it.wear + (Content.Item(it.id).Fn == ItemFn.Games ? 0.8f : 0.35f));
            S.lineQuality = Math.Max(20, S.lineQuality - 4);
            bool hasFloorStaff = S.staff.Exists(m => m.role == 1);
            S.cleanliness = hasFloorStaff ? 85 : 74;
            if (svc != null) S.cleanliness -= Math.Min(15, svc.Spills + svc.RowdyCount * 2);

            // Awareness decays slowly towards a floor
            for (int i = 0; i < 5; i++) S.awareness[i] = Math.Max(0.05f, Math.Min(1f, 0.12f + (S.awareness[i] - 0.12f) * 0.985f));
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
                else if (S.day >= sc.Days - 1) { S.scenarioResult = 2; ScenarioJustEnded = true; }
            }
            LastNight = rec;
            Svc = svc;
            Phase = Phase.Report;

            if (S.cash < 0 && S.difficulty > 0)
            {
                if (S.loansTaken == 0) S.pendingCard = "loan";
                else if (S.cash < -1500) { S.bankrupt = true; }
            }
            Changed?.Invoke();
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
            if (S.pendingCard == "loan") { /* keep */ }
            else Director.Roll(this, _r);
            Svc = null;
            Phase = Phase.Prep;
            Recompute();
        }
    }
}
