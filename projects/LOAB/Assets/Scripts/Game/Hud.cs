using System;
using System.Collections.Generic;
using Loab.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loab.Game
{
    /// <summary>All UI, built in code with UI Toolkit. Split across Hud*.cs files.</summary>
    public sealed partial class Hud
    {
        readonly GameRunner _run;
        readonly UIDocument _doc;
        readonly VisualElement _root;
        Sim.Game G => _run.G;

        // Persistent chrome
        VisualElement _top, _tabs, _side, _sideBody, _feed, _goal, _mentor, _actions, _inspector, _overlay;
        Label _tooltip, _thought, _sign, _plaque;
        ScrollView _sideScroll;
        Label _goalLabel;
        Label _barName, _dayText, _cash, _rating, _rep, _custLabel, _custVal, _clock, _goalText, _hint;
        Button _openBtn;
        VisualElement _speedBox, _stats, _camBox;
        readonly List<Button> _speedBtns = new List<Button>();
        readonly List<(VisualElement el, float until)> _toasts = new List<(VisualElement, float)>();
        string _tab;
        bool _dirty;
        float _hintUntil;
        Button _behindBtn, _happyBtn, _collectBtn, _lastOrdersBtn;
        public bool TextFocused;
        int _tooltipFrame;

        public static readonly string Star = "★", EmptyStar = "☆";

        public Hud(GameRunner run)
        {
            _run = run;
            var go = new GameObject("UI");
            _doc = go.AddComponent<UIDocument>();
            var ps = Resources.Load<PanelSettings>("UI/PanelSettings");
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/LoabTheme");
            }
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1366, 800);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.6f;
            _doc.panelSettings = ps;
            _root = _doc.rootVisualElement;
            _root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Loab"));
            _root.AddToClassList("root");
            if (PlayerPrefs.GetInt("loab_large_text", 0) == 1) ps.scale = 1.18f;
            _root.pickingMode = PickingMode.Ignore;
            BuildChrome();
        }

        // =====================================================================================
        //  Element helpers
        // =====================================================================================

        static VisualElement El(params string[] cls)
        {
            var e = new VisualElement();
            foreach (var c in cls) e.AddToClassList(c);
            return e;
        }

        static Label Lbl(string text, params string[] cls)
        {
            var l = new Label(text);
            foreach (var c in cls) l.AddToClassList(c);
            return l;
        }

        static Button Btn(string text, Action onClick, params string[] cls)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            foreach (var c in cls) b.AddToClassList(c);
            return b;
        }

        static VisualElement Row(params VisualElement[] kids)
        {
            var r = El("row");
            foreach (var k in kids) if (k != null) r.Add(k);
            return r;
        }

        static VisualElement Bar(float frac, Color? colour = null, float width = -1)
        {
            var bg = El("bar-bg");
            if (width > 0) { bg.style.width = width; bg.style.flexGrow = 0; }
            var fill = El("bar-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(frac) * 100f);
            if (colour.HasValue) fill.style.backgroundColor = colour.Value;
            bg.Add(fill);
            return bg;
        }

        static Color Col(uint rgb) => WorldView.ToColor(rgb);

        public static string Stars(float rating)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(rating), 0, 5);
            return new string(Star[0], full) + (full < 5 ? "<color=#5a4a3a>" + new string(Star[0], 5 - full) + "</color>" : "");
        }

        static string Money(float v) => (v < 0 ? "-£" : "£") + Mathf.Abs(v).ToString("#,0");

        static VisualElement Spacer() { var e = new VisualElement(); e.style.flexGrow = 1; return e; }

        // =====================================================================================
        //  Chrome
        // =====================================================================================

        void BuildChrome()
        {
            _sign = Lbl("", "sign");
            _sign.pickingMode = PickingMode.Ignore;
            _root.Add(_sign); // first children, so every other piece of UI draws over them
            _plaque = Lbl("", "sign", "plaque");
            _plaque.pickingMode = PickingMode.Ignore;
            _root.Add(_plaque);

            _top = El("topbar");
            _barName = Lbl("", "barname");
            _dayText = Lbl("", "daytext");
            _top.Add(_barName);
            _top.Add(_dayText);
            _top.Add(Spacer());
            _speedBox = El("speed");
            _clock = Lbl("18:00", "clock");
            _speedBox.Add(_clock);
            foreach (var (label, mult) in new[] { ("||", 0f), ("1x", 1f), ("2x", 2f), ("4x", 4f) })
            {
                var m = mult;
                var b = Btn(label, () => { if (m == 0) _run.Paused = !_run.Paused; else { _run.SpeedMult = m; _run.Paused = false; } }, "btn-small");
                b.style.width = 40;
                _speedBtns.Add(b);
                _speedBox.Add(b);
            }
            _speedBox.Add(Btn("Skip to close", () => _run.SkipToClose(), "btn-small"));
            _top.Add(_speedBox);
            _top.Add(Spacer());
            _stats = El("row");
            _custVal = Lbl("", "stat-value"); _custLabel = Lbl("customers", "stat-label");
            _stats.Add(Stat(_custVal, _custLabel));
            _rep = Lbl("", "stat-value"); _stats.Add(Stat(_rep, Lbl("reputation", "stat-label")));
            _rating = Lbl("", "stat-value", "stars"); _stats.Add(Stat(_rating, Lbl("BarHopper", "stat-label")));
            _cash = Lbl("", "stat-value", "cash"); _stats.Add(Stat(_cash, Lbl("cash", "stat-label")));
            _top.Add(_stats);
            var menuBtn = Btn("Menu", () => ShowPause(), "btn-small");
            menuBtn.style.marginLeft = 14;
            _top.Add(menuBtn);
            _root.Add(_top);

            _tabs = El("tabs");
            foreach (var t in new[] { "Build", "Vibe", "Drinks", "Staff", "Events", "Promote", "Money", "Reviews" })
            {
                var name = t;
                var b = Btn(t, () => ToggleTab(name), "tab");
                b.name = "tab-" + t;
                _tabs.Add(b);
            }
            _root.Add(_tabs);

            _side = El("panel", "side");
            _sideScroll = new ScrollView(ScrollViewMode.Vertical);
            _sideScroll.style.flexGrow = 1;
            _sideScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _sideBody = new VisualElement();
            _sideScroll.Add(_sideBody);
            _side.Add(_sideScroll);
            _side.style.display = DisplayStyle.None;
            _root.Add(_side);

            _goal = El("panel", "goal");
            _goalLabel = Lbl("CURRENT GOAL", "goal-label");
            _goal.Add(_goalLabel);
            _goalText = Lbl("", "goal-text");
            _goal.Add(_goalText);
            _root.Add(_goal);

            _openBtn = Btn("OPEN DOORS", () => _run.OpenDoors(), "btn-primary", "open-btn");
            _root.Add(_openBtn);

            _camBox = El("row");
            _camBox.style.position = Position.Absolute;
            _camBox.style.right = 18; _camBox.style.bottom = 90;
            AddCam("<", () => _run.Rig.Rotate(-1), "Rotate view (Q)");
            AddCam(">", () => _run.Rig.Rotate(1), "Rotate view (E)");
            AddCam("+", () => _run.Rig.ZoomBy(-0.15f), "Zoom in");
            AddCam("-", () => _run.Rig.ZoomBy(0.15f), "Zoom out");
            _root.Add(_camBox);

            _actions = El("actions");
            _happyBtn = Btn("Happy Hour\n(1 hour, once a night)", () => { var e = G.Svc?.StartHappyHour(); if (e != null) Flash(e); else _run.Audio.Play("coin"); }, "action");
            _collectBtn = Btn("Collect glasses", () => { var e = G.Svc?.CollectGlasses(); if (e != null) Flash(e); else _run.Audio.Play("place"); }, "action");
            _behindBtn = Btn("Help behind\nthe bar", () => { if (G.Svc != null) G.Svc.SetOwnerBehindBar(!G.Svc.OwnerBehindBar); }, "action");
            var emerg = Btn("Emergency\ndelivery", () => ShowEmergency(), "action");
            _lastOrdersBtn = Btn("Last orders\n(close early)", () => { var e = G.Svc?.CallLastOrdersEarly(); if (e != null) Flash(e); else _run.Audio.Play("bell"); }, "action");
            _lastOrdersBtn.tooltip = "Call last orders now and close 30 minutes later. Fewer drinks sold, but it calms a rowdy night.";
            _actions.Add(_happyBtn); _actions.Add(_collectBtn); _actions.Add(_behindBtn); _actions.Add(emerg); _actions.Add(_lastOrdersBtn);
            _root.Add(_actions);

            _feed = El("feed");
            _feed.pickingMode = PickingMode.Ignore;
            _root.Add(_feed);

            _inspector = El("panel", "inspector");
            _inspector.style.display = DisplayStyle.None;
            _root.Add(_inspector);


            _thought = Lbl("", "thought");
            _thought.pickingMode = PickingMode.Ignore;
            _thought.style.display = DisplayStyle.None;
            _root.Add(_thought);

            _tooltip = Lbl("", "tooltip");
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;
            _root.Add(_tooltip);

            _hint = Lbl("", "toast");
            _hint.style.position = Position.Absolute;
            _hint.style.bottom = 90; _hint.style.alignSelf = Align.Center;
            _hint.style.left = Length.Percent(35); _hint.style.right = Length.Percent(35);
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.display = DisplayStyle.None;
            _hint.pickingMode = PickingMode.Ignore;
            _root.Add(_hint);

            _overlay = El("overlay");
            _overlay.style.display = DisplayStyle.None;
            _root.Add(_overlay);

            _mentor = El("panel", "mentor");
            _mentor.style.display = DisplayStyle.None;
            _root.Add(_mentor);
            SetMode();
        }

        static VisualElement Stat(Label value, Label label)
        {
            var s = El("stat");
            s.Add(value); s.Add(label);
            return s;
        }

        void SetMode()
        {
            bool inGame = G != null && _overlayKind != "title";
            bool prep = inGame && G.Phase == Phase.Prep;
            bool service = inGame && G.Phase == Phase.Service;
            _top.style.display = inGame ? DisplayStyle.Flex : DisplayStyle.None;
            _tabs.style.display = prep ? DisplayStyle.Flex : DisplayStyle.None;
            _openBtn.style.display = prep ? DisplayStyle.Flex : DisplayStyle.None;
            _actions.style.display = service ? DisplayStyle.Flex : DisplayStyle.None;
            _camBox.style.display = prep || service ? DisplayStyle.Flex : DisplayStyle.None;
            _speedBox.style.display = service ? DisplayStyle.Flex : DisplayStyle.None;
            bool sideOpen = prep && (_tab != null || _run.Selected != null);
            bool hasGoal = G.CurrentGoal != null || (G.Scenario != null && G.S.scenarioResult == 0);
            _goal.style.display = (prep || service) && hasGoal && !sideOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!prep) { _side.style.display = DisplayStyle.None; _tab = null; UpdateTabButtons(); }
            if (!service) { _inspector.style.display = DisplayStyle.None; _thought.style.display = DisplayStyle.None; }
            _feed.style.display = service ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // =====================================================================================
        //  Lifecycle hooks from the runner
        // =====================================================================================

        public void OnGameLoaded()
        {
            CloseOverlay();
            ClearToasts();
            OnPrep();
        }

        public void OnPrep()
        {
            CloseOverlay();
            SetMode();
            MarkDirty();
            if (G.Phase == Phase.GameOver || G.S.bankrupt) { G.Phase = Phase.GameOver; ShowGameOver(); }
            else if (G.S.pendingCard != null) ShowCard();
            if (G.S.day == 0) Tip("prep");
            else if (G.S.day == 1) Tip("vibe");
            else if (G.S.day == 2) Tip("stock");
            else if (G.S.day == 3) Tip("events");
            else if (G.S.cash < 1500) Tip("cash");
            if (G.LastNight != null && G.LastNight.thoughts.Exists(t => t.Contains("slow") || t.Contains("gave up"))) Tip("staff");
        }

        public void OnServiceStart()
        {
            HideMentor(false);
            _tipQueue.Remove("prep"); _tipQueue.Remove("vibe"); _tipQueue.Remove("stock"); _tipQueue.Remove("events"); _tipQueue.Remove("cash");
            CloseOverlay();
            ClearToasts();
            SetMode();
            if (G.Svc != null) foreach (var m in G.Svc.Messages) Toast(m);
            Tip("service");
        }

        public void OnReport()
        {
            HideMentor(false);
            _tipQueue.Remove("service");
            SetMode();
            ShowReport();
        }

        public void OnSelected()
        {
            if (_run.Selected != null) { _tab = null; UpdateTabButtons(); }
            MarkDirty();
        }

        public void MarkDirty() => _dirty = true;
        public bool ModalOpen => _overlayKind != null;
        public bool TitleOpen => _overlayKind == "title";

        bool _photo;
        bool _photoHidden => false;
        public void TogglePhotoMode()
        {
            _photo = !_photo;
            foreach (var child in _root.Children()) child.visible = !_photo || child == _sign || child == _plaque;
            if (!_photo) SetMode();
        }

        // =====================================================================================
        //  Per-frame
        // =====================================================================================

        public void Update()
        {
            if (G == null) return;
            var s = G.S;
            _barName.text = s.barName;
            _dayText.text = $"{Content.DayNames[s.DayOfWeek]}  -  Week {s.Week}, {Content.Seasons[s.Season]}  -  {s.weather}";
            _dayText.tooltip = Economy.SeasonNote(s) ?? $"{Content.Seasons[s.Season]}, {s.weather.ToLowerInvariant()}.";
            _cash.text = Money(s.cash);
            _cash.EnableInClassList("neg", s.cash < 0);
            _rating.text = $"{Stars(s.rating)} {s.rating:0.0}";
            _rep.text = $"{s.reputation:0}";
            var gl = G.CurrentGoal;
            var sc = G.Scenario;
            if (sc != null)
            {
                _goalLabel.text = $"SCENARIO: {sc.Name.ToUpperInvariant()}  -  {G.ScenarioDaysLeft} DAYS LEFT";
                _goalText.text = $"{sc.GoalText}\n{sc.Progress(G)}";
            }
            else
            {
                _goalLabel.text = "CURRENT GOAL";
                if (gl != null) _goalText.text = gl.Reward > 0 ? $"{gl.Text}  (+£{gl.Reward})" : gl.Text;
            }
            if (G.Phase == Phase.Service && G.Svc != null)
            {
                var svc = G.Svc;
                _clock.text = Service.Clock(Mathf.Min(svc.T, svc.CloseAt));
                _custVal.text = $"{svc.Inside}/{svc.Capacity}";
                _custLabel.text = "inside";
                for (int i = 0; i < _speedBtns.Count; i++)
                {
                    bool on = i == 0 ? _run.Paused : !_run.Paused && Mathf.Approximately(_run.SpeedMult, i == 1 ? 1 : i == 2 ? 2 : 4);
                    _speedBtns[i].EnableInClassList("btn-on", on);
                }
                _happyBtn.SetEnabled(!svc.HappyHourUsed && svc.T < svc.LastOrdersAt);
                _lastOrdersBtn.SetEnabled(svc.T >= 180 && svc.T < svc.LastOrdersAt);
                _happyBtn.text = svc.HappyHour ? "Happy Hour\nON" : svc.HappyHourUsed ? "Happy Hour\nused" : $"Happy Hour\n({s.happyHourDiscount}% off, 1 hour)";
                _collectBtn.text = svc.CollectCooldown > 0 ? "Collect glasses\n(washing...)" : $"Collect glasses\n({CountGlasses(svc)} dirty)";
                _behindBtn.text = svc.OwnerBehindBar ? "Behind the bar\n(you're serving)" : "Help behind\nthe bar";
                _behindBtn.EnableInClassList("btn-on", svc.OwnerBehindBar);
                UpdateInspector(svc);
                UpdateBadges(svc);
            }
            else
            {
                if (_badges.Count > 0) UpdateBadges(null);
                float exp = Economy.ExpectedTotal(s, G.V);
                _custVal.text = $"~{exp:0}";
                _custLabel.text = "expected tonight";
            }

            // Bar name on the hanging sign outside (a screen-space label pinned to the board).
            bool showSign = _overlayKind != "title" && _root.panel != null && !_photoHidden;
            if (showSign)
            {
                var sp = WorldToPanel(WorldView.SignPos(s));
                _sign.text = s.barName;
                _sign.style.left = sp.x; _sign.style.top = sp.y;
                float ppu = _root.layout.height / _run.Rig.VisibleHeight; // panel pixels per world unit (at the focus)
                _sign.style.fontSize = Mathf.Clamp(0.16f * ppu, 5, 30);
                _sign.style.width = 1.25f * ppu;
                var pp = WorldToPanel(WorldView.PlaquePos(s));
                _plaque.text = s.barName;
                _plaque.style.left = pp.x; _plaque.style.top = pp.y;
                _plaque.style.fontSize = Mathf.Clamp(0.3f * ppu, 8, 40);
                _plaque.style.width = 3.3f * ppu;
                _plaque.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                _sign.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            }
            _sign.style.display = showSign ? DisplayStyle.Flex : DisplayStyle.None;
            _plaque.style.display = showSign ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > _toasts[i].until) { _toasts[i].el.RemoveFromHierarchy(); _toasts.RemoveAt(i); }

            if (Time.unscaledTime > _hintUntil) _hint.style.display = DisplayStyle.None;
            if (Time.frameCount - _tooltipFrame > 4) _tooltip.style.display = DisplayStyle.None;

            if (_dirty && !Input.GetMouseButton(0))
            {
                _dirty = false;
                RefreshSide();
                SetMode();
            }
            var fe = _root.focusController?.focusedElement as VisualElement;
            TextFocused = fe != null && (fe is TextField || fe.GetFirstAncestorOfType<TextField>() != null);
        }

        static int CountGlasses(Service svc)
        {
            int n = 0;
            foreach (var i in svc.Incidents) if (!i.resolved && i.kind == IncidentKind.Glasses) n += i.count;
            return n;
        }

        public bool IsOverUI(Vector2 screenPos)
        {
            var panel = _root.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = panel.Pick(p);
            return picked != null && picked != _root;
        }

        Vector2 WorldToPanel(Vector3 world)
        {
            var sp = _run.Rig.Cam.WorldToScreenPoint(world);
            return RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(sp.x, Screen.height - sp.y));
        }

        void AddCam(string label, Action act, string tip)
        {
            var b = Btn(label, act, "btn-small");
            b.style.width = 34; b.style.height = 30; b.tooltip = tip;
            _camBox.Add(b);
        }

        public void SetLargeText(bool on)
        {
            _doc.panelSettings.scale = on ? 1.18f : 1f;
            PlayerPrefs.SetInt("loab_large_text", on ? 1 : 0);
        }

        public void SetCursorHint(string text, Vector2 screenPos)
        {
            if (_root.panel == null) return;
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            _tooltipFrame = Time.frameCount;
            _tooltip.text = text;
            _tooltip.style.left = p.x + 18;
            _tooltip.style.top = p.y + 14;
            _tooltip.style.display = DisplayStyle.Flex;
        }

        public void Flash(string text)
        {
            _hint.text = text;
            _hint.style.display = DisplayStyle.Flex;
            _hintUntil = Time.unscaledTime + 2.8f;
        }

        public void Toast(ServiceMessage m)
        {
            var t = Lbl($"{Service.Clock(m.time)}  {m.text}", "toast");
            if (m.kind == 1) t.AddToClassList("toast-good");
            if (m.kind == 2) t.AddToClassList("toast-bad");
            if (m.kind == 3) t.AddToClassList("toast-event");
            _feed.Insert(0, t);
            _toasts.Add((t, Time.unscaledTime + (m.kind == 2 ? 9f : 7f)));
            while (_feed.childCount > 6) { var last = _feed[_feed.childCount - 1]; last.RemoveFromHierarchy(); _toasts.RemoveAll(x => x.el == last); }
            if (m.kind == 2) _run.Audio.Play("error", 0.25f);
        }

        void ClearToasts() { _feed.Clear(); _toasts.Clear(); }

        // =====================================================================================
        //  Tabs & side panel
        // =====================================================================================

        void ToggleTab(string tab)
        {
            _run.Audio.Play("click");
            if (_tab == tab) { CloseSide(); return; }
            _tab = tab;
            _side.style.display = DisplayStyle.Flex;
            UpdateTabButtons();
            SetMode();
            _sideScroll.scrollOffset = Vector2.zero;
            RefreshSide();
        }

        public void CloseSide()
        {
            _tab = null;
            _side.style.display = DisplayStyle.None;
            UpdateTabButtons();
            SetMode();
        }

        void UpdateTabButtons()
        {
            foreach (var child in _tabs.Children())
                child.EnableInClassList("tab-on", child.name == "tab-" + _tab);
        }

        void RefreshSide()
        {
            if (G == null || G.Phase != Phase.Prep)
            {
                return;
            }
            // Item selection panel takes over the side panel when something's selected in the world.
            if (_run.Selected != null && _tab == null)
            {
                _side.style.display = DisplayStyle.Flex;
                var off = _sideScroll.scrollOffset;
                _sideBody.Clear();
                BuildSelected(_sideBody);
                return;
            }
            if (_tab == null) { _side.style.display = DisplayStyle.None; return; }
            var offset = _sideScroll.scrollOffset;
            _sideBody.Clear();
            switch (_tab)
            {
                case "Build": BuildBuild(_sideBody); break;
                case "Vibe": BuildVibe(_sideBody); break;
                case "Drinks": BuildDrinks(_sideBody); break;
                case "Staff": BuildStaff(_sideBody); break;
                case "Events": BuildEvents(_sideBody); break;
                case "Promote": BuildPromote(_sideBody); break;
                case "Money": BuildMoney(_sideBody); break;
                case "Reviews": BuildReviews(_sideBody); break;
            }
            _sideScroll.schedule.Execute(() => _sideScroll.scrollOffset = offset);
        }

        // =====================================================================================
        //  Mentor tips
        // =====================================================================================

        public void Tip(string key)
        {
            if (G == null || G.S.seenTips.Contains(key) || !Mentor.Tips.ContainsKey(key)) return;
            if (_overlayKind == "title" || _overlayKind == "card" || _mentor.style.display == DisplayStyle.Flex)
            {
                if (!_tipQueue.Contains(key)) _tipQueue.Add(key);
                return;
            }
            var text = Mentor.Tips[key];
            _mentorKey = key;
            G.S.seenTips.Add(key);
            _mentor.Clear();
            var portrait = Lbl("M", "portrait");
            _mentor.Add(portrait);
            var col = El("col", "grow");
            col.Add(Lbl(Mentor.Name + ", the old landlady", "section"));
            var body = Lbl(text, "body");
            body.style.fontSize = 14;
            col.Add(body);
            var ok = Btn("Got it", () => { _mentor.style.display = DisplayStyle.None; _run.Audio.Play("click"); FlushPendingTip(); }, "btn-primary");
            ok.style.alignSelf = Align.FlexEnd;
            col.Add(ok);
            _mentor.Add(col);
            _mentor.style.display = DisplayStyle.Flex;
        }

        readonly List<string> _tipQueue = new List<string>();
        string _mentorKey;

        /// <summary>Hide Maggie. If requeue, her current tip comes back once the way is clear.</summary>
        void HideMentor(bool requeue)
        {
            if (_mentor.style.display != DisplayStyle.Flex) return;
            _mentor.style.display = DisplayStyle.None;
            if (requeue && _mentorKey != null)
            {
                G.S.seenTips.Remove(_mentorKey);
                _tipQueue.Insert(0, _mentorKey);
            }
        }

        void FlushPendingTip()
        {
            if (_tipQueue.Count == 0) return;
            var k = _tipQueue[0]; _tipQueue.RemoveAt(0);
            Tip(k);
        }

        // =====================================================================================
        //  Customer inspector (service)
        // =====================================================================================

        Customer _inspectedShown;
        readonly List<Label> _badges = new List<Label>();

        Label Badge(int i)
        {
            while (_badges.Count <= i)
            {
                var b = Lbl("", "badge");
                b.pickingMode = PickingMode.Ignore;
                _root.Insert(2, b); // under the panels, over the world
                _badges.Add(b);
            }
            return _badges[i];
        }

        /// <summary>Crisp UI badges for moods and problems, pinned over the 3D scene.</summary>
        void UpdateBadges(Service svc)
        {
            int n = 0;
            if (svc != null && !_photo)
            {
                foreach (var c in svc.Customers)
                {
                    if (n >= 60) break;
                    string text = null, cls = null;
                    if (c.rowdy) { text = "!"; cls = "badge-bad"; }
                    else if (c.mood < 32 && c.state != CState.Leaving) { text = "?"; cls = "badge-warn"; }
                    else if (c.mood > 80) { text = "+"; cls = "badge-good"; }
                    if (text == null || !_run.World.TryHeadPos(c, out var head)) continue;
                    var p = WorldToPanel(head);
                    var b = Badge(n++);
                    b.text = text;
                    b.EnableInClassList("badge-bad", cls == "badge-bad");
                    b.EnableInClassList("badge-warn", cls == "badge-warn");
                    b.EnableInClassList("badge-good", cls == "badge-good");
                    b.EnableInClassList("badge-wide", false);
                    b.style.left = p.x; b.style.top = p.y;
                    b.style.display = DisplayStyle.Flex;
                }
                foreach (var inc in svc.Incidents)
                {
                    if (n >= 70 || inc.resolved || inc.kind != IncidentKind.Toilet) continue;
                    var it = G.S.Item(inc.itemUid);
                    if (it == null) continue;
                    var p = WorldToPanel(WorldView.ItemCentre(it) + Vector3.up * 2.4f);
                    var b = Badge(n++);
                    b.text = "WC";
                    b.EnableInClassList("badge-bad", true);
                    b.EnableInClassList("badge-warn", false);
                    b.EnableInClassList("badge-good", false);
                    b.EnableInClassList("badge-wide", true);
                    b.style.left = p.x; b.style.top = p.y;
                    b.style.display = DisplayStyle.Flex;
                }
            }
            for (int i = n; i < _badges.Count; i++) _badges[i].style.display = DisplayStyle.None;
        }

        void UpdateInspector(Service svc)
        {
            var c = _run.Inspected;
            if (c != null && (c.state == CState.Gone || !svc.Customers.Contains(c))) { _run.Inspected = c = null; }
            if (c == null)
            {
                _inspector.style.display = DisplayStyle.None;
                // Show a random customer's thought now and then so the room feels alive.
                ShowAmbientThought(svc);
                return;
            }
            _inspector.style.display = DisplayStyle.Flex;
            if (_inspectedShown != c || Time.frameCount % 15 == 0)
            {
                _inspectedShown = c;
                _inspector.Clear();
                var head = Row(Lbl(c.name, "card-title"), Spacer(), Btn("x", () => _run.Inspected = null, "btn-small"));
                _inspector.Add(head);
                _inspector.Add(Lbl(c.regular != null ? $"Regular  -  {c.def.Name}  -  {c.regular.visits} visits" : c.def.Name, "card-sub"));
                _inspector.Add(Row(Lbl("Mood", "slider-label"), Bar(c.mood / 100f, c.mood < 35 ? Col(0xff6e64) : c.mood < 60 ? Col(0xffb347) : Col(0x78dc8c))));
                _inspector.Add(Row(Lbl("Thirst", "slider-label"), Bar(c.thirst, Col(0x4cc9f0))));
                _inspector.Add(Row(Lbl("Toilet", "slider-label"), Bar(c.bladder, Col(0xc8a2ff))));
                _inspector.Add(Lbl($"Drinks: {c.drinks}   Spent: £{c.spent:0.00}   Budget left: £{Mathf.Max(0, c.budget - c.spent):0}", "card-sub"));
                _inspector.Add(Lbl("Doing: " + Describe(c), "card-sub"));
                if (c.drink != null) _inspector.Add(Lbl("Drinking: " + c.drink.Name, "card-sub"));
                var th = c.thought ?? "Just enjoying the evening.";
                var b = Lbl("\"" + th + "\"", "body");
                b.style.marginTop = 6; b.style.fontSize = 13;
                _inspector.Add(b);
            }
            PlaceThought(c, c.thought);
        }

        static string Describe(Customer c) => c.state switch
        {
            CState.ToBar => "Heading to the bar",
            CState.Queue => "Queuing at the bar",
            CState.Ordering => "Being served",
            CState.ToSeat => "Looking for a seat",
            CState.Seated => "Sitting down",
            CState.Standing => "Standing around",
            CState.ToToilet or CState.ToiletQueue => "Queuing for the loo",
            CState.InToilet => "In the loo",
            CState.ToGame or CState.Playing => "Playing games",
            CState.Leaving => "Heading home",
            _ => "Arriving",
        };

        Customer _ambient;
        float _ambientUntil;

        void ShowAmbientThought(Service svc)
        {
            if (_ambient != null && (Time.unscaledTime > _ambientUntil || !svc.Customers.Contains(_ambient))) _ambient = null;
            if (_ambient == null && svc.Customers.Count > 0 && UnityEngine.Random.value < 0.01f)
            {
                var c = svc.Customers[UnityEngine.Random.Range(0, svc.Customers.Count)];
                if (c.thought != null && c.thoughtTime > 0 && c.state != CState.InToilet) { _ambient = c; _ambientUntil = Time.unscaledTime + 4f; }
            }
            if (_ambient != null) PlaceThought(_ambient, _ambient.thought);
            else _thought.style.display = DisplayStyle.None;
        }

        void PlaceThought(Customer c, string text)
        {
            if (string.IsNullOrEmpty(text) || c.state == CState.InToilet) { _thought.style.display = DisplayStyle.None; return; }
            var p = WorldToPanel(new Vector3(c.x, 2.2f, c.y));
            _thought.text = text;
            _thought.style.left = p.x - 60;
            _thought.style.top = p.y - 44;
            _thought.style.display = DisplayStyle.Flex;
        }

        void ShowEmergency()
        {
            if (G.Svc == null) return;
            var m = Modal("Emergency delivery", "A van can be here in about 20 minutes, at 60% over the usual price. What do you need?");
            var list = El("col");
            foreach (var me in G.S.menu)
            {
                if (!me.onMenu) continue;
                var d = Content.Drink(me.id);
                float cost = d.CaseSize * d.UnitCost * Economy.CostMult(G.S) * 1.6f;
                var id = me.id;
                var b = Btn($"{d.Name}  -  {me.stock} left  -  £{cost:0}", () => { var e = G.Svc.EmergencyDelivery(id); if (e != null) Flash(e); CloseOverlay(); }, "option");
                if (me.stock < 20) b.AddToClassList("btn-bad");
                list.Add(b);
            }
            m.Add(list);
            m.Add(Btn("Never mind", CloseOverlay, "option"));
        }
    }
}
