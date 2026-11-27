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
