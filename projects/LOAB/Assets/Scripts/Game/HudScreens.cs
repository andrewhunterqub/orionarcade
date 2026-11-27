using System.Linq;
using Loab.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loab.Game
{
    public sealed partial class Hud
    {
        string _overlayKind;

        VisualElement Modal(string title, string body, bool wide = false)
        {
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.Flex;
            _overlayKind = "modal";
            var panel = El("panel", wide ? "modal-wide" : "modal");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(scroll);
            if (title != null) scroll.Add(Lbl(title, "modal-title"));
            if (body != null) scroll.Add(Lbl(body, "body"));
            _overlay.Add(panel);
            return scroll;
        }

        public void CloseOverlay()
        {
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.None;
            _overlayKind = null;
            SetMode();
            FlushPendingTip();
        }

        // =====================================================================================
        //  Title
        // =====================================================================================

        public void ShowTitle()
        {
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.Flex;
            _overlayKind = "title";
            SetMode();
            var col = El("col");
            col.style.alignItems = Align.Center;
            col.Add(Lbl("Let's Open A Bar!", "title-logo"));
            col.Add(Lbl("Build the bar you've always talked about,\nand find out if you'd actually be any good at running it.", "title-tag"));
            if (_run.HasSave)
            {
                string what = _run.LastMode == 2 ? "Continue scenario" : _run.LastMode == 1 ? "Continue sandbox" : "Continue";
                col.Add(Btn(what, () => { if (!_run.Continue()) Flash("Couldn't load your save"); }, "btn-primary", "title-btn"));
            }
            if (_run.LastMode != 0 && _run.HasSaveFor(0))
                col.Add(Btn("Continue career", () => { if (!_run.Continue(0)) Flash("Couldn't load your career"); }, "title-btn"));
            col.Add(Btn("New career", () => ShowNewCareer(), _run.HasSave ? "btn" : "btn-primary", "title-btn"));
            col.Add(Btn("Scenarios", () => ShowScenarios(), "title-btn"));
            col.Add(Btn("Sandbox (unlimited cash)", () => _run.NewSandbox(), "title-btn"));
            col.Add(Btn("How to play", () => ShowHelp(true), "title-btn"));
            var foot = Lbl("An Orion Arcade original. Progress saves automatically in your browser.", "hint");
            foot.style.marginTop = 24;
            col.Add(foot);
            _overlay.Add(col);
        }

        void ShowNewCareer()
        {
            var m = Modal("New career", "You've quit the day job and taken the lease on The Crooked Anchor, a tired old bar in the Docklands. Maggie, the retired landlady, will show you the ropes.");
            var nameRow = Row(Lbl("Bar name", "slider-label"));
            var field = new TextField { value = "The Crooked Anchor", maxLength = 28 };
            field.style.flexGrow = 1;
            nameRow.Add(field);
            m.Add(nameRow);
            void Start(int diff)
            {
                _run.NewCareer(diff);
                G.Rename(field.value);
                _run.Save();
            }
            var relaxed = Btn("", () => Start(0), "option");
            relaxed.Add(Lbl("Relaxed", "option-label")); relaxed.Add(Lbl("Patient customers, fewer incidents, no bankruptcy. Just build and enjoy.", "option-detail"));
            m.Add(relaxed);
            var standard = Btn("", () => Start(1), "option", "btn-primary");
            standard.Add(Lbl("Standard", "option-label")); standard.Add(Lbl("The intended challenge. Bills are real. Run out of cash and the bank gets involved.", "option-detail"));
            m.Add(standard);
            var hard = Btn("", () => Start(2), "option");
            hard.Add(Lbl("Hardcore", "option-label")); hard.Add(Lbl("Impatient punters, rowdier nights, no mercy.", "option-detail"));
            m.Add(hard);
            m.Add(Btn("Back", () => ShowTitle(), "option"));
        }

        void ShowScenarios()
        {
            var m = Modal("Scenarios", "Short challenges with a deadline. Each one starts a fresh bar.");
            foreach (var sc in Scenarios.All)
            {
                var id = sc.Id;
                var b = Btn("", () => _run.NewScenario(id), "option");
                b.Add(Lbl($"{sc.Name}  ({sc.Days} days)", "option-label"));
                b.Add(Lbl(sc.Blurb, "option-detail"));
                var goal = Lbl("Goal: " + sc.GoalText, "option-detail");
                goal.style.color = new Color(1f, 0.82f, 0.4f);
                b.Add(goal);
                m.Add(b);
            }
            m.Add(Btn("Back", () => ShowTitle(), "option"));
        }

        void ShowHelp(bool fromTitle)
        {
            var m = Modal("How to play", null, true);
            void H(string head, string text) { m.Add(Lbl(head, "section")); m.Add(Lbl(text, "body")); }
            H("THE DAY", "Each day has a PREP phase (time stands still: build, set prices, hire, book events) and a SERVICE phase (6pm to midnight, in real time). Then you read the nightly report and do it all again.");
            H("BUILDING", "Open the Build tab, pick an item and click the floor to place it. R rotates. Click a placed item to move, rotate or sell it. Customers must be able to reach the bar, seats, toilets and games.");
            H("VIBE", "Everything you choose (furniture, floor, walls, music, lighting, prices) shapes your bar's Vibe across five axes. Each type of customer has a Vibe they love. Collect enough matching items to get a theme recognised.");
            H("SERVICE", "Watch people come in, queue, drink and chat. Click spills, blocked toilets and rowdy customers (red !) to deal with them. Orange ? means someone is unhappy; green + means they love it. Click anyone to see what they're thinking. Use Happy Hour, Help behind the bar and Emergency delivery when it gets hectic.");
            H("GROWING", "Good nights earn good BarHopper reviews, which build reputation and bring bigger crowds and better staff. Book quizzes, comedy and open mic nights, keep regulars happy, and work through Maggie's goals.");
            H("MODES", "Career is the full story from your first night. Scenarios are short challenges with a deadline. Sandbox gives you unlimited cash to build the bar of your dreams.");
            H("THE MODEL", "Your bar is a little scale model on its plinth. In the day it sits in a bright studio; when the doors open the lights go down and the bar glows. The soft blur at the top and bottom of the screen is the miniature lens: turn it off in the menu if you prefer.");
            H("CONTROLS", "Drag or WASD to pan, scroll or pinch to zoom, Q/E to rotate the view. Space pauses service, 1/2/3 change speed. Ctrl+Z undoes building. Enter moves on from the nightly report. H hides the interface for photos.");
            m.Add(Btn("Close", () => { if (fromTitle) ShowTitle(); else CloseOverlay(); }, "btn-primary", "option"));
        }

        void ShowPause()
        {
            var m = Modal("Paused", null);
            bool wasPaused = _run.Paused;
            _run.Paused = true;
            m.Add(Btn("Resume", () => { _run.Paused = wasPaused; CloseOverlay(); }, "btn-primary", "option"));
            m.Add(Btn("How to play", () => ShowHelp(false), "option"));
            m.Add(Btn(_run.Audio.Muted ? "Sound: OFF" : "Sound: ON", () => { _run.Audio.Muted = !_run.Audio.Muted; ShowPause(); }, "option"));
            m.Add(Btn(_doc.panelSettings.scale > 1.01f ? "Text size: LARGE" : "Text size: normal", () => { SetLargeText(_doc.panelSettings.scale <= 1.01f); ShowPause(); }, "option"));
            m.Add(Btn(_run.Blur.Enabled ? "Miniature blur: ON" : "Miniature blur: OFF", () => { _run.SetBlur(!_run.Blur.Enabled); ShowPause(); }, "option"));
            m.Add(Btn(WorldView.CharView.StopMotion ? "Stop-motion figures: ON" : "Stop-motion figures: OFF", () =>
            {
                WorldView.CharView.StopMotion = !WorldView.CharView.StopMotion;
                PlayerPrefs.SetInt("loab_stopmo", WorldView.CharView.StopMotion ? 1 : 0);
                ShowPause();
            }, "option"));
            m.Add(Btn(G.Phase == Phase.Service ? "Quit to title (abandons tonight)" : "Save and quit to title", () => _run.QuitToTitle(), "option"));
            if (G.Phase == Phase.Service) m.Add(Lbl("Your progress is saved at the start of each day.", "hint"));
        }

        // =====================================================================================
        //  Director cards
        // =====================================================================================

        public void ShowCard()
        {
            if (G.S.pendingCard == "loan") { ShowLoan(); return; }
            var card = Director.Get(G.S.pendingCard);
            if (card == null) { G.S.pendingCard = null; return; }
            HideMentor(true);
            var m = Modal(card.Title, card.Format != null ? card.Format(G) : card.Text);
            _overlayKind = "card";
            for (int i = 0; i < card.Options.Count; i++)
            {
                int idx = i;
                var o = card.Options[i];
                var b = Btn("", () =>
                {
                    var err = G.ChooseCard(idx);
                    if (err != null) { Flash(err); return; }
                    _run.Audio.Play("click");
                    CloseOverlay();
                    Tip("card");
                    MarkDirty();
                }, "option", i == 0 ? "btn-primary" : "btn");
                b.Add(Lbl(o.Label, "option-label"));
                b.Add(Lbl(o.Detail, "option-detail"));
                var why = o.CanChoose?.Invoke(G);
                if (why != null) { b.SetEnabled(false); b.Add(Lbl(why, "option-detail")); }
                m.Add(b);
            }
        }

        void ShowLoan()
        {
            var m = Modal("The bank is calling", $"You're {Money(-G.S.cash)} overdrawn. The bank will offer one emergency loan: £3,000 now, repaid at £90 a night until £3,600 is paid back. Say no, and it's over.");
            var take = Btn("", () => { G.TakeEmergencyLoan(); _run.Save(); CloseOverlay(); MarkDirty(); }, "option", "btn-primary");
            take.Add(Lbl("Take the loan", "option-label")); take.Add(Lbl("A second chance. Don't waste it.", "option-detail"));
            m.Add(take);
            var no = Btn("", () => { G.S.bankrupt = true; G.Phase = Phase.GameOver; _run.Save(); ShowGameOver(); }, "option");
            no.Add(Lbl("Hand back the keys", "option-label")); no.Add(Lbl("Close the bar for good.", "option-detail"));
            m.Add(no);
        }

        public void ShowGameOver()
        {
            var s = G.S;
            var m = Modal("Last orders", $"{s.barName} has closed its doors after {s.day + 1} nights. You served {s.history.Sum(h => h.customers):#,0} customers and earned {s.reviewCount} reviews along the way. Every great landlord has a failed bar somewhere in their past.");
            m.Add(Btn("Start a new career", () => { _run.DeleteSave(); ShowNewCareer(); }, "btn-primary", "option"));
            m.Add(Btn("Back to title", () => { _run.DeleteSave(); ShowTitle(); }, "option"));
        }

        // =====================================================================================
        //  Nightly report
        // =====================================================================================

        // Tonight's verdict (task L5): the best thing, the biggest problem and what to do about it.
        VisualElement VerdictCard(NightRecord n)
        {
            if (string.IsNullOrEmpty(n.highlight) && string.IsNullOrEmpty(n.problem)) return null;
            var c = El("card");
            c.Add(Lbl("TONIGHT'S VERDICT", "section"));
            if (!string.IsNullOrEmpty(n.highlight))
            {
                var good = Lbl("Best: " + n.highlight, "body");
                good.style.color = Col(0x6ee7a8);
                c.Add(good);
            }
            if (!string.IsNullOrEmpty(n.problem))
            {
                var bad = Lbl("Problem: " + n.problem, "body");
                bad.style.color = Col(0xff8a80);
                c.Add(bad);
                if (!string.IsNullOrEmpty(n.fix)) c.Add(Lbl($"{Mentor.Name}: \"{n.fix}\"", "card-sub"));
            }
            return c;
        }

        // A small bar chart of recent nights, newest on the right, with tonight's value.
        VisualElement TrendRow(string label, System.Collections.Generic.List<float> values, uint color, string now)
        {
            var row = Row();
            var name = Lbl(label, "slider-label");
            name.style.width = 90;
            row.Add(name);
            var bars = Row();
            bars.style.alignItems = Align.FlexEnd;
            bars.style.height = 22;
            bars.style.flexGrow = 1;
            float max = Mathf.Max(0.0001f, values.Max());
            for (int i = 0; i < values.Count; i++)
            {
                var b = new VisualElement();
                b.style.width = 10;
                b.style.marginRight = 3;
                b.style.height = Mathf.Max(2f, 22f * Mathf.Max(0, values[i]) / max);
                b.style.backgroundColor = Col(color);
                b.style.opacity = i == values.Count - 1 ? 1f : 0.55f;
                b.tooltip = values[i].ToString("0.#");
                bars.Add(b);
            }
            row.Add(bars);
            var v = Lbl(now, "card-sub");
            v.style.width = 64;
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(v);
            return row;
        }

        // Sunday's report: where the week's money went (task T6).
        VisualElement WeeklyStatementCard(GameState s)
        {
            var w = Economy.LastWeek(s);
            var c = El("card");
            c.Add(Lbl("WEEKLY STATEMENT", "section"));
            void Line(string label, float amount, bool cost)
            {
                if (cost && amount < 0.5f) return;
                var r = Row(Lbl(label, "body"), Spacer(), Lbl(cost ? "-" + Money(amount) : Money(amount), "body"));
                c.Add(r);
            }
            Line("Takings", w.Takings, false);
            Line("Drinks from suppliers", w.Drinks, true);
            Line("Wages", w.Wages, true);
            Line("Rent", w.Rent, true);
            Line("Utilities", w.Utilities, true);
            Line("Loan repayments", w.Loan, true);
            Line("Other (fines, emergency deliveries, the dog)", w.Other, true);
            var total = Lbl(Money(w.Profit), "card-title");
            total.AddToClassList(w.Profit >= 0 ? "cash" : "neg");
            c.Add(Row(Lbl($"Profit over {w.Nights} nights", "card-title"), Spacer(), total));
            if (w.HasPrev)
            {
                float diff = w.Profit - w.PrevProfit;
                c.Add(Lbl(diff >= 0 ? $"Up {Money(diff)} on last week." : $"Down {Money(-diff)} on last week.", "card-sub"));
            }
            c.Add(Lbl("Rent is paid on Sunday nights, so Sunday's own profit often looks low.", "hint"));
            return c;
        }

        public void ShowReport()
        {
            var n = G.LastNight;
            if (n == null) return;
            var s = G.S;
            var m = Modal($"{Content.DayNames[n.day % 7]} night at {s.barName}", null, true);
            _run.Audio.Play(n.profit > 0 ? "fanfare" : "error", 0.4f);

            if (s.pendingCard != "loan")
            {
                var quick = Row(Lbl($"{s.weather}  -  {Content.Seasons[s.Season]}, week {s.Week}", "hint"), Spacer(), Btn("Next day (Enter)", () => _run.NextDay(), "btn-primary"));
                m.Add(quick);
            }
            var kpis = Row();
            kpis.Add(Kpi(n.customers.ToString(), "customers"));
            kpis.Add(Kpi(n.servedDrinks.ToString(), "drinks served"));
            kpis.Add(Kpi(Money(n.takings), "takings"));
            var profit = Kpi(Money(n.profit), "profit");
            profit.Q<Label>().AddToClassList(n.profit >= 0 ? "cash" : "neg");
            kpis.Add(profit);
            kpis.Add(Kpi($"{n.satisfaction:0}%", "satisfaction"));
            var rt = Kpi($"{s.rating:0.0}", "BarHopper");
            rt.Q<Label>().AddToClassList("stars");
            kpis.Add(rt);
            m.Add(kpis);

            var cols = Row();
            cols.style.alignItems = Align.FlexStart;
            var left = El("col"); left.style.width = Length.Percent(50); left.style.paddingRight = 10;
            var right = El("col"); right.style.width = Length.Percent(50);
            cols.Add(left); cols.Add(right);
            m.Add(cols);

            var sc = G.Scenario;
            if (sc != null && G.ScenarioJustEnded)
            {
                var c = El("card");
                bool won = s.scenarioResult == 1;
                var head = Lbl(won ? $"SCENARIO COMPLETE: {sc.Name}!" : $"OUT OF TIME: {sc.Name}", "big-num");
                head.style.fontSize = 22;
                head.style.color = won ? new Color(0.47f, 0.86f, 0.55f) : new Color(1f, 0.43f, 0.39f);
                c.Add(head);
                c.Add(Lbl(won ? $"Done in {s.day + 1} of {sc.Days} days. You can keep running the bar, or head back to the title for another challenge."
                              : $"{sc.Progress(G)}. Close, but the deadline has passed. Keep playing for fun, or try again.", "body"));
                // Move on to the next morning first, so the result is saved.
                c.Add(Row(Btn("Back to title", () => { _run.NextDay(); ShowTitle(); }, "btn-small"), Btn("Try another scenario", () => { _run.NextDay(); ShowScenarios(); }, "btn-small")));
                left.Add(c);
                if (won) _run.Audio.Play("cheer", 0.8f);
            }
            var verdict = VerdictCard(n);
            if (verdict != null) left.Add(verdict);
            if (n.rent > 0) left.Add(WeeklyStatementCard(s));
            if (G.GoalsDoneLastNight.Count > 0)
            {
                left.Add(Lbl("GOALS COMPLETE", "section"));
                foreach (var gi in G.GoalsDoneLastNight)
                {
                    var g = Goals.Career[int.Parse(gi)];
                    var c = El("card");
                    c.Add(Lbl($"{g.Text}" + (g.Reward > 0 ? $"   +£{g.Reward}" : ""), "card-title"));
                    c.Add(Lbl($"{Mentor.Name}: \"{g.Mentor}\"", "card-sub"));
                    left.Add(c);
                }
            }
            if (n.eventName != null)
            {
                left.Add(Lbl("EVENT", "section"));
                left.Add(Lbl($"{n.eventName}: {n.eventOutcome}", "body"));
            }
            left.Add(Lbl("WHAT PEOPLE THOUGHT", "section"));
            if (n.thoughts.Count == 0) left.Add(Lbl("Nobody came, so nobody thought anything.", "hint"));
            foreach (var t in n.thoughts) left.Add(Lbl("- " + t, "body"));
            if (n.moments.Count > 0)
            {
                left.Add(Lbl("MOMENTS", "section"));
                foreach (var mo in n.moments.Take(8)) left.Add(Lbl("- " + mo, "body"));
            }
            if (n.stockOuts.Count > 0) left.Add(Lbl("Ran out of: " + string.Join(", ", n.stockOuts), "chip-bad"));
            if (n.turnedAway > 0) left.Add(Lbl($"{n.turnedAway} people were turned away because you were full.", "chip-bad"));

            right.Add(Lbl("MONEY", "section"));
            void Line(string a, float v, bool hideZero = false) { if (hideZero && Mathf.Abs(v) < 0.5f) return; var r = Row(Lbl(a, "body"), Spacer(), Lbl(Money(v), "body")); r.style.marginBottom = -6; right.Add(r); }
            Line("Takings (inc. tickets)", n.takings);
            Line("Cost of drinks sold", -n.cogs);
            Line("Wages", -n.wages);
            Line("Rent (weekly)", -n.rent, true);
            Line("Utilities", -n.utilities, true);
            Line("Loan repayment", -n.loan, true);
            Line("Other", -n.other, true);
            float unexplained = n.costs - n.cogs - n.wages - n.rent - n.utilities - n.loan - n.other;
            Line("Bills", -unexplained, true); // nights saved before the breakdown existed
            Line("Profit", n.profit);

            var recent = s.history.Skip(Mathf.Max(0, s.history.Count - 7)).ToList();
            if (recent.Count >= 2)
            {
                right.Add(Lbl("LAST 7 NIGHTS", "section"));
                right.Add(TrendRow("Takings", recent.Select(h => h.takings).ToList(), 0x4cc9f0, Money(n.takings)));
                right.Add(TrendRow("Customers", recent.Select(h => (float)h.customers).ToList(), 0xffd166, n.customers.ToString()));
                right.Add(TrendRow("Satisfaction", recent.Select(h => h.satisfaction).ToList(), 0x6ee7a8, $"{n.satisfaction:0}%"));
                right.Add(TrendRow("Rating", recent.Select(h => h.rating).ToList(), 0xff9f1c, $"{n.rating:0.0}"));
            }

            right.Add(Lbl("WHO CAME", "section"));
            if (n.segCounts.Count == 5 && n.customers > 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    var d = Content.Segments[i];
                    right.Add(Row(Lbl(d.Plural, "slider-label"), Bar(n.segCounts[i] / (float)Mathf.Max(1, n.customers), Col(d.Color)), Lbl($" {n.segCounts[i]}", "card-sub")));
                }
            }
            right.Add(Lbl("NEW REVIEWS ON BARHOPPER", "section"));
            if (n.reviews.Count == 0) right.Add(Lbl("No new reviews tonight.", "hint"));
            foreach (var r in n.reviews.Take(6)) right.Add(ReviewCard(r));

            if (s.tags.Count > 0)
            {
                var tags = Row(); tags.AddToClassList("wrap");
                foreach (var t in s.tags) tags.Add(Lbl(t, "chip", IsBadTag(t) ? "chip-bad" : "chip-good"));
                right.Add(Lbl("YOUR REPUTATION TAGS", "section"));
                right.Add(tags);
            }

            var next = Btn(s.pendingCard == "loan" ? "Talk to the bank" : $"Next day: {Content.DayNames[(n.day + 1) % 7]}", () => { _run.NextDay(); }, "btn-primary", "option");
            next.style.marginTop = 12;
            m.Add(next);
            Tip("report");
        }

        static bool IsBadTag(string t) => t == "Overpriced" || t == "Slow Service" || t == "Sticky Tables" || t == "Rough on a Saturday";

        VisualElement Kpi(string value, string label)
        {
            var k = El("card", "kpi");
            k.Add(Lbl(value, "big-num"));
            k.Add(Lbl(label, "stat-label"));
            return k;
        }

        VisualElement ReviewCard(Review r)
        {
            var c = El("card", "review");
            var head = Row(Lbl(Stars(r.stars), "stars"), Lbl($"  {r.author}  -  {r.segment}", "review-head"));
            c.Add(head);
            c.Add(Lbl(r.text, "review-text"));
            return c;
        }
    }
}
