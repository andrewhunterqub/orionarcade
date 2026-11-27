using System.Collections.Generic;
using Loab.Sim;
using UnityEngine;

namespace Loab.Game
{
    /// <summary>Character marker so clicks on a body resolve to the customer/staff it belongs to.</summary>
    public sealed class AgentTag : MonoBehaviour { public Customer Customer; public StaffAgent Staff; }
    public sealed class ItemTag : MonoBehaviour { public int Uid; }
    public sealed class IncidentTag : MonoBehaviour { public int Id; }

    /// <summary>Renders the Game state: room, furniture, people, mess. Reads the sim; never changes it.</summary>
    public sealed class WorldView : MonoBehaviour
    {
        Sim.Game _g;
        Transform _roomRoot, _itemRoot, _agentRoot, _fxRoot;
        readonly List<Renderer> _floorTiles = new List<Renderer>();
        readonly List<(Transform t, int side)> _walls = new List<(Transform, int)>();
        readonly Dictionary<int, GameObject> _items = new Dictionary<int, GameObject>();
        readonly Dictionary<int, string> _itemKey = new Dictionary<int, string>();
        readonly Dictionary<Customer, CharView> _customers = new Dictionary<Customer, CharView>();
        readonly Dictionary<StaffAgent, CharView> _staff = new Dictionary<StaffAgent, CharView>();
        readonly Dictionary<int, GameObject> _incidents = new Dictionary<int, GameObject>();
        readonly List<CharView> _pool = new List<CharView>();
        readonly List<Light> _lights = new List<Light>();
        Light _sun;
        string _floorKey, _wallKey;
        GameObject _ghost, _selectFrame;
        string _ghostId;
        bool _ghostOk;
        public CameraRig Rig;
        Service _lastSvc;
        readonly List<CharView> _idleStaff = new List<CharView>();
        CharView _performer;
        Light _spot;
        Vector3 _stagePos;
        bool _eventLive;

        public static WorldView Create(Sim.Game g, CameraRig rig)
        {
            var go = new GameObject("World");
            var w = go.AddComponent<WorldView>();
            w.Rig = rig;
            w._roomRoot = new GameObject("Room").transform; w._roomRoot.SetParent(go.transform);
            w._itemRoot = new GameObject("Items").transform; w._itemRoot.SetParent(go.transform);
            w._agentRoot = new GameObject("People").transform; w._agentRoot.SetParent(go.transform);
            w._fxRoot = new GameObject("Fx").transform; w._fxRoot.SetParent(go.transform);
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(go.transform);
            w._sun = sunGo.AddComponent<Light>();
            w._sun.type = LightType.Directional;
            w._sun.shadows = LightShadows.Soft;
            w._sun.shadowStrength = 0.55f;
            sunGo.transform.rotation = Quaternion.Euler(55f, -30f, 0);
            w.Bind(g);
            return w;
        }

        public void Bind(Sim.Game g)
        {
            _g = g;
            foreach (Transform c in _roomRoot) Destroy(c.gameObject);
            foreach (var kv in _items) Destroy(kv.Value);
            _items.Clear(); _itemKey.Clear(); _floorTiles.Clear(); _walls.Clear();
            ClearAgents();
            _floorKey = _wallKey = null;
            BuildRoom();
            SyncItems();
        }

        // =====================================================================================
        //  Room
        // =====================================================================================

        void BuildRoom()
        {
            var s = _g.S;
            for (int x = 0; x < s.gridW; x++)
                for (int y = 0; y < s.gridH; y++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(tile.GetComponent<Collider>());
                    tile.transform.SetParent(_roomRoot);
                    tile.transform.localPosition = new Vector3(x, -0.05f, y);
                    tile.transform.localScale = new Vector3(1f, 0.1f, 1f);
                    _floorTiles.Add(tile.GetComponent<Renderer>());
                }
            float w = s.gridW, h = s.gridH, wallH = 3f, t = 0.25f;
            // south wall (y = -0.5) with a door gap, north (y = h - 0.5), west (x = -0.5), east (x = w - 0.5)
            float dx = s.doorX;
            AddWall(new Vector3((dx - 0.5f - 0.5f) / 2f - 0.25f, wallH / 2, -0.5f - t / 2), new Vector3(dx + t, wallH, t), 0);
            AddWall(new Vector3((dx + 0.5f + w - 0.5f) / 2f + 0.125f, wallH / 2, -0.5f - t / 2), new Vector3(w - dx - 1 + t, wallH, t), 0);
            AddWall(new Vector3(dx, wallH - 0.4f, -0.5f - t / 2), new Vector3(1f, 0.8f, t), 0); // lintel over the door
            AddWall(new Vector3((w - 1) / 2f, wallH / 2, h - 0.5f + t / 2), new Vector3(w + t * 2, wallH, t), 2);
            AddWall(new Vector3(-0.5f - t / 2, wallH / 2, (h - 1) / 2f), new Vector3(t, wallH, h), 1);
            AddWall(new Vector3(w - 0.5f + t / 2, wallH / 2, (h - 1) / 2f), new Vector3(t, wallH, h), 3);

            // Street outside the door
            var street = Part(_roomRoot, PrimitiveType.Cube, new Vector3((w - 1) / 2f, -0.06f, -2.2f), new Vector3(w + 6, 0.1f, 3f), ModelFactory.Mat(0x3a3f4b));
            var kerb = Part(_roomRoot, PrimitiveType.Cube, new Vector3((w - 1) / 2f, -0.04f, -3.75f), new Vector3(w + 6, 0.14f, 0.12f), ModelFactory.Mat(0x8a8f99));
            var mat = Part(_roomRoot, PrimitiveType.Cube, new Vector3(dx, 0.005f, -0.25f), new Vector3(0.9f, 0.02f, 0.5f), ModelFactory.Mat(0x6b1e1e));
            // Lamp post with a glow
            Part(_roomRoot, PrimitiveType.Cylinder, new Vector3(dx - 3f, 1.4f, -3.3f), new Vector3(0.12f, 1.4f, 0.12f), ModelFactory.Mat(0x222222));
            Part(_roomRoot, PrimitiveType.Sphere, new Vector3(dx - 3f, 2.9f, -3.3f), new Vector3(0.4f, 0.4f, 0.4f), ModelFactory.Glow(0xffd28a, 2f));
            var lampLight = new GameObject("StreetLamp").AddComponent<Light>();
            lampLight.transform.SetParent(_roomRoot);
            lampLight.transform.localPosition = new Vector3(dx - 3f, 2.6f, -3.3f);
            lampLight.type = LightType.Point; lampLight.range = 6; lampLight.intensity = 1.2f; lampLight.color = new Color(1f, 0.82f, 0.55f);
        }

        void AddWall(Vector3 pos, Vector3 scale, int side)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(wall.GetComponent<Collider>());
            wall.transform.SetParent(_roomRoot);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            _walls.Add((wall.transform, side));
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        void SyncStyle()
        {
            var s = _g.S;
            if (_floorKey != s.floor)
            {
                _floorKey = s.floor;
                var f = Content.Floor(s.floor);
                int i = 0;
                for (int x = 0; x < s.gridW; x++)
                    for (int y = 0; y < s.gridH; y++)
                        _floorTiles[i++].sharedMaterial = ModelFactory.Mat(((x + y) & 1) == 0 ? f.Color : f.Color2);
            }
            if (_wallKey != s.wall)
            {
                _wallKey = s.wall;
                var wl = Content.Wall(s.wall);
                foreach (var (t, side) in _walls) t.GetComponent<Renderer>().sharedMaterial = ModelFactory.Mat(wl.Color);
            }
            // Cut away the walls facing the camera.
            Rig.FrontWalls(out bool south, out bool west, out bool north, out bool east);
            foreach (var (t, side) in _walls)
            {
                bool cut = (side == 0 && south) || (side == 1 && west) || (side == 2 && north) || (side == 3 && east);
                float targetH = cut ? 0.35f : (t.localScale.x < 1.01f && t.localScale.y < 1f ? 0.8f : 3f);
                bool lintel = t.localScale.x <= 1.01f && t.localPosition.y > 2f;
                if (lintel) { t.gameObject.SetActive(!cut); continue; }
                var sc = t.localScale;
                sc.y = Mathf.Lerp(sc.y, targetH, 0.25f);
                t.localScale = sc;
                var p = t.localPosition; p.y = sc.y / 2f; t.localPosition = p;
            }
            // Lighting: dim bars are warm and moody, bright bars are flat.
            float L = s.lighting / 100f;
            bool service = _g.Phase == Phase.Service;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.42f, 0.32f, 0.26f), new Color(0.66f, 0.63f, 0.6f), L) * (service ? 0.95f : 1f);
            _sun.intensity = Mathf.Lerp(0.6f, 1.1f, L);
            _sun.color = Color.Lerp(new Color(1f, 0.75f, 0.5f), new Color(1f, 0.95f, 0.88f), L);
        }

        // =====================================================================================
        //  Items
        // =====================================================================================

        public static Vector3 ItemCentre(PlacedItem it)
        {
            var d = Content.Item(it.id);
            Layout.Size(d, it.rot, out int w, out int h);
            return new Vector3(it.x + (w - 1) / 2f, 0, it.y + (h - 1) / 2f);
        }

        void SyncItems()
        {
            var seen = new HashSet<int>();
            foreach (var it in _g.S.items)
            {
                seen.Add(it.uid);
                string key = $"{it.id}:{it.x}:{it.y}:{it.rot}";
                if (_items.TryGetValue(it.uid, out var go))
                {
                    if (_itemKey[it.uid] == key) continue;
                    go.transform.position = ItemCentre(it);
                    go.transform.rotation = Quaternion.Euler(0, 90f * it.rot, 0);
                    _itemKey[it.uid] = key;
                    continue;
                }
                var d = Content.Item(it.id);
                go = ModelFactory.Build(d);
                go.transform.SetParent(_itemRoot);
                go.transform.position = ItemCentre(it);
                go.transform.rotation = Quaternion.Euler(0, 90f * it.rot, 0);
                go.AddComponent<ItemTag>().Uid = it.uid;
                if (d.Glow && _lights.Count < 12)
                {
                    var l = new GameObject("Glow").AddComponent<Light>();
                    l.transform.SetParent(go.transform, false);
                    l.transform.localPosition = new Vector3(0, d.Fn == ItemFn.Screen ? 1.2f : 1.6f, 0);
                    l.type = LightType.Point;
                    l.range = 3.2f;
                    l.intensity = 1.1f;
                    l.color = ToColor(d.Color2);
                    l.shadows = LightShadows.None;
                    _lights.Add(l);
                }
                _items[it.uid] = go;
                _itemKey[it.uid] = key;
            }
            var remove = new List<int>();
            foreach (var kv in _items) if (!seen.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var uid in remove)
            {
                foreach (var l in _items[uid].GetComponentsInChildren<Light>()) _lights.Remove(l);
                Destroy(_items[uid]);
                _items.Remove(uid); _itemKey.Remove(uid);
            }
        }

        public GameObject ItemObject(int uid) => _items.TryGetValue(uid, out var go) ? go : null;

        public static Color ToColor(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        // =====================================================================================
        //  Build ghost & selection
        // =====================================================================================

        public void ShowGhost(string itemId, int x, int y, int rot, bool ok)
        {
            if (_ghost == null || _ghostId != itemId || _ghostOk != ok)
            {
                if (_ghost != null) Destroy(_ghost);
                var d = Content.Item(itemId);
                _ghost = ModelFactory.Build(d);
                foreach (var c in _ghost.GetComponentsInChildren<Collider>()) Destroy(c);
                var m = ModelFactory.Glow(ok ? 0x3ddc84 : 0xff4d4d, 0.5f);
                foreach (var r in _ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
                _ghostId = itemId; _ghostOk = ok;
            }
            var temp = new PlacedItem { id = itemId, x = x, y = y, rot = rot };
            _ghost.transform.position = ItemCentre(temp) + Vector3.up * 0.02f;
            _ghost.transform.rotation = Quaternion.Euler(0, 90f * rot, 0);
            _ghost.SetActive(true);
        }

        public void HideGhost() { if (_ghost != null) _ghost.SetActive(false); }

        public void Select(PlacedItem it)
        {
            if (_selectFrame == null)
            {
                _selectFrame = new GameObject("Selection");
                var m = ModelFactory.Glow(0xffd166, 1.4f);
                for (int i = 0; i < 4; i++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(bar.GetComponent<Collider>());
                    bar.transform.SetParent(_selectFrame.transform);
                    bar.GetComponent<Renderer>().sharedMaterial = m;
                }
            }
            if (it == null) { _selectFrame.SetActive(false); return; }
            var d = Content.Item(it.id);
            Layout.Size(d, it.rot, out int w, out int h);
            var c = ItemCentre(it);
            _selectFrame.transform.position = c + Vector3.up * 0.03f;
            var bars = _selectFrame.transform;
            float t = 0.07f;
            bars.GetChild(0).localPosition = new Vector3(0, 0, -h / 2f); bars.GetChild(0).localScale = new Vector3(w + t, 0.04f, t);
            bars.GetChild(1).localPosition = new Vector3(0, 0, h / 2f); bars.GetChild(1).localScale = new Vector3(w + t, 0.04f, t);
            bars.GetChild(2).localPosition = new Vector3(-w / 2f, 0, 0); bars.GetChild(2).localScale = new Vector3(t, 0.04f, h + t);
            bars.GetChild(3).localPosition = new Vector3(w / 2f, 0, 0); bars.GetChild(3).localScale = new Vector3(t, 0.04f, h + t);
            _selectFrame.SetActive(true);
        }

        // =====================================================================================
        //  People
        // =====================================================================================

        void ClearAgents()
        {
            foreach (var kv in _customers) Recycle(kv.Value);
            foreach (var kv in _staff) Recycle(kv.Value);
            _customers.Clear(); _staff.Clear();
            foreach (var kv in _incidents) Destroy(kv.Value);
            _incidents.Clear();
        }

        CharView Rent()
        {
            CharView v;
            if (_pool.Count > 0) { v = _pool[_pool.Count - 1]; _pool.RemoveAt(_pool.Count - 1); v.Root.SetActive(true); }
            else v = CharView.Create(_agentRoot);
            return v;
        }

        void Recycle(CharView v) { v.Root.SetActive(false); v.Tag.Customer = null; v.Tag.Staff = null; _pool.Add(v); }

        void LateUpdate()
        {
            if (_g == null) return;
            SyncStyle();
            SyncItems();
            var svc = _g.Phase == Phase.Service ? _g.Svc : null;
            if (svc != _lastSvc) { ClearAgents(); _lastSvc = svc; }
            if (svc != null) SyncService(svc);
            else SyncIdleStaff();
        }

        void SyncIdleStaff()
        {
            // In Prep, show the team standing around the bar so the place doesn't look abandoned.
            int i = 0;
            var bars = _g.S.items.FindAll(it => Content.Item(it.id).Fn == ItemFn.BarCounter);
            while (_idleStaff.Count < _g.S.staff.Count) _idleStaff.Add(Rent());
            while (_idleStaff.Count > _g.S.staff.Count) { Recycle(_idleStaff[_idleStaff.Count - 1]); _idleStaff.RemoveAt(_idleStaff.Count - 1); }
            foreach (var m in _g.S.staff)
            {
                var v = _idleStaff[i];
                v.Dress(m.shirt, m.skin, m.hair, true, m.role == 0);
                Vector3 pos;
                if (m.role == 0 && bars.Count > 0)
                {
                    var bp = Layout.ServicePoints(bars[0]);
                    var f = Layout.Facing(bars[0].rot);
                    var c = bp[Mathf.Min(i, bp.Count - 1)];
                    pos = new Vector3(c.x - f.x * 2f, 0, c.y - f.y * 2f);
                    v.Place(pos, new Vector3(f.x, 0, f.y), false, 0, false);
                }
                else
                {
                    pos = new Vector3(_g.S.doorX + 1 + i * 0.6f, 0, 1.5f);
                    v.Place(pos, Vector3.back, false, 0, false);
                }
                v.SetMood(-1, false, false);
                i++;
            }
        }

        void SyncService(Service svc)
        {
            foreach (var v in _idleStaff) Recycle(v);
            _idleStaff.Clear();
            // Customers
            var alive = new HashSet<Customer>(svc.Customers);
            var drop = new List<Customer>();
            foreach (var kv in _customers) if (!alive.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var c in drop) { Recycle(_customers[c]); _customers.Remove(c); }
            foreach (var c in svc.Customers)
            {
                if (!_customers.TryGetValue(c, out var v))
                {
                    v = Rent();
                    v.Dress(c.shirt, c.skin, c.hair, c.tall, false);
                    v.Tag.Customer = c;
                    v.Snap = true;
                    _customers[c] = v;
                }
                PlaceCustomer(svc, c, v);
            }
            // Staff
            foreach (var a in svc.Staff)
            {
                if (!_staff.TryGetValue(a, out var v))
                {
                    v = Rent();
                    v.Dress(a.m.shirt, a.IsOwner ? 0xf1c27d : a.m.skin, a.IsOwner ? 0x4a3020 : a.m.hair, true, a.m.role == 0);
                    v.Tag.Staff = a;
                    v.Snap = true;
                    _staff[a] = v;
                }
                bool show = a.present && (!a.IsOwner || svc.OwnerBehindBar);
                v.Root.SetActive(show);
                if (!show) continue;
                var pos = new Vector3(a.x, 0, a.y);
                Vector3 face = Vector3.back;
                if (a.m.role == 0 || a.IsOwner)
                {
                    if (a.barPoint >= 0 && a.barPoint < svc.BarPoints.Count)
                    {
                        var bp = svc.BarPoints[a.barPoint];
                        face = new Vector3(bp.cell.x - bp.sx, 0, bp.cell.y - bp.sy).normalized;
                    }
                }
                v.Place(pos, face, a.serving != null || a.task != null, 0, false);
                v.SetMood(-1, false, a.serving != null);
            }
            SyncPerformer(svc);
            // Incidents
            var live = new HashSet<int>();
            foreach (var inc in svc.Incidents)
            {
                if (inc.resolved || inc.kind == IncidentKind.Rowdy) continue;
                if (inc.kind == IncidentKind.Glasses && inc.count <= 0) continue;
                live.Add(inc.id);
                if (!_incidents.TryGetValue(inc.id, out var go))
                {
                    go = BuildIncident(inc);
                    _incidents[inc.id] = go;
                }
                if (inc.kind == IncidentKind.Glasses)
                {
                    int n = Mathf.Min(6, inc.count);
                    for (int i = 0; i < go.transform.childCount; i++) go.transform.GetChild(i).gameObject.SetActive(i < n);
                }
                if (inc.kind != IncidentKind.Spill)
                {
                    float bob = Mathf.Sin(Time.time * 4f + inc.id) * 0.08f;
                    if (inc.kind == IncidentKind.Toilet) go.transform.position = new Vector3(go.transform.position.x, 2.4f + bob, go.transform.position.z);
                }
            }
            var gone = new List<int>();
            foreach (var kv in _incidents) if (!live.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { Destroy(_incidents[id]); _incidents.Remove(id); }
        }

        void SyncPerformer(Service svc)
        {
            _eventLive = svc.Event != null && (svc.EventPhase == 1 || svc.EventPhase == 3);
            if (_performer == null)
            {
                _performer = CharView.Create(_agentRoot);
                _performer.Dress(0x9b5de5, 0xc68642, 0x111111, true, false);
                _spot = new GameObject("Spotlight").AddComponent<Light>();
                _spot.type = LightType.Spot; _spot.range = 7; _spot.spotAngle = 45; _spot.intensity = 3f;
                _spot.color = new Color(1f, 0.93f, 0.8f);
                _spot.transform.SetParent(_fxRoot);
            }
            _performer.Root.SetActive(_eventLive);
            _spot.gameObject.SetActive(_eventLive);
            if (!_eventLive) return;
            var stage = _g.S.items.Find(i => Content.Item(i.id).Fn == ItemFn.Stage);
            Vector3 face; float lift = 0;
            if (stage != null)
            {
                var f = Layout.Facing(stage.rot);
                _stagePos = ItemCentre(stage) - new Vector3(f.x, 0, f.y) * 0.1f;
                face = new Vector3(f.x, 0, f.y);
                lift = 0.35f;
            }
            else if (svc.BarPoints.Count > 0)
            {
                var bp = svc.BarPoints[svc.BarPoints.Count / 2];
                var away = new Vector3(bp.cell.x - bp.sx, 0, bp.cell.y - bp.sy).normalized;
                _stagePos = new Vector3(bp.cell.x, 0, bp.cell.y) + away * 1.2f;
                face = away;
            }
            else { _stagePos = new Vector3(_g.S.gridW / 2f, 0, _g.S.gridH / 2f); face = Vector3.back; }
            _performer.Place(_stagePos, face, true, 0, false);
            _performer.Root.transform.position += Vector3.up * lift;
            _performer.SetMood(-1, false, false);
            _spot.transform.position = _stagePos + Vector3.up * 3.6f - face * 1.2f;
            _spot.transform.LookAt(_stagePos + Vector3.up * 0.8f);
        }

        GameObject BuildIncident(Incident inc)
        {
            var root = new GameObject("Incident");
            root.transform.SetParent(_fxRoot);
            root.AddComponent<IncidentTag>().Id = inc.id;
            switch (inc.kind)
            {
                case IncidentKind.Spill:
                {
                    root.transform.position = new Vector3(inc.cell.x + 0.1f, 0.01f, inc.cell.y - 0.1f);
                    Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.75f, 0.01f, 0.55f), ModelFactory.Mat(0x8a5a1c));
                    Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.22f, 0.005f, 0.12f), new Vector3(0.3f, 0.012f, 0.25f), ModelFactory.Mat(0x8a5a1c));
                    var col = root.AddComponent<BoxCollider>(); col.size = new Vector3(1f, 0.3f, 1f); col.center = new Vector3(0, 0.15f, 0);
                    break;
                }
                case IncidentKind.Glasses:
                {
                    Vector3 pos;
                    float hgt = 0.02f;
                    var it = inc.itemUid != 0 ? _g.S.Item(inc.itemUid) : null;
                    if (it != null) { pos = ItemCentre(it); hgt = it.id == "high_table" ? 1.05f : 0.78f; if (it.id is "sofa" or "chesterfield" or "armchair" or "beanbags" or "bar_stool") hgt = 0.02f; }
                    else pos = new Vector3(inc.cell.x, 0, inc.cell.y);
                    root.transform.position = pos + Vector3.up * hgt;
                    var m = ModelFactory.Mat(0xcfe8ef);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 2.1f;
                        Part(root.transform, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.18f, 0.08f, Mathf.Sin(a) * 0.18f), new Vector3(0.1f, 0.08f, 0.1f), m);
                    }
                    break;
                }
                case IncidentKind.Toilet:
                {
                    var it = _g.S.Item(inc.itemUid);
                    root.transform.position = (it != null ? ItemCentre(it) : new Vector3(inc.cell.x, 0, inc.cell.y)) + Vector3.up * 2.4f;
                    Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(0.18f, 0.5f, 0.18f), ModelFactory.Glow(0xff4d4d, 1.6f));
                    Part(root.transform, PrimitiveType.Cube, new Vector3(0, -0.28f, 0), new Vector3(0.18f, 0.16f, 0.18f), ModelFactory.Glow(0xff4d4d, 1.6f));
                    var col = root.AddComponent<BoxCollider>(); col.size = new Vector3(1.2f, 1.6f, 1.2f);
                    break;
                }
            }
            return root;
        }

        void PlaceCustomer(Service svc, Customer c, CharView v)
        {
            bool hide = c.state == CState.InToilet;
            v.Root.SetActive(!hide);
            if (hide) return;
            Vector3 pos = new Vector3(c.x, 0, c.y);
            Vector3 face = Vector3.zero;
            bool seated = false;
            float seatH = 0;
            switch (c.state)
            {
                case CState.Seated:
                    if (c.seatSlot >= 0)
                    {
                        var it = _g.S.Item(c.seatItem);
                        if (it != null)
                        {
                            Service.SeatPos(it, c.seatSlot, out float sx, out float sy, out float fx, out float fy);
                            pos = new Vector3(sx, 0, sy);
                            face = new Vector3(fx, 0, fy);
                            seated = true;
                            seatH = ModelFactory.SeatHeight(Content.Item(it.id));
                        }
                    }
                    break;
                case CState.Queue:
                case CState.Ordering:
                    if (c.barPoint >= 0 && c.barPoint < svc.BarPoints.Count)
                    {
                        var bp = svc.BarPoints[c.barPoint];
                        int idx = bp.queue.IndexOf(c);
                        var f = new Vector3(bp.cell.x - bp.sx, 0, bp.cell.y - bp.sy).normalized; // away from the bar
                        pos = new Vector3(bp.cell.x, 0, bp.cell.y) + f * (Mathf.Max(0, idx) * 0.55f - 0.15f);
                        face = -f;
                    }
                    break;
                case CState.Playing:
                    if (c.gameItem != 0)
                    {
                        var it = _g.S.Item(c.gameItem);
                        if (it != null) { var ctr = ItemCentre(it); face = (ctr - pos).normalized; }
                    }
                    break;
                case CState.ToiletQueue:
                    if (c.toiletItem != 0)
                    {
                        var it = _g.S.Item(c.toiletItem);
                        if (it != null) { var ctr = ItemCentre(it); face = (ctr - pos).normalized; }
                    }
                    break;
            }
            if (_eventLive && c.watching && (c.state == CState.Seated || c.state == CState.Standing))
                face = (_stagePos - pos).normalized;
            bool moving = c.state is CState.ToBar or CState.ToSeat or CState.ToGame or CState.ToToilet or CState.Leaving or CState.Standing;
            v.Place(pos, face, moving, seated ? seatH : 0, seated);
            v.SetDrink(c.drink != null && c.state != CState.Ordering);
            v.SetMood(c.mood, c.rowdy, false);
        }

        public bool ShowMoods = true;

        public sealed class CharView
        {
            public GameObject Root;
            public AgentTag Tag;
            Transform _body, _head, _hair, _apron, _drink, _marker, _alert;
            Renderer _shirtR, _headR, _hairR, _markerR;
            Vector3 _pos, _lastTarget;
            float _bobT;
            public bool Snap;

            public static CharView Create(Transform parent)
            {
                var v = new CharView();
                v.Root = new GameObject("Person");
                v.Root.transform.SetParent(parent);
                v.Tag = v.Root.AddComponent<AgentTag>();
                var col = v.Root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.8f, 0); col.height = 1.7f; col.radius = 0.3f;
                v._body = Part(v.Root.transform, PrimitiveType.Capsule, new Vector3(0, 0.55f, 0), new Vector3(0.42f, 0.5f, 0.32f), ModelFactory.Mat(0x888888)).transform;
                v._shirtR = v._body.GetComponent<Renderer>();
                v._head = Part(v.Root.transform, PrimitiveType.Sphere, new Vector3(0, 1.22f, 0), new Vector3(0.34f, 0.36f, 0.34f), ModelFactory.Mat(0xf1c27d)).transform;
                v._headR = v._head.GetComponent<Renderer>();
                v._hair = Part(v.Root.transform, PrimitiveType.Sphere, new Vector3(0, 1.32f, 0.03f), new Vector3(0.36f, 0.24f, 0.36f), ModelFactory.Mat(0x2b1b10)).transform;
                v._hairR = v._hair.GetComponent<Renderer>();
                // Eyes, so you can tell which way people face
                Part(v._head, PrimitiveType.Sphere, new Vector3(-0.18f, 0.05f, -0.42f), new Vector3(0.12f, 0.14f, 0.1f), ModelFactory.Mat(0x111111));
                Part(v._head, PrimitiveType.Sphere, new Vector3(0.18f, 0.05f, -0.42f), new Vector3(0.12f, 0.14f, 0.1f), ModelFactory.Mat(0x111111));
                v._apron = Part(v.Root.transform, PrimitiveType.Cube, new Vector3(0, 0.5f, -0.15f), new Vector3(0.36f, 0.5f, 0.05f), ModelFactory.Mat(0x1a1a1a)).transform;
                v._drink = Part(v.Root.transform, PrimitiveType.Cylinder, new Vector3(0.24f, 0.78f, -0.12f), new Vector3(0.09f, 0.1f, 0.09f), ModelFactory.Mat(0xe0a030)).transform;
                v._marker = Part(v.Root.transform, PrimitiveType.Cube, new Vector3(0, 1.75f, 0), new Vector3(0.14f, 0.14f, 0.14f), ModelFactory.Glow(0x3ddc84, 1.2f)).transform;
                v._marker.localRotation = Quaternion.Euler(45, 0, 45);
                v._markerR = v._marker.GetComponent<Renderer>();
                v._alert = new GameObject("Alert").transform;
                v._alert.SetParent(v.Root.transform, false);
                v._alert.localPosition = new Vector3(0, 2.0f, 0);
                Part(v._alert, PrimitiveType.Cube, new Vector3(0, 0.12f, 0), new Vector3(0.14f, 0.38f, 0.14f), ModelFactory.Glow(0xff3b3b, 2f));
                Part(v._alert, PrimitiveType.Cube, new Vector3(0, -0.2f, 0), new Vector3(0.14f, 0.12f, 0.14f), ModelFactory.Glow(0xff3b3b, 2f));
                return v;
            }

            public void Dress(uint shirt, uint skin, uint hair, bool tall, bool apron)
            {
                _shirtR.sharedMaterial = ModelFactory.Mat(shirt);
                _headR.sharedMaterial = ModelFactory.Mat(skin);
                _hairR.sharedMaterial = ModelFactory.Mat(hair);
                Root.transform.localScale = Vector3.one * (tall ? 1.0f : 0.92f);
                _apron.gameObject.SetActive(apron);
                _drink.gameObject.SetActive(false);
            }

            public void SetDrink(bool on) => _drink.gameObject.SetActive(on);

            public void SetMood(float mood, bool rowdy, bool busy)
            {
                _alert.gameObject.SetActive(rowdy);
                bool showMarker = mood >= 0 && !rowdy && (mood < 40 || mood > 75);
                _marker.gameObject.SetActive(showMarker);
                if (showMarker) _markerR.sharedMaterial = ModelFactory.Glow(mood < 25 ? 0xff4d4d : mood < 40 ? 0xffb347 : 0x3ddc84, 1.3f);
                if (rowdy) _alert.localRotation = Quaternion.Euler(0, Time.time * 180f, 0);
            }

            public void Place(Vector3 target, Vector3 face, bool moving, float seatH, bool seated)
            {
                if (Snap) { _pos = target; Snap = false; }
                float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
                _pos = Vector3.Lerp(_pos, target, k);
                var delta = target - _lastTarget;
                _lastTarget = target;
                Vector3 dir = face;
                if (dir.sqrMagnitude < 0.01f && delta.sqrMagnitude > 0.0001f) dir = delta.normalized;
                if (dir.sqrMagnitude > 0.01f)
                {
                    // models face -z
                    var q = Quaternion.LookRotation(-dir, Vector3.up);
                    Root.transform.rotation = Quaternion.Slerp(Root.transform.rotation, q, 1f - Mathf.Exp(-Time.deltaTime * 10f));
                }
                bool walking = moving && delta.sqrMagnitude > 0.00001f;
                if (walking) _bobT += Time.deltaTime * 12f;
                float bob = walking ? Mathf.Abs(Mathf.Sin(_bobT)) * 0.06f : 0f;
                float y = seated ? Mathf.Max(0, seatH - 0.42f) : 0f;
                Root.transform.position = new Vector3(_pos.x, y + bob, _pos.z);
                _body.localScale = seated ? new Vector3(0.42f, 0.42f, 0.36f) : new Vector3(0.42f, 0.5f, 0.32f);
            }
        }
    }
}
