using System;
using System.Linq;
using Loab.Sim;

// Simulates a career with a simple scripted "sensible player" and prints nightly metrics.
//   dotnet run -- [days] [seed] [strategy: idle|sensible]
public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "balance") { Environment.Exit(Balance.Report(args.Length > 1 ? int.Parse(args[1]) : 84, args.Length > 2 ? int.Parse(args[2]) : 5)); return; }
        if (args.Length > 0 && args[0] == "test") { Environment.Exit(Tests.Run()); return; }
        int days = args.Length > 0 ? int.Parse(args[0]) : 28;
        ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 42;
        string strategy = args.Length > 2 ? args[2] : "sensible";

        int diff = args.Length > 3 ? int.Parse(args[3]) : 1;
        if (strategy == "speak") { var gs = Game.NewScenario("speakeasy", seed); Speak(gs); return; }
        var g = strategy.StartsWith("scen:") ? Game.NewScenario(strategy.Substring(5), seed) : Game.NewCareer(seed, diff);
        bool studentPlan = strategy == "scen:freshers";
        if (strategy.StartsWith("scen:")) strategy = "sensible";
        Console.WriteLine($"Start: cash £{g.S.cash:0}, vibe {string.Join("/", g.V.Axes.Select(a => a.ToString("0")))} ({g.V.Summary}), seats {g.V.Seats}, match {string.Join(" ", g.V.SegMatch.Select(m => m.ToString("0.00")))}");
        Console.WriteLine("day  dow  wx      cust  away  drinks walk  takings   profit     cash  sat   rating rep   staff  event");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int d = 0; d < days; d++)
        {
            if (g.S.pendingCard != null)
            {
                if (g.S.pendingCard == "loan") g.TakeEmergencyLoan();
                else if (g.ChooseCard(0) != null) g.ChooseCard(1);
            }
            if (strategy == "sensible") Play(g);
            if (studentPlan) Students(g);
            if (strategy == "speak" && d == 0) Speak(g);
            if (strategy == "fuzz") Fuzz(g, new Random((int)seed * 1000 + d));
            g.OpenDoors();
            int ticks = 0;
            var fr = new Random((int)seed + d);
            while (g.Phase == Phase.Service)
            {
                g.Svc.Step(); ticks++;
                if (strategy == "fuzz" && ticks % 50 == 0)
                {
                    switch (fr.Next(5))
                    {
                        case 0: g.Svc.StartHappyHour(); break;
                        case 1: g.Svc.CollectGlasses(); break;
                        case 2: g.Svc.SetOwnerBehindBar(fr.Next(2) == 0); break;
                        case 3: if (g.Svc.Incidents.Count > 0) g.Svc.ResolveIncident(g.Svc.Incidents[fr.Next(g.Svc.Incidents.Count)].id); break;
                        case 4: g.Svc.EmergencyDelivery(g.S.menu[fr.Next(g.S.menu.Count)].id); break;
                    }
                }
                g.EndServiceIfDone();
            }
            var n = g.LastNight;
            Console.WriteLine($"{n.day,3}  {Content.DayShort[n.day % 7]}  {n.weather,-7} {n.customers,4}  {n.turnedAway,4}  {n.servedDrinks,5}  {n.walkouts,4}  £{n.takings,7:0}  £{n.profit,7:0}  £{g.S.cash,7:0}  {n.satisfaction,4:0}  {g.S.rating,4:0.00}  {g.S.reputation,4:0}  {g.S.staff.Count,3}   {n.eventName} {n.eventOutcome}");
            if (d == days - 1 || d % 7 == 6)
            {
                Console.WriteLine("     thoughts: " + string.Join(" | ", n.thoughts));
                foreach (var r in n.reviews.Take(3)) Console.WriteLine($"     {r.stars}* {r.author} ({r.segment}): {r.text}");
                foreach (var m in n.moments.Take(4)) Console.WriteLine("     * " + m);
                Console.WriteLine($"     segs: {string.Join(" ", n.segCounts)}  awareness: {string.Join(" ", g.S.awareness.Select(a => a.ToString("0.00")))}  regulars {g.S.regulars.Count}  goal {g.S.goalIndex}  tags: {string.Join(", ", g.S.tags)}");
            }
            if (g.Scenario != null && g.ScenarioJustEnded) Console.WriteLine($"SCENARIO {(g.S.scenarioResult == 1 ? "WON" : "LOST")} on day {n.day}: {g.Scenario.Progress(g)}");
            if (g.Phase == Phase.GameOver || g.S.bankrupt) { Console.WriteLine("BANKRUPT"); break; }
            g.NextDay();
        }
        Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms. Vibe now {string.Join("/", g.V.Axes.Select(a => a.ToString("0")))} theme {(g.V.ActiveTheme?.Def.Name ?? "none")} coherence {g.V.Coherence:0}");
    }

    // A basic player: keeps stock up, hires when queues are long, books a weekly quiz, buys some furniture.
    internal static void Play(Game g)
    {
        var s = g.S;
        if (s.lineQuality < 70) g.CleanLines();
        g.RestockAll();
        g.SocialPost();
        var last = g.LastNight;
        int bartenders = s.staff.Count(m => m.role == 0);
        bool queues = last != null && (last.thoughts.Any(t => t.Contains("slow") || t.Contains("gave up")) || last.walkouts > last.customers * 0.04f);
        // Staff to demand: about one bartender per 45 customers on the busiest recent night, up to the bar's serving points.
        int busiest = s.history.Skip(Math.Max(0, s.history.Count - 7)).Select(h => h.customers).DefaultIfEmpty(0).Max();
        int counters = s.items.Count(i => Content.Item(i.id).Fn == ItemFn.BarCounter);
        int wanted = Math.Min(counters * 3, Math.Max(1, (int)Math.Ceiling(busiest / 45f)));
        float wageBill = s.staff.Sum(m => m.wage) * 7;
        if ((queues || bartenders < wanted) && bartenders < Math.Max(4, wanted) && s.cash > 2500 + wageBill * 0.5f)
        {
            var a = s.applicants.Where(x => x.role == 0).OrderByDescending(x => x.speed).FirstOrDefault();
            if (a != null) g.Hire(a.uid);
        }
        // Rota: once there's cover, everyone gets a quiet night off (Monday or Tuesday, alternating).
        for (int i = 0; i < s.staff.Count; i++)
        {
            var st = s.staff[i];
            if (st.daysOff != 0 || s.staff.Count(m => m.role == st.role) < 2) continue;
            st.daysOff = 1 << (i % 2);
        }
        int floor = s.staff.Count(m => m.role == 1);
        // Packed out? Door security keeps the room comfortable.
        if (last != null && last.thoughts.Any(t => t.Contains("crowded")) && !s.staff.Any(m => m.role == Roles.Door) && s.cash > 8000)
        {
            var a = s.applicants.FirstOrDefault(x => x.role == Roles.Door) ?? s.applicants.LastOrDefault();
            if (a != null) { a.role = Roles.Door; g.Hire(a.uid); }
        }
        if (last != null && last.thoughts.Any(t => t.Contains("dirty")) && s.cleanliness < 80 && s.cash > 4000) g.DeepClean();
        bool dirtyComplaints = last != null && last.thoughts.Any(t => t.Contains("dirty"));
        // Hire floor staff once open a few nights, and add more if the report keeps saying it's dirty.
        if (((floor == 0 && s.day > 4) || (dirtyComplaints && floor < 3 && floor * 2 < bartenders + 1)) && s.cash > 2500)
        {
            var a = s.applicants.FirstOrDefault(x => x.role == 1) ?? s.applicants.FirstOrDefault();
            if (a != null) { a.role = 1; g.Hire(a.uid); }
        }
        if (last != null && last.thoughts.Any(t => t.Contains("slow")) && bartenders >= counters * 3 - 1 && counters < 3 && s.cash > 5000)
            TryPlace(g, "bar_counter");
        if (last != null && last.thoughts.Any(t => t.Contains("seat")) && s.cash > 3000)
            TryPlace(g, "long_table");
        if (s.day == 2) { TryPlace(g, "pool"); TryPlace(g, "photos"); TryPlace(g, "mirror"); }
        if (s.day == 5 && s.cash > 3000) { TryPlace(g, "toilets"); }
        if (g.CanExpand && s.reputation >= Game.ExpandRep && s.cash > 20000)
        {
            if (g.Expand() == null) { Console.WriteLine($"   expanded to {s.gridW} wide on day {s.day}"); for (int i = 0; i < 3; i++) TryPlace(g, "long_table"); TryPlace(g, "bar_counter"); }
        }
        if (s.day >= 8 && !g.V.HasStage && s.cash > 4000) TryPlace(g, "stage");
        if (g.V.HasStage && s.DayOfWeek == 4 && !s.events.Any(e => e.kind == 2))
        {
            var om = s.agency.FirstOrDefault(a => a.kind == 2);
            if (om != null) g.BookEvent(om.uid, 2, 50, 1);
        }
        if (g.V.HasStage && s.DayOfWeek == 2 && !s.events.Any(e => e.kind == 1))
        {
            var comic = s.agency.Where(a => a.kind == 1 && a.fee < s.cash * 0.15f).OrderByDescending(a => a.quality).FirstOrDefault();
            if (comic != null) g.BookEvent(comic.uid, 2, 150, 5);
        }
        if (s.DayOfWeek == 1 && !s.events.Any())
        {
            var quiz = s.agency.FirstOrDefault(a => a.kind == 0 && a.fee > 0 && a.fee < s.cash * 0.2f);
            if (quiz != null) g.BookEvent(quiz.uid, 2, 60, 2);
        }
    }

    // Random player: exercises every command with odd inputs to shake out crashes.
    static void Fuzz(Game g, Random r)
    {
        var s = g.S;
        for (int i = 0; i < 6; i++)
        {
            switch (r.Next(16))
            {
                case 0: case 1:
                {
                    var d = Content.Items[r.Next(Content.Items.Count)];
                    g.Execute(new PlaceCmd { Id = d.Id, X = r.Next(-1, s.gridW + 1), Y = r.Next(-1, s.gridH + 1), Rot = r.Next(4) });
                    break;
                }
                case 2: if (s.items.Count > 0) { var it = s.items[r.Next(s.items.Count)]; g.Execute(new SellCmd { Uid = it.uid }); } break;
                case 3: if (s.items.Count > 0) { var it = s.items[r.Next(s.items.Count)]; g.Execute(new MoveCmd { Uid = it.uid, X = r.Next(s.gridW), Y = r.Next(s.gridH), Rot = r.Next(4) }); } break;
                case 4: g.Undo(); break;
                case 5: g.Redo(); break;
                case 6: { var m = s.menu[r.Next(s.menu.Count)]; g.SetPrice(m.id, (float)(r.NextDouble() * 15)); break; }
                case 7: { var m = s.menu[r.Next(s.menu.Count)]; g.ToggleMenu(m.id); break; }
                case 8: g.RestockAll(); break;
                case 9: if (s.applicants.Count > 0) g.Hire(s.applicants[r.Next(s.applicants.Count)].uid); break;
                case 10: if (s.staff.Count > 1 && r.Next(3) == 0) g.Fire(s.staff[r.Next(s.staff.Count)].uid); break;
                case 11: if (s.agency.Count > 0) g.BookEvent(s.agency[r.Next(s.agency.Count)].uid, r.Next(7), new[] { 0, 50, 150 }[r.Next(3)], r.Next(3) * 5); break;
                case 12: g.SetMusic(Content.Music[r.Next(Content.Music.Count)].Id); g.SetLighting(r.Next(101)); g.SetVolume(r.Next(101)); break;
                case 13: g.Execute(new StyleCmd { Kind = r.Next(2), Id = (r.Next(2) == 0 ? Content.Floors[r.Next(Content.Floors.Count)].Id : Content.Walls[r.Next(Content.Walls.Count)].Id) }); break;
                case 14: g.SocialPost(); g.Flyers(); g.SetHappyHour(new[] { -1, 18, 20 }[r.Next(3)]); g.SetResponsible(r.Next(2) == 0); break;
                case 15: if (s.staff.Count > 0) { g.Train(s.staff[0].uid); } if (s.items.Count > 0) g.Repair(s.items[r.Next(s.items.Count)].uid); g.CleanLines(); g.DeepClean(); break;
            }
        }
        string err = g.L.CheckReachability();
        if (err != null) throw new Exception("Layout became unreachable: " + err);
    }

    // Builds a speakeasy to check the theme is reachable.
    static void Speak(Game g)
    {
        var s = g.S;
        foreach (var it in s.items.ToList()) { var d = Content.Item(it.id); if (d.Id == "plastic_set" || d.Id == "pub_table" || d.Id == "darts" || d.Id == "chalkboard") g.Execute(new SellCmd { Uid = it.uid }); }
        g.Execute(new StyleCmd { Kind = 0, Id = "carpet" }); g.Execute(new StyleCmd { Kind = 1, Id = "green" }); g.SetMusic("jazz"); g.SetLighting(30);
        foreach (var id in new[] { "espresso_martini", "old_fashioned", "fizz", "whisky" }) g.OrderStock(id, 1);
        foreach (var id in new[] { "booth", "booth", "armchair", "armchair", "chesterfield", "candles", "candles", "candles", "edison", "edison", "bookshelf", "bookshelf", "curtains", "curtains", "rug" }) TryPlace(g, id);
        foreach (var t in g.V.Themes) Console.WriteLine($"   theme {t.Def.Name}: count {t.Count} share {t.Share:0.00} active {t.Active}");
        Console.WriteLine($"   cash left £{s.cash:0}");
    }

    static void Students(Game g)
    {
        var s = g.S;
        if (s.day == 0)
        {
            g.SetMusic("pop"); g.SetVolume(80); g.SetLighting(55);
            foreach (var m in s.menu) g.SetPrice(m.id, Content.Drink(m.id).DefaultPrice * 0.85f);
            foreach (var id in new[] { "pool", "foosball", "arcade_cab", "neon_open", "flamingo", "long_table", "long_table" }) TryPlace(g, id);
            g.Execute(new StyleCmd { Kind = 1, Id = "pink" });
        }
        g.Flyers();
        if (s.DayOfWeek == 1 && !s.events.Any()) { var q = s.agency.FirstOrDefault(a => a.kind == 0 && a.fee > 0); if (q != null) g.BookEvent(q.uid, 2, 150, 0); }
    }

    internal static void TryPlace(Game g, string id)
    {
        for (int y = 1; y < g.S.gridH; y++)
            for (int x = 0; x < g.S.gridW; x++)
                if (g.Execute(new PlaceCmd { Id = id, X = x, Y = y, Rot = 0 }) == null) return;
    }
}
