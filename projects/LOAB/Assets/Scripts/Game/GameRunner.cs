using System;
using Loab.Sim;
using UnityEngine;

namespace Loab.Game
{
    /// <summary>Owns the Game, drives the real-time Service phase and turns pointer input into commands.</summary>
    public sealed class GameRunner : MonoBehaviour
    {
        public const string SaveKey = "loab_save_v1";

        public Sim.Game G;
        public WorldView World;
        public CameraRig Rig;
        public Hud Ui;
        public Sfx Audio;
        public TiltShift Blur;

        // Interaction state
        public string PlaceId;      // item being placed from the catalogue
        public int PlaceRot;
        public PlacedItem Selected;
        public bool Moving;         // moving the selected item
        public Customer Inspected;
        public float SpeedMult = 1f;
        /// Night length setting (task U2): 1 = six real minutes at 1x, 0.6 = ten minutes.
        public float Pace = 1f;
        public bool Paused;

        Vector2 _downPos;
        bool _down, _dragging;
        float _pinchDist;
        float _tickAcc;
        int _lastEventPhase;
        int _hoverX = int.MinValue, _hoverY;
        bool _hoverOk;
        string _hoverErr;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRunner>() != null) return;
            new GameObject("LOAB").AddComponent<GameRunner>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.shadowDistance = 100;
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadows = ShadowQuality.All;
            ModelFactory.Init(Resources.Load<Material>("Materials/Lit"), Resources.Load<Material>("Materials/Glow"));
            Audio = gameObject.AddComponent<Sfx>();
            // Start with a fresh career in the background of the title screen so there's something to look at.
            G = Sim.Game.NewCareer((ulong)DateTime.Now.Ticks, 1);
            BuildScene();
            Ui = new Hud(this);
            Ui.ShowTitle();
        }

        void BuildScene()
        {
            var s = G.S;
            var centre = new Vector3((s.gridW - 1) / 2f, 0, (s.gridH - 1) / 2f);
            Rig = CameraRig.Create(centre, new Vector3(-2, 0, -4), new Vector3(s.gridW + 1, 0, s.gridH + 1));
            Blur = Rig.Cam.gameObject.AddComponent<TiltShift>();
            Blur.Mat = Resources.Load<Material>("Materials/TiltShift");
            Blur.Enabled = PlayerPrefs.GetInt("loab_blur", 1) == 1;
            WorldView.CharView.StopMotion = PlayerPrefs.GetInt("loab_stopmo", 0) == 1;
            Pace = PlayerPrefs.GetInt("loab_long_nights", 0) == 1 ? 0.6f : 1f;
            World = WorldView.Create(G, Rig);
        }

        // =====================================================================================
        //  Game lifecycle
        // =====================================================================================

        // Career, scenario and sandbox each get their own save slot, so trying one never wipes another.
        public static string KeyFor(int mode) => mode == 0 ? SaveKey : mode == 2 ? "loab_save_scenario" : "loab_save_sandbox";
        public int LastMode => PlayerPrefs.GetInt("loab_last_mode", 0);
        public bool HasSaveFor(int mode) => PlayerPrefs.HasKey(KeyFor(mode));
        public bool HasSave => HasSaveFor(LastMode);

        public void NewCareer(int difficulty)
        {
            var g = Sim.Game.NewCareer((ulong)DateTime.Now.Ticks, difficulty);
            G = g;
            Ui.Tip("welcome");
            Load(g);
        }

        public void NewScenario(string id)
        {
            var g = Sim.Game.NewScenario(id, (ulong)DateTime.Now.Ticks);
            G = g;
            Load(g);
        }

        public void NewSandbox() => Load(Sim.Game.NewSandbox((ulong)DateTime.Now.Ticks));

        public bool Continue(int mode = -1)
        {
            if (mode < 0) mode = LastMode;
            try
            {
                var json = PlayerPrefs.GetString(KeyFor(mode), null);
                if (string.IsNullOrEmpty(json)) return false;
                var s = JsonUtility.FromJson<GameState>(json);
                if (s == null || s.items == null || s.items.Count == 0) return false;
                Load(new Sim.Game(s));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Couldn't load save: " + e.Message);
                return false;
            }
        }

        /// Swaps in a different game in the same save slot (the second venue, task P5).
        public void SwitchTo(Sim.Game g) => Load(g);

        void Load(Sim.Game g)
        {
            G = g;
            G.Changed += () => Ui?.MarkDirty();
            World.Bind(G);
            Rig.Target = new Vector3((g.S.gridW - 1) / 2f, 0, (g.S.gridH - 1) / 2f - 1.6f);
            Rig.SetBounds(new Vector3(-2, 0, -4), new Vector3(g.S.gridW + 1, 0, g.S.gridH + 1));
            ClearInteraction();
            Paused = false; SpeedMult = 1;
            Ui.OnGameLoaded();
            Save();
            Resources.UnloadUnusedAssets(); // frees the previous model's combined meshes
        }

        public void Save()
        {
            // Only save at a day boundary (Prep) or game over. Saving on the report would replay that night on load.
            if (G == null || (G.Phase != Phase.Prep && G.Phase != Phase.GameOver)) return;
            try
            {
                PlayerPrefs.SetString(KeyFor(G.S.mode), JsonUtility.ToJson(G.S));
                PlayerPrefs.SetInt("loab_last_mode", G.S.mode);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        /// <summary>The room got bigger: rebuild the model and re-centre the camera.</summary>
        public void OnExpanded()
        {
            ClearInteraction();
            World.Bind(G);
            var s = G.S;
            Rig.Target = new Vector3((s.gridW - 1) / 2f, 0, (s.gridH - 1) / 2f - 1.6f);
            Rig.SetBounds(new Vector3(-2, 0, -4), new Vector3(s.gridW + 1, 0, s.gridH + 1));
            Rig.Zoom = Mathf.Min(CameraRig.MaxDist, Rig.Zoom * (s.gridW / 16f));
            Audio.Play("fanfare", 0.5f);
            Ui.Flash("The wall's down! You've got room to grow.");
            Save();
            Resources.UnloadUnusedAssets();
        }

        // ---- Device tiers (visual brief 16.2): step quality down if the frame rate can't keep up.
        int _tier = 2;
        // "?quality=high" in the page URL keeps full quality (for screenshots and visual checks on slow machines).
        static readonly bool PinnedQuality = Application.absoluteURL != null && Application.absoluteURL.Contains("quality=high");
        float _dtAvg = 1f / 60f, _tierTimer;

        void AutoQuality()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            _dtAvg = Mathf.Lerp(_dtAvg, dt, 0.02f);
            _tierTimer += dt;
            if (_tierTimer < 6f || _tier == 0 || Time.realtimeSinceStartup < 20f || PinnedQuality) return;
            _tierTimer = 0;
            if (_dtAvg > 1f / 28f)
            {
                _tier--;
                if (_tier == 1) { QualitySettings.pixelLightCount = 4; QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowDistance = 60; }
                else { QualitySettings.pixelLightCount = 2; QualitySettings.shadows = ShadowQuality.Disable; Blur.Enabled = false; Ui?.SetGrain(false); WorldView.HalosOn = false; }
                Debug.Log($"LOAB: lowered quality to tier {_tier} (avg {1f / _dtAvg:0} fps)");
            }
        }

        public void SetBlur(bool on)
        {
            Blur.Enabled = on;
            PlayerPrefs.SetInt("loab_blur", on ? 1 : 0);
        }

        /// <summary>Tilt-shift rules from the visual brief (5.2): stronger when zoomed in, gentle while building.</summary>
        void UpdateBlur()
        {
            float t = Rig.ZoomT;
            bool building = G.Phase == Phase.Prep && (PlaceId != null || Moving);
            float strength = Mathf.Lerp(0.55f, 1f, t);
            float band = Mathf.Lerp(0.3f, 0.15f, t);
            if (building) { strength *= 0.3f; band = 0.35f; }
            Blur.Strength = Mathf.Lerp(Blur.Strength, strength, 0.15f);
            Blur.Band = Mathf.Lerp(Blur.Band, band, 0.15f);
            Blur.Softness = Mathf.Lerp(0.3f, 0.2f, t);
            Blur.Spread = Mathf.Lerp(1.1f, 1.8f, t);
            // The sharp band follows what you're looking at (brief 5.1).
            float focus = 0.5f;
            Vector3? subject = null;
            if (G.Phase == Phase.Service && World.EventLive) subject = World.StagePos + Vector3.up;
            else if (Inspected != null) subject = new Vector3(Inspected.x, 1f, Inspected.y);
            else if (Selected != null) subject = WorldView.ItemCentre(Selected);
            if (subject.HasValue)
            {
                var sp = Rig.Cam.WorldToViewportPoint(subject.Value);
                if (sp.z > 0) focus = Mathf.Clamp(sp.y, 0.25f, 0.75f);
            }
            Blur.Focus = Mathf.Lerp(Blur.Focus, focus, 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.3f * 3f));
        }

        public void DeleteSave() { if (G != null) PlayerPrefs.DeleteKey(KeyFor(G.S.mode)); PlayerPrefs.Save(); }

        public void OpenDoors()
        {
            ClearInteraction();
            Save(); // keep tonight's Prep decisions
            var err = G.OpenDoors();
            if (err != null) { Ui.Flash(err); return; }
            Paused = false; SpeedMult = 1;
            Audio.Play("bell");
            if (G.Phase == Phase.Service) Ui.OnServiceStart();
            else Ui.OnReport();
        }

        public void SkipToClose()
        {
            if (G.Phase != Phase.Service) return;
            G.Svc.RunToEnd();
            G.Svc.SoundCues.Clear(); // don't play a whole night's cues at once
            G.EndServiceIfDone();
            if (G.Phase == Phase.Report) { ClearInteraction(); Audio.Play("till"); Ui.OnReport(); }
        }

        /// <summary>Leave to the title. Mid-service, the night is abandoned and the game reloads from the last save.</summary>
        public void QuitToTitle()
        {
            Paused = false;
            if (G.Phase == Phase.Service)
            {
                Audio.Ambience(0);
                Audio.Music(null, 0);
                if (!Continue(G.S.mode)) Load(Sim.Game.NewCareer((ulong)DateTime.Now.Ticks, 1));
            }
            else Save();
            Ui.ShowTitle();
        }

        public void NextDay()
        {
            G.NextDay();
            Save();
            Ui.OnPrep();
        }

        // =====================================================================================
        //  Interaction
        // =====================================================================================

        public void ClearInteraction()
        {
            PlaceId = null; Moving = false; Selected = null; Inspected = null;
            World?.HideGhost(); World?.Select(null);
            _hoverX = int.MinValue;
        }

        public void BeginPlace(string itemId)
        {
            ClearInteraction();
            PlaceId = itemId;
            PlaceRot = 0;
        }

        public void BeginMove()
        {
            if (Selected == null) return;
            Moving = true;
            PlaceRot = Selected.rot;
        }

        public void RotateSelectedOrGhost()
        {
            if (PlaceId != null || Moving) { PlaceRot = (PlaceRot + 1) % 4; _hoverX = int.MinValue; return; }
            if (Selected != null)
            {
                var err = G.Execute(new MoveCmd { Uid = Selected.uid, X = Selected.x, Y = Selected.y, Rot = (Selected.rot + 1) % 4 });
                if (err != null) Ui.Flash(err); else { Selected = G.S.Item(Selected.uid); World.Select(Selected); Audio.Play("place"); }
            }
        }

        public void SellSelected()
        {
            if (Selected == null) return;
            var err = G.Execute(new SellCmd { Uid = Selected.uid });
            if (err != null) { Ui.Flash(err); return; }
            Audio.Play("till");
            ClearInteraction();
        }

        void Update()
        {
            if (G == null) return;
            HandleKeys();
            HandlePointer();
            if (G.Phase == Phase.Service && !Ui.TitleOpen) TickService();
            UpdateGhost();
            UpdateBlur();
            AutoQuality();
            if (G != null && G.Phase == Phase.Prep) Audio.Music(G.S.music, 0.12f); // a quiet preview while you plan
            Ui.Update();
        }

        void TickService()
        {
            var svc = G.Svc;
            if (svc == null) return;
            if (!Paused)
            {
                // 1x = one game minute per real second (a 6-minute evening).
                _tickAcc += Time.deltaTime * SpeedMult * Pace / Service.Tick;
                int steps = Mathf.Min((int)_tickAcc, 400);
                _tickAcc -= steps;
                int before = svc.Messages.Count;
                float takings = svc.Takings;
                for (int i = 0; i < steps && !svc.Finished; i++) svc.Step();
                for (int i = before; i < svc.Messages.Count; i++) Ui.Toast(svc.Messages[i]);
                foreach (var cue in svc.SoundCues) { Audio.Play(cue, 0.45f); if (cue == "goal") Audio.Play("cheer", 0.55f); }
                svc.SoundCues.Clear();
                if (svc.EventPhase == 4 && _lastEventPhase != 4 && (svc.EventOutcome == "Hit" || svc.EventOutcome == "Viral hit")) Audio.Play("cheer", 0.7f);
                _lastEventPhase = svc.EventPhase;
                if (svc.Takings > takings && Time.frameCount % 3 == 0) Audio.Play("till", 0.25f);
            }
            Audio.Ambience(svc.Inside / (float)Mathf.Max(1, svc.Capacity));
            // The bar's own music under the crowd, scaled by its volume setting (task V1). Bands and the match take over.
            bool liveAct = svc.Event != null && svc.EventPhase >= 1 && svc.EventPhase <= 3 && (svc.Event.kind == Sim.EventKinds.LiveMusic || svc.Event.kind == Sim.EventKinds.Match);
            Audio.Music(G.S.music, liveAct ? 0.15f : 0.35f + G.S.volume / 100f * 0.65f);
            G.EndServiceIfDone();
            if (G.Phase == Phase.Report) { Audio.Ambience(0); Audio.Music(G.S.music, 0.2f); Audio.Play("till"); ClearInteraction(); Ui.OnReport(); }
        }

        void HandleKeys()
        {
            if (Ui.TextFocused) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (PlaceId != null || Moving || Selected != null || Inspected != null) ClearInteraction(); else Ui.CloseSide(); }
            if (Input.GetKeyDown(KeyCode.R)) RotateSelectedOrGhost();
            if (Input.GetKeyDown(KeyCode.H)) Ui.TogglePhotoMode();
            if (G.Phase == Phase.Report && !Ui.TitleOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && G.S.pendingCard == null && !G.S.bankrupt) NextDay();
            if (Input.GetKeyDown(KeyCode.Q)) Rig.Rotate(-1);
            if (Input.GetKeyDown(KeyCode.E)) Rig.Rotate(1);
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) SellSelected();
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand)) && Input.GetKeyDown(KeyCode.Z)) { G.Undo(); ClearInteraction(); }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand)) && Input.GetKeyDown(KeyCode.Y)) { G.Redo(); ClearInteraction(); }
            if (G.Phase == Phase.Service && !Ui.ModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.Space)) Paused = !Paused;
                if (Input.GetKeyDown(KeyCode.Alpha1)) { SpeedMult = 1; Paused = false; }
                if (Input.GetKeyDown(KeyCode.Alpha2)) { SpeedMult = 2; Paused = false; }
                if (Input.GetKeyDown(KeyCode.Alpha3)) { SpeedMult = 4; Paused = false; }
            }
            // Keyboard panning
            var pan = new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                                  (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            if (pan.sqrMagnitude > 0) Rig.Pan(-pan * Time.unscaledDeltaTime * 600f);
        }

        void HandlePointer()
        {
            // Pinch zoom
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                float d = Vector2.Distance(a, b);
                if (_pinchDist > 0) Rig.ZoomBy((_pinchDist - d) / 400f);
                _pinchDist = d;
                _down = false; _dragging = false;
                return;
            }
            _pinchDist = 0;

            Vector2 pos = Input.mousePosition;
            bool overUi = Ui.IsOverUI(pos);
            float wheel = Input.mouseScrollDelta.y;
            if (!overUi && Mathf.Abs(wheel) > 0.01f) Rig.ZoomBy(-wheel * 0.12f);

            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) { _dragging = true; _downPos = pos; _down = false; }
            if ((Input.GetMouseButton(1) || Input.GetMouseButton(2)) && _dragging)
            {
                Rig.Pan(pos - _downPos); _downPos = pos;
                return;
            }
            if (Input.GetMouseButtonUp(1)) { if ((PlaceId != null || Moving) && !_dragging) ClearInteraction(); _dragging = false; }
            if (Input.GetMouseButtonUp(2)) _dragging = false;

            if (Input.GetMouseButtonDown(0) && !overUi) { _down = true; _dragging = false; _downPos = pos; }
            if (_down && Input.GetMouseButton(0))
            {
                if (!_dragging && (pos - _downPos).magnitude > 8f && PlaceId == null && !Moving) _dragging = true;
                if (_dragging) { Rig.Pan(pos - _downPos); _downPos = pos; }
            }
            if (_down && Input.GetMouseButtonUp(0))
            {
                _down = false;
                if (!_dragging) Click(pos);
                _dragging = false;
            }
        }

        void Click(Vector2 screen)
        {
            if (PlaceId != null || Moving) { ConfirmGhost(); return; }
            var ray = Rig.Cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 200f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            if (G.Phase == Phase.Service)
            {
                foreach (var h in hits)
                {
                    var inc = h.collider.GetComponentInParent<IncidentTag>();
                    if (inc != null) { if (G.Svc.ResolveIncident(inc.Id)) { Audio.Play("mop"); } return; }
                    var ag = h.collider.GetComponentInParent<AgentTag>();
                    if (ag != null && ag.Customer != null)
                    {
                        var ri = G.Svc.IncidentForCustomer(ag.Customer);
                        if (ri != null && ag.Customer.rowdy) { G.Svc.ResolveIncident(ri.id); Audio.Play("whistle"); return; }
                        Inspected = ag.Customer;
                        Audio.Play("click");
                        return;
                    }
                    if (ag != null && ag.Staff != null && !ag.Staff.IsOwner)
                    {
                        var err = G.Svc.SwitchDuty(ag.Staff);
                        if (err != null) Ui.Flash(err); else Audio.Play("click");
                        return;
                    }
                    var itag = h.collider.GetComponentInParent<ItemTag>();
                    if (itag != null && G.Svc.ToiletBlocked(itag.Uid))
                    {
                        var ti = G.Svc.Incidents.Find(i => !i.resolved && i.kind == IncidentKind.Toilet && i.itemUid == itag.Uid);
                        if (ti != null) { G.Svc.ResolveIncident(ti.id); Audio.Play("mop"); return; }
                    }
                }
                Inspected = null;
                return;
            }
            if (G.Phase != Phase.Prep) return;
            foreach (var h in hits)
            {
                var itag = h.collider.GetComponentInParent<ItemTag>();
                if (itag != null)
                {
                    Selected = G.S.Item(itag.Uid);
                    World.Select(Selected);
                    Ui.OnSelected();
                    Audio.Play("click");
                    return;
                }
            }
            ClearInteraction();
            Ui.OnSelected();
        }

        void UpdateHover()
        {
            if (_dragging || Time.frameCount % 3 != 0) return;
            Vector2 pos = Input.mousePosition;
            if (Ui.IsOverUI(pos)) return;
            var ray = Rig.Cam.ScreenPointToRay(pos);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;
            if (G.Phase == Phase.Service)
            {
                var ag = hit.collider.GetComponentInParent<AgentTag>();
                if (ag != null && ag.Customer != null)
                {
                    var c = ag.Customer;
                    Ui.SetCursorHint(c.rowdy ? c.name + " is getting rowdy. Click to have a word." : c.name + " (" + c.def.Name.ToLowerInvariant() + "). Click for details.", pos);
                    return;
                }
                if (ag != null && ag.Staff != null && !ag.Staff.IsOwner && ag.Staff.m != null)
                {
                    Ui.SetCursorHint($"{ag.Staff.m.name} ({(ag.Staff.Duty == 0 ? "on the bar" : "on the floor")}): click to move them {(ag.Staff.Duty == 0 ? "onto the floor" : "behind the bar")}", pos);
                    return;
                }
                if (hit.collider.GetComponentInParent<IncidentTag>() != null) Ui.SetCursorHint("Click to sort this out (collect, mop or fix)", pos);
            }
            else if (G.Phase == Phase.Prep)
            {
                var itag = hit.collider.GetComponentInParent<ItemTag>();
                var it = itag != null ? G.S.Item(itag.Uid) : null;
                if (it != null) Ui.SetCursorHint(Content.Item(it.id).Name + "  -  click to move, rotate or sell", pos);
            }
        }

        void UpdateGhost()
        {
            string id = PlaceId ?? (Moving && Selected != null ? Selected.id : null);
            if (id == null && (G.Phase == Phase.Prep || G.Phase == Phase.Service)) UpdateHover();
            if (id == null || G.Phase != Phase.Prep) { World.HideGhost(); return; }
            Vector2 pos = Input.mousePosition;
            if (Ui.IsOverUI(pos) || !Rig.ScreenToGround(pos, out var world)) { World.HideGhost(); return; }
            var d = Content.Item(id);
            Layout.Size(d, PlaceRot, out int w, out int h);
            int x = Mathf.RoundToInt(world.x - (w - 1) / 2f);
            int y = Mathf.RoundToInt(world.z - (h - 1) / 2f);
            if (x != _hoverX || y != _hoverY)
            {
                _hoverX = x; _hoverY = y;
                _hoverErr = Moving ? new MoveCmd { Uid = Selected.uid, X = x, Y = y, Rot = PlaceRot }.Validate(G)
                                   : new PlaceCmd { Id = id, X = x, Y = y, Rot = PlaceRot }.Validate(G);
                _hoverOk = _hoverErr == null;
            }
            World.ShowGhost(id, x, y, PlaceRot, _hoverOk);
            Ui.SetCursorHint(_hoverOk ? (Moving ? "Click to move here" : $"Click to place (£{d.Cost:#,0})  -  R to rotate, right-click to cancel") : _hoverErr, pos);
        }

        void ConfirmGhost()
        {
            if (_hoverX == int.MinValue) return;
            string err;
            if (Moving)
            {
                err = G.Execute(new MoveCmd { Uid = Selected.uid, X = _hoverX, Y = _hoverY, Rot = PlaceRot });
                if (err == null) { Moving = false; Selected = G.S.Item(Selected.uid); World.Select(Selected); Audio.Play("place"); }
            }
            else
            {
                err = G.Execute(new PlaceCmd { Id = PlaceId, X = _hoverX, Y = _hoverY, Rot = PlaceRot });
                if (err == null)
                {
                    Audio.Play("place");
                    // Keep placing the same thing (handy for stools) unless it was a big item or cash ran out.
                    var d = Content.Item(PlaceId);
                    if (d.W * d.H >= 4 || (G.S.cash < d.Cost && G.S.difficulty > 0)) ClearInteraction();
                }
            }
            if (err != null) { Ui.Flash(err); Audio.Play("error"); }
            _hoverX = int.MinValue;
        }
    }
}
