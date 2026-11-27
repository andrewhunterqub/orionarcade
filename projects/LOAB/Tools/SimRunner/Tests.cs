using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Loab.Sim;

// Headless regression tests for the simulation. Run: dotnet run -c Release -- test
// Exits non-zero if any test fails. See DEVELOPMENT_PLAN.md task E2.
public static class Tests
{
    static int _failed, _passed;
    static readonly List<string> Warnings = new List<string>();

    static readonly JsonSerializerOptions Json = new JsonSerializerOptions
    {
        IncludeFields = true, // like Unity's JsonUtility: public fields, not properties
        WriteIndented = false,
    };

    public static int Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Test("determinism: same seed gives the same 14 nights", Determinism);
        Test("save round trip: reloaded game continues identically", SaveRoundTrip);
        Test("migration: an old, incomplete save loads and plays", OldSaveLoads);
        Test("weekly statement: the lines add up to the profit", WeeklyStatementAddsUp);
        Test("cash crisis: loan, then final demand, then bankrupt only after the deadline", CashCrisisArc);
        Test("last orders early: the night ends sooner and can't be called before 9pm", LastOrdersEarly);
        Test("switch duty: a bartender can move to the floor and back mid-service", SwitchDuty);
        Test("verdict: nights get a highlight or a problem, and every problem has a fix", Verdicts);
        Test("seasons: hot drinks sell more in winter than in summer", SeasonalDrinks);
        Test("calendar: no overlapping nights, and St Patrick's Day draws a crowd", CalendarNights);
        Test("door security: fewer rowdy customers, and Hardcore weekends without it get fined", DoorSecurity);
        Test("cleaner: works after close and raises tomorrow's cleanliness", Cleaner);
        foreach (int diff in new[] { 0, 1, 2 })
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
                Test($"invariants: career diff {diff} seed {seed}, 28 nights", () => Invariants(Game.NewCareer(seed, diff), 28, true));
        Test("invariants: idle player, 28 nights", () => Invariants(Game.NewCareer(11, 1), 28, false));
        foreach (var sc in Scenarios.All)
            Test($"scenario smoke: {sc.Id}", () => Invariants(Game.NewScenario(sc.Id, 5), Math.Min(sc.Days, 28), true));
        Test("sandbox smoke: 14 nights", () => Invariants(Game.NewSandbox(9), 14, true));
        Test("fuzz: random service actions don't break invariants", Fuzz);

        Console.WriteLine();
        foreach (var w in Warnings) Console.WriteLine("WARN  " + w);
        Console.WriteLine($"{_passed} passed, {_failed} failed in {sw.ElapsedMilliseconds} ms");
        return _failed == 0 ? 0 : 1;
    }

    static void Test(string name, Action body)
    {
        try { body(); _passed++; Console.WriteLine("PASS  " + name); }
        catch (Exception e) { _failed++; Console.WriteLine("FAIL  " + name + "\n      " + e.Message.Replace("\n", "\n      ")); }
    }

    static void Assert(bool cond, string message) { if (!cond) throw new Exception(message); }

    // ---------------------------------------------------------------------------------------

    /// Plays one full day (Prep + Service + Close) with an optional scripted player. Returns false once the game is over.
    public static bool PlayDay(Game g, bool sensible, Action<Game, int> duringService = null, Action<Game> afterService = null)
        => PlayDay(g, sensible ? Program.Play : (Action<Game>)null, duringService, afterService);

    public static bool PlayDay(Game g, Action<Game> prep, Action<Game, int> duringService = null, Action<Game> afterService = null)
    {
        if (g.S.pendingCard != null)
        {
            if (g.S.pendingCard == "loan") g.TakeEmergencyLoan();
            else if (g.ChooseCard(0) != null) g.ChooseCard(1);
        }
        prep?.Invoke(g);
        g.OpenDoors();
        int ticks = 0;
        while (g.Phase == Phase.Service)
        {
            g.Svc.Step();
            duringService?.Invoke(g, ++ticks);
            g.EndServiceIfDone();
            if (ticks > 20000) throw new Exception($"service never ended on day {g.S.day}");
        }
        afterService?.Invoke(g);
        if (g.Phase == Phase.GameOver || g.S.bankrupt) return false;
        g.NextDay();
        return true;
    }

    static string Snapshot(Game g) => JsonSerializer.Serialize(g.S, Json);

    static void Determinism()
    {
        var a = Game.NewCareer(42, 1);
        var b = Game.NewCareer(42, 1);
        for (int d = 0; d < 14; d++)
        {
            PlayDay(a, true); PlayDay(b, true);
            Assert(Snapshot(a) == Snapshot(b), $"states diverged after night {d}");
        }
    }

    static void SaveRoundTrip()
    {
        var a = Game.NewCareer(7, 1);
        for (int d = 0; d < 7; d++) PlayDay(a, true);
        var json = Snapshot(a);
        var b = new Game(JsonSerializer.Deserialize<GameState>(json, Json));
        Assert(Snapshot(b) == json, "state changed just by loading it");
        for (int d = 0; d < 7; d++)
        {
            PlayDay(a, true); PlayDay(b, true);
            var sa = Snapshot(a); var sb = Snapshot(b);
            if (sa != sb)
            {
                int i = 0; while (i < sa.Length && i < sb.Length && sa[i] == sb[i]) i++;
                int from = Math.Max(0, i - 80);
                throw new Exception($"reloaded game diverged on night {7 + d}, near: ...{sa.Substring(from, Math.Min(160, sa.Length - from))}");
            }
        }
    }

    static void WeeklyStatementAddsUp()
    {
        var g = Game.NewCareer(33, 1);
        for (int d = 0; d < 14; d++)
        {
            PlayDay(g, true);
            var w = Economy.LastWeek(g.S);
            float lines = w.Takings - w.Drinks - w.Wages - w.Rent - w.Utilities - w.Loan - w.Other;
            Assert(Math.Abs(lines - w.Profit) < 1f, $"day {g.S.day}: lines {lines:0.00} vs profit {w.Profit:0.00}");
            if (g.S.DayOfWeek == 0) Assert(w.Rent > 0, $"day {g.S.day}: no rent in the week just ended");
        }
    }

    static void LastOrdersEarly()
    {
        var g = Game.NewCareer(91, 1);
        g.OpenDoors();
        Assert(g.Svc.CallLastOrdersEarly() != null, "allowed before 9pm");
        while (g.Svc.T < 200) g.Svc.Step();
        Assert(g.Svc.CallLastOrdersEarly() == null, "refused at 9:20pm");
        Assert(g.Svc.CloseAt < Service.Close, "closing time didn't move");
        int ticks = 0;
        while (g.Phase == Phase.Service && ticks++ < 20000) { g.Svc.Step(); g.EndServiceIfDone(); }
        Assert(g.Phase == Phase.Report, "night didn't finish");
        Assert(g.Svc.T < Service.Close, $"night ran until {Service.Clock(g.Svc.T)}");
        Assert(g.LastNight.moments.Exists(m => m.Contains("last orders early")), "no moment recorded");
    }

    static void SwitchDuty()
    {
        var g = Game.NewCareer(92, 1);
        g.OpenDoors();
        while (g.Svc.T < 60) g.Svc.Step();
        var a = g.Svc.Staff.Find(x => !x.IsOwner && x.present);
        Assert(a != null, "no staff on shift");
        Assert(a.Duty == 0, "starting bartender isn't on the bar");
        for (int i = 0; i < 200 && a.serving != null; i++) g.Svc.Step();
        Assert(g.Svc.SwitchDuty(a) == null, "couldn't move to the floor");
        Assert(a.Duty == 1 && a.barPoint == -1, "didn't leave the bar");
        for (int i = 0; i < 300; i++) g.Svc.Step();
        Assert(g.Svc.SwitchDuty(a) == null, "couldn't move back behind the bar");
        Assert(a.Duty == 0 && a.task == null, "didn't return to the bar");
        int ticks = 0;
        while (g.Phase == Phase.Service && ticks++ < 20000) { g.Svc.Step(); g.EndServiceIfDone(); }
        Assert(g.Phase == Phase.Report, "night didn't finish");
        Assert(g.Svc.SwitchDuty(g.Svc.Staff.Find(x => x.IsOwner)) != null, "owner shouldn't be switchable");
    }

    static void Verdicts()
    {
        foreach (bool sensible in new[] { true, false })
        {
            var g = Game.NewCareer(55, 1);
            int highlights = 0, problems = 0;
            for (int d = 0; d < 21; d++)
            {
                if (!PlayDay(g, sensible)) break;
                var n = g.S.history[g.S.history.Count - 1];
                if (!string.IsNullOrEmpty(n.highlight)) highlights++;
                if (!string.IsNullOrEmpty(n.problem)) { problems++; Assert(!string.IsNullOrEmpty(n.fix), $"day {n.day}: problem '{n.problem}' has no fix"); }
            }
            Assert(problems > 0, $"{(sensible ? "sensible" : "idle")} player never had a problem in 3 weeks");
            if (sensible) Assert(highlights > 0, "sensible player never had a highlight in 3 weeks");
        }
        foreach (Note n in Enum.GetValues(typeof(Note)))
            if (n == Note.WaitedLong || n == Note.Dirty || n == Note.OutOfStock || n == Note.Pricey || n == Note.Crowded)
                Assert(Economy.FixFor(n) != null, $"no fix text for {n}");
    }

    static void SeasonalDrinks()
    {
        int HotSold(int season)
        {
            var g = Game.NewCareer(61, 1);
            g.S.day = season * 28 + 3;            // a mid-season Thursday
            g.S.weather = "Rain";
            g.ToggleMenu("tea");                   // make sure a hot drink is on (no-op if it doesn't exist)
            var hot = g.S.menu.FindAll(m => Content.Drink(m.id).Cat == DrinkCat.Hot);
            Assert(hot.Count > 0, "no hot drinks in the content");
            foreach (var m in hot) { m.onMenu = true; m.stock = 500; }
            int sold = 0;
            for (int n = 0; n < 4; n++)
            {
                g.S.weather = "Rain";
                g.OpenDoors(); int t = 0;
                while (g.Phase == Phase.Service && t++ < 20000) { g.Svc.Step(); g.EndServiceIfDone(); }
                sold += hot.Sum(m => m.soldTonight);
                g.NextDay(); g.S.pendingCard = null;
            }
            return sold;
        }
        int winter = HotSold(1), summer = HotSold(3);
        // Customers at the responsible-service limit can only pick soft or hot drinks, which dampens the gap.
        Assert(winter > summer * 1.5, $"hot drinks: winter {winter}, summer {summer}");
    }

    static void CalendarNights()
    {
        for (int d = 0; d < Calendar.YearDays; d++)
        {
            int hits = Calendar.All.Count(c => c.Contains(d));
            Assert(hits <= 1, $"day {d} is in {hits} calendar entries");
        }
        foreach (var c in Calendar.All) Assert(c.From <= c.To && c.To < 28 && !string.IsNullOrEmpty(c.Blurb), $"bad entry {c.Id}");
        var g = Game.NewCareer(14, 1);
        g.S.day = 2 * 28 + 8; // the day before St Patrick's
        var up = Calendar.Upcoming(g.S, 3);
        Assert(up.Count == 1 && up[0].def.Id == "st_patricks", "Upcoming didn't find St Patrick's Day");
        float normal = Economy.ExpectedTotal(g.S, g.V);
        g.S.day++;
        Assert(Calendar.Today(g.S)?.Id == "st_patricks", "St Patrick's isn't today");
        float paddys = Economy.ExpectedTotal(g.S, g.V);
        Assert(paddys > normal * 1.25f, $"St Patrick's footfall {paddys:0} vs normal {normal:0}");
        Assert(Economy.SeasonalDrinkMult(g.S, DrinkCat.Stout) >= 2f, "no stout boost on St Patrick's");
    }

    static void DoorSecurity()
    {
        (int rowdy, int fines) Weekends(bool door)
        {
            int rowdy = 0, fines = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var g = Game.NewCareer(seed, 2);
                g.S.cash = 50000; g.S.responsibleService = false; g.S.reputation = 60;
                foreach (var m in g.S.awareness.Select((_, i) => i).ToList()) g.S.awareness[m] = 0.8f;
                if (door)
                {
                    var d = Economy.MakeApplicant(g.S, new Rng(seed), Roles.Door, 12);
                    g.S.staff.Add(d);
                }
                g.S.day = 5; // a Saturday
                g.Recompute();
                g.OpenDoors(); int t = 0;
                while (g.Phase == Phase.Service && t++ < 20000) { g.Svc.Step(); g.EndServiceIfDone(); }
                rowdy += g.Svc.RowdyCount;
                if (g.LastNight.moments.Exists(x => x.Contains("licensing officer"))) fines++;
            }
            return (rowdy, fines);
        }
        var without = Weekends(false); var with = Weekends(true);
        Assert(with.rowdy < without.rowdy, $"rowdy with door staff {with.rowdy} vs without {without.rowdy}");
        Assert(with.fines == 0, "fined despite door staff");
        Assert(without.fines > 0, $"no licensing fines on rowdy Hardcore Saturdays without door staff (rowdy {without.rowdy})");
    }

    static void Cleaner()
    {
        float Morning(bool cleaner)
        {
            var g = Game.NewCareer(44, 1);
            if (cleaner) g.S.staff.Add(Economy.MakeApplicant(g.S, new Rng(44), Roles.Cleaner, 12));
            g.OpenDoors();
            Assert(!g.Svc.Staff.Exists(a => a.m != null && a.m.role == Roles.Cleaner), "cleaner turned up during service");
            int t = 0; while (g.Phase == Phase.Service && t++ < 20000) { g.Svc.Step(); g.EndServiceIfDone(); }
            return g.S.cleanliness;
        }
        float without = Morning(false), with = Morning(true);
        Assert(with > without + 5, $"tomorrow's cleanliness: with cleaner {with:0}, without {without:0}");
    }

    static void CashCrisisArc()
    {
        var g = Game.NewCareer(77, 1);
        // First time in the red: the bank's emergency loan.
        Night(g, -4000);
        Assert(g.S.pendingCard == "loan", $"expected the loan offer, got '{g.S.pendingCard}'");
        g.NextDay(); g.TakeEmergencyLoan();
        // Back in the red after the loan: a final demand and the bailiff card, not instant bankruptcy.
        Night(g, -4000);
        Assert(!g.S.bankrupt, "went bankrupt without a final demand");
        Assert(g.S.pendingCard == "bailiff", $"expected the bailiff card, got '{g.S.pendingCard}'");
        int deadline = g.FinalDemandDeadline;
        Assert(deadline > g.S.day, $"deadline {deadline} isn't after day {g.S.day}");
        g.NextDay();
        Assert(g.ChooseCard(0) == null, "couldn't sell something");
        // Stay in the red: bankrupt only once the deadline passes.
        for (int guard = 0; !g.S.bankrupt && guard < 20; guard++)
        {
            Night(g, -3500);
            if (!g.S.bankrupt) { Assert(g.S.day < deadline, $"still trading on day {g.S.day}, past the deadline {deadline}"); g.NextDay(); }
        }
        Assert(g.S.bankrupt && g.S.day >= deadline, $"bankrupt={g.S.bankrupt} on day {g.S.day}, deadline {deadline}");
        // Recovering clears the demand.
        var h = Game.NewCareer(78, 1);
        h.S.loansTaken = 1;
        Night(h, -4000);
        Assert(h.S.GetFlag("final_demand") > 0, "no final demand issued");
        h.NextDay(); h.ChooseCard(2);
        Night(h, 5000);
        Assert(h.S.GetFlag("final_demand") == 0 && !h.S.bankrupt, "final demand not cleared after recovering");
    }

    /// Plays one night starting from the given cash, skipping any unrelated morning card.
    static void Night(Game g, float cash)
    {
        if (g.S.pendingCard != null && g.S.pendingCard != "bailiff" && g.S.pendingCard != "loan") g.S.pendingCard = null;
        g.S.cash = cash;
        var err = g.OpenDoors();
        if (err != null) throw new Exception($"couldn't open on day {g.S.day}: {err}");
        int ticks = 0;
        while (g.Phase == Phase.Service && ticks++ < 20000) { g.Svc.Step(); g.EndServiceIfDone(); }
    }

    static void OldSaveLoads()
    {
        var a = Game.NewCareer(21, 1);
        for (int d = 0; d < 3; d++) PlayDay(a, true);
        // Fake a version-1 save: missing lists, a short awareness array, a drink and an item that don't exist.
        var node = System.Text.Json.Nodes.JsonNode.Parse(Snapshot(a)).AsObject();
        node["version"] = 1;
        foreach (var k in new[] { "flags", "seenTips", "cardHistory", "unlockedItems", "residencyOffer" }) node.Remove(k);
        node["awareness"] = new System.Text.Json.Nodes.JsonArray(0.3f, 0.2f, 0.4f);
        node["menu"].AsArray().RemoveAt(0);
        node["items"].AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("{\"uid\":999,\"id\":\"retired_item\",\"x\":0,\"y\":0,\"rot\":0,\"wear\":0}"));
        var s = JsonSerializer.Deserialize<GameState>(node.ToJsonString(), Json);
        var g = new Game(s);
        Assert(g.S.version == SaveMigration.Current, $"version is {g.S.version}, expected {SaveMigration.Current}");
        Assert(g.S.awareness.Length == Content.Segments.Count, "awareness not resized to the segment count");
        Assert(g.S.menu.Count == Content.Drinks.Count, "missing drink not restored to the menu");
        Assert(!g.S.items.Exists(i => i.id == "retired_item"), "unknown item not removed");
        Invariants(g, 5, true);
    }

    static void Invariants(Game g, int nights, bool sensible)
    {
        for (int d = 0; d < nights; d++)
        {
            bool going = PlayDay(g, sensible);
            Check(g);
            if (!going) return; // game over is allowed; it just has to be reached cleanly
        }
    }

    static void Check(Game g)
    {
        var s = g.S; var n = g.LastNight;
        string at = $"day {s.day}";
        Assert(float.IsFinite(s.cash), $"{at}: cash is {s.cash}");
        Assert(s.reputation >= 0 && s.reputation <= 100, $"{at}: reputation {s.reputation} outside 0..100");
        Assert(s.rating >= 0 && s.rating <= 5.0001f, $"{at}: rating {s.rating} outside 0..5");
        Assert(s.cleanliness >= 0 && s.cleanliness <= 100.0001f, $"{at}: cleanliness {s.cleanliness}");
        Assert(s.lineQuality >= 0 && s.lineQuality <= 100.0001f, $"{at}: line quality {s.lineQuality}");
        foreach (var m in s.menu) Assert(m.stock >= 0, $"{at}: negative stock for {m.id} ({m.stock})");
        foreach (var m in s.menu) Assert(m.price >= 0 && float.IsFinite(m.price), $"{at}: bad price for {m.id}");
        foreach (var st in s.staff) Assert(st.morale >= 0 && st.morale <= 100.0001f, $"{at}: {st.name} morale {st.morale}");
        foreach (var a in s.awareness) Assert(a >= 0 && a <= 1.0001f, $"{at}: awareness {a} outside 0..1");
        foreach (var it in s.items) Assert(Content.Item(it.id) != null, $"{at}: unknown item id {it.id}");
        Assert(s.items.Select(i => i.uid).Distinct().Count() == s.items.Count, $"{at}: duplicate item uids");
        if (n != null)
        {
            Assert(n.customers >= 0 && n.servedDrinks >= 0 && n.walkouts >= 0, $"{at}: negative night counts");
            Assert(float.IsFinite(n.takings) && n.takings >= 0, $"{at}: takings {n.takings}");
            Assert(float.IsFinite(n.profit), $"{at}: profit {n.profit}");
            Assert(n.satisfaction >= 0 && n.satisfaction <= 100.0001f, $"{at}: satisfaction {n.satisfaction}");
            foreach (var r in n.reviews) Assert(r.stars >= 1 && r.stars <= 5 && !string.IsNullOrWhiteSpace(r.text), $"{at}: bad review");
        }
    }

    static void Fuzz()
    {
        var g = Game.NewCareer(123, 1);
        var r = new Random(123);
        for (int d = 0; d < 21; d++)
        {
            bool going = PlayDay(g, true, (game, tick) =>
            {
                if (tick % 37 != 0) return;
                var svc = game.Svc;
                switch (r.Next(5))
                {
                    case 0: svc.StartHappyHour(); break;
                    case 1: svc.CollectGlasses(); break;
                    case 2: svc.SetOwnerBehindBar(r.Next(2) == 0); break;
                    case 3: if (svc.Incidents.Count > 0) svc.ResolveIncident(svc.Incidents[r.Next(svc.Incidents.Count)].id); break;
                    case 4: svc.EmergencyDelivery(game.S.menu[r.Next(game.S.menu.Count)].id); break;
                }
            });
            Check(g);
            if (!going) return;
        }
    }
}
