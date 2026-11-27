using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public static class Economy
    {
        public const float RentPerWeek = 1400f;
        public const float UtilitiesPerNight = 60f;
        public const float BaseFootfall = 270f;

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
                case 0: return 1.0f + (dayInSeason < 7 ? 0.1f : 0f);   // autumn: students return
                case 1: return dayInSeason >= 14 && dayInSeason < 21 ? 1.35f : dayInSeason >= 21 ? 0.8f : 1.05f; // winter: Christmas peak, January slump
                case 2: return 1.0f;
                default: return 1.12f;                                 // summer
            }
        }

        public static string SeasonNote(GameState s)
        {
            int d = s.day % 28;
            if (s.Season == 1 && d >= 14 && d < 21) return "Christmas party season: expect big crowds.";
            if (s.Season == 1 && d >= 21) return "The January slump. Alcohol-free drinks are in demand.";
            if (s.Season == 0 && d < 7) return "Freshers' week: students are out in force.";
            if (s.Season == 3) return "Summer evenings: everyone's out.";
            return null;
        }

        /// <summary>Expected customers per segment tonight, plus extra event-driven arrivals.</summary>
        public static void ExpectedArrivals(GameState s, VibeReport v, float[] outExpected, BookedEvent ev, out float eventExtra)
        {
            float foot = BaseFootfall * SeasonMult(s) * WeatherMult(s.weather) * (1f - s.rivalPressure * 0.3f);
            if (s.day == 0) foot *= 1.5f; // opening-night curiosity
            float rep = 0.35f + s.reputation / 100f * 1.25f;
            float ratio = VibeCalc.PriceRatio(s);
            bool canServe = v.BarPoints > 0;
            eventExtra = 0;
            for (int i = 0; i < 5; i++)
            {
                var d = Content.Segments[i];
                float day = d.DayWeights[s.DayOfWeek] * (s.DayOfWeek >= 4 ? 1.15f : 1f);
                float price = (float)Math.Exp(-d.PriceSensitivity * Math.Max(0, ratio - 1f) * 2.2f) * (1f + d.PriceSensitivity * Math.Max(0, 1f - ratio) * 0.8f);
                float ent = 1f + d.Games * Math.Min(v.Games, 3) * 0.06f + d.Screens * (v.Screens > 0 ? 0.22f : 0f) + d.Stage * (v.HasStage ? 0.06f : 0f);
                float theme = 1f;
                if (v.ActiveTheme != null && d.ThemeAffinity.TryGetValue(v.ActiveTheme.Def.Id, out float aff)) theme += aff * 0.25f;
                float sports = (s.DayOfWeek == 2 || s.DayOfWeek == 5) && i == (int)Seg.SportsFans && v.Screens > 0 ? 1.5f : 1f; // match days
                float appeal = (0.15f + 0.85f * v.SegMatch[i]) * rep * price * ent * theme * sports;
                float social = 1f + s.socialPostBoost;
                outExpected[i] = canServe ? foot * d.Share * s.awareness[i] * appeal * day * social : 0;

                if (ev != null)
                {
                    float aff2 = ev.kind == 0 ? d.Quiz : d.Comedy;
                    float draw = (ev.kind == 0 ? 10f : 6f) + ev.act.following * 0.25f + ev.promo * 0.06f;
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
            for (int i = 0; i < 4; i++) s.applicants.Add(MakeApplicant(s, r, i < 2 ? 0 : (r.Chance(0.5) ? 0 : 1), baseQ + r.Range(-2, 4)));
        }

        // ---- Acts ----------------------------------------------------------------------------------------

        public static void RefreshAgency(GameState s, Rng r)
        {
            s.agency.Clear();
            s.agency.Add(new Act { uid = s.nextUid++, kind = 0, name = "You (DIY quiz)", style = "home-made", fee = 0, quality = 42, reliability = 100, following = 0, volatility = 0.3f });
            s.agency.Add(new Act { uid = s.nextUid++, kind = 0, name = "Quizmaster Quentin", style = "classic", fee = 90, quality = 70, reliability = 95, following = 15, volatility = 0.15f });
            s.agency.Add(new Act { uid = s.nextUid++, kind = 0, name = "The QuizBot 3000", style = "machine", fee = 45, quality = 56, reliability = 80, following = 5, volatility = 0.45f });
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
            string evName = svc.Event == null ? "event" : svc.Event.kind == 0 ? "quiz" : "comedy";
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
