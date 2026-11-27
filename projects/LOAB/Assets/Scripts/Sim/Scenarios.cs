using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    /// <summary>Set challenges with a deadline (build brief 8.1, "Scenarios").</summary>
    public sealed class ScenarioDef
    {
        public string Id, Name, Blurb, GoalText;
        public int Days;
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
