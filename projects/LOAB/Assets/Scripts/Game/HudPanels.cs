using System;
using System.Linq;
using Loab.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loab.Game
{
    public sealed partial class Hud
    {
        string _buildCat = "Seating";
        int _eventKind = 0, _eventDay = 0, _eventPromo = 50, _eventTicket = 0;

        static readonly string[] BuildCats = { "Bar", "Seating", "Toilets", "Games", "Stage & Music", "Screens", "Lighting", "Decor", "Floors", "Walls" };

        VisualElement Title(VisualElement parent, string title, string hint = null)
        {
            parent.Add(Lbl(title, "side-title"));
            if (hint != null) parent.Add(Lbl(hint, "hint"));
            return parent;
        }

        static string VibeGlyph(float[] v)
        {
            var parts = new System.Collections.Generic.List<string>();
            string[] shortNames = { "Energy", "Polish", "Modern", "Cosy", "Quirk" };
            for (int a = 0; a < Axis.Count; a++)
                if (Mathf.Abs(v[a]) >= 3) parts.Add((v[a] > 0 ? "+" : "-") + shortNames[a]);
            return parts.Count == 0 ? "Neutral" : string.Join(" ", parts.Take(3));
        }

        // =====================================================================================
        //  BUILD
        // =====================================================================================

        void BuildBuild(VisualElement p)
        {
            Title(p, "Build", "Pick something, then click the floor. R rotates, right-click cancels. Click placed items to move or sell them.");
            if (G.CanExpand)
            {
                var ex = El("card");
                ex.Add(Lbl("Knock through next door", "card-title"));
                ex.Add(Lbl($"The empty unit next door (the white card model) adds {Sim.Game.ExpandCols} columns of floor. Needs reputation {Sim.Game.ExpandRep}.", "card-sub"));
                var eb = Btn($"Expand ({Money(Sim.Game.ExpandCost)})", () =>
                {
                    var e = G.Expand();
                    if (e != null) { Flash(e); return; }
                    _run.OnExpanded();
                }, "btn-primary");
                eb.SetEnabled(G.S.reputation >= Sim.Game.ExpandRep || G.S.mode == 1);
                ex.Add(eb);
                p.Add(ex);
            }
            var undo = Row(Btn("Undo", () => { G.Undo(); _run.ClearInteraction(); }, "btn-small"), Btn("Redo", () => { G.Redo(); _run.ClearInteraction(); }, "btn-small"), Spacer(),
                Lbl($"Seats {G.V.Seats}  -  Serving points {G.V.BarPoints}  -  Toilets {G.V.Toilets}", "card-sub"));
            p.Add(undo);
            var cats = Row(); cats.AddToClassList("wrap");
            foreach (var c in BuildCats)
            {
                var cat = c;
                var b = Btn(c, () => { _buildCat = cat; MarkDirty(); }, "btn-small");
                b.EnableInClassList("btn-on", _buildCat == c);
                cats.Add(b);
            }
            p.Add(cats);
            var grid = Row(); grid.AddToClassList("wrap");
            p.Add(grid);

            if (_buildCat == "Floors" || _buildCat == "Walls")
            {
                bool floor = _buildCat == "Floors";
                foreach (var st in floor ? Content.Floors : Content.Walls)
                {
                    var sd = st;
                    bool current = (floor ? G.S.floor : G.S.wall) == st.Id;
                    var card = El("card", "item-card");
                    if (current) card.AddToClassList("card-sel");
                    var sw = new VisualElement();
                    sw.style.height = 22; sw.style.borderTopLeftRadius = sw.style.borderTopRightRadius = sw.style.borderBottomLeftRadius = sw.style.borderBottomRightRadius = 4;
                    sw.style.backgroundColor = Col(st.Color);
                    card.Add(sw);
                    card.Add(Lbl(st.Name, "card-title"));
                    card.Add(Lbl(current ? "Current" : (st.Cost == 0 ? "Free" : Money(st.Cost)), current ? "card-sub" : "price"));
                    card.Add(Lbl(VibeGlyph(st.Vibe), "card-sub"));
                    card.RegisterCallback<ClickEvent>(_ =>
                    {
                        var err = G.Execute(new StyleCmd { Kind = floor ? 0 : 1, Id = sd.Id });
                        if (err != null) Flash(err); else _run.Audio.Play("place");
                    });
                    grid.Add(card);
                }
                return;
            }

            foreach (var d in Content.Items)
            {
                if (d.Category != _buildCat) continue;
                if (d.Hidden && !G.S.unlockedItems.Contains(d.Id)) continue;
                var def = d;
                bool unlocked = G.IsUnlocked(d);
                var card = El("card", "item-card");
                if (_run.PlaceId == d.Id) card.AddToClassList("card-sel");
                if (!unlocked) card.AddToClassList("card-locked");
                card.Add(Lbl(d.Name, "card-title"));
                card.Add(Row(Lbl(Money(d.Cost), "price"), Spacer(), Lbl($"{d.W}x{d.H}", "card-sub")));
                string info = d.Fn == ItemFn.Seat ? $"Seats {d.Capacity}, comfort {d.Comfort}" : d.Fn == ItemFn.Toilet ? $"{d.Capacity} cubicles" : d.Fn == ItemFn.BarCounter ? "3 serving points" : VibeGlyph(d.Vibe);
                card.Add(Lbl(unlocked ? info : $"Locked: reputation {d.UnlockRep}", "card-sub"));
                if (d.Themes.Length > 0) card.Add(Lbl(string.Join(", ", d.Themes.Select(t => Content.Theme(t).Name)), "card-sub"));
                card.tooltip = d.Blurb;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!unlocked) { Flash($"Reach reputation {def.UnlockRep} to unlock"); return; }
                    _run.BeginPlace(def.Id);
                    _run.Audio.Play("click");
                    MarkDirty();
                });
                grid.Add(card);
            }
        }

        void BuildSelected(VisualElement p)
        {
            var it = _run.Selected;
            var d = Content.Item(it.id);
            Title(p, d.Name, d.Blurb);
            p.Add(Row(
                Btn("Move", () => { _run.BeginMove(); }, "btn-primary"),
                Btn("Rotate (R)", () => _run.RotateSelectedOrGhost()),
                Btn($"Sell (+{Money(G.SellValue(it))})", () => _run.SellSelected(), "btn-bad"),
                Btn("Close", () => { _run.ClearInteraction(); MarkDirty(); })));
            p.Add(Lbl("VIBE CONTRIBUTION", "section"));
            for (int a = 0; a < Axis.Count; a++)
            {
                float v = d.Vibe[a];
                if (Mathf.Abs(v) < 0.5f) continue;
                p.Add(Row(Lbl(Axis.Names[a], "slider-label"), Lbl((v > 0 ? "+" : "") + v.ToString("0") + "  " + (v > 0 ? Axis.High[a] : Axis.Low[a]), "card-sub")));
            }
            if (d.Themes.Length > 0) p.Add(Lbl("Counts towards: " + string.Join(", ", d.Themes.Select(t => Content.Theme(t).Name)), "hint"));
            var cond = Row(Lbl($"Condition: {100 - it.wear:0}%", "card-sub"), Spacer());
            if (it.wear >= 5)
            {
                var uid = it.uid;
                cond.Add(Btn($"Repair ({Money(G.RepairCost(it))})", () => { var e = G.Repair(uid); if (e != null) Flash(e); else _run.Audio.Play("place"); }, "btn-small"));
            }
            p.Add(cond);
            p.Add(Lbl("Worn items drag the Vibe down a little. Repairs make them good as new.", "hint"));
        }

        // =====================================================================================
        //  VIBE
        // =====================================================================================

        void BuildVibe(VisualElement p)
        {
            var v = G.V;
            Title(p, "Vibe", "Everything in the bar shapes its personality. Each crowd has a favourite Vibe: the markers show where they'd like each bar to be.");
            p.Add(Lbl(v.Summary, "body"));
            for (int a = 0; a < Axis.Count; a++)
            {
                var row = El("axis-row");
                row.Add(Lbl(Axis.Low[a], "axis-lo"));
                var track = El("axis-track");
                for (int sgi = 0; sgi < 5; sgi++)
                {
                    var seg = Content.Segments[sgi];
                    var pref = El("axis-pref");
                    pref.style.left = Length.Percent(seg.VibePref[a]);
                    pref.style.backgroundColor = Col(seg.Color);
                    track.Add(pref);
                }
                var dot = El("axis-dot");
                dot.style.left = Length.Percent(v.Axes[a]);
                track.Add(dot);
                row.Add(track);
                row.Add(Lbl(Axis.High[a], "axis-hi"));
                p.Add(row);
            }
            p.Add(Lbl("WHO WILL LOVE IT", "section"));
            for (int i = 0; i < 5; i++)
            {
                var seg = Content.Segments[i];
                var r = Row(Lbl(seg.Plural, "slider-label"), Bar(v.SegMatch[i], Col(seg.Color)), Lbl($" {v.SegMatch[i] * 100:0}%", "card-sub"));
                r.tooltip = seg.Blurb;
                p.Add(r);
            }
            p.Add(Lbl($"COHERENCE {v.Coherence:0}%", "section"));
            p.Add(Lbl(v.Coherence > 70 ? "Everything feels like it belongs together." : v.Coherence > 45 ? "A bit of a mix. Pick a direction." : "It's a jumble. Customers can't tell what you're going for.", "hint"));
            p.Add(Lbl("THEMES", "section"));
            foreach (var t in v.Themes)
            {
                var r = Row(Lbl(t.Def.Name, "slider-label"), Bar(t.Progress, t.Active ? Col(0x78dc8c) : Col(0xd4a64a)), Lbl(t.Active ? "  ACTIVE" : $"  {t.Progress * 100:0}%", t.Active ? "toggle-on" : "card-sub"));
                r.Q<Label>().style.width = 130;
                r.tooltip = t.Def.Blurb + $" ({t.Count}/12 matching elements, {t.Share * 100:0}% of the look)";
                p.Add(r);
            }
            p.Add(Lbl("Themes need about 12 matching elements (floors, walls and music count double) making up nearly half the look.", "hint"));

            p.Add(Lbl("MUSIC", "section"));
            var music = Row(); music.AddToClassList("wrap");
            foreach (var mdef in Content.Music)
            {
                var md = mdef;
                var b = Btn(md.Name, () => { G.SetMusic(md.Id); _run.Audio.Play("click"); }, "btn-small");
                b.EnableInClassList("btn-on", G.S.music == md.Id);
                b.tooltip = VibeGlyph(md.Vibe);
                music.Add(b);
            }
            p.Add(music);
            p.Add(SliderRow("Lighting", G.S.lighting, x => G.SetLighting(x), "Dim", "Bright"));
            p.Add(SliderRow("Volume", G.S.volume, x => G.SetVolume(x), "Quiet", "Loud"));
        }

        VisualElement SliderRow(string label, int value, Action<int> set, string lo, string hi)
        {
            var r = El("slider-row");
            r.Add(Lbl(label, "slider-label"));
            r.Add(Lbl(lo, "card-sub"));
            var s = new SliderInt(0, 100) { value = value };
            s.style.flexGrow = 1;
            s.RegisterValueChangedCallback(e => set(e.newValue));
            r.Add(s);
            r.Add(Lbl(hi, "card-sub"));
            return r;
        }

        // =====================================================================================
        //  DRINKS
        // =====================================================================================

        void BuildDrinks(VisualElement p)
        {
            Title(p, "Drinks & stock", "Tick what's on the menu, set prices and keep it stocked. A keg is about 88 pints.");
            float restock = G.RestockCost();
            var topRow = Row(
                Btn(restock > 0 ? $"Restock all ({Money(restock)})" : "Stock looks healthy", () => { var e = G.RestockAll(); if (e != null) Flash(e); else _run.Audio.Play("coin"); }, "btn-primary"),
                Btn($"Clean lines £40 ({G.S.lineQuality:0}%)", () => { var e = G.CleanLines(); if (e != null) Flash(e); }),
                Btn($"Deep clean £{G.DeepCleanCost} ({G.S.cleanliness:0}%)", () => { var e = G.DeepClean(); if (e != null) Flash(e); }));
            topRow.AddToClassList("wrap");
            p.Add(topRow);
            var resp = Btn(G.S.responsibleService ? "Responsible service: ON" : "Responsible service: OFF", () => G.SetResponsible(!G.S.responsibleService), "btn-small");
            resp.EnableInClassList("btn-good", G.S.responsibleService);
            resp.tooltip = "Staff stop serving alcohol to anyone who's had five. Fewer rowdy incidents, and people respect it.";
            var hh = Row(Lbl("Happy hour:", "card-sub"));
            foreach (var h in new[] { -1, 18, 19, 20, 21 })
            {
                int hour = h;
                var b = Btn(h < 0 ? "None" : $"{h}:00", () => G.SetHappyHour(hour), "btn-small");
                b.EnableInClassList("btn-on", G.S.happyHourStart == h);
                hh.Add(b);
            }
            p.Add(Row(resp));
            p.Add(hh);

            DrinkCat? lastCat = null;
            foreach (var m in G.S.menu)
            {
                var d = Content.Drink(m.id);
                if (lastCat != d.Cat) { p.Add(Lbl(CatName(d.Cat).ToUpperInvariant(), "section")); lastCat = d.Cat; }
                var me = m;
                var row = El("drink-row");
                var tog = Btn(m.onMenu ? "ON" : "off", () => G.ToggleMenu(me.id), "btn-small");
                tog.style.width = 38;
                tog.EnableInClassList("btn-good", m.onMenu);
                row.Add(tog);
                var nameCol = El("col");
                nameCol.Add(Lbl(d.Name, "drink-name"));
                float q = Service.DrinkQuality(G.S, d);
                int par = m.onMenu ? G.ParLevel(m.id) : 0;
                var stockLbl = Lbl($"{m.stock} in stock  -  quality {q:0}", "drink-meta");
                if (m.onMenu && m.stock < par * 0.6f) stockLbl.style.color = Col(0xff8a80);
                nameCol.Add(stockLbl);
                row.Add(nameCol);
                row.Add(Spacer());
                row.Add(Btn("-", () => G.SetPrice(me.id, me.price - 0.1f), "btn-small"));
                var price = Lbl($"£{m.price:0.00}", "price");
                price.style.width = 46; price.style.unityTextAlign = TextAnchor.MiddleCenter;
                float ratio = m.price / d.DefaultPrice;
                if (ratio > 1.2f) price.style.color = Col(0xff8a80);
                else if (ratio < 0.9f) price.style.color = Col(0x4cc9f0);
                price.tooltip = $"Usual price £{d.DefaultPrice:0.00}. Margin {(1 - d.UnitCost / m.price) * 100:0}%";
                row.Add(price);
                row.Add(Btn("+", () => G.SetPrice(me.id, me.price + 0.1f), "btn-small"));
                var order = Btn($"+{d.CaseSize}\n£{G.CaseCost(m.id):0}", () => { var e = G.OrderStock(me.id, 1); if (e != null) Flash(e); else _run.Audio.Play("coin"); }, "btn-small");
                order.style.width = 50;
                order.tooltip = $"Order {(d.CaseSize == 88 ? "a keg" : "a case")} from {Content.Supplier(d.Supplier).Name}";
                row.Add(order);
                p.Add(row);
            }
            p.Add(Lbl("SUPPLIERS", "section"));
            foreach (var s in Content.Suppliers) p.Add(Lbl($"{s.Name}: {s.Blurb} (quality {s.Quality:0})", "hint"));
        }

        static string CatName(DrinkCat c) => c switch
        {
            DrinkCat.Lager => "Lager", DrinkCat.Ale => "Ale", DrinkCat.Stout => "Stout", DrinkCat.Craft => "Craft beer", DrinkCat.Cider => "Cider",
            DrinkCat.Wine => "Wine", DrinkCat.Spirit => "Spirits & mixers", DrinkCat.Cocktail => "Cocktails", DrinkCat.AlcoholFree => "Alcohol-free", _ => "Hot drinks",
        };

        // =====================================================================================
        //  STAFF
        // =====================================================================================

        void BuildStaff(VisualElement p)
        {
            Title(p, "Staff", "Bartenders serve (one per serving point). Floor staff collect glasses, mop spills and calm rowdy customers. New applicants every Monday.");
            float wages = G.S.staff.Sum(m => m.wage);
            p.Add(Lbl($"Team of {G.S.staff.Count}  -  wages {Money(wages)} a night", "body"));
            foreach (var m in G.S.staff) p.Add(StaffCard(m, false));
            p.Add(Lbl("APPLICANTS", "section"));
            if (G.S.applicants.Count == 0) p.Add(Lbl("No applicants this week. More on Monday.", "hint"));
            foreach (var m in G.S.applicants) p.Add(StaffCard(m, true));
        }

        VisualElement StaffCard(StaffMember m, bool applicant)
        {
            var c = El("card");
            var head = Row(Lbl(m.name, "card-title"), Lbl(m.role == 0 ? "  Bartender" : "  Floor staff", "card-sub"), Spacer(), Lbl($"£{m.wage}/night", "price"));
            c.Add(head);
            void Attr(string n, int v) => c.Add(Row(Lbl(n, "slider-label"), Bar(v / 20f, null), Lbl($" {v}", "card-sub")));
            Attr("Speed", m.speed); Attr("Charm", m.charm);
            if (m.role == 0) Attr("Mixology", m.skill);
            Attr("Stamina", m.stamina);
            var traits = Row(); traits.AddToClassList("wrap");
            foreach (var t in m.traits)
            {
                var td = Content.Trait(t);
                bool bad = t == "always_late" || t == "butterfingers" || t == "grumpy";
                var chip = Lbl(td.Name, "chip", bad ? "chip-bad" : "chip-good");
                chip.tooltip = td.Blurb;
                traits.Add(chip);
            }
            c.Add(traits);
            if (!applicant)
            {
                c.Add(Row(Lbl("Morale", "slider-label"), Bar(m.morale / 100f, m.morale < 30 ? Col(0xff6e64) : Col(0x78dc8c)), Lbl($"  Lv {m.level}", "card-sub")));
                var me = m;
                bool trained = G.S.GetFlag("trained_" + m.uid) >= G.S.Week;
                var course = Btn(trained ? "On a course this week" : $"Training (£{Sim.Game.TrainingCost})", () => { var e = G.Train(me.uid); if (e != null) Flash(e); else _run.Audio.Play("coin"); }, "btn-small");
                course.SetEnabled(!trained);
                course.tooltip = "+2 to their weakest skill and a morale boost. One course per person per week.";
                c.Add(Row(Btn("Pay +£5", () => G.SetWage(me.uid, me.wage + 5), "btn-small"), Btn("Pay -£5", () => G.SetWage(me.uid, me.wage - 5), "btn-small"), course, Spacer(),
                    Btn("Let go", () => { G.Fire(me.uid); }, "btn-small", "btn-bad")));
            }
            else
            {
                var me = m;
                var roleBtn = Btn(m.role == 0 ? "Hire as bartender" : "Hire as floor staff", () => { var e = G.Hire(me.uid); if (e != null) Flash(e); else _run.Audio.Play("coin"); }, "btn-primary");
                var swap = Btn("Switch role", () => { me.role = 1 - me.role; MarkDirty(); }, "btn-small");
                c.Add(Row(roleBtn, swap));
            }
            return c;
        }

        // =====================================================================================
        //  EVENTS
        // =====================================================================================

        void BuildEvents(VisualElement p)
        {
            Title(p, "Events", "Book a Pub Quiz, Comedy Night or Open Mic. Promotion brings a bigger crowd. Acts are hit and miss: high quality is safer, volatile acts are a gamble.");
            if (G.S.residencyDay >= 0)
            {
                var rc = El("card");
                rc.Add(Row(Lbl($"Residency: {G.S.residencyAct.name}", "card-title"), Spacer(), Lbl("every " + Content.DayNames[G.S.residencyDay], "chip")));
                rc.Add(Row(Lbl($"£{G.S.residencyAct.fee} a night  -  following {G.S.residencyAct.following}", "card-sub"), Spacer(), Btn("End residency", () => G.EndResidency(), "btn-small", "btn-bad")));
                p.Add(rc);
            }
            // The next four weeks of special nights (task C3). Those within a week can be picked for booking.
            var upcoming = Calendar.Upcoming(G.S, 28);
            var today = Calendar.Today(G.S);
            if (today != null || upcoming.Count > 0)
            {
                p.Add(Lbl("COMING UP", "section"));
                if (today != null) p.Add(CalendarCard(0, today));
                foreach (var (day, def) in upcoming.Take(5)) p.Add(CalendarCard(day - G.S.day, def));
            }
            if (G.S.events.Count > 0)
            {
                p.Add(Lbl("BOOKED", "section"));
                foreach (var e in G.S.events.OrderBy(x => x.day))
                {
                    var ev = e;
                    int offset = e.day - G.S.day;
                    var c = El("card");
                    c.Add(Row(Lbl($"{Economy.EventName(e.kind)}: {e.act.name}", "card-title"), Spacer(), Lbl(offset == 0 ? "TONIGHT" : Content.DayNames[e.day % 7], "chip")));
                    c.Add(Row(Lbl($"Promo £{e.promo}  -  {(e.ticket > 0 ? $"£{e.ticket} a head" : "free entry")}", "card-sub"), Spacer(), Btn("Cancel (50% refund)", () => G.CancelEvent(ev.uid), "btn-small")));
                    p.Add(c);
                }
            }
            p.Add(Lbl("BOOK A NIGHT", "section"));
            var kinds = Row(
                Toggle("Pub Quiz", _eventKind == 0, () => { _eventKind = 0; _eventTicket = 0; MarkDirty(); }),
                Toggle("Comedy Night", _eventKind == 1, () => { _eventKind = 1; _eventTicket = 5; MarkDirty(); }),
                Toggle("Open Mic", _eventKind == 2, () => { _eventKind = 2; _eventTicket = 0; MarkDirty(); }));
            p.Add(kinds);
            if (_eventKind >= 1 && !G.V.HasStage) p.Add(Lbl($"{Economy.EventName(_eventKind)} needs a Small Stage (Build > Stage & Music).", "chip-bad"));
            if (_eventKind == 2) p.Add(Lbl("Open mic is cheap and wildly unpredictable. Creatives and students love it.", "hint"));

            var days = Row(); days.AddToClassList("wrap");
            days.Add(Lbl("Night:", "card-sub"));
            for (int i = 0; i < 7; i++)
            {
                int off = i;
                bool taken = G.S.events.Exists(e => e.day == G.S.day + off);
                var special = Calendar.On(G.S.day + i);
                var b = Toggle((i == 0 ? "Tonight" : Content.DayShort[(G.S.day + i) % 7]) + (special != null ? " *" : ""), _eventDay == i, () => { _eventDay = off; MarkDirty(); });
                if (special != null) b.tooltip = special.Name;
                b.SetEnabled(!taken);
                days.Add(b);
            }
            p.Add(days);
            var promo = Row(Lbl("Promotion:", "card-sub"));
            foreach (var amt in new[] { 0, 50, 150, 300 })
            {
                int a = amt;
                promo.Add(Toggle(a == 0 ? "None" : $"£{a}", _eventPromo == a, () => { _eventPromo = a; MarkDirty(); }));
            }
            p.Add(promo);
            var tix = Row(Lbl(_eventKind == 0 ? "Entry:" : "Tickets:", "card-sub"));
            foreach (var t in _eventKind == 1 ? new[] { 0, 5, 10, 15 } : new[] { 0, 1, 2 })
            {
                int tt = t;
                tix.Add(Toggle(t == 0 ? "Free" : $"£{t}", _eventTicket == t, () => { _eventTicket = tt; MarkDirty(); }));
            }
            p.Add(tix);
            p.Add(Lbl(_eventKind == 0 ? "QUIZMASTERS" : _eventKind == 1 ? "COMEDY AGENCY (new acts each Monday)" : "OPEN MIC HOSTS", "section"));
            foreach (var act in G.S.agency.Where(a => a.kind == _eventKind))
            {
                var a = act;
                var c = El("card");
                c.Add(Row(Lbl(a.name, "card-title"), Lbl($"  {a.style}", "card-sub"), Spacer(), Lbl(a.fee == 0 ? "Free" : Money(a.fee), "price")));
                c.Add(Row(Lbl("Quality", "slider-label"), Bar(a.quality / 100f, Col(0xffd166))));
                c.Add(Row(Lbl("Reliability", "slider-label"), Bar(a.reliability / 100f, Col(0x4cc9f0))));
                c.Add(Row(Lbl("Following", "slider-label"), Bar(a.following / 100f, Col(0xc8a2ff))));
                c.Add(Lbl(a.volatility > 0.38f ? "Wildly unpredictable" : a.volatility > 0.22f ? "A bit hit and miss" : "Consistent", "card-sub"));
                c.Add(Btn($"Book for {(_eventDay == 0 ? "tonight" : Content.DayNames[(G.S.day + _eventDay) % 7])}  ({Money(a.fee + _eventPromo)})", () =>
                {
                    var err = G.BookEvent(a.uid, _eventDay, _eventPromo, _eventTicket);
                    if (err != null) Flash(err); else { _run.Audio.Play("fanfare", 0.3f); Flash("Booked!"); }
                }, "btn-primary"));
                p.Add(c);
            }
        }

        VisualElement CalendarCard(int offset, CalendarDef def)
        {
            var c = El("card");
            string when = offset == 0 ? "TONIGHT" : offset == 1 ? "TOMORROW" : offset < 7 ? Content.DayNames[(G.S.day + offset) % 7].ToUpperInvariant() : $"IN {offset} DAYS";
            c.Add(Row(Lbl(def.Name, "card-title"), Spacer(), Lbl(when, "chip", "chip-good")));
            c.Add(Lbl(def.Blurb, "card-sub"));
            if (offset < 7 && !G.S.events.Exists(e => e.day == G.S.day + offset))
            {
                string suggest = def.SuggestEvent >= 0 ? $"Plan a {Economy.EventName(def.SuggestEvent)} for it" : "Book something for it";
                c.Add(Btn(suggest, () =>
                {
                    _eventDay = offset;
                    if (def.SuggestEvent >= 0) { _eventKind = def.SuggestEvent; _eventTicket = def.SuggestEvent == 1 ? 5 : 0; }
                    MarkDirty();
                }, "btn-small"));
            }
            return c;
        }

        Button Toggle(string text, bool on, Action click)
        {
            var b = Btn(text, click, "btn-small");
            b.EnableInClassList("btn-on", on);
            return b;
        }

        // =====================================================================================
        //  PROMOTE
        // =====================================================================================

        void BuildPromote(VisualElement p)
        {
            Title(p, "Promote", "Awareness is how many people in each crowd know you exist. Word of mouth from happy customers is the best advert of all.");
            for (int i = 0; i < 5; i++)
            {
                var seg = Content.Segments[i];
                p.Add(Row(Lbl(seg.Plural, "slider-label"), Bar(G.S.awareness[i], Col(seg.Color)), Lbl($" {G.S.awareness[i] * 100:0}%", "card-sub")));
            }
            p.Add(Lbl("CAMPAIGNS", "section"));
            var post = El("card");
            post.Add(Lbl("Post on social media (free, once a day)", "card-title"));
            post.Add(Lbl($"A coherent, themed bar makes better photos. Your coherence: {G.V.Coherence:0}%.", "card-sub"));
            var pb = Btn(G.S.postedToday ? "Posted today" : "Post a photo", () => { var e = G.SocialPost(); if (e != null) Flash(e); else _run.Audio.Play("coin"); }, "btn-primary");
            pb.SetEnabled(!G.S.postedToday);
            post.Add(pb);
            p.Add(post);
            var fly = El("card");
            fly.Add(Lbl("Flyers & posters (£60)", "card-title"));
            fly.Add(Lbl("Reaches students, locals and sports fans.", "card-sub"));
            fly.Add(Btn("Print flyers", () => { var e = G.Flyers(); if (e != null) Flash(e); else _run.Audio.Play("coin"); }));
            p.Add(fly);
            var ad = El("card");
            ad.Add(Lbl("Targeted social ad (£180)", "card-title"));
            ad.Add(Lbl("Reaches young professionals, creatives and sports fans.", "card-sub"));
            ad.Add(Btn("Run the ad", () => { var e = G.PaidAd(); if (e != null) Flash(e); else _run.Audio.Play("coin"); }));
            p.Add(ad);
            p.Add(Lbl("BAR NAME", "section"));
            var field = new TextField { value = G.S.barName, maxLength = 28 };
            field.RegisterCallback<FocusOutEvent>(_ => G.Rename(field.value));
            p.Add(field);
        }

        // =====================================================================================
        //  MONEY
        // =====================================================================================

        void BuildMoney(VisualElement p)
        {
            Title(p, "Money", "Rent is due every Sunday night. Wages and bills are paid nightly. Stock is paid for when you order it.");
            var s = G.S;
            p.Add(Row(Lbl("Cash", "slider-label"), Lbl(Money(s.cash), "price")));
            p.Add(Row(Lbl("Rent", "slider-label"), Lbl($"{Money(Economy.WeeklyRent(s))} on Sunday" + (s.DayOfWeek == 6 ? " (TONIGHT)" : $" (in {6 - s.DayOfWeek} days)"), "card-sub")));
            p.Add(Row(Lbl("Wages", "slider-label"), Lbl($"{Money(s.staff.Sum(m => m.wage))} a night", "card-sub")));
            if (s.loanBalance > 0) p.Add(Row(Lbl("Loan", "slider-label"), Lbl($"{Money(s.loanBalance)} left at {Money(s.loanDaily)}/night", "card-sub")));
            p.Add(Row(Lbl("Spent today", "slider-label"), Lbl(Money(G.SpentToday), "card-sub")));
            var recent = s.history.Skip(Math.Max(0, s.history.Count - 14)).ToList();
            if (recent.Count > 0)
            {
                p.Add(Lbl("PROFIT, LAST 14 NIGHTS", "section"));
                float max = Mathf.Max(1, recent.Max(h => Mathf.Abs(h.profit)));
                var chart = El("chart");
                foreach (var h in recent)
                {
                    var b = El("chart-bar");
                    if (h.profit < 0) b.AddToClassList("chart-bar-neg");
                    b.style.height = Length.Percent(Mathf.Max(3, Mathf.Abs(h.profit) / max * 100));
                    b.tooltip = $"{Content.DayShort[h.day % 7]}: {Money(h.profit)}";
                    chart.Add(b);
                }
                p.Add(chart);
                p.Add(Lbl("RECENT NIGHTS", "section"));
                foreach (var h in recent.AsEnumerable().Reverse().Take(7))
                    p.Add(Row(Lbl($"{Content.DayShort[h.day % 7]} wk{h.day / 7 + 1}", "slider-label"), Lbl($"{h.customers} in", "card-sub"), Spacer(), Lbl(Money(h.takings), "card-sub"), Lbl("   " + Money(h.profit), h.profit >= 0 ? "price" : "neg")));
            }
        }

        // =====================================================================================
        //  REVIEWS
        // =====================================================================================

        void BuildReviews(VisualElement p)
        {
            var s = G.S;
            Title(p, "BarHopper", "What the internet thinks of you. Reviews are a to-do list in disguise.");
            p.Add(Row(Lbl($"{Stars(s.rating)}  {s.rating:0.0}", "big-num", "stars"), Lbl($"   {s.reviewCount} reviews  -  reputation {s.reputation:0}", "card-sub")));
            if (s.tags.Count > 0)
            {
                var tags = Row(); tags.AddToClassList("wrap");
                foreach (var t in s.tags) tags.Add(Lbl(t, "chip", IsBadTag(t) ? "chip-bad" : "chip-good"));
                p.Add(tags);
            }
            if (s.regulars.Count > 0)
            {
                p.Add(Lbl($"REGULARS ({s.regulars.Count})", "section"));
                var regs = Row(); regs.AddToClassList("wrap");
                foreach (var r in s.regulars.Take(20))
                {
                    var chip = Lbl(r.name, "chip");
                    chip.tooltip = $"{Content.Segments[r.seg].Name}, {r.visits} visits, drinks {Content.Drink(r.favDrink)?.Name}";
                    regs.Add(chip);
                }
                p.Add(regs);
            }
            p.Add(Lbl("LATEST", "section"));
            if (s.reviews.Count == 0) p.Add(Lbl("No reviews yet. Open the doors!", "hint"));
            foreach (var r in s.reviews.Take(25)) p.Add(ReviewCard(r));
        }
    }
}
