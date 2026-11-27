using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public sealed class BarPoint
    {
        public PlacedItem item;
        public Cell cell;               // where the customer stands
        public float sx, sy;            // where the bartender stands
        public readonly List<Customer> queue = new List<Customer>();
        public StaffAgent bartender;
    }

    /// <summary>
    /// One evening of trade, 18:00 to midnight, simulated at a fixed tick (0.1 game minutes).
    /// Pure logic: the Unity layer only reads this state and sends commands.
    /// </summary>
    public sealed class Service
    {
        public const float Tick = 0.1f;
        public const float LastOrders = 330f, Close = 360f, HardEnd = 420f;
        /// Tonight's last orders and closing time: normally 23:30 and midnight, earlier if the owner calls it (task L2).
        public float LastOrdersAt = LastOrders, CloseAt = Close;
        public bool ClosedEarly => LastOrdersAt < LastOrders;

        public readonly GameState S;
        public readonly Layout L;
        public readonly VibeReport V;
        readonly Rng R;

        public float T;                 // minutes since 18:00
        public bool Finished;
        public readonly List<Customer> Customers = new List<Customer>();
        public readonly List<StaffAgent> Staff = new List<StaffAgent>();
        public readonly List<BarPoint> BarPoints = new List<BarPoint>();
        public readonly List<Incident> Incidents = new List<Incident>();
        public readonly List<ServiceMessage> Messages = new List<ServiceMessage>();
        public readonly int[] NoteCounts = new int[(int)Note.Count];
        public readonly int[] SegCounts = new int[5];
        public readonly List<Review> NewReviews = new List<Review>();
        public readonly List<string> Moments = new List<string>();
        public readonly List<string> StockOuts = new List<string>();
        public readonly List<Customer> Departed = new List<Customer>();

        // Stats
        public float Takings, Cogs, TicketIncome, EmergencySpend;
        public int Served, Arrivals, TurnedAway, Walkouts, Spills, RowdyCount, Slips;
        public int Capacity;
        public int CleanGlasses;
        readonly List<(float t, int n)> _washing = new List<(float, int)>();
        readonly List<(float t, string id, int units)> _deliveries = new List<(float, string, int)>();

        // Event
        public BookedEvent Event;
        public float EventStart = -1, EventPerf;
        public int EventPhase;          // 0 not started, 1 running, 2 interval, 3 second half, 4 done
        public string EventOutcome;
        int _quizRound;
        bool _heckled;

        // Player tools
        public float HappyHourUntil = -1;
        public bool HappyHourUsed;
        public float CollectCooldown;
        public bool OwnerBehindBar;
        public float Speed = 1f;        // UI speed multiplier (the Unity layer reads this)

        readonly float[] _expected = new float[5];
        float _eventExpected;
        readonly Dictionary<int, Customer[]> _seats = new Dictionary<int, Customer[]>();
        readonly Dictionary<int, List<Customer>> _users = new Dictionary<int, List<Customer>>();
        readonly Dictionary<int, List<Customer>> _toiletQueue = new Dictionary<int, List<Customer>>();
        readonly HashSet<int> _blockedToilets = new HashSet<int>();
        readonly List<Cell> _standSpots = new List<Cell>();
        int _nextId = 1, _nextIncident = 1;
        float _minuteAcc;
        float _henAt = -1;
        bool _busker;
        float _noGlassMsgAt = -99, _fullMsgAt = -99, _queueMsgAt = -99;
        readonly HashSet<string> _warnedOut = new HashSet<string>();

        public Service(GameState s, Layout layout, VibeReport vibe)
        {
            S = s; L = layout; V = vibe;
            R = new Rng(s.seed * 7919UL + (ulong)s.day * 104729UL + 17UL);
            L.ClearPathCache();

            foreach (var it in s.items)
            {
                var d = Content.Item(it.id);
                if (d.Fn == ItemFn.BarCounter)
                {
                    var f = Layout.Facing(it.rot);
                    foreach (var c in Layout.ServicePoints(it))
                        if (L.Walkable(c))
                            BarPoints.Add(new BarPoint { item = it, cell = c, sx = c.x - f.x * 2f, sy = c.y - f.y * 2f });
                }
                if (d.Fn == ItemFn.Seat) _seats[it.uid] = new Customer[d.Capacity];
                if (d.Fn == ItemFn.Games || d.Fn == ItemFn.Toilet) _users[it.uid] = new List<Customer>();
                if (d.Fn == ItemFn.Toilet) _toiletQueue[it.uid] = new List<Customer>();
            }

            var reach = L.ReachableFromDoor();
            foreach (var c in reach)
                if (c.y >= 2 && L.ItemAt(c) == 0) _standSpots.Add(c);
            Capacity = Math.Max(20, (int)(reach.Count * 1.2f));
            CleanGlasses = s.GetFlag("glasswasher") == s.day + 1 ? 30 : 90;
            foreach (var m in s.menu) { m.soldTonight = 0; m.missedTonight = 0; }
            if (s.GetFlag("hen_day") == s.day + 1) _henAt = 120;
            _busker = s.GetFlag("busker") == s.day + 1;
            if (_busker) Say("A busker is playing in the corner. Lovely.", 1);

            // Staff
            foreach (var m in s.staff)
            {
                if (!Roles.WorksService(m.role)) continue;
                var a = new StaffAgent { m = m, x = L.Door.x, y = L.Door.y, arriveAt = 0 };
                if (m.HasTrait("always_late") && R.Chance(0.3)) a.arriveAt = 45;
                Staff.Add(a);
            }

            Event = s.events.Find(e => e.day == s.day);
            if (Event != null && Event.kind >= 1 && !V.HasStage)
            {
                Say($"No stage, no show: the {Economy.EventName(Event.kind)} was called off.", 2);
                Event = null;
            }

            Economy.ExpectedArrivals(s, V, _expected, Event, out _eventExpected);
            if (s.day == 0) Say("Opening night! Word has got round and people are curious.", 1);
            var seasonNote = Economy.SeasonNote(s);
            if (seasonNote != null) Say(seasonNote, 1);
            if (Event != null)
                Say(Event.kind == 0 ? $"Tonight: Pub Quiz with {Event.act.name}, from 8pm." : Event.kind == 1 ? $"Tonight: Comedy Night headlined by {Event.act.name}, from 8pm." : $"Tonight: Open Mic Night ({Event.act.name}), from 8pm. Anything could happen.", 3);
            if (BarPoints.Count == 0) Say("There's no bar counter! Nobody can be served.", 2);
            if (s.staff.Find(m => m.role == Roles.Bartender) == null) Say("No bartenders on the rota. You'll have to serve everyone yourself.", 2);
            if (s.staff.Find(m => m.role == Roles.Bartender) == null) SetOwnerBehindBar(true);
            if (s.difficulty == 2 && s.DayOfWeek >= 4 && s.DayOfWeek <= 5 && s.staff.Find(m => m.role == Roles.Door) == null)
                Say("A weekend night with no door staff. The licensing officer won't like a rough one.", 2);
        }

        public static string Clock(float t)
        {
            int mins = (int)t + 18 * 60;
            return $"{(mins / 60) % 24:00}:{mins % 60:00}";
        }

        public int Inside { get { int n = 0; foreach (var c in Customers) if (c.Inside) n++; return n; } }
        public bool HappyHour => T < HappyHourUntil || IsScheduledHappyHour();
        bool IsScheduledHappyHour() { if (S.happyHourStart < 0) return false; float h = 18 + T / 60f; return h >= S.happyHourStart && h < S.happyHourStart + 1; }
        public float Price(MenuEntry m) => HappyHour ? m.price * (1f - S.happyHourDiscount / 100f) : m.price;

        /// Below this cleanliness, customers start to notice the mess.
        public const float GrimeThreshold = 45f;
        /// Minutes-weighted dirt a customer puts up with before they think the place is dirty.
        public const float GrimeToComplain = 8f;

        public float CleanlinessNow
        {
            get
            {
                float dirt = 0;
                foreach (var i in Incidents)
                {
                    if (i.resolved) continue;
                    if (i.kind == IncidentKind.Glasses) dirt += i.count * 0.25f;
                    else if (i.kind == IncidentKind.Spill) dirt += 6;
                    else if (i.kind == IncidentKind.Toilet) dirt += 8;
                }
                return Math.Max(0, Math.Min(100, S.cleanliness - Math.Min(45, dirt) - T * 0.01f));
            }
        }

        public void Say(string text, int kind, Cell? cell = null)
        {
            Messages.Add(new ServiceMessage { time = T, text = text, kind = kind, hasCell = cell.HasValue, cell = cell ?? default });
        }

        // =====================================================================================
        //  Main loop
        // =====================================================================================

        public void Step()
        {
            if (Finished) return;
            T += Tick;
            if (CollectCooldown > 0) CollectCooldown -= Tick;

            SpawnArrivals();
            UpdateEvent();
            for (int i = _washing.Count - 1; i >= 0; i--)
                if (_washing[i].t <= T) { CleanGlasses += _washing[i].n; _washing.RemoveAt(i); }
            for (int i = _deliveries.Count - 1; i >= 0; i--)
                if (_deliveries[i].t <= T)
                {
                    var m = S.Menu(_deliveries[i].id);
                    if (m != null) m.stock += _deliveries[i].units;
                    Say($"Emergency delivery: {Content.Drink(_deliveries[i].id).Name} restocked.", 1);
                    _deliveries.RemoveAt(i);
                }

            foreach (var a in Staff) UpdateStaff(a);
            for (int i = 0; i < Customers.Count; i++) UpdateCustomer(Customers[i]);

            _minuteAcc += Tick;
            if (_minuteAcc >= 1f)
            {
                _minuteAcc -= 1f;
                foreach (var c in Customers) if (c.Inside) MinuteTick(c);
                CheckQueues();
            }

            if (T >= CloseAt)
            {
                foreach (var c in Customers) if (c.Inside && c.state != CState.Leaving) Leave(c, false);
            }
            Customers.RemoveAll(c => c.state == CState.Gone);
            if ((T >= CloseAt && Customers.Count == 0) || T >= HardEnd) Finished = true;
        }

        /// <summary>Run the rest of the night instantly.</summary>
        public void RunToEnd() { int guard = 0; while (!Finished && guard++ < 10000) Step(); }

        // =====================================================================================
        //  Arrivals
        // =====================================================================================

        static float Profile(float t, Seg s)
        {
            // Relative arrival intensity through the evening.
            float h = t / 60f; // hours since 18:00
            float p = h < 1 ? 0.8f : h < 2 ? 0.9f : h < 3 ? 1.2f : h < 4 ? 1.25f : h < 5 ? 0.9f : h < 5.5f ? 0.35f : 0f;
            if (s == Seg.YoungPros && h < 2) p *= 1.6f; // after-work crowd
            if (s == Seg.Students && h < 1.5f) p *= 0.6f;
            return p;
        }

        const float ProfileIntegral = 0.8f + 0.9f + 1.2f + 1.25f + 0.9f + 0.35f * 0.5f; // area under Profile in hours

        void SpawnArrivals()
        {
            if (T >= LastOrdersAt) return;
            if (_henAt >= 0 && T >= _henAt)
            {
                _henAt = -1;
                Say("The hen party has arrived! Sashes everywhere.", 3);
                Moments.Add("A hen party of twelve took over the bar for the night.");
                for (int k = 0; k < 12; k++)
                {
                    var c = Spawn(k % 3 == 0 ? Seg.Students : Seg.YoungPros, true);
                    if (c != null) { c.budget *= 1.8f; c.maxStay = 200; c.shirt = 0xff6fb5; }
                }
            }
            float hh = HappyHour ? 1.25f : 1f;
            for (int s = 0; s < 5; s++)
            {
                var seg = (Seg)s;
                float rate = _expected[s] * Profile(T, seg) / (ProfileIntegral * 60f) * Tick;
                if (Content.Segments[s].PriceSensitivity > 0.5f) rate *= hh;
                int n = R.Poisson(rate);
                for (int k = 0; k < n; k++) Spawn(seg, false);
            }
            if (Event != null && _eventExpected > 0 && T >= 80 && T < 125)
            {
                float rate = _eventExpected / 45f * Tick;
                int n = R.Poisson(rate);
                for (int k = 0; k < n; k++) Spawn(PickEventSegment(), true);
            }
        }

        Seg PickEventSegment()
        {
            var w = new float[5];
            for (int i = 0; i < 5; i++)
            {
                var d = Content.Segments[i];
                w[i] = Economy.EventAffinity(d, Event.kind) * d.Share * (0.3f + V.SegMatch[i]);
            }
            int idx = R.WeightedIndex(w);
            return (Seg)Math.Max(0, idx);
        }

        static readonly uint[] Skins = { 0xf1c27d, 0xe0ac69, 0xc68642, 0x8d5524, 0xffdbac, 0x5c3a21, 0xd9a066 };
        static readonly uint[] Hairs = { 0x2b1b10, 0x4a3020, 0xd9b26f, 0x8b4513, 0x111111, 0xb0b0b0, 0xa0522d, 0xe8c07d };

        Customer Spawn(Seg seg, bool forEvent)
        {
            if (Inside >= Capacity)
            {
                TurnedAway++;
                if (T - _fullMsgAt > 30) { Say("The bar's at capacity. People are being turned away at the door.", 2); _fullMsgAt = T; }
                return null;
            }
            var d = Content.Segment(seg);
            var c = new Customer
            {
                id = _nextId++, seg = seg, def = d,
                x = L.Outside.x, y = L.Outside.y,
                arrivedAt = T,
                budget = R.Range(d.BudgetMin, d.BudgetMax) * (S.DayOfWeek >= 4 && S.DayOfWeek <= 5 ? 1.15f : 1f),
                maxStay = R.Range(70f, 190f) * (seg == Seg.Students ? 1.2f : 1f),
                patience = d.Patience * (S.difficulty == 0 ? 1.5f : S.difficulty == 2 ? 0.75f : 1f),
                thirst = R.Range(0.7f, 1f),
                bladder = R.Range(0f, 0.3f),
                fun = R.Range(0.3f, 0.7f),
                mood = 55 + R.Range(-5f, 10f),
                skin = R.Pick(Skins), hair = R.Pick(Hairs), tall = R.Chance(0.5),
            };
            c.shirt = Tint(d.Color, R.Range(-2, 3) * 0.1f); // a few shades, so materials can be shared/instanced
            if (forEvent) c.maxStay = Math.Max(c.maxStay, 150f);

            // Regulars
            var pool = S.regulars.FindAll(r => r.seg == (int)seg);
            int friendsToBring = 0;
            if (pool.Count > 0 && R.Chance(0.12 + 0.03 * Math.Min(pool.Count, 8)))
            {
                var r = pool[R.Range(0, pool.Count)];
                if (!Customers.Exists(x => x.regular == r) && !Departed.Exists(x => x.regular == r))
                {
                    c.regular = r; c.name = r.name; c.mood += 6; c.patience *= 1.3f; c.Mark(Note.Regular);
                    r.visits++;
                    if (R.Chance(0.18 * r.loyalty))
                    {
                        friendsToBring = R.Range(1, 3);
                        Moments.Add($"{r.name} brought {friendsToBring} friend{(friendsToBring > 1 ? "s" : "")} along.");
                    }
                }
            }
            if (c.name == null) c.name = Content.RandomName(R);

            // Comedy tickets
            if (Event != null && Event.kind == 1 && Event.ticket > 0 && T < EventEndTime)
            {
                if (forEvent || T >= 100)
                {
                    c.paidTicket = true;
                    TicketIncome += Event.ticket;
                    c.budget = Math.Max(5, c.budget - Event.ticket * 0.5f);
                }
            }

            // #11: late arrivals for a running event join in
            if (forEvent && Event != null && EventPhase >= 1 && EventPhase <= 3)
            {
                c.watching = true;
                if (Event.kind != 1 && Event.ticket > 0) TicketIncome += Event.ticket;
            }
            Customers.Add(c);
            Arrivals++;
            SegCounts[(int)seg]++;
            Decide(c);
            for (int i = 0; i < friendsToBring; i++) Spawn(seg, false);
            return c;
        }

        static uint Tint(uint rgb, float amt)
        {
            int r = (int)((rgb >> 16) & 255), g = (int)((rgb >> 8) & 255), b = (int)(rgb & 255);
            int f(int v) => Math.Max(0, Math.Min(255, (int)(v + (amt > 0 ? (255 - v) * amt : v * amt))));
            return (uint)((f(r) << 16) | (f(g) << 8) | f(b));
        }

        // =====================================================================================
        //  Customers
        // =====================================================================================

        Cell CellOf(Customer c) => new Cell((int)Math.Round(c.x), (int)Math.Round(c.y));

        bool WalkTo(Customer c, Cell target)
        {
            var path = L.Path(CellOf(c), target);
            if (path == null) return false;
            c.path = path; c.pathIdx = 0;
            return true;
        }

        /// <summary>Advance along path. True when arrived.</summary>
        bool Move(Customer c, float speed)
        {
            if (c.path == null || c.pathIdx >= c.path.Count) return true;
            float step = speed * Tick;
            while (step > 0 && c.pathIdx < c.path.Count)
            {
                var t = c.path[c.pathIdx];
                float dx = t.x - c.x, dy = t.y - c.y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist <= step) { c.x = t.x; c.y = t.y; step -= dist; c.pathIdx++; }
                else { c.x += dx / dist * step; c.y += dy / dist * step; step = 0; }
            }
            return c.pathIdx >= c.path.Count;
        }

        bool CanAffordSomething(Customer c)
        {
            float left = c.budget - c.spent;
            foreach (var m in S.menu)
                if (m.onMenu && m.stock > 0 && Price(m) <= left) return true;
            return false;
        }

        void Decide(Customer c)
        {
            if (c.state == CState.Leaving || c.state == CState.Gone) return;
            bool closing = T >= LastOrdersAt;
            float stayed = T - c.arrivedAt;

            if (T >= CloseAt) { Leave(c, false); return; }
            if (c.bladder > 0.8f)
            {
                if (GoToilet(c)) return;
            }
            bool wantsDrink = c.drink == null && !closing;
            if (wantsDrink && (stayed > c.maxStay || !CanAffordSomething(c)))
            {
                if (!CanAffordSomething(c) && c.spent < 3 && S.menu.Exists(m => m.onMenu && m.stock > 0)) { c.Mark(Note.Pricey); }
                Leave(c, false); return;
            }
            if (wantsDrink && BarPoints.Count > 0 && (c.thirst > 0.35f || c.drinks == 0))
            {
                GoToBar(c); return;
            }
            if (c.drink == null && closing) { Leave(c, false); return; }

            if (c.fun < 0.35f && c.def.Games > 0.4f && R.Chance(c.def.Games * 0.6f) && GoGame(c)) return;
            Settle(c);
        }

        void GoToBar(Customer c)
        {
            BarPoint best = null; float bestScore = float.MaxValue;
            var here = CellOf(c);
            foreach (var bp in BarPoints)
            {
                float d = L.Distance(here, bp.cell);
                if (d >= 999) continue;
                float score = bp.queue.Count * 4f + d * 0.3f;
                if (score < bestScore) { bestScore = score; best = bp; }
            }
            if (best == null) { Settle(c); return; }
            c.barPoint = BarPoints.IndexOf(best);
            c.state = CState.ToBar;
            WalkTo(c, best.cell);
        }

        void Settle(Customer c)
        {
            // Back to their seat if they have one, otherwise find one, otherwise stand.
            if (c.seatSlot < 0) ClaimSeat(c);
            if (c.seatSlot >= 0)
            {
                var it = S.Item(c.seatItem);
                var target = ReachableApproach(it, CellOf(c));
                if (target.HasValue)
                {
                    c.state = CState.ToSeat;
                    if (!WalkTo(c, target.Value)) c.state = CState.Seated;
                    return;
                }
                // Can't get to that seat: give it up and stand instead.
                if (_seats.TryGetValue(c.seatItem, out var arr) && arr[c.seatSlot] == c) arr[c.seatSlot] = null;
                c.seatSlot = -1;
            }
            c.state = CState.Standing;
            c.standCell = _standSpots.Count > 0 ? _standSpots[R.Range(0, _standSpots.Count)] : L.Door;
            WalkTo(c, c.standCell);
            if (T - c.arrivedAt > 15) c.Mark(Note.NoSeat);
        }

        /// <summary>The approach cell with the shortest walk, or null if none can be reached.</summary>
        Cell? ReachableApproach(PlacedItem it, Cell from)
        {
            if (it == null) return null;
            Cell? best = null; float bd = float.MaxValue;
            foreach (var c in L.Approaches(it))
            {
                float d = L.Distance(from, c);
                if (d < bd && d < 999) { bd = d; best = c; }
            }
            return best;
        }

        Cell Nearest(List<Cell> cells, Cell from)
        {
            if (cells.Count == 0) return from;
            Cell best = cells[0]; float bd = float.MaxValue;
            foreach (var c in cells)
            {
                float d = Math.Abs(c.x - from.x) + Math.Abs(c.y - from.y);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        void ClaimSeat(Customer c)
        {
            int bestItem = 0, bestSlot = -1; float bestScore = float.MinValue;
            foreach (var kv in _seats)
            {
                var it = S.Item(kv.Key);
                var d = Content.Item(it.id);
                for (int i = 0; i < kv.Value.Length; i++)
                {
                    if (kv.Value[i] != null) continue;
                    float score = d.Comfort * (c.seg == Seg.YoungPros ? 1.5f : 1f) + R.Range(0f, 4f);
                    // groups: prefer tables with people of the same segment
                    foreach (var o in kv.Value) if (o != null && o.seg == c.seg) score += 2;
                    if (score > bestScore) { bestScore = score; bestItem = kv.Key; bestSlot = i; }
                    break; // one free slot per item is enough to score it
                }
            }
            if (bestSlot >= 0)
            {
                c.seatItem = bestItem; c.seatSlot = bestSlot;
                _seats[bestItem][bestSlot] = c;
            }
        }

        bool GoGame(Customer c)
        {
            foreach (var kv in _users)
            {
                var it = S.Item(kv.Key);
                var d = Content.Item(it.id);
                if (d.Fn != ItemFn.Games || kv.Value.Count >= d.Capacity) continue;
                var spot = ReachableApproach(it, CellOf(c));
                if (!spot.HasValue) continue;
                kv.Value.Add(c);
                c.gameItem = it.uid;
                c.state = CState.ToGame;
                WalkTo(c, spot.Value);
                return true;
            }
            return false;
        }

        bool GoToilet(Customer c)
        {
            int best = 0; int bestQ = int.MaxValue;
            foreach (var kv in _toiletQueue)
            {
                if (_blockedToilets.Contains(kv.Key) && _toiletQueue.Count > 1) continue;
                if (kv.Value.Count < bestQ) { bestQ = kv.Value.Count; best = kv.Key; }
            }
            if (best == 0)
            {
                c.Mark(Note.NoToilet);
                c.mood -= 25;
                Think(c, "There are no toilets?!");
                Leave(c, false);
                return true;
            }
            var it = S.Item(best);
            var spot = ReachableApproach(it, CellOf(c));
            if (!spot.HasValue) return false;
            c.toiletItem = best;
            c.state = CState.ToToilet;
            WalkTo(c, spot.Value);
            return true;
        }

        void Leave(Customer c, bool walkout)
        {
            if (c.state == CState.Leaving || c.state == CState.Gone) return;
            ReleaseAll(c);
            if (walkout)
            {
                Walkouts++;
                c.moodSum += c.mood * 3; c.moodSamples += 3; // a walkout drags the visit down
            }
            if (c.drink != null) { DropGlass(c); c.drink = null; }
            c.state = CState.Leaving;
            if (!WalkTo(c, L.Outside)) { c.state = CState.Gone; Finalise(c); }
        }

        void ReleaseAll(Customer c)
        {
            foreach (var a in Staff)
            {
                if (a.serving != c) continue;
                a.serving = null;
                a.workLeft = 0;
                if (a.barPoint >= 0 && a.barPoint < BarPoints.Count) BarPoints[a.barPoint].bartender = null;
                _washing.Add((T + 1, 1)); // the glass goes back
                if (c.state == CState.Ordering) c.drink = null;
            }
            if (c.barPoint >= 0 && c.barPoint < BarPoints.Count) BarPoints[c.barPoint].queue.Remove(c);
            c.barPoint = -1;
            if (c.seatSlot >= 0 && _seats.TryGetValue(c.seatItem, out var arr) && arr[c.seatSlot] == c) arr[c.seatSlot] = null;
            c.seatSlot = -1;
            if (c.gameItem != 0 && _users.TryGetValue(c.gameItem, out var g)) g.Remove(c);
            c.gameItem = 0;
            if (c.toiletItem != 0)
            {
                if (_users.TryGetValue(c.toiletItem, out var u)) u.Remove(c);
                if (_toiletQueue.TryGetValue(c.toiletItem, out var q)) q.Remove(c);
            }
            c.toiletItem = 0;
            if (c.rowdy) { c.rowdy = false; foreach (var i in Incidents) if (i.customer == c) i.resolved = true; }
        }

        void UpdateCustomer(Customer c)
        {
            const float walk = 2.4f; // cells per minute
            if (c.thoughtTime > 0) c.thoughtTime -= Tick;
            if (c.drink != null && c.state != CState.Ordering)
            {
                c.drinkLeft -= Tick;
                if (c.drinkLeft <= 0) FinishDrink(c);
            }
            c.thirst = Math.Min(1f, c.thirst + Tick * (c.drink == null ? 0.02f : 0.002f));
            c.bladder = Math.Min(1f, c.bladder + Tick * 0.0025f);

            switch (c.state)
            {
                case CState.ToBar:
                    if (Move(c, walk))
                    {
                        if (c.barPoint < 0 || c.barPoint >= BarPoints.Count) { Decide(c); break; }
                        BarPoints[c.barPoint].queue.Add(c);
                        c.queueStart = T;
                        c.state = CState.Queue;
                    }
                    break;
                case CState.Queue:
                    if (T - c.queueStart > c.patience * 1.6f)
                    {
                        c.Mark(Note.GaveUpQueue);
                        c.mood -= 15;
                        Think(c, "I've been stood here for ages. Forget it.");
                        BarPoints[c.barPoint].queue.Remove(c);
                        c.barPoint = -1;
                        c.thirst = 0.2f;
                        if (c.mood < 35 || R.Chance(0.4)) Leave(c, true); else Settle(c);
                    }
                    break;
                case CState.ToSeat:
                    if (Move(c, walk)) c.state = CState.Seated;
                    break;
                case CState.Standing:
                case CState.Seated:
                    Move(c, walk);
                    if (c.drink == null) { c.timer += Tick; if (c.timer > 2f) { c.timer = 0; Decide(c); } }
                    else if (c.bladder > 0.85f) GoToilet(c);
                    else if (c.fun < 0.25f && c.def.Games > 0.4f && R.Chance(0.002)) GoGame(c);
                    break;
                case CState.ToGame:
                    if (Move(c, walk)) { c.state = CState.Playing; c.timer = R.Range(8f, 20f); }
                    break;
                case CState.Playing:
                    c.timer -= Tick;
                    c.fun = Math.Min(1f, c.fun + Tick * 0.05f);
                    if (c.timer <= 0)
                    {
                        if (_users.TryGetValue(c.gameItem, out var g)) g.Remove(c);
                        c.gameItem = 0;
                        c.Mark(Note.Games);
                        Settle(c);
                    }
                    break;
                case CState.ToToilet:
                    if (Move(c, walk))
                    {
                        _toiletQueue[c.toiletItem].Add(c);
                        c.state = CState.ToiletQueue;
                        c.timer = 0;
                    }
                    break;
                case CState.ToiletQueue:
                {
                    c.timer += Tick;
                    var it = S.Item(c.toiletItem);
                    var d = Content.Item(it.id);
                    var users = _users[c.toiletItem];
                    var q = _toiletQueue[c.toiletItem];
                    bool blocked = _blockedToilets.Contains(c.toiletItem);
                    int cap = blocked ? Math.Max(0, d.Capacity - 1) : d.Capacity;
                    if (q.Count > 0 && q[0] == c && users.Count < cap)
                    {
                        q.RemoveAt(0); users.Add(c);
                        c.state = CState.InToilet; c.timer = R.Range(1.5f, 3f);
                    }
                    else if (c.timer > 6) { c.Mark(Note.ToiletQueue); c.mood -= 0.05f; }
                    if (c.timer > 20) { q.Remove(c); c.bladder = 0.3f; Think(c, "That toilet queue is ridiculous."); Leave(c, true); }
                    break;
                }
                case CState.InToilet:
                    c.timer -= Tick;
                    if (c.timer <= 0)
                    {
                        _users[c.toiletItem].Remove(c);
                        c.bladder = 0;
                        if (!_blockedToilets.Contains(c.toiletItem) && R.Chance(0.007)) BlockToilet(c.toiletItem);
                        c.toiletItem = 0;
                        Settle(c);
                    }
                    break;
                case CState.Leaving:
                    if (Move(c, walk * 1.2f)) { c.state = CState.Gone; Finalise(c); }
                    break;
            }
        }

        void FinishDrink(Customer c)
        {
            DropGlass(c);
            // Spills happen when finishing (and more when lively or rowdy).
            float p = 0.012f * c.def.RowdyBase * (V.Axes[Axis.Energy] / 55f) * (c.rowdy ? 3f : 1f);
            if (R.Chance(p)) AddSpill(CellOf(c));
            c.drink = null;
            c.thirst = Math.Max(c.thirst, 0.5f);
            if (c.state == CState.Seated || c.state == CState.Standing) Decide(c);
        }

        void DropGlass(Customer c)
        {
            // Locals often bring their glass back.
            if (c.seg == Seg.Locals && R.Chance(0.5)) { _washing.Add((T + 3, 1)); return; }
            Cell at; int item = 0;
            if (c.seatSlot >= 0) { item = c.seatItem; var it = S.Item(item); at = new Cell(it.x, it.y); }
            else at = CellOf(c);
            var inc = Incidents.Find(i => !i.resolved && i.kind == IncidentKind.Glasses && (item != 0 ? i.itemUid == item : i.cell.Equals(at)));
            if (inc == null)
            {
                inc = new Incident { id = _nextIncident++, kind = IncidentKind.Glasses, cell = at, itemUid = item, time = T };
                Incidents.Add(inc);
            }
            inc.count++;
        }

        void AddSpill(Cell at)
        {
            if (!L.InBounds(at)) return;
            if (Incidents.Exists(i => !i.resolved && i.kind == IncidentKind.Spill && i.cell.Equals(at))) return;
            Incidents.Add(new Incident { id = _nextIncident++, kind = IncidentKind.Spill, cell = at, time = T });
            Spills++;
            if (Spills == 1 || R.Chance(0.3)) Say("Spill! Someone needs to mop that up.", 2, at);
        }

        void BlockToilet(int uid)
        {
            _blockedToilets.Add(uid);
            var it = S.Item(uid);
            Incidents.Add(new Incident { id = _nextIncident++, kind = IncidentKind.Toilet, cell = new Cell(it.x, it.y), itemUid = uid, time = T });
            Say("A toilet's blocked. Tap it to sort it out.", 2, new Cell(it.x, it.y));
        }

        void Think(Customer c, string text) { c.thought = text; c.thoughtTime = 6f; }

        void MinuteTick(Customer c)
        {
            if (c.state == CState.Leaving) return;
            var d = c.def;
            int s = (int)c.seg;
            float target = 50;
            target += (V.SegMatch[s] - 0.5f) * 50f;

            bool seated = c.state == CState.Seated;
            if (seated && c.seatSlot >= 0)
            {
                var it = S.Item(c.seatItem);
                target += Content.Item(it.id).Comfort * 1.3f;
                if (Content.Item(it.id).Comfort >= 7) c.Mark(Note.ComfySeat);
            }
            else if (c.state == CState.Standing)
                target -= c.seg == Seg.SportsFans || c.seg == Seg.Students ? 3 : 7;

            var genre = Content.Genre(S.music);
            float like = genre.SegLikes[s];
            target += like * 12f;
            if (like > 0.4f) c.Mark(Note.LovedMusic);
            if (like < -0.3f) c.Mark(Note.HatedMusic);

            if (V.Screens > 0) { target += d.Screens * Math.Min(V.Screens, 3) * 3f; if (d.Screens > 1) c.Mark(Note.Screens); }
            else if (c.seg == Seg.SportsFans) target -= 8;
            target += d.Games * Math.Min(V.Games, 4) * 1.5f;
            if (c.state == CState.Playing) target += 10;
            c.fun = Math.Max(0, c.fun - 0.006f);

            if (V.ActiveTheme != null && d.ThemeAffinity.TryGetValue(V.ActiveTheme.Def.Id, out float aff)) target += aff * 10f;

            // Customers judge the whole visit, not one glance: a brief dip or a spill they walk past
            // doesn't make the place "dirty", sitting in a mess for a while does.
            float clean = CleanlinessNow;
            target += (clean - 65f) * 0.3f;
            if (clean < GrimeThreshold) c.grime += (GrimeThreshold - clean) / 15f;

            float occ = Inside / (float)Capacity;
            float crowdPref = (d.VibePref[Axis.Energy] - 50f) / 50f;
            target += (occ - 0.35f) * 18f * crowdPref - Math.Max(0, occ - 0.85f) * 30f;
            if (occ > 0.9f && (c.crushed += 1f) >= 15f) c.Mark(Note.Crowded); // a sustained crush, not one busy moment

            if (c.watching && EventPhase >= 1 && EventPhase <= 3)
            {
                float affE = Economy.EventAffinity(d, Event.kind);
                target += (EventPerf - 45f) / 3.5f * affE;
            }

            // Rowdy people nearby
            foreach (var o in Customers)
            {
                if (!o.rowdy || o == c) continue;
                if (Math.Abs(o.x - c.x) + Math.Abs(o.y - c.y) < 4) { target -= 10; c.Mark(Note.RowdyNearby); break; }
            }
            foreach (var i in Incidents)
            {
                if (i.resolved || i.kind != IncidentKind.Spill) continue;
                if (Math.Abs(i.cell.x - c.x) + Math.Abs(i.cell.y - c.y) < 2) { target -= 6; c.grime += 1.5f; break; }
            }
            if (c.grime >= GrimeToComplain) c.Mark(Note.Dirty);

            if (c.regular != null) target += 5;
            if (_busker) target += 4;
            if (S.GetFlag("dog") == 1) { target += 3; if (R.Chance(0.008)) { c.Mark(Note.Dog); Think(c, "Is that a dog? Best bar ever."); } }
            foreach (var a in Staff) if (a.present && a.m != null && a.m.HasTrait("life_soul")) { target += 3; break; }
            if (HappyHour && d.PriceSensitivity > 0.5f) { target += 4; c.Mark(Note.HappyHour); }

            c.mood += (target - c.mood) * 0.08f;
            c.mood = Math.Max(0, Math.Min(100, c.mood));
            c.moodSum += c.mood; c.moodSamples++;

            if (V.SegMatch[s] > 0.75f) c.Mark(Note.LovedVibe);
            if (V.SegMatch[s] < 0.35f) c.Mark(Note.HatedVibe);

            // Occasional thoughts
            if (c.thoughtTime <= 0 && R.Chance(0.03)) Think(c, PickThought(c));

            // Rowdiness
            if (!c.rowdy && c.alcoholic >= 3)
            {
                float p = 0.00022f * (c.alcoholic - 2) * d.RowdyBase * (V.Axes[Axis.Energy] / 55f)
                          * (S.responsibleService ? 0.5f : 1f) * (S.difficulty == 0 ? 0.5f : S.difficulty == 2 ? 1.5f : 1f)
                          * (DoorStaffOn ? 0.55f : 1f); // a visible bouncer keeps people in line
                if (R.Chance(p)) MakeRowdy(c);
            }
            if (c.rowdy)
            {
                c.rowdyFor += 1;
                if (c.rowdyFor > 20) { Say($"{FirstName(c)} stormed off after getting too rowdy.", 0); Leave(c, false); }
            }

            if (c.mood < 14 && T - c.arrivedAt > 10) { Think(c, "I'm off. This place is grim."); Leave(c, true); }
        }

        static string FirstName(Customer c) => c.name.Split(' ')[0];

        // Door staff stand just inside the entrance between jobs.
        void WalkToDoor(StaffAgent a, float walk)
        {
            var spot = new Cell(L.Door.x, Math.Min(S.gridH - 1, L.Door.y + 1));
            float dx = spot.x - a.x, dy = spot.y - a.y, dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.05f) return;
            float step = Math.Min(dist, walk * Tick);
            a.x += dx / dist * step; a.y += dy / dist * step;
        }

        void MakeRowdy(Customer c)
        {
            c.rowdy = true; c.rowdyFor = 0; RowdyCount++;
            Incidents.Add(new Incident { id = _nextIncident++, kind = IncidentKind.Rowdy, customer = c, cell = CellOf(c), time = T });
            Say($"{FirstName(c)} is getting a bit loud. Tap them to have a word.", 2, CellOf(c));
        }

        string PickThought(Customer c)
        {
            if (c.Has(Note.OutOfStock)) return "They've run out of my usual.";
            if (c.Has(Note.Dirty)) return "This place could do with a clean.";
            if (c.state == CState.Standing) return "Wish I could find a seat.";
            if (c.Has(Note.HatedMusic)) return "Who chose this music?";
            if (c.Has(Note.LovedDrink)) return $"This {c.drink?.Name ?? "drink"} is incredible.";
            if (c.watching && EventPerf > 65) return Event.kind == 0 ? "We're definitely winning this round." : Event.kind == 1 ? "I can't breathe, this is so funny." : "That last act gave me chills.";
            if (c.Has(Note.LovedVibe)) return "Love the feel of this place.";
            var complaint = VibeCalc.Complaint(V.Axes, c.def);
            if (complaint != null) return complaint + ".";
            if (c.Has(Note.Bargain)) return "Bargain prices!";
            return c.mood > 65 ? "Good night, this." : c.mood < 40 ? "Not feeling it tonight." : "It's alright here.";
        }

        void CheckQueues()
        {
            int longest = 0;
            foreach (var bp in BarPoints) longest = Math.Max(longest, bp.queue.Count);
            if (longest >= 5 && T - _queueMsgAt > 40)
            {
                int bartenders = 0; foreach (var a in Staff) if (a.present && ((!a.IsOwner && a.Duty == 0) || (a.IsOwner && OwnerBehindBar))) bartenders++;
                Say(bartenders >= BarPoints.Count ? "Every serving point is busy. You need another bar counter to serve more people at once."
                    : "The bar queue is getting long. More bartenders, or step in yourself?", 2);
                _queueMsgAt = T;
            }
        }

        // =====================================================================================
        //  Drinks & serving
        // =====================================================================================

        public static float DrinkQuality(GameState s, DrinkDef d)
        {
            var sup = Content.Supplier(d.Supplier);
            float q = sup.Quality;
            bool draught = d.Cat == DrinkCat.Lager || d.Cat == DrinkCat.Ale || d.Cat == DrinkCat.Stout || d.Cat == DrinkCat.Craft || d.Cat == DrinkCat.Cider;
            if (draught) q = q * 0.6f + s.lineQuality * 0.4f;
            return q;
        }

        DrinkDef ChooseDrink(Customer c, out string problem)
        {
            problem = null;
            var d = c.def;
            float left = c.budget - c.spent;
            var options = new List<DrinkDef>();
            var weights = new List<float>();
            bool wantedButOut = false, wantedButPricey = false;
            // How many drinks of each category are on the menu, to split category weight.
            var catCount = new int[10];
            foreach (var m in S.menu) if (m.onMenu) catCount[(int)Content.Drink(m.id).Cat]++;
            foreach (var m in S.menu)
            {
                if (!m.onMenu) continue;
                var dd = Content.Drink(m.id);
                float w = d.DrinkPrefs[(int)dd.Cat] / Math.Max(1, catCount[(int)dd.Cat]);
                if (dd.Cat == DrinkCat.AlcoholFree) w *= 1f + c.alcoholic * 0.25f;
                w *= Economy.SeasonalDrinkMult(S, dd.Cat);
                if (c.regular != null && c.regular.favDrink == dd.Id) w *= 4f;
                if (V.ActiveTheme != null && Array.IndexOf(dd.Themes, V.ActiveTheme.Def.Id) >= 0) w *= 1.3f;
                float ratio = m.price / dd.DefaultPrice;
                w *= (float)Math.Exp(-d.PriceSensitivity * Math.Max(0, ratio - 1f) * 3f);
                if (S.responsibleService && dd.Alcoholic && c.alcoholic >= 5) continue;
                options.Add(dd); weights.Add(w);
            }
            // What would they ideally have? If that's unavailable, note it and pick again from what's possible.
            int first = R.WeightedIndex(weights);
            if (first >= 0)
            {
                var fm = S.Menu(options[first].Id);
                if (fm.stock <= 0) { wantedButOut = true; fm.missedTonight++; }
                else if (Price(fm) > left) wantedButPricey = true;
            }
            for (int i = options.Count - 1; i >= 0; i--)
            {
                var om = S.Menu(options[i].Id);
                if (om.stock <= 0 || Price(om) > left) { options.RemoveAt(i); weights.RemoveAt(i); }
            }
            if (wantedButOut) c.Mark(Note.OutOfStock);
            int idx = R.WeightedIndex(weights);
            if (idx < 0)
            {
                problem = wantedButOut ? "out" : wantedButPricey ? "pricey" : "nothing";
                return null;
            }
            // If their first choice was out of stock they take something else, grumbling.
            if (wantedButOut && R.Chance(0.5)) { c.mood -= 4; }
            return options[idx];
        }

        float BartenderSpeed(StaffAgent a, DrinkDef d)
        {
            var m = a.m;
            float speedAttr = m.speed + (m.HasTrait("grumpy") ? 3 : 0);
            float f = 1.2f - speedAttr * 0.028f; // 10 -> 0.92x prep time, 20 -> 0.64x
            if (d.Cat == DrinkCat.Cocktail) f *= 1.3f - m.skill * 0.025f;
            if (d.Cat == DrinkCat.Cocktail && m.HasTrait("cocktail_nerd")) f *= 0.65f;
            if (m.HasTrait("gossip")) f *= 1.1f;
            int q = 0; foreach (var bp in BarPoints) q += bp.queue.Count;
            if (q > 6 && !m.HasTrait("calm")) f *= 1.15f;
            float fatigue = T / 360f * (1.2f - m.stamina * 0.04f) * (Economy.Heatwave(S) ? 1.4f : 1f);
            f *= 1f + Math.Max(0, fatigue) * 0.25f;
            f *= 1.15f - m.morale / 100f * 0.25f;
            return Math.Max(0.4f, f);
        }

        void UpdateStaff(StaffAgent a)
        {
            if (!a.present)
            {
                if (T >= a.arriveAt)
                {
                    a.present = true;
                    if (a.arriveAt > 0) Say($"{a.m.name} finally turns up, 45 minutes late.", 2);
                }
                else return;
            }
            if (a.IsOwner || a.Duty == Roles.Bartender) UpdateBartender(a);
            else UpdateFloor(a, a.Duty == Roles.Door);
        }

        void UpdateBartender(StaffAgent a)
        {
            if (a.serving != null)
            {
                a.busyMinutes += Tick;
                a.workLeft -= Tick;
                if (a.workLeft <= 0) CompleteServe(a);
                return;
            }
            if (a.IsOwner && !OwnerBehindBar) return;
            // Pick the bar point whose front customer has waited longest and isn't being served.
            BarPoint best = null; float oldest = float.MaxValue;
            foreach (var bp in BarPoints)
            {
                if (bp.queue.Count == 0 || bp.bartender != null) continue;
                var front = bp.queue[0];
                if (front.state != CState.Queue) continue;
                if (front.queueStart < oldest) { oldest = front.queueStart; best = bp; }
            }
            if (best == null)
            {
                a.activity = "Polishing glasses";
                a.taskTimer -= Tick;
                if (a.taskTimer <= 0)
                {
                    a.taskTimer = 0.6f;
                    foreach (var inc in Incidents)
                    {
                        if (inc.resolved || inc.kind != IncidentKind.Glasses || inc.count <= 0) continue;
                        bool near = false;
                        foreach (var bp in BarPoints) if (Math.Abs(bp.cell.x - inc.cell.x) + Math.Abs(bp.cell.y - inc.cell.y) <= 4) { near = true; break; }
                        if (!near) continue;
                        inc.count--; _washing.Add((T + 3, 1));
                        if (inc.count <= 0) inc.resolved = true;
                        a.activity = "Collecting glasses";
                        break;
                    }
                }
                if (BarPoints.Count > 0 && a.barPoint < 0) { a.barPoint = 0; a.x = BarPoints[0].sx; a.y = BarPoints[0].sy; }
                return;
            }
            var c = best.queue[0];
            var drink = ChooseDrink(c, out string problem);
            if (drink == null)
            {
                best.queue.RemoveAt(0);
                c.barPoint = -1;
                if (problem == "out")
                {
                    Think(c, "They've run out of everything I like!");
                    c.mood -= 10;
                }
                else if (problem == "pricey") { c.Mark(Note.Pricey); Think(c, "How much?! No thanks."); c.mood -= 6; }
                else if (S.responsibleService && c.alcoholic >= 5) { c.Mark(Note.Refused); Think(c, "Fair enough, I'll have a water."); c.alcoholic = 0; c.mood -= 2; }
                c.thirst = 0;
                if (c.drinks == 0 || R.Chance(0.5)) Leave(c, problem == "out"); else Settle(c);
                return;
            }
            best.bartender = a;
            a.barPoint = BarPoints.IndexOf(best);
            a.x = best.sx; a.y = best.sy;
            a.serving = c;
            c.drink = drink;
            c.state = CState.Ordering;
            a.workLeft = drink.PrepMinutes * BartenderSpeed(a, drink);
            if (CleanGlasses <= 0)
            {
                a.workLeft += 0.9f;
                if (T - _noGlassMsgAt > 30) { Say("Out of clean glasses! Bartenders are washing as they go.", 2); _noGlassMsgAt = T; }
            }
            else CleanGlasses--;
            a.activity = "Making a " + drink.Name;
        }

        void CompleteServe(StaffAgent a)
        {
            var c = a.serving;
            a.serving = null;
            if (a.barPoint >= 0 && a.barPoint < BarPoints.Count)
            {
                var bp = BarPoints[a.barPoint];
                bp.bartender = null;
                bp.queue.Remove(c);
            }
            if (c == null || c.state != CState.Ordering) return;
            c.barPoint = -1;
            var m = S.Menu(c.drink.Id);
            if (m == null || m.stock <= 0)
            {
                c.drink = null;
                _washing.Add((T, 1)); // unused glass goes back on the shelf
                Settle(c);
                return;
            }
            float price = Price(m);
            m.stock--;
            m.soldTonight++;
            if (m.stock == 0 && !_warnedOut.Contains(m.id))
            {
                _warnedOut.Add(m.id);
                StockOuts.Add(c.drink.Name);
                Say($"{c.drink.Name} has run out!", 2);
            }
            Takings += price;
            Cogs += c.drink.UnitCost * Economy.CostMult(S);
            c.spent += price;
            c.drinks++;
            Served++;
            a.served++;
            if (!a.IsOwner) a.m.xp++;
            if (c.drink.Alcoholic) c.alcoholic++;
            c.bladder += c.drink.Alcoholic ? 0.28f : 0.2f;
            c.thirst = 0;
            c.drinkLeft = R.Range(16f, 30f) * (c.drink.Cat == DrinkCat.Spirit || c.drink.Cat == DrinkCat.Cocktail ? 0.8f : 1f);

            // Reactions
            float wait = T - c.queueStart;
            // Every slow drink hurts their mood, but they only call the service "slow" after one really long wait
            // or several slowish ones, so a single unlucky round doesn't define the night (like grime, task T5b).
            if (wait > c.patience * 0.9f) { c.mood -= Math.Min(15, wait * 0.8f); Think(c, $"Waited {wait:0} minutes for that."); }
            c.slowWait += Math.Max(0, wait - c.patience * 0.6f);
            if (wait > c.patience * 1.25f || c.slowWait > c.patience * 0.9f) c.Mark(Note.WaitedLong);
            float ratio = m.price / c.drink.DefaultPrice;
            if (HappyHour) ratio *= 1f - S.happyHourDiscount / 100f;
            if (ratio > 1.15f)
            {
                c.mood -= (ratio - 1f) * c.def.PriceSensitivity * 25f;
                if (ratio > 1.25f && R.Chance(c.def.PriceSensitivity)) { c.Mark(Note.Pricey); Think(c, $"£{price:0.00} for a {c.drink.Name}? Steep."); }
            }
            else if (ratio < 0.9f) { c.Mark(Note.Bargain); c.mood += 3; }
            float q = DrinkQuality(S, c.drink);
            if (c.drink.Cat == DrinkCat.Cocktail && !a.IsOwner) q += (a.m.skill - 10) * 1.5f + (a.m.HasTrait("cocktail_nerd") ? 10 : 0);
            c.mood += (q - 60f) / 8f;
            if (q > 75 && R.Chance(0.5)) { c.Mark(Note.LovedDrink); Think(c, $"This {c.drink.Name} is excellent."); }
            if (q < 45 && R.Chance(0.5)) { c.Mark(Note.BadDrink); Think(c, $"That {c.drink.Name} tasted a bit off."); }
            if (!a.IsOwner)
            {
                int charm = a.m.charm - (a.m.HasTrait("grumpy") ? 6 : 0);
                c.mood += (charm - 10) * 0.4f;
                if (charm >= 14 && R.Chance(0.1)) c.Mark(Note.FriendlyStaff);
                if (charm <= 6 && R.Chance(0.12)) c.Mark(Note.GrumpyStaff);
                if (a.m.HasTrait("butterfingers") && R.Chance(0.05)) { AddSpill(BarPoints[Math.Max(0, a.barPoint)].cell); Say($"{a.m.name} dropped a glass. Again.", 0); }
            }
            Settle(c);
        }

        /// Door security on duty tonight (task S1): fewer customers turn rowdy, and rowdy ones are dealt with first.
        public bool DoorStaffOn => Staff.Exists(x => x.present && !x.IsOwner && x.Duty == Roles.Door);

        /// Floor staff take any job; door staff (doorOnly) only deal with rowdy customers and otherwise mind the door.
        void UpdateFloor(StaffAgent a, bool doorOnly = false)
        {
            const float walk = 2.8f;
            if (a.task == null)
            {
                a.activity = doorOnly ? "Minding the door" : "Looking for jobs";
                if (doorOnly) WalkToDoor(a, walk);
                Incident best = null; float bestScore = float.MaxValue;
                foreach (var i in Incidents)
                {
                    if (i.resolved || i.claimed || T < i.retryAt) continue;
                    if (doorOnly && i.kind != IncidentKind.Rowdy) continue;
                    float pri = i.kind == IncidentKind.Rowdy ? 0 : i.kind == IncidentKind.Spill ? 5 : i.kind == IncidentKind.Toilet ? 8 : (i.count >= 3 ? 10 : 18);
                    Cell at = i.kind == IncidentKind.Rowdy && i.customer != null ? new Cell((int)Math.Round(i.customer.x), (int)Math.Round(i.customer.y)) : i.cell;
                    float score = pri + Math.Abs(at.x - a.x) + Math.Abs(at.y - a.y);
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                if (best == null) return;
                best.claimed = true;
                a.task = best;
                a.taskTimer = -1;
                Cell target = TaskCell(best);
                a.path = L.Path(new Cell((int)Math.Round(a.x), (int)Math.Round(a.y)), target);
                a.pathIdx = 0;
                if (a.path == null) { best.claimed = false; best.retryAt = T + 10; a.task = null; }
                return;
            }
            var t = a.task;
            if (t.resolved) { a.task = null; return; }
            if (a.taskTimer < 0)
            {
                a.activity = t.kind switch
                {
                    IncidentKind.Spill => "Heading to a spill",
                    IncidentKind.Glasses => "Collecting glasses",
                    IncidentKind.Toilet => "Off to unblock a toilet",
                    _ => "Going to have a word",
                };
                // walk
                float step = walk * Tick;
                while (step > 0 && a.path != null && a.pathIdx < a.path.Count)
                {
                    var c = a.path[a.pathIdx];
                    float dx = c.x - a.x, dy = c.y - a.y, dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= step) { a.x = c.x; a.y = c.y; step -= dist; a.pathIdx++; }
                    else { a.x += dx / dist * step; a.y += dy / dist * step; step = 0; }
                }
                if (a.path == null || a.pathIdx >= a.path.Count)
                    a.taskTimer = t.kind == IncidentKind.Spill ? 1.5f : t.kind == IncidentKind.Toilet ? 4f : t.kind == IncidentKind.Rowdy ? 2f : 0.4f + t.count * 0.15f;
                return;
            }
            a.taskTimer -= Tick;
            a.busyMinutes += Tick;
            if (a.taskTimer > 0) return;
            switch (t.kind)
            {
                case IncidentKind.Glasses: _washing.Add((T + 3, t.count)); t.count = 0; break;
                case IncidentKind.Toilet: _blockedToilets.Remove(t.itemUid); break;
                case IncidentKind.Rowdy:
                    if (t.customer != null && t.customer.rowdy)
                    {
                        float p = 0.45f + a.m.charm / 40f + (a.m.HasTrait("life_soul") ? 0.2f : 0) + (a.Duty == Roles.Door ? 0.2f : 0);
                        if (R.Chance(p)) { t.customer.rowdy = false; Say($"{a.m.name} calmed {FirstName(t.customer)} down.", 1); }
                        else { t.customer.Mark(Note.ShownDoor); Say($"{a.m.name} showed {FirstName(t.customer)} the door.", 0); Leave(t.customer, false); }
                    }
                    break;
            }
            t.resolved = true;
            a.task = null;
        }

        Cell TaskCell(Incident i)
        {
            if (i.kind == IncidentKind.Rowdy && i.customer != null) return new Cell((int)Math.Round(i.customer.x), (int)Math.Round(i.customer.y));
            if (i.itemUid != 0)
            {
                var it = S.Item(i.itemUid);
                if (it != null) { var app = L.Approaches(it); if (app.Count > 0) return app[0]; } // UpdateFloor backs off if unreachable
            }
            return L.Walkable(i.cell) ? i.cell : L.Door;
        }

        // =====================================================================================
        //  Events
        // =====================================================================================

        static readonly string[] OpenMicActs =
        {
            "A nervous poet", "A ukulele duo", "A retired postman with a banjo", "A teenage rapper", "A woman doing bird impressions",
            "A folk singer", "A stand-up first-timer", "A bartender's cousin", "A beatboxer", "An accordion player",
        };

        public float EventEndTime => EventStart < 0 ? 240 : EventStart + 120;

        void UpdateEvent()
        {
            if (Event == null || EventPhase == 4) return;
            var act = Event.act;
            if (EventPhase == 0)
            {
                if (EventStart < 0 && T >= 110)
                {
                    bool late = R.Next() * 100 > act.reliability;
                    EventStart = late ? 150 : 120;
                    float perf = act.quality + R.Gaussian() * act.volatility * 35f;
                    if (V.HasSpeakers) perf += 6;
                    if (Staff.Exists(s => s.present && s.m != null && s.m.HasTrait("secret_comic"))) perf += 8;
                    if (late) perf -= 8;
                    EventPerf = Math.Max(5, Math.Min(100, perf));
                    if (late) Say($"{act.name} is running late. The crowd is getting restless...", 2);
                }
                if (EventStart >= 0 && T >= EventStart)
                {
                    EventPhase = 1;
                    int joined = 0;
                    foreach (var c in Customers)
                    {
                        if (!c.Inside || c.state == CState.Leaving) continue;
                        float aff = Economy.EventAffinity(c.def, Event.kind);
                        if (R.Chance(aff)) { c.watching = true; joined++; if (Event.kind != 1 && Event.ticket > 0) TicketIncome += Event.ticket; }
                    }
                    Say(Event.kind == 0 ? $"Pens at the ready! The quiz begins with {joined} players." : Event.kind == 1 ? $"Lights down. Comedy Night begins for a crowd of {joined}." : $"The open mic sign-up sheet is full. {joined} people are watching.", 3);
                }
                return;
            }
            float into = T - EventStart;
            if (Event.kind == 0)
            {
                int round = (int)(into / 20f);
                if (round > _quizRound && round <= 6)
                {
                    _quizRound = round;
                    if (round < 6)
                        Say($"Quiz round {round} done. {Content.QuizTeams[(S.day + round * 3) % Content.QuizTeams.Length]} {(EventPerf > 60 ? "are loving it." : EventPerf > 40 ? "lead narrowly." : "are arguing with the quizmaster.")}", 3);
                    foreach (var c in Customers) if (c.watching) c.thirst = Math.Max(c.thirst, 0.6f);
                }
                if (into >= 120) EndEvent();
            }
            else if (Event.kind == 2)
            {
                // Open mic: a new act every 20 minutes, each a fresh gamble.
                int slot = (int)(into / 20f);
                if (slot > _quizRound && slot <= 6)
                {
                    _quizRound = slot;
                    if (slot < 6)
                    {
                        float roll = EventPerf + R.Gaussian() * 22f;
                        string who = R.Pick(OpenMicActs);
                        if (roll > 80) { Say($"{who} brings the house down. Is that a talent scout?", 1); EventPerf = Math.Min(100, EventPerf + 3); if (roll > 92) Moments.Add($"An unknown {who.Replace("A ", "").Replace("An ", "")} stole the show at open mic."); }
                        else if (roll > 50) Say($"{who} gets a warm round of applause.", 3);
                        else { Say($"{who} forgets the words halfway through. Supportive clapping.", 2); EventPerf = Math.Max(5, EventPerf - 2); }
                    }
                    foreach (var c in Customers) if (c.watching) c.thirst = Math.Max(c.thirst, 0.6f);
                }
                if (into >= 120) EndEvent();
            }
            else
            {
                if (EventPhase == 1 && into >= 50)
                {
                    EventPhase = 2;
                    Say("Interval! Expect a rush at the bar.", 3);
                    foreach (var c in Customers) if (c.watching && c.drink == null) c.thirst = 1f;
                }
                if (EventPhase == 2 && into >= 70)
                {
                    EventPhase = 3;
                    Say($"{Event.act.name} takes the stage for the headline set.", 3);
                }
                if (EventPhase == 3 && !_heckled && into >= 85)
                {
                    _heckled = true;
                    if (R.Chance(0.35))
                    {
                        bool staffHelp = Staff.Exists(s => s.present && !s.IsOwner && s.Duty == 1);
                        if (EventPerf > 62) { Say($"A heckler pipes up... and {Event.act.name} turns it into the best bit of the night!", 1); EventPerf = Math.Min(100, EventPerf + 6); Moments.Add("A heckler got destroyed by the headliner. The room erupted."); }
                        else if (staffHelp) Say("A heckler starts up. Your floor staff have a quiet word.", 0);
                        else { Say("A heckler derails the set. Awkward.", 2); EventPerf -= 8; }
                    }
                }
                if (into >= 120) EndEvent();
            }
        }

        void EndEvent()
        {
            EventPhase = 4;
            foreach (var c in Customers)
            {
                if (!c.watching) continue;
                if (EventPerf >= 60) c.Mark(Note.LovedEvent);
                else if (EventPerf < 38) c.Mark(Note.EventBombed);
            }
            string what = Event.kind == 0 ? "quiz" : Event.kind == 1 ? "comedy night" : "open mic";
            // Going viral needs a great show AND people there to film and share it: promotion and a full room help (task T7).
            float viralChance = 0.16f + Math.Min(0.35f, Event.promo / 600f) + Math.Min(0.25f, Inside / (float)Capacity * 0.4f);
            if (EventPerf >= 90 && R.Next() < viralChance) { EventOutcome = "Viral hit"; Say($"Incredible {what}! Clips are flying round social media.", 1); }
            else if (EventPerf >= 66) { EventOutcome = "Hit"; Say($"The {what} went down a storm.", 1); }
            else if (EventPerf >= 48) { EventOutcome = "Decent"; Say($"A decent {what}. Nobody's complaining.", 0); }
            else { EventOutcome = "Flop"; Say(Event.kind == 0 ? "The quiz was a shambles. Two teams walked out." : Event.kind == 1 ? $"{Event.act.name} died on stage. Tumbleweed." : "The open mic was... brave. Very brave.", 2); }
            Moments.Add($"{Economy.EventName(Event.kind)} with {Event.act.name}: {EventOutcome.ToLowerInvariant()}.");
        }

        // =====================================================================================
        //  Player actions (called through commands)
        // =====================================================================================

        public bool ResolveIncident(int incidentId)
        {
            var i = Incidents.Find(x => x.id == incidentId && !x.resolved);
            if (i == null) return false;
            switch (i.kind)
            {
                case IncidentKind.Spill: break;
                case IncidentKind.Glasses: _washing.Add((T + 3, i.count)); break;
                case IncidentKind.Toilet: _blockedToilets.Remove(i.itemUid); Say("Sorted. Don't ask.", 1); break;
                case IncidentKind.Rowdy:
                    if (i.customer != null && i.customer.rowdy)
                    {
                        if (R.Chance(0.75)) { i.customer.rowdy = false; Say($"You had a word with {FirstName(i.customer)}. All calm.", 1); }
                        else { i.customer.Mark(Note.ShownDoor); Say($"{FirstName(i.customer)} wasn't having it, so you showed them the door.", 0); Leave(i.customer, false); }
                    }
                    break;
            }
            i.resolved = true;
            return true;
        }

        public Incident IncidentForCustomer(Customer c) => Incidents.Find(i => !i.resolved && i.customer == c);

        /// Call last orders now, with closing 30 minutes later. For a rough night: fewer drinks, fewer incidents,
        /// a few grumbles. Not before 9pm.
        public string CallLastOrdersEarly()
        {
            if (T >= LastOrdersAt) return "It's already last orders";
            if (T < 180) return "Too early: you can call it from 9pm";
            LastOrdersAt = T;
            CloseAt = T + 30f;
            foreach (var c in Customers) if (c.Inside) c.mood -= 4;
            foreach (var c in Customers) if (c.Inside && c.rowdy) { c.rowdy = false; c.mood -= 6; }
            Say($"Last orders! Doors close at {Clock(CloseAt)}.", 1);
            Moments.Add($"You called last orders early, at {Clock(T)}.");
            return null;
        }

        /// Move a staff member between the bar and the floor for the rest of tonight (task L3).
        public string SwitchDuty(StaffAgent a)
        {
            if (a == null || a.IsOwner) return "Use \"Help behind the bar\" for yourself";
            if (a.m.role == Roles.Door) return $"{a.m.name} stays on the door";
            if (!a.present) return $"{a.m.name} isn't in yet";
            if (a.serving != null) return $"{a.m.name} is mid-pour. Try again in a moment";
            if (a.Duty == 0)
            {
                if (a.barPoint >= 0 && a.barPoint < BarPoints.Count && BarPoints[a.barPoint].bartender == a) BarPoints[a.barPoint].bartender = null;
                a.barPoint = -1;
                a.duty = 1;
                Say($"{a.m.name} heads out onto the floor.", 0);
            }
            else
            {
                if (a.task != null) { a.task.claimed = false; a.task = null; }
                a.path = null;
                a.duty = 0;
                Say($"{a.m.name} jumps behind the bar.", 0);
            }
            return null;
        }

        public string StartHappyHour()
        {
            if (HappyHourUsed) return "You've already run a happy hour tonight";
            if (T >= LastOrdersAt) return "It's last orders";
            HappyHourUsed = true;
            HappyHourUntil = T + 60;
            Say($"Happy hour! {S.happyHourDiscount}% off for the next hour.", 1);
            return null;
        }

        public string CollectGlasses()
        {
            if (CollectCooldown > 0) return "You're still washing the last lot";
            int n = 0;
            foreach (var i in Incidents)
            {
                if (i.resolved || i.kind != IncidentKind.Glasses) continue;
                n += i.count; i.count = 0; i.resolved = true;
                if (n >= 14) break;
            }
            if (n == 0) return "No dirty glasses to collect";
            _washing.Add((T + 2, n));
            CollectCooldown = 4;
            Say($"You collected {n} glasses.", 0);
            return null;
        }

        public void SetOwnerBehindBar(bool on)
        {
            OwnerBehindBar = on;
            if (on && !Staff.Exists(a => a.IsOwner))
            {
                var owner = new StaffMember { uid = -1, name = "You", role = 0, speed = 9, charm = 13, skill = 8, stamina = 12, morale = 80, shirt = 0xffd166 };
                Staff.Add(new StaffAgent { m = owner, IsOwner = true, present = true, x = BarPoints.Count > 0 ? BarPoints[0].sx : L.Door.x, y = BarPoints.Count > 0 ? BarPoints[0].sy : L.Door.y });
            }
        }

        public string EmergencyDelivery(string drinkId)
        {
            var d = Content.Drink(drinkId);
            var m = S.Menu(drinkId);
            if (d == null || m == null) return "Unknown drink";
            float cost = d.CaseSize * d.UnitCost * Economy.CostMult(S) * 1.6f;
            if (T + 20 >= LastOrdersAt) return "Too late: it wouldn't arrive before last orders";
            if (S.cash < cost && S.difficulty > 0) return "Not enough cash";
            S.cash -= cost;
            EmergencySpend += cost;
            _deliveries.Add((T + 20, drinkId, d.CaseSize));
            Say($"Emergency delivery of {d.Name} ordered (£{cost:0}). Arriving in about 20 minutes.", 0);
            return null;
        }

        // =====================================================================================
        //  Departures, reviews, regulars
        // =====================================================================================

        void Finalise(Customer c)
        {
            Departed.Add(c);
            for (int n = 0; n < (int)Note.Count; n++) if (c.notes[n]) NoteCounts[n]++;
            float sat = c.Satisfaction;
            if (c.Has(Note.GaveUpQueue)) sat -= 10;
            if (c.drinks == 0) sat -= 10;
            int stars = sat >= 74 ? 5 : sat >= 62 ? 4 : sat >= 48 ? 3 : sat >= 34 ? 2 : 1;

            // Word of mouth
            float aw = S.awareness[(int)c.seg];
            float wom = stars >= 4 ? 0.0016f * (1f - aw) : stars <= 2 ? -0.0022f : 0.0003f * (1f - aw);
            S.awareness[(int)c.seg] = Math.Max(0.05f, Math.Min(1f, S.awareness[(int)c.seg] + wom));

            if (R.Chance(stars == 5 || stars == 1 ? 0.35 : 0.18))
            {
                var rv = Reviews.Write(c, stars, R, S, this);
                NewReviews.Add(rv);
            }

            // Regulars
            if (c.regular != null)
            {
                c.regular.loyalty = Math.Max(0, Math.Min(1, c.regular.loyalty + (stars - 3) * 0.08f));
                if (c.regular.loyalty <= 0.05f)
                {
                    S.regulars.Remove(c.regular);
                    Moments.Add($"Regular {c.regular.name} says they won't be back. Ouch.");
                }
            }
            else if (stars >= 4 && c.drinks > 0 && S.regulars.Count < 30 && R.Chance(0.07 + (stars == 5 ? 0.05 : 0)))
            {
                string name = c.name;
                if (R.Chance(0.25)) name = $"{R.Pick(Content.Nicknames)} {name.Split(' ')[0]}";
                var reg = new Regular
                {
                    uid = S.nextUid++, name = name, seg = (int)c.seg, favDrink = c.drink?.Id ?? FavouriteGuess(c),
                    visits = 1, loyalty = 0.5f,
                };
                S.regulars.Add(reg);
                Moments.Add($"{name} ({c.def.Name.ToLowerInvariant()}) loved it and is becoming a regular.");
            }
        }

        string FavouriteGuess(Customer c)
        {
            float best = 0; string id = "lager";
            foreach (var m in S.menu)
            {
                if (!m.onMenu) continue;
                var d = Content.Drink(m.id);
                float w = c.def.DrinkPrefs[(int)d.Cat];
                if (w > best) { best = w; id = d.Id; }
            }
            return id;
        }

        /// <summary>Seat position in cell units (shared with the renderer).</summary>
        public static void SeatPos(PlacedItem it, int slot, out float x, out float y, out float faceX, out float faceY)
        {
            // Work in the model's unrotated local space (front = -y), then rotate with the item, so seats
            // always line up with the chairs the renderer builds.
            var d = Content.Item(it.id);
            int w0 = d.W, h0 = d.H;
            int n = Math.Max(1, d.Capacity);
            bool oneRow = d.Model == "sofa" || d.Model == "armchair" || d.Model == "beanbag" || d.Model == "stool";
            bool alongX = w0 >= h0;
            float along, across;
            if (n == 1) { along = 0; across = 0; }
            else if (oneRow)
            {
                along = (slot - (n - 1) / 2f) * (Math.Max(w0, h0) / (float)n);
                across = 0;
            }
            else
            {
                int perSide = (n + 1) / 2;
                int side = slot % 2;
                int k = slot / 2;
                float len = Math.Max(w0, h0);
                along = perSide == 1 ? 0 : (k - (perSide - 1) / 2f) * (len / perSide);
                float half = Math.Min(w0, h0) / 2f;
                across = (side == 0 ? -1 : 1) * Math.Max(0.32f, half - 0.12f);
            }
            float lx, ly, fx, fy;
            if (alongX) { lx = along; ly = across; fx = 0; fy = across == 0 ? -1 : -Math.Sign(across); }
            else { lx = across; ly = along; fx = across == 0 ? -1 : -Math.Sign(across); fy = 0; }
            if (oneRow) { fx = 0; fy = -1; }
            Rotate(it.rot, ref lx, ref ly);
            Rotate(it.rot, ref fx, ref fy);
            Layout.Size(d, it.rot, out int w, out int h);
            x = it.x + (w - 1) / 2f + lx;
            y = it.y + (h - 1) / 2f + ly;
            faceX = fx; faceY = fy;
        }

        /// <summary>Rotate a local offset by item rotation (matches the renderer's 90-degree Y rotations).</summary>
        static void Rotate(int rot, ref float x, ref float y)
        {
            float ox = x, oy = y;
            switch (rot & 3)
            {
                case 1: x = oy; y = -ox; break;
                case 2: x = -ox; y = -oy; break;
                case 3: x = -oy; y = ox; break;
            }
        }

        public Customer[] SeatsOf(int uid) => _seats.TryGetValue(uid, out var a) ? a : null;
        public bool ToiletBlocked(int uid) => _blockedToilets.Contains(uid);
    }
}
