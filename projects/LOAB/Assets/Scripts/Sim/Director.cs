using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public sealed class CardOption
    {
        public string Label, Detail;
        public Func<Game, string> CanChoose; // null = always
        public Action<Game> Apply;
    }

    public sealed class CardDef
    {
        public string Id, Title, Text, Art;
        public bool Once;
        public float Weight = 1f;
        public Func<GameState, bool> When;
        public Func<Game, string> Format; // optional dynamic text
        public List<CardOption> Options = new List<CardOption>();
    }

    /// <summary>The storyteller: one opportunity or problem most mornings, scaled to the bar's situation.</summary>
    public static class Director
    {
        public static readonly List<CardDef> Cards = new List<CardDef>();

        public static CardDef Get(string id) => Cards.Find(c => c.Id == id);

        static CardOption Opt(string label, string detail, Action<Game> apply, Func<Game, string> can = null) =>
            new CardOption { Label = label, Detail = detail, Apply = apply, CanChoose = can };

        static Func<Game, string> Afford(float amount) => g => g.S.cash >= amount || g.S.difficulty == 0 ? null : "Not enough cash";

        static Director()
        {
            Cards.Add(new CardDef
            {
                Id = "dog", Title = "A visitor", Art = "dog", Once = true, Weight = 1.4f,
                Text = "A scruffy, extremely polite dog has wandered in and lain down by the bar. Nobody knows whose it is. It does not want to leave.",
                When = s => s.day >= 3,
                Options =
                {
                    Opt("Adopt them", "Meet Biscuit, the bar dog. +Quirk, customers love it. £5 a night in biscuits.", g =>
                    {
                        g.S.SetFlag("dog", 1);
                        g.S.unlockedItems.Add("dog_bed");
                        g.TryAutoPlace("dog_bed");
                    }),
                    Opt("Find the owner", "Locals appreciate it. Small reputation boost.", g => g.S.reputation += 1.5f),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "brewery", Title = "Brewery offer", Art = "keg", Weight = 1f,
                Text = "Docklands Brewing Co. are launching a new hazy IPA and want it on your bar. They'll give you a free keg to get started.",
                When = s => s.day >= 4,
                Options =
                {
                    Opt("Pour it", "A free keg of Docklands IPA goes on the menu. Craft beer fans take note.", g =>
                    {
                        var m = g.S.Menu("ipa"); m.onMenu = true; m.stock += 88;
                        g.S.awareness[(int)Seg.Creatives] += 0.03f;
                    }),
                    Opt("Not for us", "Keep your menu as it is.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "pipe", Title = "Burst pipe", Art = "pipe", Weight = 0.9f,
                Text = "Water is pouring through the ceiling of the toilets. It's not clear where from. It's definitely not clean.",
                When = s => s.day >= 2,
                Options =
                {
                    Opt("Emergency plumber", "£250. Fixed properly before opening.", g => g.Spend(250, "Emergency plumber"), Afford(250)),
                    Opt("Bodge it", "Free, but the place will be grim tonight (cleanliness down).", g => g.S.cleanliness = Math.Max(20, g.S.cleanliness - 30)),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "rival", Title = "Rival opening", Art = "rival", Weight = 0.8f,
                Text = "'The Gilded Pint' opens across the road this week, with £2.50 pints and a man in a giant pint costume outside.",
                When = s => s.day >= 7 && s.rivalDays <= 0,
                Options =
                {
                    Opt("Fight back", "Run a daily 6-7pm happy hour this week. Rival impact halved.", g => { g.S.rivalPressure = 0.25f; g.S.rivalDays = 7; g.S.happyHourStart = 18; }),
                    Opt("Hold your nerve", "Trust your regulars. Footfall dips for a week.", g => { g.S.rivalPressure = 0.5f; g.S.rivalDays = 7; }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "film", Title = "Lights, camera...", Art = "film", Once = true,
                Text = "A TV crew wants to film a scene for a gritty detective drama in your bar tonight. They'll pay well, but you'll have to stay closed.",
                When = s => s.day >= 5 && !s.events.Exists(e => e.day == s.day),
                Options =
                {
                    Opt("Take the money", "+£1,100, closed tonight. Your bar will be on telly!", g => { g.S.closedTonight = true; g.S.cash += 1100; g.S.SetFlag("film", 1); }),
                    Opt("No thanks", "Business as usual.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "hen", Title = "Hen party!", Art = "hen", Weight = 1f,
                Text = "A hen party of twelve wants to make your bar their first stop tonight. Sashes, inflatables, enormous enthusiasm.",
                When = s => s.day >= 3 && s.DayOfWeek >= 3,
                Options =
                {
                    Opt("Welcome them", "Twelve big spenders arrive at 8pm. It may get loud.", g => g.S.SetFlag("hen_day", g.S.day + 1)),
                    Opt("Politely decline", "A quieter night.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "inspector", Title = "Inspection tip-off", Art = "clipboard", Weight = 0.9f,
                Text = "A friendly neighbour mentions the licensing inspector is doing the rounds tonight.",
                When = s => s.day >= 6,
                Options =
                {
                    Opt("Deep clean (£80)", "Spotless tonight. The inspector will be impressed.", g => { g.Spend(80, "Deep clean"); g.S.cleanliness = 100; g.S.SetFlag("inspector", g.S.day + 1); }, Afford(80)),
                    Opt("Wing it", "If it's grubby or rowdy, expect a fine.", g => g.S.SetFlag("inspector", g.S.day + 1)),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "critic", Title = "The critic", Art = "critic", Weight = 0.8f,
                Text = "Fenella Crisp, the city's most feared bar critic, has requested a table tonight.",
                When = s => s.day >= 8,
                Options =
                {
                    Opt("Roll out the red carpet", "She'll judge the whole night. Big reputation swing either way.", g => g.S.SetFlag("critic", g.S.day + 1)),
                    Opt("'Fully booked'", "Avoid the risk.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "influencer", Title = "Sponsored post", Art = "phone", Weight = 1f,
                Text = "A local influencer with a suspicious number of followers offers to post about your bar for £150.",
                When = s => s.day >= 3,
                Options =
                {
                    Opt("Pay up (£150)", "Awareness boost with students, young professionals and creatives.", g =>
                    {
                        g.Spend(150, "Influencer post");
                        g.S.awareness[(int)Seg.Students] += 0.05f; g.S.awareness[(int)Seg.YoungPros] += 0.05f; g.S.awareness[(int)Seg.Creatives] += 0.04f;
                    }, Afford(150)),
                    Opt("Pass", "Save your money.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "raise", Title = "Pay rise request", Art = "staff", Weight = 1.1f,
                When = s => s.staff.Count > 0 && s.day >= 6,
                Format = g => $"{g.CardStaff()?.name ?? "A member of staff"} has asked for a £10 a night pay rise. They say they've been carrying the place.",
                Options =
                {
                    Opt("Agree", "+£10 a night, big morale boost.", g => { var m = g.CardStaff(); if (m != null) { m.wage += 10; m.morale = Math.Min(100, m.morale + 25); } }),
                    Opt("Refuse", "Saves money. Morale will suffer.", g => { var m = g.CardStaff(); if (m != null) m.morale -= 25; }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "charity", Title = "Community collection", Art = "heart", Weight = 0.8f,
                Text = "The locals are raising money to fix the community centre roof. Would the bar like to chip in and host a collection tin?",
                When = s => s.day >= 4,
                Options =
                {
                    Opt("Donate £100", "Locals will remember this. Reputation and local awareness up.", g => { g.Spend(100, "Charity donation"); g.S.reputation += 3; g.S.awareness[(int)Seg.Locals] += 0.06f; }, Afford(100)),
                    Opt("Just the tin", "A small gesture.", g => g.S.awareness[(int)Seg.Locals] += 0.015f),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "glasswasher", Title = "Grinding noises", Art = "glass", Weight = 0.9f,
                Text = "The glass washer is making a noise like a dying walrus.",
                When = s => s.day >= 5,
                Options =
                {
                    Opt("Repair it (£120)", "Business as usual.", g => g.Spend(120, "Glass washer repair"), Afford(120)),
                    Opt("Ignore it", "You'll start tonight with far fewer clean glasses.", g => g.S.SetFlag("glasswasher", g.S.day + 1)),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "busker", Title = "The busker", Art = "guitar", Weight = 0.8f,
                Text = "A genuinely talented busker asks if she can play a few songs in the corner tonight, just for tips.",
                When = s => s.day >= 2,
                Options =
                {
                    Opt("Let her play", "Everyone's a bit happier tonight.", g => g.S.SetFlag("busker", g.S.day + 1)),
                    Opt("Not tonight", "Keep your usual music.", g => { }),
                },
            });
        }

        public static void Roll(Game g, Rng r)
        {
            var s = g.S;
            if (s.day < 2 || !r.Chance(0.5)) return;
            var options = new List<CardDef>();
            var weights = new List<float>();
            foreach (var c in Cards)
            {
                if (c.When != null && !c.When(s)) continue;
                if (c.Once && s.cardHistory.Contains(c.Id)) continue;
                int lastIdx = s.cardHistory.LastIndexOf(c.Id);
                if (lastIdx >= 0 && s.cardHistory.Count - lastIdx < 6) continue; // not too often
                options.Add(c); weights.Add(c.Weight);
            }
            int i = r.WeightedIndex(weights);
            if (i < 0) return;
            s.pendingCard = options[i].Id;
            if (options[i].Id == "raise") s.SetFlag("card_staff", s.staff[r.Range(0, s.staff.Count)].uid);
        }
    }

    public sealed class GoalDef
    {
        public string Text, Mentor;
        public int Reward;
        public Func<Game, bool> Done;
    }

    public static class Goals
    {
        public static readonly List<GoalDef> Career = new List<GoalDef>
        {
            new GoalDef { Text = "Open the doors for your first night", Reward = 0, Done = g => g.S.history.Count >= 1,
                Mentor = "You did it, love. First night under your belt. Read the report, it tells you what people thought." },
            new GoalDef { Text = "Take £400 at the bar in a single night", Reward = 200, Done = g => g.S.history.Exists(h => h.takings >= 400),
                Mentor = "Four hundred quid through the till! Now keep an eye on your stock, running dry is the worst." },
            new GoalDef { Text = "Hire a second member of staff", Reward = 200, Done = g => g.S.staff.Count >= 2,
                Mentor = "Good. You can't do it all yourself. Floor staff keep the place clean, bartenders keep the queue down." },
            new GoalDef { Text = "Host a Pub Quiz", Reward = 300, Done = g => g.S.GetFlag("quiz_hosted") > 0,
                Mentor = "Quiz night! Nothing builds a crowd of regulars like a weekly quiz." },
            new GoalDef { Text = "Reach a 3.5★ BarHopper rating (10+ reviews)", Reward = 400, Done = g => g.S.rating >= 3.5f && g.S.reviewCount >= 10,
                Mentor = "Three and a half stars. People are starting to talk about you, in a good way." },
            new GoalDef { Text = "Build a stage and host a Comedy Night", Reward = 500, Done = g => g.S.GetFlag("comedy_hosted") > 0,
                Mentor = "Comedy's a gamble. Some nights they bomb. But when it lands, the whole city hears about it." },
            new GoalDef { Text = "Get a bar theme recognised (see the Vibe panel)", Reward = 500, Done = g => g.V != null && g.V.ActiveTheme != null,
                Mentor = "Now it feels like a proper place with its own identity. Customers who love that style will seek you out." },
            new GoalDef { Text = "Serve 100 customers in one night", Reward = 600, Done = g => g.S.history.Exists(h => h.customers >= 100),
                Mentor = "A hundred people through the door! You'll want more bar staff and seats if this keeps up." },
            new GoalDef { Text = "Earn the loyalty of 6 regulars", Reward = 600, Done = g => g.S.regulars.Count >= 6,
                Mentor = "Regulars are the heart of a bar. Look after them and they'll look after you." },
            new GoalDef { Text = "Build up £15,000 in the bank", Reward = 1000, Done = g => g.S.cash >= 15000,
                Mentor = "Act One complete, my love. You survived the dream. I always knew you would. Keep going, the city's yours." },
        };
    }

    public static class Mentor
    {
        public const string Name = "Maggie";

        public static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            { "welcome", "Welcome to The Crooked Anchor! I ran this place for thirty years before you bought the lease. It's tired, but it's got good bones. Let's get you open." },
            { "prep", "This is Prep time. The clock's stopped, so take as long as you like. Build, set the menu, sort staff, then hit OPEN DOORS when you're ready." },
            { "service", "Doors are open! Customers come in, queue at the bar and find a seat. Tap spills and rowdy customers to deal with them. Use the buttons at the bottom if things get busy." },
            { "report", "Here's how the night went. Reviews tell you what to fix: slow service, prices, dirty tables, the lot." },
            { "vibe", "Everything you put in here changes the Vibe. Different crowds like different things. Students like it lively and cheap; young professionals want it polished and modern. Match the crowd you want." },
            { "stock", "Keep an eye on stock. A keg's about 88 pints. If you run out mid-service, people get grumpy. Use Restock All if you can't be bothered with the maths." },
            { "events", "Book a Pub Quiz or a Comedy Night from the Events tab. Promote it for a bigger crowd. Comedy needs a stage." },
            { "staff", "One bartender can manage about 30 drinks an hour. If queues are long, hire more. Floor staff collect glasses and mop spills." },
            { "cash", "Cash is getting low. Watch your wages and stock spending. Rent's due every Sunday night." },
            { "card", "Every so often something happens: an opportunity, a problem, or a dog. Choose wisely." },
        };
    }
}
