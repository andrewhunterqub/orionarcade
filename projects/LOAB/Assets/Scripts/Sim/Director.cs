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
                    Opt("Deep clean", "Spotless tonight. The inspector will be impressed.", g => { g.Spend(g.DeepCleanCost, "Deep clean"); g.S.cleanliness = 100; g.S.SetFlag("inspector", g.S.day + 1); }, g => g.S.cash >= g.DeepCleanCost || g.S.difficulty == 0 ? null : "Not enough cash"),
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
            // ---- More stories (task K7) ---------------------------------------------------------------
            Cards.Add(new CardDef
            {
                Id = "power_cut", Title = "Power cut", Art = "pipe", Weight = 0.7f,
                Text = "The electrics trip at five o'clock. The whole street's dark.",
                When = s => s.day >= 4,
                Options =
                {
                    Opt("Emergency electrician (£180)", "Lights back on in time to open.", g => g.Spend(180, "Electrician"), Afford(180)),
                    Opt("Candlelit opening", "Open an hour late by candlelight. Couples love it; everyone else is confused.", g => { g.S.SetFlag("late_open", g.S.day + 1); g.S.awareness[(int)Seg.Couples] += 0.03f; }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "burst_keg", Title = "Burst keg", Art = "keg", Weight = 0.8f,
                Format = g => $"A keg of {BurstName(g)} has split in the cellar. It's all over the floor.",
                When = s => s.day >= 4 && s.menu.Exists(m => m.onMenu && m.stock > 0 && Content.Drink(m.id).CaseSize == 88),
                Options =
                {
                    Opt("Order a replacement (£150)", "Back in stock tonight.", g => { g.Spend(150, "Replacement keg"); }, Afford(150)),
                    Opt("Do without", "It's off tonight. Mop and move on.", g => { var m = BurstEntry(g); if (m != null) m.stock = 0; }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "stag", Title = "Stag do", Art = "hen", Weight = 0.7f,
                Text = "A stag party of twelve, matching T-shirts and all, wants to make your bar their first stop tonight.",
                When = s => s.day >= 5 && s.DayOfWeek >= 3,
                Options =
                {
                    Opt("Welcome them", "Big spenders. Also big singers. Expect a rowdier night.", g => { g.S.SetFlag("hen_day", g.S.day + 1); g.S.SetFlag("stag", g.S.day + 1); }),
                    Opt("Politely decline", "A quieter night for your regulars.", g => g.S.reputation += 0.5f),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "scout", Title = "Location scout", Art = "film", Weight = 0.5f, Once = true,
                Text = "A TV drama wants to film a scene at your bar tomorrow morning. They'll be gone by opening.",
                When = s => s.reputation >= 25,
                Options =
                {
                    Opt("Roll camera", "£400, and the bar's on telly. They'll leave a bit of a mess.", g => { g.S.cash += 400; g.S.cleanliness = Math.Max(0, g.S.cleanliness - 12); g.S.reputation += 2; g.AddTagPublic("As Seen on TV"); }),
                    Opt("No thanks", "Peace and quiet.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "licensing", Title = "Licensing review", Art = "clipboard", Weight = 0.6f,
                Text = "The council is reviewing late licences in the area, and yours is on the list.",
                When = s => s.day >= 14,
                Options =
                {
                    Opt("Show them your policies", "If responsible service is on, you'll sail through. If not, a £400 fine.", g =>
                    {
                        if (g.S.responsibleService) g.S.reputation += 2;
                        else g.Spend(400, "Licensing fine");
                    }),
                    Opt("Hire a licensing consultant (£250)", "Expensive, but guaranteed to pass.", g => g.Spend(250, "Licensing consultant"), Afford(250)),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "romance", Title = "Behind-the-bar romance", Art = "heart", Weight = 0.5f,
                Format = g => { var p = Pair(g); return p == null ? "Love is in the air." : $"{p.Item1.name} and {p.Item2.name} have started seeing each other. Everyone knows. They think nobody knows."; },
                When = s => s.staff.Count >= 2,
                Options =
                {
                    Opt("Wish them well", "Morale up for both, for now.", g => { var p = Pair(g); if (p != null) { p.Item1.morale = Math.Min(100, p.Item1.morale + 10); p.Item2.morale = Math.Min(100, p.Item2.morale + 10); } }),
                    Opt("Keep it professional", "Different shifts where you can. A little grumbling.", g => { var p = Pair(g); if (p != null) { p.Item1.morale = Math.Max(0, p.Item1.morale - 5); p.Item2.morale = Math.Max(0, p.Item2.morale - 5); } }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "poach", Title = "Poaching attempt", Art = "rival", Weight = 0.6f,
                Format = g => { var m = Best(g); return m == null ? "A rival is sniffing around your staff." : $"The bar across town has offered {m.name} a job, and more money."; },
                When = s => s.staff.Count >= 2 && s.day >= 10,
                Options =
                {
                    Opt("Match it (+£15 a night)", "They stay, and they're touched you fought for them.", g => { var m = Best(g); if (m != null) { m.wage += 15; m.morale = Math.Min(100, m.morale + 12); } }),
                    Opt("Let them decide", "If they're happy here, they'll stay. If not...", g =>
                    {
                        var m = Best(g);
                        if (m != null && m.morale < 60) { g.S.staff.Remove(m); g.S.reputation -= 1; }
                        else if (m != null) m.morale = Math.Min(100, m.morale + 5);
                    }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "quiz_vote", Title = "Best Quiz in Town?", Art = "phone", Weight = 0.6f, Once = true,
                Text = "The local paper is running a reader vote for the best pub quiz in the city. You're on the shortlist.",
                When = s => s.GetFlag("quiz_hosted") >= 3,
                Options =
                {
                    Opt("Campaign for votes (£100)", "Posters, social posts, shameless begging. You'll probably win.", g => { g.Spend(100, "Quiz campaign"); g.AddTagPublic("Best Quiz in Town"); g.S.awareness[(int)Seg.Locals] += 0.05f; }, Afford(100)),
                    Opt("Let the quiz speak for itself", "If your quizzes have been hits, you'll win anyway.", g => { if (g.S.GetFlag("quiz_hits") >= 2) g.AddTagPublic("Best Quiz in Town"); }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "snow", Title = "Snow forecast", Art = "phone", Weight = 1.2f,
                Text = "Heavy snow is forecast for tonight. Most people will stay home.",
                When = s => s.Season == 1,
                Options =
                {
                    Opt("Snow night special (£60)", "Light the fire, post a cosy photo, mulled wine on the board. The locals will trudge in.", g => { g.Spend(60, "Snow night promo"); g.S.weather = "Storm"; g.S.socialPostBoost += 0.4f; g.S.awareness[(int)Seg.Locals] += 0.02f; }, Afford(60)),
                    Opt("Batten down the hatches", "A quiet night.", g => g.S.weather = "Storm"),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "heatwave", Title = "Heatwave", Art = "keg", Weight = 1.2f,
                Text = "It's going to be scorching tonight. Everyone will want something cold.",
                When = s => s.Season == 3,
                Options =
                {
                    Opt("Stock up on cold ones (£200)", "An extra keg of lager and one of cider.", g =>
                    {
                        g.Spend(200, "Heatwave stock");
                        g.S.weather = "Sunny";
                        foreach (var id in new[] { "lager", "cider" }) { var m = g.S.Menu(id); if (m != null) { m.onMenu = true; m.stock += 88; } }
                    }, Afford(200)),
                    Opt("Hope for the best", "Sunny and hot. Staff will tire fast.", g => g.S.weather = "Sunny"),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "ring", Title = "Lost and found", Art = "heart", Weight = 0.5f, Once = true,
                Text = "Your cleaner found a wedding ring under a booth. Someone, somewhere, is in a lot of trouble.",
                When = s => s.day >= 6,
                Options =
                {
                    Opt("Track down the owner", "A grateful couple and a lovely story in the local paper.", g => { g.S.reputation += 2.5f; g.S.awareness[(int)Seg.Couples] += 0.04f; }),
                    Opt("Put up a notice", "They'll find it eventually. Probably.", g => g.S.reputation += 0.5f),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "food_truck", Title = "Street-food van", Art = "keg", Weight = 0.5f, Once = true,
                Text = "A street-food van wants to park outside on Fridays and Saturdays. Hungry people are thirsty people.",
                When = s => s.day >= 10,
                Options =
                {
                    Opt("Let them park", "They pay you £50 a week, and weekends get busier.", g => g.S.SetFlag("food_truck", 1)),
                    Opt("No thanks", "Your bar, your rules.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "noise", Title = "Noise complaint", Art = "clipboard", Weight = 1.1f,
                Text = "The flat upstairs has complained about the music again. They've mentioned the council.",
                When = s => s.volume >= 70 && s.day >= 5,
                Options =
                {
                    Opt("Turn it down", "Volume to 50. Peace with the neighbours.", g => g.SetVolume(50)),
                    Opt("It's a bar", "Keep it loud. The council sends a warning letter and a £150 fine.", g => { g.Spend(150, "Noise fine"); g.S.reputation -= 1; }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "radio", Title = "On the radio", Art = "mic", Weight = 0.6f,
                Text = "Local radio wants you on the breakfast show to talk about the bar.",
                When = s => s.reputation >= 20,
                Options =
                {
                    Opt("Do the interview", "Everyone in town hears about you.", g => { for (int i = 0; i < g.S.awareness.Length; i++) g.S.awareness[i] += 0.025f; }),
                    Opt("Too early for me", "You need your sleep.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "beer_fest", Title = "Beer festival", Art = "keg", Weight = 0.5f,
                Text = "The city beer festival has offered you a stall this weekend.",
                When = s => s.day >= 12,
                Options =
                {
                    Opt("Take a stall (£150)", "Craft fans and creatives get to know you.", g => { g.Spend(150, "Beer festival stall"); g.S.awareness[(int)Seg.Creatives] += 0.05f; g.S.awareness[(int)Seg.Tourists] += 0.04f; g.S.reputation += 1; }, Afford(150)),
                    Opt("Skip it", "Not this year.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "student_union", Title = "Student union deal", Art = "phone", Weight = 0.6f, Once = true,
                Text = "The student union wants to list you as a 'partner bar' in their freshers' guide.",
                When = s => s.Season == 0 || s.day >= 20,
                Options =
                {
                    Opt("Sign up", "Students hear about you. Expect them to want the cheap stuff.", g => g.S.awareness[(int)Seg.Students] += 0.1f),
                    Opt("Not our crowd", "Keep it grown-up.", g => g.S.awareness[(int)Seg.YoungPros] += 0.02f),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "mystery_shopper", Title = "Mystery shopper", Art = "glass", Weight = 0.6f,
                Text = "Word is the brewery is sending a mystery shopper to check how their beer is kept.",
                When = s => s.day >= 7,
                Options =
                {
                    Opt("Clean the lines now (£40)", "Perfect pints. They'll be impressed.", g => { g.Spend(40, "Line clean"); g.S.lineQuality = 100; g.S.reputation += 1; }, Afford(40)),
                    Opt("Leave it", "If your lines are clean, no harm done.", g => { if (g.S.lineQuality >= 70) g.S.reputation += 1; else g.S.reputation -= 1; }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "bailiff", Title = "Final demand", Art = "letter", Weight = 0, When = s => false,
                Format = g => $"A letter in red ink. You're £{-g.S.cash:#,0} overdrawn and the bank wants it back. Get above £0 by {Content.DayNames[Math.Max(0, g.FinalDemandDeadline) % 7]} ({g.FinalDemandDays} days), or the bailiffs take the keys.",
                Options =
                {
                    Opt("Sell something", "Sell your most valuable piece (never the last bar counter or the toilets) for cash today.", g =>
                    {
                        var it = g.BestToSell();
                        if (it != null) { g.S.cash += g.SellValue(it); g.S.items.Remove(it); } // not undoable
                    }, g => g.BestToSell() == null ? "Nothing left worth selling" : null),
                    Opt("Second loan", "£2,000 now, £3,000 to repay at £110 a night. Expensive, but it buys time.", g => g.TakeSecondLoan(), g => g.SecondLoanBlocker()),
                    Opt("Tough it out", "Trust the takings. Cut costs, fill the room and hope.", g => { }),
                },
            });
            Cards.Add(new CardDef
            {
                Id = "residency", Title = "A residency offer", Art = "mic", Weight = 0, When = s => false,
                Format = g => $"{g.S.residencyOffer.name} loved your crowd. They'd like a weekly residency every {Content.DayNames[Math.Max(0, g.S.residencyOfferDay)]}, for their usual £{g.S.residencyOffer.fee} a night. Their following grows every time it goes well.",
                Options =
                {
                    Opt("Make it a weekly fixture", "Booked automatically every week. End it any time from the Events tab.", g =>
                    {
                        var o = g.S.residencyOffer;
                        g.S.residencyAct = new Act { uid = g.S.nextUid++, kind = o.kind, name = o.name, style = o.style, fee = o.fee, quality = o.quality, reliability = o.reliability, following = o.following, volatility = o.volatility };
                        g.S.residencyDay = g.S.residencyOfferDay;
                        g.BookResidency();
                    }),
                    Opt("Not right now", "Maybe another time.", g => { }),
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

        // Helpers for the K7 cards. Deterministic, so a card's text and effect agree.
        static MenuEntry BurstEntry(Game g) => g.S.menu.Find(m => m.onMenu && m.stock > 0 && Content.Drink(m.id).CaseSize == 88);
        static string BurstName(Game g) { var m = BurstEntry(g); return m != null ? Content.Drink(m.id).Name : "beer"; }
        static Tuple<StaffMember, StaffMember> Pair(Game g) => g.S.staff.Count >= 2 ? Tuple.Create(g.S.staff[0], g.S.staff[1]) : null;
        static StaffMember Best(Game g)
        {
            StaffMember best = null;
            foreach (var m in g.S.staff) if (best == null || m.speed + m.charm + m.skill > best.speed + best.charm + best.skill) best = m;
            return best;
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
            new GoalDef { Text = "Build up £8,000 in the bank", Reward = 1000, Done = g => g.S.cash >= 8000,
                Mentor = "Act One complete, my love. You survived the dream. I always knew you would. Keep going, the city's yours." },
            // ---- Act 2: Making a Name (build brief 8.2)
            new GoalDef { Text = "Act 2: land a weekly residency (a hit comedy or open mic night)", Reward = 800, Done = g => g.S.residencyDay >= 0,
                Mentor = "A regular night with a regular crowd. That's how a bar becomes an institution." },
            new GoalDef { Text = "Act 2: earn a Stained Oak plinth (reputation 40)", Reward = 800, Done = g => g.S.reputation >= 40,
                Mentor = "Look at that base! People are starting to think of this place as somewhere special." },
            new GoalDef { Text = "Act 2: knock through into the unit next door", Reward = 1500, Done = g => g.S.gridW > 16,
                Mentor = "More room, more trade, more trouble. I wouldn't have it any other way." },
            new GoalDef { Text = "Act 2: earn 12 regulars", Reward = 1000, Done = g => g.S.regulars.Count >= 12,
                Mentor = "Twelve regulars. That's a darts team, a quiz team and a few strays. Lovely." },
            new GoalDef { Text = "Act 2: reach a Walnut plinth (reputation 60) and a 4.2★ rating", Reward = 2500, Done = g => g.S.reputation >= 60 && g.S.rating >= 4.2f,
                Mentor = "Act Two complete. You've made a name for yourself. Bar of the Year next? I'd bet on it." },
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
