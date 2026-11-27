using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    /// <summary>A special night (or run of nights) on the yearly calendar (brief 5.2-5.3, task C2).</summary>
    public sealed class CalendarDef
    {
        public string Id, Name, Blurb;
        public int Season;          // 0 autumn, 1 winter, 2 spring, 3 summer
        public int From, To;        // day within the season, inclusive (0-27)
        public float Footfall = 1f; // multiplier on everyone
        public float[] SegBoost;    // per-segment multiplier (Students, YoungPros, Locals, SportsFans, Creatives), or null
        public DrinkCat? Drink; public float DrinkBoost = 1f; // a drink people want more of tonight
        public int SuggestEvent = -1; // event kind that fits (0 quiz, 1 comedy, 2 open mic), -1 none
        public bool NeedsScreens;   // the boost for sports fans only applies if you have screens
        public bool Contains(int dayOfYear) => dayOfYear / 28 == Season && dayOfYear % 28 >= From && dayOfYear % 28 <= To;
    }

    /// <summary>
    /// The calendar of a 112-day year: four 28-day seasons starting in autumn. Names are generic
    /// (no licensed tournaments or brands; brief 5.3).
    /// </summary>
    public static class Calendar
    {
        public const int YearDays = 112;

        static float[] Segs(float students, float youngPros, float locals, float sports, float creatives, float afterWork = 1f, float tourists = 1f, float couples = 1f)
            => new[] { students, youngPros, locals, sports, creatives, afterWork, tourists, couples };

        public static readonly List<CalendarDef> All = new List<CalendarDef>
        {
            // Autumn
            new CalendarDef { Id = "freshers", Name = "Freshers' Week", Season = 0, From = 0, To = 6, Footfall = 1f, SegBoost = Segs(1.45f, 1, 1, 1, 1.1f), SuggestEvent = 0,
                Blurb = "Freshers' week: students are out in force, and they love a quiz." },
            new CalendarDef { Id = "halloween", Name = "Halloween", Season = 0, From = 20, To = 20, Footfall = 1.08f, SegBoost = Segs(1.25f, 1.05f, 1, 1, 1.35f), Drink = DrinkCat.Cocktail, DrinkBoost = 1.4f, SuggestEvent = 2,
                Blurb = "Halloween: fancy dress, spooky cocktails and an open mic full of ghouls." },
            new CalendarDef { Id = "bonfire", Name = "Bonfire Night", Season = 0, From = 25, To = 25, Footfall = 1.1f, SegBoost = Segs(1, 1, 1.25f, 1, 1), Drink = DrinkCat.Hot, DrinkBoost = 1.8f,
                Blurb = "Bonfire Night: locals drop in after the fireworks for something warm." },
            // Winter
            new CalendarDef { Id = "xmas_parties", Name = "Christmas party season", Season = 1, From = 14, To = 19, Footfall = 1.12f, SegBoost = Segs(1, 1.3f, 1.05f, 1, 1, 1.6f, 1.1f, 1f), Drink = DrinkCat.Wine, DrinkBoost = 1.3f,
                Blurb = "Christmas party season: office parties book out tables. Expect big crowds." },
            new CalendarDef { Id = "xmas_eve", Name = "Christmas Eve", Season = 1, From = 20, To = 20, Footfall = 1.2f, SegBoost = Segs(0.8f, 1.05f, 1.35f, 1, 1), Drink = DrinkCat.Hot, DrinkBoost = 1.6f,
                Blurb = "Christmas Eve: everyone's home, everyone's out. The busiest night of the year." },
            new CalendarDef { Id = "nye", Name = "New Year's Eve", Season = 1, From = 21, To = 21, Footfall = 1.3f, SegBoost = Segs(1.1f, 1.15f, 1.05f, 1, 1.1f), Drink = DrinkCat.Cocktail, DrinkBoost = 1.5f,
                Blurb = "New Year's Eve: a full house and a midnight countdown. Staff up." },
            new CalendarDef { Id = "dry_january", Name = "Dry January", Season = 1, From = 22, To = 27, Footfall = 0.75f, Drink = DrinkCat.AlcoholFree, DrinkBoost = 2.5f,
                Blurb = "Dry January: quiet nights, and alcohol-free drinks are in demand." },
            // Spring
            new CalendarDef { Id = "valentines", Name = "Valentine's Day", Season = 2, From = 3, To = 3, Footfall = 1.1f, SegBoost = Segs(1, 1.4f, 0.9f, 0.8f, 1.1f, 0.8f, 1f, 2.5f), Drink = DrinkCat.Wine, DrinkBoost = 1.5f,
                Blurb = "Valentine's: couples want somewhere intimate. Booths and candles pay off." },
            new CalendarDef { Id = "st_patricks", Name = "St Patrick's Day", Season = 2, From = 9, To = 9, Footfall = 1.3f, SegBoost = Segs(1.1f, 1.05f, 1.2f, 1.05f, 1.05f), Drink = DrinkCat.Stout, DrinkBoost = 2.5f, SuggestEvent = 2,
                Blurb = "St Patrick's Day: the stout will flow. Make sure you've plenty in." },
            new CalendarDef { Id = "easter", Name = "Easter weekend", Season = 2, From = 18, To = 20, Footfall = 1.12f, SegBoost = Segs(0.9f, 1, 1.25f, 1, 1),
                Blurb = "Easter weekend: long lunches and families out in the evening." },
            new CalendarDef { Id = "may_bank", Name = "Bank holiday", Season = 2, From = 27, To = 27, Footfall = 1.2f, SegBoost = Segs(1.1f, 1.2f, 1.1f, 1, 1),
                Blurb = "Bank holiday Sunday: nobody's working tomorrow." },
            // Summer: a generic international football tournament, four match nights.
            new CalendarDef { Id = "tournament_1", Name = "The Big Tournament: opening match", Season = 3, From = 3, To = 3, SegBoost = Segs(1, 1.1f, 1.1f, 2.2f, 1), NeedsScreens = true, Drink = DrinkCat.Lager, DrinkBoost = 1.3f,
                Blurb = "The Big Tournament kicks off tonight. Sports fans want screens." },
            new CalendarDef { Id = "tournament_2", Name = "The Big Tournament: group decider", Season = 3, From = 9, To = 9, SegBoost = Segs(1, 1.1f, 1.1f, 2.2f, 1), NeedsScreens = true, Drink = DrinkCat.Lager, DrinkBoost = 1.3f,
                Blurb = "A Big Tournament group decider tonight. Screens on." },
            new CalendarDef { Id = "tournament_3", Name = "The Big Tournament: semi-final", Season = 3, From = 15, To = 15, Footfall = 1.1f, SegBoost = Segs(1.1f, 1.2f, 1.2f, 2.6f, 1), NeedsScreens = true, Drink = DrinkCat.Lager, DrinkBoost = 1.4f,
                Blurb = "Semi-final night! The whole street's watching." },
            new CalendarDef { Id = "tournament_4", Name = "The Big Tournament: final", Season = 3, From = 21, To = 21, Footfall = 1.25f, SegBoost = Segs(1.2f, 1.3f, 1.3f, 3f, 1.1f), NeedsScreens = true, Drink = DrinkCat.Lager, DrinkBoost = 1.5f,
                Blurb = "The final. If you've got screens, you'll be rammed." },
            new CalendarDef { Id = "solstice", Name = "Midsummer", Season = 3, From = 25, To = 25, Footfall = 1.12f, SegBoost = Segs(1.1f, 1, 1, 1, 1.4f), Drink = DrinkCat.Cider, DrinkBoost = 1.3f,
                Blurb = "Midsummer: the longest evening of the year. Creatives come out to play." },
        };

        /// The special night on a given absolute day, or null.
        public static CalendarDef On(int day)
        {
            int doy = ((day % YearDays) + YearDays) % YearDays;
            foreach (var c in All) if (c.Contains(doy)) return c;
            return null;
        }

        public static CalendarDef Today(GameState s) => On(s.day);

        /// Footfall multiplier for one segment tonight.
        public static float SegmentMult(GameState s, VibeReport v, int seg)
        {
            var c = Today(s);
            if (c == null) return 1f;
            float m = c.Footfall;
            if (c.SegBoost != null && seg < c.SegBoost.Length)
            {
                float b = c.SegBoost[seg];
                if (c.NeedsScreens && seg == (int)Seg.SportsFans && (v == null || v.Screens == 0)) b = 1f + (b - 1f) * 0.15f; // they go elsewhere
                m *= b;
            }
            return m;
        }

        public static float DrinkMult(GameState s, DrinkCat cat)
        {
            var c = Today(s);
            return c != null && c.Drink == cat ? c.DrinkBoost : 1f;
        }

        /// Calendar nights in the next `days` days (from tomorrow), for planning.
        public static List<(int day, CalendarDef def)> Upcoming(GameState s, int days)
        {
            var list = new List<(int, CalendarDef)>();
            CalendarDef last = null;
            for (int d = s.day + 1; d <= s.day + days; d++)
            {
                var c = On(d);
                if (c != null && c != last) list.Add((d, c));
                last = c;
            }
            return list;
        }
    }
}
