using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public static class Economy
    {
        public static readonly string[] EventNames = { "Pub Quiz", "Comedy Night", "Open Mic Night" };
        public static string EventName(int kind) => EventNames[Math.Max(0, Math.Min(EventNames.Length - 1, kind))];
        /// <summary>How much a segment wants to come to / join in with this kind of event.</summary>
        public static float EventAffinity(SegmentDef d, int kind) => kind == 0 ? d.Quiz : kind == 1 ? d.Comedy : d.Stage * 0.9f;

        public const float RentPerWeek = 1400f;
        public const float UtilitiesPerNight = 60f;
        /// How strongly footfall falls as prices rise above normal (times each segment's price sensitivity).
        public const float PriceElasticity = 4.0f;
        /// How strongly footfall rises when prices are below normal ("great value" draws a crowd).
        public const float ValueDraw = 2.2f;
        public const float BaseFootfall = 270f;

        // ---- Difficulty and running costs (task T5) ---------------------------------------------------
        /// Supplier prices: Relaxed 0.92x, Hardcore 1.07x.
        public static float CostMult(GameState s) => s.difficulty == 0 ? 0.92f : s.difficulty == 2 ? 1.07f : 1f;
        /// Footfall: Relaxed 1.08x, Hardcore 0.93x.
        public static float FootfallMult(GameState s) => s.difficulty == 0 ? 1.08f : s.difficulty == 2 ? 0.93f : 1f;
        /// The landlord reviews the rent as the bar's reputation grows, and a bigger unit costs more.
        public static float WeeklyRent(GameState s)
        {
            float diff = s.difficulty == 0 ? 0.8f : s.difficulty == 2 ? 1.2f : 1f;
            float rent = RentPerWeek * diff * (1f + s.reputation / 100f * 0.75f) * (float)Math.Pow(s.gridW / 16f, 0.7);
            return (float)Math.Round(rent / 10f) * 10f;
        }

        public static readonly string[] Weathers = { "Sunny", "Cloudy", "Rain", "Storm", "Mild" };

        public static float WeatherMult(string w) => w switch
        {
            "Sunny" => 1.08f, "Mild" => 1.04f, "Rain" => 0.85f, "Storm" => 0.68f, _ => 1f,
        };

        public static float SeasonMult(GameState s)
        {
            int dayInSeason = s.day % 28;
            switch (s.Season)
            {
                case 0: return 1.0f;                                   // autumn (freshers' week is on the Calendar)
                case 1: return 1.05f;                                  // winter (Christmas, New Year and Dry January are on the Calendar)
                case 2: return 1.0f;
                default: return 1.12f;                                 // summer
            }
        }

        // ---- Seasons that matter (task C1) -------------------------------------------------------------

        /// How much more (or less) people want a kind of drink, given the season and tonight's weather.
        public static float SeasonalDrinkMult(GameState s, DrinkCat cat)
        {
            float m = Calendar.DrinkMult(s, cat);
            switch (s.Season)
            {
                case 1: // winter
                    if (cat == DrinkCat.Hot) m *= 2.5f; else if (cat == DrinkCat.Stout) m *= 1.3f; else if (cat == DrinkCat.Wine) m *= 1.15f;
                    else if (cat == DrinkCat.Cider || cat == DrinkCat.Lager) m *= 0.85f;
                    break;
                case 3: // summer
                    if (cat == DrinkCat.Cider) m *= 1.5f; else if (cat == DrinkCat.Lager) m *= 1.3f; else if (cat == DrinkCat.AlcoholFree) m *= 1.2f;
                    else if (cat == DrinkCat.Hot) m *= 0.3f; else if (cat == DrinkCat.Stout) m *= 0.8f;
                    break;
            }
            switch (s.weather)
            {
                case "Sunny": if (cat == DrinkCat.Lager || cat == DrinkCat.Cider) m *= 1.2f; else if (cat == DrinkCat.Hot) m *= 0.6f; break;
                case "Rain":
                case "Storm": if (cat == DrinkCat.Hot) m *= 1.4f; else if (cat == DrinkCat.Stout) m *= 1.1f; break;
            }
            return m;
        }

        /// The season's trend: cosy, intimate bars draw better in winter; lively ones in summer. Roughly ±8%.
        public static float SeasonalVibeMult(GameState s, VibeReport v)
        {
            if (v == null) return 1f;
            if (s.Season == 1) return 1f + (v.Axes[Axis.Intimacy] - 50f) / 50f * 0.08f;
            if (s.Season == 3) return 1f + (v.Axes[Axis.Energy] - 50f) / 50f * 0.08f;
            return 1f;
        }

        /// Sunny summer nights wear staff out faster.
        public static bool Heatwave(GameState s) => s.Season == 3 && s.weather == "Sunny";

        public static string SeasonNote(GameState s)
        {
            int d = s.day % 28;
            var cal = Calendar.Today(s);
            if (cal != null) return cal.Blurb;
            if (Heatwave(s)) return "A hot summer night: cider and lager will fly, and staff will tire faster.";
            if (s.Season == 1 && d < 14 && (s.weather == "Rain" || s.weather == "Storm")) return "A cold, wet winter night: hot drinks and stout sell well, and cosy bars win.";
            if (s.Season == 3) return "Summer evenings: everyone's out.";
            return null;
        }

        /// <summary>Expected customers per segment tonight, plus extra event-driven arrivals.</summary>
        public static void ExpectedArrivals(GameState s, VibeReport v, float[] outExpected, BookedEvent ev, out float eventExtra)
        {
            float foot = BaseFootfall * FootfallMult(s) * SeasonMult(s) * SeasonalVibeMult(s, v) * WeatherMult(s.weather) * (1f - s.rivalPressure * 0.3f);
            if (s.day == 0) foot *= 1.5f; // opening-night curiosity
            float rep = 0.35f + s.reputation / 100f * 1.25f;
            float ratio = VibeCalc.PriceRatio(s);
            bool canServe = v.BarPoints > 0;
            eventExtra = 0;
            for (int i = 0; i < 5; i++)
            {
                var d = Content.Segments[i];
                float day = d.DayWeights[s.DayOfWeek] * (s.DayOfWeek >= 4 ? 1.15f : 1f);
                float price = (float)Math.Exp(-d.PriceSensitivity * Math.Max(0, ratio - 1f) * PriceElasticity) * (1f + d.PriceSensitivity * Math.Max(0, 1f - ratio) * ValueDraw);
                float ent = 1f + d.Games * Math.Min(v.Games, 3) * 0.06f + d.Screens * (v.Screens > 0 ? 0.22f : 0f) + d.Stage * (v.HasStage ? 0.06f : 0f);
                float theme = 1f;
                if (v.ActiveTheme != null && d.ThemeAffinity.TryGetValue(v.ActiveTheme.Def.Id, out float aff)) theme += aff * 0.25f;
                float sports = (s.DayOfWeek == 2 || s.DayOfWeek == 5) && i == (int)Seg.SportsFans && v.Screens > 0 ? 1.5f : 1f; // match days
                float appeal = (0.15f + 0.85f * v.SegMatch[i]) * rep * price * ent * theme * sports * Calendar.SegmentMult(s, v, i);
                float social = 1f + s.socialPostBoost;
                outExpected[i] = canServe ? foot * d.Share * s.awareness[i] * appeal * day * social : 0;

                if (ev != null)
                {
                    float aff2 = EventAffinity(d, ev.kind);
                    float draw = (ev.kind == 0 ? 10f : ev.kind == 2 ? 7f : 6f) + ev.act.following * 0.25f + ev.promo * 0.06f;
                    float ticketDrag = ev.ticket > 0 ? (float)Math.Exp(-ev.ticket * 0.05f * d.PriceSensitivity) : 1f;
                    eventExtra += draw * aff2 * d.Share * (0.4f + v.SegMatch[i]) * rep * ticketDrag * 1.6f;
                }
            }
        }

        public static float ExpectedTotal(GameState s, VibeReport v)
        {
            var e = new float[5];
            var ev = s.events.Find(x => x.day == s.day);
            ExpectedArrivals(s, v, e, ev, out float extra);
            float t = extra;
            foreach (var x in e) t += x;
            return t;
        }

        // ---- Tonight's verdict (task L5) ---------------------------------------------------------------

        static readonly Note[] Complaints =
        {
            Note.WaitedLong, Note.GaveUpQueue, Note.Dirty, Note.OutOfStock, Note.Pricey, Note.NoSeat, Note.Crowded,
            Note.ToiletQueue, Note.NoToilet, Note.HatedVibe, Note.HatedMusic, Note.RowdyNearby, Note.GrumpyStaff, Note.BadDrink,
        };

        /// What to do about each complaint, in the mentor's plain words.
        public static string FixFor(Note n)
        {
            switch (n)
            {
                case Note.WaitedLong:
                case Note.GaveUpQueue: return "Hire another bartender, add a second bar counter, or jump behind the bar yourself when it's busy.";
                case Note.Dirty: return "Floor staff keep tables and spills under control. A deep clean before opening gives you a fresh start.";
                case Note.OutOfStock: return "Use Restock all before opening: it now orders what actually sells.";
                case Note.Pricey: return "Bring prices closer to normal (red prices on the Drinks tab), or try a happy hour.";
                case Note.NoSeat: return "Add more seating: long tables seat six.";
                case Note.Crowded: return "You're packed out: knock through next door, or charge a little more on busy nights.";
                case Note.ToiletQueue:
                case Note.NoToilet: return "Add a toilet block. Nobody forgives a toilet queue.";
                case Note.HatedVibe: return "Check the Vibe tab: your look doesn't match the crowd you're getting.";
                case Note.HatedMusic: return "Try different music, or turn the volume down a notch.";
                case Note.RowdyNearby: return "Keep responsible service on, call last orders early on rough nights, and deal with rowdy customers quickly.";
                case Note.GrumpyStaff: return "Low morale: check wages on the Staff tab and don't overwork people.";
                case Note.BadDrink: return "Clean the beer lines (Drinks tab).";
                default: return null;
            }
        }

        /// Fills rec.highlight, rec.problem and rec.fix from tonight's service.
        public static void Verdict(GameState s, NightRecord rec, int[] noteCounts, int arrivals)
        {
            // Biggest problem: the most common complaint, if enough people had it to matter.
            Note worst = Note.Count; int worstCount = 0;
            foreach (var n in Complaints) if (noteCounts[(int)n] > worstCount) { worst = n; worstCount = noteCounts[(int)n]; }
            if (worst != Note.Count && arrivals > 0 && worstCount >= Math.Max(3, arrivals * 0.12f))
            {
                rec.problem = ProblemText(worst, worstCount, arrivals);
                rec.fix = FixFor(worst);
            }

            // Best thing: a record, a big event, or what people loved most.
            float bestTakings = 0; int mostCustomers = 0;
            foreach (var h in s.history) { bestTakings = Math.Max(bestTakings, h.takings); mostCustomers = Math.Max(mostCustomers, h.customers); }
            if (rec.eventOutcome == "Viral hit") rec.highlight = $"{rec.eventName} went viral.";
            else if (s.history.Count >= 3 && rec.takings > bestTakings) rec.highlight = $"Your best takings yet: £{rec.takings:#,0}.";
            else if (s.history.Count >= 3 && rec.customers > mostCustomers) rec.highlight = $"Your busiest night yet: {rec.customers} customers.";
            else if (rec.eventOutcome == "Hit") rec.highlight = $"{rec.eventName} was a hit.";
            else
            {
                Note best = Note.Count; int bestCount = 0;
                foreach (var n in new[] { Note.LovedVibe, Note.LovedDrink, Note.FriendlyStaff, Note.LovedMusic, Note.ComfySeat, Note.Bargain, Note.Dog, Note.Games })
                    if (noteCounts[(int)n] > bestCount) { best = n; bestCount = noteCounts[(int)n]; }
                if (best != Note.Count && arrivals > 0 && bestCount >= Math.Max(3, arrivals * 0.1f)) rec.highlight = PraiseText(best, bestCount, arrivals);
            }
        }

        static string Pct(int count, int total) => $"{Math.Round(100f * count / Math.Max(1, total))}%";

        static string ProblemText(Note n, int count, int total)
        {
            switch (n)
            {
                case Note.WaitedLong: return $"Slow service: {Pct(count, total)} waited too long at the bar.";
                case Note.GaveUpQueue: return $"{count} people gave up queuing for a drink.";
                case Note.Dirty: return $"{Pct(count, total)} thought the place was dirty.";
                case Note.OutOfStock: return $"{count} couldn't get the drink they wanted.";
                case Note.Pricey: return $"{Pct(count, total)} thought it was overpriced.";
                case Note.NoSeat: return $"{Pct(count, total)} couldn't find a seat.";
                case Note.Crowded: return $"{Pct(count, total)} found it too crowded.";
                case Note.ToiletQueue: return $"{count} complained about the toilet queue.";
                case Note.NoToilet: return $"{count} were horrified there were no toilets.";
                case Note.HatedVibe: return $"{Pct(count, total)} felt it wasn't their kind of place.";
                case Note.HatedMusic: return $"{Pct(count, total)} hated the music.";
                case Note.RowdyNearby: return $"{count} were bothered by rowdy customers.";
                case Note.GrumpyStaff: return $"{count} found the staff grumpy.";
                case Note.BadDrink: return $"{Pct(count, total)} said a drink tasted off.";
                default: return null;
            }
        }

        static string PraiseText(Note n, int count, int total)
        {
            switch (n)
            {
                case Note.LovedVibe: return $"{Pct(count, total)} loved the vibe.";
                case Note.LovedDrink: return $"{Pct(count, total)} loved their drinks.";
                case Note.FriendlyStaff: return $"{Pct(count, total)} said the staff were lovely.";
                case Note.LovedMusic: return $"{Pct(count, total)} liked the music.";
                case Note.ComfySeat: return $"{Pct(count, total)} loved the comfy seating.";
                case Note.Bargain: return $"{Pct(count, total)} thought it was great value.";
                case Note.Dog: return $"{count} people fell for Biscuit the dog.";
                case Note.Games: return $"{count} people had fun with the games.";
                default: return null;
            }
        }

        // ---- Weekly statement (task T6) ----------------------------------------------------------------

        public sealed class WeekStatement
        {
            public int Nights;
            public float Takings, Drinks, Wages, Utilities, Rent, Loan, Other, Profit;
            public float PrevProfit; public bool HasPrev;
            public int BestNightDay = -1; public float BestNightTakings;
            public int Customers;
        }

        /// The week ending with the most recent night in history (up to 7 nights), plus last week's profit for comparison.
        public static WeekStatement LastWeek(GameState s)
        {
            var w = new WeekStatement();
            int end = s.history.Count - 1;
            for (int i = end; i >= 0 && i > end - 7; i--)
            {
                var h = s.history[i];
                w.Nights++;
                w.Takings += h.takings; w.Drinks += h.cogs; w.Wages += h.wages; w.Utilities += h.utilities;
                w.Rent += h.rent; w.Loan += h.loan; w.Other += h.other; w.Profit += h.profit;
                w.Customers += h.customers;
                if (h.takings > w.BestNightTakings) { w.BestNightTakings = h.takings; w.BestNightDay = h.day; }
            }
            for (int i = end - 7; i >= 0 && i > end - 14; i--) { w.PrevProfit += s.history[i].profit; w.HasPrev = true; }
            return w;
        }

        // ---- Staff -------------------------------------------------------------------------------------

        static readonly uint[] Shirts = { 0x264653, 0x2a9d8f, 0xe76f51, 0x6d597a, 0x355070, 0xb56576, 0x1d3557, 0x588157 };
        static readonly uint[] Skins = { 0xf1c27d, 0xe0ac69, 0xc68642, 0x8d5524, 0xffdbac, 0x5c3a21 };
        static readonly uint[] Hairs = { 0x2b1b10, 0x4a3020, 0xd9b26f, 0x8b4513, 0x111111, 0xa0522d, 0xc0392b };

        public static StaffMember MakeApplicant(GameState s, Rng r, int role, int quality)
        {
            int Attr() => Math.Max(1, Math.Min(20, (int)Math.Round(quality + r.Gaussian() * 3.5f)));
            var m = new StaffMember
            {
                uid = s.nextUid++, name = Content.RandomName(r), role = role,
                speed = Attr(), charm = Attr(), skill = Attr(), stamina = Attr(),
                morale = r.Range(60f, 80f), level = 1,
                shirt = r.Pick(Shirts), skin = r.Pick(Skins), hair = r.Pick(Hairs),
            };
            int traits = r.Chance(0.35) ? 2 : 1;
            while (m.traits.Count < traits)
            {
                var t = Content.Traits[r.Range(0, Content.Traits.Count)].Id;
                if (t == "cocktail_nerd" && role != 0) continue;
                if (!m.traits.Contains(t)) m.traits.Add(t);
            }
            m.wage = FairWage(m);
            if (m.HasTrait("always_late") || m.HasTrait("butterfingers")) m.wage -= 8;
            if (m.HasTrait("calm") || m.HasTrait("cocktail_nerd")) m.wage += 6;
            return m;
        }

        public static int FairWage(StaffMember m)
        {
            float avg = (m.speed + m.charm + m.skill + m.stamina) / 4f;
            return (int)(55 + avg * 5f);
        }

        public static void RefreshApplicants(GameState s, Rng r)
        {
            s.applicants.Clear();
            int baseQ = 7 + (int)(s.reputation / 12f);
            // Two bartenders, then bar or floor, then floor or (once you're known) door security.
            for (int i = 0; i < 4; i++)
            {
                int role = i < 2 ? Roles.Bartender : i == 2 ? (r.Chance(0.5) ? Roles.Bartender : Roles.Floor) : (r.Chance(0.5) ? Roles.Floor : (s.reputation >= 15 ? Roles.Door : Roles.Floor));
                s.applicants.Add(MakeApplicant(s, r, role, baseQ + r.Range(-2, 4)));
            }
        }

        // ---- Acts ----------------------------------------------------------------------------------------

        public static void AddOpenMicActs(GameState s, Rng r)
        {
            s.agency.Add(new Act { uid = s.nextUid++, kind = 2, name = "Open Mic (you host)", style = "anything goes", fee = 30, quality = r.Range(30, 70), reliability = 100, following = 5, volatility = 0.6f });
            s.agency.Add(new Act { uid = s.nextUid++, kind = 2, name = "Poetry & Pints collective", style = "spoken word", fee = 80, quality = r.Range(45, 80), reliability = 90, following = 25, volatility = 0.4f });
        }

        public static void RefreshAgency(GameState s, Rng r)
        {
            s.agency.Clear();
            s.agency.Add(new Act { uid = s.nextUid++, kind = 0, name = "You (DIY quiz)", style = "home-made", fee = 0, quality = 42, reliability = 100, following = 0, volatility = 0.3f });
            s.agency.Add(new Act { uid = s.nextUid++, kind = 0, name = "Quizmaster Quentin", style = "classic", fee = 90, quality = 70, reliability = 95, following = 15, volatility = 0.15f });
            s.agency.Add(new Act { uid = s.nextUid++, kind = 0, name = "The QuizBot 3000", style = "machine", fee = 45, quality = 56, reliability = 80, following = 5, volatility = 0.45f });
            AddOpenMicActs(s, r);
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                bool star = i == n - 1 && s.reputation >= 35;
                int q = star ? r.Range(75, 92) : r.Range(28, 78);
                int follow = star ? r.Range(55, 90) : Math.Max(0, q - 30 + r.Range(-10, 15));
                int fee = (int)(40 + q * q * 0.04f + follow * 2.5f) / 10 * 10;
                s.agency.Add(new Act
                {
                    uid = s.nextUid++, kind = 1, name = Content.RandomName(r), style = r.Pick(Content.ComedyStyles),
                    fee = fee, quality = q, reliability = r.Range(62, 99), following = follow,
                    volatility = q < 50 ? r.Range(0.3f, 0.55f) : r.Range(0.1f, 0.35f),
                });
            }
        }
    }

    public static class Reviews
    {
        static readonly Dictionary<Note, string[]> Good = new Dictionary<Note, string[]>
        {
            { Note.LovedDrink, new[] { "The {drink} was spot on.", "Best {drink} I've had in ages.", "They really know their drinks." } },
            { Note.Bargain, new[] { "Prices are a steal.", "Great value for money." } },
            { Note.ComfySeat, new[] { "Comfiest seats in the city.", "Sank into a lovely seat for the night." } },
            { Note.LovedVibe, new[] { "Love the atmosphere.", "Just the right vibe.", "Feels like my kind of place." } },
            { Note.LovedEvent, new[] { "The {event} was brilliant!", "Came for the {event}, will be back for the next one." } },
            { Note.FriendlyStaff, new[] { "Staff were lovely.", "Bartender was a legend." } },
            { Note.Dog, new[] { "There's a DOG. Five stars for the dog alone.", "Biscuit the bar dog is the real star." } },
            { Note.Games, new[] { "Great games to keep us busy.", "Lost a fortune on the pool table, worth it." } },
            { Note.Screens, new[] { "Perfect spot to watch the match.", "Big screens, great atmosphere for the game." } },
            { Note.LovedMusic, new[] { "Music was bang on.", "Great playlist." } },
            { Note.HappyHour, new[] { "Happy hour deals are top notch." } },
            { Note.Regular, new[] { "My local. Never lets me down.", "Been coming here for weeks and it just gets better." } },
            { Note.Refused, new[] { "Staff look after you here, which I respect." } },
        };

        static readonly Dictionary<Note, string[]> Bad = new Dictionary<Note, string[]>
        {
            { Note.WaitedLong, new[] { "Waited forever at the bar.", "Service was painfully slow.", "Lovely place but waited ages for a drink." } },
            { Note.GaveUpQueue, new[] { "Gave up queuing and left.", "Never actually got served." } },
            { Note.BadDrink, new[] { "The {drink} tasted off.", "Clean your lines, please." } },
            { Note.OutOfStock, new[] { "They'd run out of half the menu.", "No {drink}? Really?" } },
            { Note.Pricey, new[] { "Overpriced for what it is.", "Daylight robbery at the bar.", "Bit of a rip-off." } },
            { Note.Dirty, new[] { "Sticky tables and empties everywhere.", "Could do with a good clean." } },
            { Note.NoSeat, new[] { "Nowhere to sit.", "Stood all night, my feet hurt." } },
            { Note.HatedVibe, new[] { "Not my kind of place.", "Weird atmosphere." } },
            { Note.EventBombed, new[] { "The {event} was painful.", "Worst {event} I've ever sat through." } },
            { Note.ToiletQueue, new[] { "Toilet queue was a nightmare.", "Need more toilets!" } },
            { Note.NoToilet, new[] { "No toilets. NO TOILETS.", "How can a bar not have toilets?" } },
            { Note.GrumpyStaff, new[] { "Bartender had a face like thunder.", "Staff were rude." } },
            { Note.RowdyNearby, new[] { "Some idiot ruined the night for everyone.", "Got a bit rough." } },
            { Note.Slipped, new[] { "Someone went flying on a spill. Hilarious but dangerous." } },
            { Note.HatedMusic, new[] { "The music was dreadful.", "Who picks these tunes?" } },
            { Note.Crowded, new[] { "Absolutely rammed.", "Couldn't move for people." } },
            { Note.ShownDoor, new[] { "Got thrown out for having fun. Rubbish." } },
        };

        public static Review Write(Customer c, int stars, Rng r, GameState s, Service svc)
        {
            var parts = new List<string>();
            var goods = new List<Note>(); var bads = new List<Note>();
            foreach (var kv in Good) if (c.Has(kv.Key)) goods.Add(kv.Key);
            foreach (var kv in Bad) if (c.Has(kv.Key)) bads.Add(kv.Key);
            string evName = svc.Event == null ? "event" : svc.Event.kind == 0 ? "quiz" : svc.Event.kind == 1 ? "comedy" : "open mic";
            string drink = c.drink?.Name ?? (c.regular != null ? Content.Drink(c.regular.favDrink)?.Name : null) ?? "pint";
            string Fill(string t) => t.Replace("{drink}", drink).Replace("{event}", evName);

            if (stars >= 4)
            {
                if (goods.Count > 0) parts.Add(Fill(r.Pick(Good[r.Pick(goods)])));
                if (goods.Count > 1 && r.Chance(0.5)) parts.Add(Fill(r.Pick(Good[r.Pick(goods)])));
                if (bads.Count > 0 && r.Chance(0.6)) parts.Add("Only gripe: " + Lower(Fill(r.Pick(Bad[r.Pick(bads)]))));
                if (parts.Count == 0) parts.Add(r.Pick(new[] { "Great night out.", "Lovely spot.", "Would recommend." }));
            }
            else if (stars <= 2)
            {
                if (bads.Count > 0) parts.Add(Fill(r.Pick(Bad[r.Pick(bads)])));
                if (bads.Count > 1 && r.Chance(0.6)) parts.Add(Fill(r.Pick(Bad[r.Pick(bads)])));
                var complaint = VibeCalc.Complaint(svc.V.Axes, c.def);
                if (complaint != null && r.Chance(0.5)) parts.Add(complaint + ".");
                if (parts.Count == 0) parts.Add(r.Pick(new[] { "Meh.", "Won't be rushing back.", "Disappointing." }));
            }
            else
            {
                if (goods.Count > 0) parts.Add(Fill(r.Pick(Good[r.Pick(goods)])));
                if (bads.Count > 0) parts.Add("But " + Lower(Fill(r.Pick(Bad[r.Pick(bads)]))));
                if (parts.Count == 0) parts.Add(r.Pick(new[] { "Decent enough.", "Fine for a quick one.", "Nothing special, nothing wrong." }));
            }
            // de-duplicate
            var seen = new HashSet<string>(); var final = new List<string>();
            foreach (var p in parts) if (seen.Add(p)) final.Add(p);

            var name = c.name.Split(' ');
            return new Review
            {
                day = s.day, stars = stars,
                author = c.regular != null ? c.regular.name : $"{name[0]} {name[name.Length - 1][0]}.",
                segment = c.def.Name, text = string.Join(" ", final),
            };
        }

        static string Lower(string s) => s.Length > 0 ? char.ToLowerInvariant(s[0]) + s.Substring(1) : s;
    }
}
