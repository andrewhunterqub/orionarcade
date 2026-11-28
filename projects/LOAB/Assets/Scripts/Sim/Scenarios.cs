using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    /// <summary>Set challenges with a deadline (build brief 8.1, "Scenarios").</summary>
    public sealed class ScenarioDef
    {
        public string Id, Name, Blurb, GoalText;
        public int Days;
        public int StartDay;        // absolute day the scenario begins (0 = autumn, week 1)
        public Action<GameState> Setup;
        public Func<Game, bool> Won;
        public Func<Game, string> Progress;
    }

    public static class Scenarios
    {
        public static readonly List<ScenarioDef> All = new List<ScenarioDef>
        {
            new ScenarioDef
            {
                Id = "rescue", Name = "Last Orders", Days = 21,
                Blurb = "The Crooked Anchor is on its last legs: tired furniture, grubby floors, a 2.4-star rating and £2,000 in the bank. Turn it round before the brewery pulls the plug.",
                GoalText = "Hold a 4.2★ rating or better for 5 nights in a row, within 3 weeks",
                Setup = s =>
                {
                    s.cash = 2000; s.reputation = 6; s.rating = 2.4f; s.cleanliness = 60; s.lineQuality = 45;
                    foreach (var it in s.items) it.wear = 85;
                    for (int i = 0; i < 12; i++) s.reviews.Add(new Review { day = -1, stars = i % 3 == 0 ? 3 : 2, author = "Old review", segment = "Local", text = i % 2 == 0 ? "Used to be great. Sticky everything now." : "Flat beer, grumpy vibe." });
                    s.reviewCount = 12;
                    for (int i = 0; i < s.awareness.Length; i++) s.awareness[i] *= 0.7f;
                    s.barName = "The Crooked Anchor";
                },
                Won = g => Streak(g, 4.2f) >= 5,
                Progress = g => $"Rating {g.S.rating:0.0}  -  streak {Streak(g, 4.2f)}/5 nights at 4.2★+",
            },
            new ScenarioDef
            {
                Id = "speakeasy", Name = "The Velvet Room", Days = 28,
                Blurb = "An investor has handed you £12,000 and a dream: a proper speakeasy. Dark wood, jazz, serious cocktails, and a bar that actually makes money.",
                GoalText = "Get the Speakeasy theme recognised and make £1,500 profit in one night, within 4 weeks",
                Setup = s => { s.cash = 12000; s.reputation = 35; s.barName = "The Velvet Room"; },
                Won = g => g.V != null && g.V.ActiveTheme != null && g.V.ActiveTheme.Def.Id == "speakeasy" && g.S.history.Exists(h => h.profit >= 1500),
                Progress = g =>
                {
                    bool theme = g.V != null && g.V.ActiveTheme != null && g.V.ActiveTheme.Def.Id == "speakeasy";
                    float best = 0; foreach (var h in g.S.history) best = Math.Max(best, h.profit);
                    var t = g.V?.Themes.Find(x => x.Def.Id == "speakeasy");
                    return $"Speakeasy {(theme ? "DONE" : $"{(t?.Progress ?? 0) * 100:0}%")}  -  best night £{best:#,0}/£1,500";
                },
            },
            new ScenarioDef
            {
                Id = "freshers", Name = "Freshers' Fortnight", Days = 14,
                Blurb = "Term starts Monday and twenty thousand students are about to arrive. Cheap drinks, loud music, games and quizzes: get them through your door.",
                GoalText = "Serve 80 students in a single night within 2 weeks",
                Setup = s => { s.cash = 5000; s.awareness[(int)Seg.Students] = 0.35f; s.barName = "The Union Jack"; },
                Won = g => g.S.history.Exists(h => h.segCounts.Count > 0 && h.segCounts[(int)Seg.Students] >= 80),
                Progress = g =>
                {
                    int best = 0;
                    foreach (var h in g.S.history) if (h.segCounts.Count > 0) best = Math.Max(best, h.segCounts[(int)Seg.Students]);
                    return $"Best night: {best}/80 students";
                },
            },
            // ---- More scenarios (task P3) ------------------------------------------------------------------
            new ScenarioDef
            {
                Id = "dryjan", Name = "Dry January", Days = 7, StartDay = 28 + 21, // New Year's Eve, then the dry week
                Blurb = "It's New Year's Eve. Tomorrow the whole city stops drinking, or says it will. Can a bar thrive in Dry January?",
                GoalText = "Sell 250 alcohol-free drinks and still have £5,000 in the bank when the week's out",
                Setup = s =>
                {
                    s.cash = 7000; s.reputation = 30; s.barName = "The Clear Head";
                    foreach (var m in s.menu) if (Content.Drink(m.id).Cat == DrinkCat.AlcoholFree && !Content.Drink(m.id).Signature) { m.onMenu = true; m.stock = Math.Max(m.stock, Content.Drink(m.id).CaseSize); }
                    s.SetFlag("af_sold", 0);
                },
                Won = g => g.S.GetFlag("af_sold") >= 250 && g.S.cash >= 5000 && g.ScenarioDay >= 6,
                Progress = g => $"Alcohol-free sold {g.S.GetFlag("af_sold")}/250  -  cash £{g.S.cash:#,0} (keep £5,000)",
            },
            new ScenarioDef
            {
                Id = "matchday", Name = "Match Day", Days = 22, StartDay = 3 * 28, // summer: the Big Tournament
                Blurb = "The Big Tournament starts in three days and you've got no screens, no sports crowd and a lot of space. The final is in three weeks.",
                GoalText = "Host a Big Match Screening on the night of the final and pack in 300 customers",
                Setup = s =>
                {
                    s.cash = 9000; s.reputation = 30; s.barName = "The Final Whistle";
                    s.items.RemoveAll(i => Content.Item(i.id).Fn == ItemFn.Screen);
                    s.awareness[(int)Seg.SportsFans] = 0.3f;
                },
                Won = g => g.S.history.Exists(h => h.day == 3 * 28 + 21 && h.customers >= 300 && h.eventName != null && h.eventName.StartsWith(EventKinds.Names[EventKinds.Match])),
                Progress = g =>
                {
                    int best = 0; foreach (var h in g.S.history) if (Calendar.On(h.day)?.NeedsScreens == true) best = Math.Max(best, h.customers);
                    int toFinal = 3 * 28 + 21 - g.S.day;
                    return toFinal > 0 ? $"Final in {toFinal} days  -  best match night {best}/300 (book a screening!)" : $"Best match night {best}/300";
                },
            },
            new ScenarioDef
            {
                Id = "inheritance", Name = "The Inheritance", Days = 56,
                Blurb = "Great-uncle Fergus left you his bar. And his debts: £10,000 to the brewery, at £180 a night. Clear it in eight weeks and the place is truly yours.",
                GoalText = "Pay off the £10,000 debt and have £5,000 in the bank, within 8 weeks",
                Setup = s =>
                {
                    s.cash = 3500; s.reputation = 15; s.rating = 3.0f; s.barName = "Fergus's";
                    s.loanBalance = 10000; s.loanDaily = 180; s.loansTaken = 1;
                    foreach (var it in s.items) it.wear = 70;
                },
                Won = g => g.S.loanBalance <= 0 && g.S.cash >= 5000,
                Progress = g => $"Debt £{Math.Max(0, g.S.loanBalance):#,0} left  -  cash £{g.S.cash:#,0}/£5,000",
            },
        };

        /// <summary>How many of the most recent nights in a row ended with at least this rating.</summary>
        public static int Streak(Game g, float rating)
        {
            int n = 0;
            for (int i = g.S.history.Count - 1; i >= 0 && g.S.history[i].rating >= rating; i--) n++;
            return n;
        }

        public static ScenarioDef Get(string id) => string.IsNullOrEmpty(id) ? null : All.Find(s => s.Id == id);
    }
}
