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
        readonly List<(Transform t, int side, Transform cap)> _walls = new List<(Transform, int, Transform)>();
        readonly Dictionary<int, GameObject> _items = new Dictionary<int, GameObject>();
        readonly Dictionary<int, string> _itemKey = new Dictionary<int, string>();
        readonly Dictionary<Customer, CharView> _customers = new Dictionary<Customer, CharView>();
        readonly Dictionary<StaffAgent, CharView> _staff = new Dictionary<StaffAgent, CharView>();
        readonly Dictionary<int, GameObject> _incidents = new Dictionary<int, GameObject>();
        readonly List<CharView> _pool = new List<CharView>();
        readonly List<Light> _lights = new List<Light>();
        Light _sun;
        string _floorKey, _wallKey;
        public static Vector3 SignPos(GameState s) => new Vector3(s.doorX + 0.85f, 2.35f, -0.82f);
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
            _lights.Clear(); _wearBand.Clear();
            ClearAgents();
            _floorKey = _wallKey = null;
            BuildRoom();
            SyncItems();
        }

        // =====================================================================================
        //  Room
        // =====================================================================================

        // Visual brief palette (section 7.2): the frame never changes with the bar's theme.
        const uint FoamBoard = 0xFBE3CF, PlinthWood = 0x4A3326, Pavement = 0xCFC7BB, Kerb = 0xB3AA9C, WhiteCard = 0xF4F1EC, CardShade = 0xDAD4CA;
        static readonly Color StudioDayTop = new Color32(0xD9, 0xE3, 0xEA, 255), StudioDayFloor = new Color32(0x9F, 0xB2, 0xC0, 255);
        static readonly Color StudioNightTop = new Color32(0x2A, 0x34, 0x40, 255), StudioNightFloor = new Color32(0x14, 0x1A, 0x21, 255);

        Renderer _studioFloor;
        static Material _studioMat;
        Light _fill;
        float _night, _nightStart = -99f;
        bool _wasService;
        readonly List<Light> _practical = new List<Light>();
        bool _practicalDirty = true;
        public static Vector3 PlaquePos(GameState s) => new Vector3((s.gridW - 1) / 2f, -0.36f, -3.17f);

        // Beer garden (task O1): a lawn on a plinth extension behind the back wall, picnic benches and festoon lights.
        void BuildGarden(Transform st, float w, float maxZ)
        {
            float z0 = maxZ, depth = 3.2f, cx = (w - 1) / 2f;
            Part(st, PrimitiveType.Cube, new Vector3(cx, -0.38f, z0 + depth / 2f), new Vector3(w + 2.5f, 0.7f, depth), ModelFactory.Mat(PlinthWood));
            Part(st, PrimitiveType.Cube, new Vector3(cx, -0.02f, z0 + depth / 2f), new Vector3(w + 2.48f, 0.04f, depth - 0.02f), ModelFactory.Mat(0x7fb069));
            // Low fence along the back
            Part(st, PrimitiveType.Cube, new Vector3(cx, 0.35f, z0 + depth - 0.1f), new Vector3(w + 2.4f, 0.7f, 0.06f), ModelFactory.Mat(0x8a6a42));
            var wood = ModelFactory.Mat(0xc9a26b);
            for (int i = 0; i < 3; i++)
            {
                float x = cx - w * 0.3f + i * w * 0.3f, z = z0 + 1.5f;
                Part(st, PrimitiveType.Cube, new Vector3(x, 0.72f, z), new Vector3(1.8f, 0.06f, 0.7f), wood);
                Part(st, PrimitiveType.Cube, new Vector3(x, 0.42f, z - 0.55f), new Vector3(1.8f, 0.05f, 0.25f), wood);
                Part(st, PrimitiveType.Cube, new Vector3(x, 0.42f, z + 0.55f), new Vector3(1.8f, 0.05f, 0.25f), wood);
                Part(st, PrimitiveType.Cube, new Vector3(x - 0.7f, 0.36f, z), new Vector3(0.08f, 0.72f, 1.3f), wood);
                Part(st, PrimitiveType.Cube, new Vector3(x + 0.7f, 0.36f, z), new Vector3(0.08f, 0.72f, 1.3f), wood);
                // Parasol
                Part(st, PrimitiveType.Cylinder, new Vector3(x, 1.3f, z), new Vector3(0.05f, 0.6f, 0.05f), ModelFactory.Mat(0xeeeeee));
                Part(st, PrimitiveType.Cylinder, new Vector3(x, 1.95f, z), new Vector3(1.6f, 0.04f, 1.6f), ModelFactory.Mat(i % 2 == 0 ? 0x2a9d8fu : 0xe76f51u));
            }
            // Festoon lights strung across the garden
            var glow = ModelFactory.Glow(0xFFD27A, 2f);
            for (int i = 0; i <= 10; i++)
            {
                float x = cx - w / 2f - 0.8f + i * (w + 1.6f) / 10f;
                float sag = Mathf.Sin(i / 10f * Mathf.PI) * 0.35f;
                Part(st, PrimitiveType.Sphere, new Vector3(x, 2.5f - sag, z0 + 2.6f), new Vector3(0.12f, 0.12f, 0.12f), glow);
            }
        }

        void BuildRoom()
        {
            var s = _g.S;
            // Everything that never moves goes under one root and is statically batched at the end.
            var st = new GameObject("Static").transform;
            st.SetParent(_roomRoot);
            _staticRoot = st;
            for (int x = 0; x < s.gridW; x++)
                for (int y = 0; y < s.gridH; y++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(tile.GetComponent<Collider>());
                    tile.transform.SetParent(st);
                    tile.transform.localPosition = new Vector3(x, -0.05f, y);
                    tile.transform.localScale = new Vector3(1f, 0.1f, 1f);
                    _floorTiles.Add(tile.GetComponent<Renderer>());
                }
            float w = s.gridW, h = s.gridH, wallH = 3f, t = 0.25f;
            float dx = s.doorX;
            AddWall(new Vector3((dx - 0.5f - 0.5f) / 2f - 0.25f, wallH / 2, -0.5f - t / 2), new Vector3(dx + t, wallH, t), 0);
            AddWall(new Vector3((dx + 0.5f + w - 0.5f) / 2f + 0.125f, wallH / 2, -0.5f - t / 2), new Vector3(w - dx - 1 + t, wallH, t), 0);
            AddWall(new Vector3(dx, wallH - 0.4f, -0.5f - t / 2), new Vector3(1f, 0.8f, t), 0); // lintel over the door
            AddWall(new Vector3((w - 1) / 2f, wallH / 2, h - 0.5f + t / 2), new Vector3(w + t * 2, wallH, t), 2);
            AddWall(new Vector3(-0.5f - t / 2, wallH / 2, (h - 1) / 2f), new Vector3(t, wallH, h), 1);
            AddWall(new Vector3(w - 0.5f + t / 2, wallH / 2, (h - 1) / 2f), new Vector3(t, wallH, h), 3);

            // ---- The plinth (brief 9.1): the model's base, one unit beyond the footprint, with pavement in front.
            float minX = -1.75f, maxX = w + 0.75f, minZ = -3.15f, maxZ = h + 0.75f;
            var pc = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
            var pz = new Vector3(maxX - minX, 0, maxZ - minZ);
            _plinth = Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.38f, pc.z), new Vector3(pz.x, 0.7f, pz.z), ModelFactory.Mat(PlinthWood)).GetComponent<Renderer>();
            _rainBox = new Bounds(new Vector3(pc.x, 2.5f, pc.z), new Vector3(pz.x, 5f, pz.z));
            Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.025f, pc.z), new Vector3(pz.x - 0.02f, 0.03f, pz.z - 0.02f), ModelFactory.Mat(Pavement));
            Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.0f, minZ + 0.18f), new Vector3(pz.x - 0.02f, 0.05f, 0.14f), ModelFactory.Mat(Kerb));
            // Foam-board trim where the model's ground meets the plinth
            Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.035f, minZ - 0.005f), new Vector3(pz.x + 0.02f, 0.05f, 0.03f), ModelFactory.Mat(FoamBoard));
            // Name plaque on the front (cream card on a darker mount)
            _plaqueMount = Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.36f, minZ - 0.01f), new Vector3(3.6f, 0.46f, 0.03f), ModelFactory.Mat(0x2b1d14)).GetComponent<Renderer>();
            _plaqueCard = Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.36f, minZ - 0.025f), new Vector3(3.4f, 0.36f, 0.02f), ModelFactory.Mat(0xFFF1C9)).GetComponent<Renderer>();
            _plinthTier = -1;

            // ---- Studio (brief 9.3): seamless floor that catches the plinth's soft shadow.
            var studio = Part(st, PrimitiveType.Cube, new Vector3(pc.x, -0.78f, pc.z), new Vector3(400f, 0.05f, 400f), ModelFactory.Mat(0x9FB2C0));
            _studioFloor = studio.GetComponent<Renderer>();
            if (_studioMat == null) _studioMat = new Material(ModelFactory.Mat(0x9FB2C0));
            _studioFloor.sharedMaterial = _studioMat;

            // ---- Neighbouring buildings as white-card massing models (brief 9.2)
            if (s.gridW + Sim.Game.ExpandCols <= Sim.Game.MaxWidth)
                Massing(new Vector3(maxX + 0.2f + Sim.Game.ExpandCols / 2f, 0, (h - 1) / 2f + 0.5f), new Vector3(Sim.Game.ExpandCols - 0.4f, 2.9f, h + 0.6f), dx);
            else
                Massing(new Vector3(maxX + 4.6f, 0, (h - 1) / 2f + 1.2f), new Vector3(5f, 2.8f, h), dx);
            Massing(new Vector3((w - 1) / 2f - 2f, 0, maxZ + 4.2f + (s.garden ? 3.4f : 0f)), new Vector3(9f, 2.4f, 4.5f), dx);
            if (s.garden) BuildGarden(st, w, maxZ);

            // ---- Pavement furniture
            Part(st, PrimitiveType.Cube, new Vector3(dx, 0.005f, -0.25f), new Vector3(0.9f, 0.02f, 0.5f), ModelFactory.Mat(0xA8573A));
            Part(st, PrimitiveType.Cylinder, new Vector3(dx - 3f, 1.4f, -2.6f), new Vector3(0.12f, 1.4f, 0.12f), ModelFactory.Mat(0x2b2b2b));
            Part(st, PrimitiveType.Sphere, new Vector3(dx - 3f, 2.9f, -2.6f), new Vector3(0.4f, 0.4f, 0.4f), ModelFactory.Glow(0xFFD27A, 2f));
            // Hanging pub sign: bottle green with cream lettering (brief 7.1)
            Part(st, PrimitiveType.Cube, new Vector3(dx + 1.4f, 1.5f, -0.75f), new Vector3(0.1f, 3f, 0.1f), ModelFactory.Mat(0x2b1b10));
            Part(st, PrimitiveType.Cube, new Vector3(dx + 1.0f, 2.95f, -0.75f), new Vector3(0.9f, 0.08f, 0.08f), ModelFactory.Mat(0x2b1b10));
            Part(st, PrimitiveType.Cube, new Vector3(dx + 0.85f, 2.35f, -0.75f), new Vector3(1.5f, 0.85f, 0.06f), ModelFactory.Mat(0x3D8A7A));
            Part(st, PrimitiveType.Cube, new Vector3(dx + 0.85f, 2.35f, -0.79f), new Vector3(1.38f, 0.73f, 0.02f), ModelFactory.Mat(0xFFF1C9));
            Part(st, PrimitiveType.Cube, new Vector3(dx + 0.85f, 2.35f, -0.8f), new Vector3(1.32f, 0.67f, 0.02f), ModelFactory.Mat(0x3D8A7A));
            // Planters either side of the door
            foreach (var px in new[] { dx - 1.4f, dx + 2.6f })
            {
                if (px < minX + 0.4f || px > maxX - 0.4f) continue;
                Part(st, PrimitiveType.Cube, new Vector3(px, 0.2f, -1.15f), new Vector3(0.7f, 0.4f, 0.45f), ModelFactory.Mat(0x8a5a3a));
                Part(st, PrimitiveType.Sphere, new Vector3(px - 0.15f, 0.5f, -1.15f), new Vector3(0.38f, 0.32f, 0.38f), ModelFactory.Mat(0x6CC070));
                Part(st, PrimitiveType.Sphere, new Vector3(px + 0.17f, 0.52f, -1.12f), new Vector3(0.34f, 0.36f, 0.34f), ModelFactory.Mat(0x5aab5e));
            }
            var lampLight = new GameObject("StreetLamp").AddComponent<Light>();
            lampLight.transform.SetParent(_roomRoot);
            lampLight.transform.localPosition = new Vector3(dx - 3f, 2.6f, -2.6f);
            lampLight.type = LightType.Point; lampLight.range = 5; lampLight.intensity = 0f; lampLight.color = new Color(1f, 0.82f, 0.48f);
            _practicalLamp = lampLight;

            // Soft fill from behind the camera keeps characters readable in moody bars (brief 10.3)
            if (_fill == null)
            {
                _fill = new GameObject("Fill").AddComponent<Light>();
                _fill.type = LightType.Directional;
                _fill.shadows = LightShadows.None;
                _fill.color = new Color(1f, 0.93f, 0.85f);
                _fill.transform.SetParent(Rig.Cam.transform, false);
            }
            _practicalDirty = true;
            StaticBatchingUtility.Combine(st.gameObject);
        }

        Light _practicalLamp;
        Transform _staticRoot;
        Renderer _plinth, _plaqueMount, _plaqueCard;
        int _plinthTier = -1;
        Bounds _rainBox;
        readonly List<Transform> _rain = new List<Transform>();
        public bool EventLive => _eventLive;
        public Vector3 StagePos => _stagePos;

        // Plinth progression (brief 9.1): cardboard -> painted MDF -> stained oak -> walnut -> marble.
        static readonly uint[] PlinthColours = { 0xC8A47A, 0x8C9AAB, 0x7A4E2C, 0x4A3326, 0xE9E5DF };
        static readonly uint[] PlaqueCards = { 0xFFF1C9, 0xFFFFFF, 0xF3E3C0, 0xD4A64A, 0xE2B85C };
        static readonly uint[] PlaqueMounts = { 0x9C7B55, 0x5d6b7a, 0x3a2416, 0x2b1d14, 0x6b6b6b };

        void SyncPlinth()
        {
            int tier = Sim.Game.PlinthTier(_g.S.reputation);
            if (tier == _plinthTier || _plinth == null) return;
            _plinthTier = tier;
            _plinth.sharedMaterial = ModelFactory.Mat(PlinthColours[tier]);
            _plaqueMount.sharedMaterial = ModelFactory.Mat(PlaqueMounts[tier]);
            _plaqueCard.sharedMaterial = tier >= 3 ? ModelFactory.Glow(PlaqueCards[tier], 0.25f) : ModelFactory.Mat(PlaqueCards[tier]);
        }

        // Rain as fine clear threads falling onto the model (brief 13).
        void SyncRain()
        {
            bool wet = (_g.S.weather == "Rain" || _g.S.weather == "Storm") && _g.Phase != Phase.Report;
            int want = wet ? (_g.S.weather == "Storm" ? 140 : 80) : 0;
            if (_rain.Count < want)
            {
                var m = ModelFactory.Mat(0xBFD4E6);
                while (_rain.Count < want)
                {
                    var d = Part(_fxRoot, PrimitiveType.Cube, RandomRainPos(true), new Vector3(0.012f, 0.32f, 0.012f), m);
                    d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _rain.Add(d.transform);
                }
            }
            for (int i = 0; i < _rain.Count; i++)
            {
                bool on = i < want;
                var t = _rain[i];
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                if (!on) continue;
                var p = t.position;
                p.y -= Time.deltaTime * 9f;
                if (p.y < 0.05f) p = RandomRainPos(false);
                t.position = p;
            }
        }

        Vector3 RandomRainPos(bool anyHeight)
        {
            // Only outside the building: the model has no roof, but the rain shouldn't fall on the customers.
            var b = _rainBox;
            float w = _g.S.gridW, h = _g.S.gridH;
            for (int tries = 0; tries < 20; tries++)
            {
                float x = Random.Range(b.min.x, b.max.x), z = Random.Range(b.min.z, b.max.z);
                bool inside = x > -0.9f && x < w - 0.1f && z > -0.9f && z < h - 0.1f;
                if (!inside) return new Vector3(x, anyHeight ? Random.Range(0.1f, b.max.y) : b.max.y, z);
            }
            return new Vector3(b.min.x + 0.3f, b.max.y, b.min.z + 0.3f);
        }

        void Massing(Vector3 centre, Vector3 size, float doorX)
        {
            float baseY = -0.755f;
            var root = new GameObject("WhiteCardBuilding").transform;
            root.SetParent(_staticRoot);
            root.localPosition = centre;
            Part(root, PrimitiveType.Cube, new Vector3(0, baseY + size.y / 2f, 0), size, ModelFactory.Mat(WhiteCard));
            // Window cut-outs on the street face and the side facing the bar
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    Part(root, PrimitiveType.Cube, new Vector3(-size.x / 4f + i * size.x / 2f, baseY + 1.2f + j * 1.3f, -size.z / 2f - 0.01f),
                        new Vector3(size.x / 4f, 0.7f, 0.02f), ModelFactory.Mat(CardShade));
            Part(root, PrimitiveType.Cube, new Vector3(0, baseY + 0.55f, -size.z / 2f - 0.01f), new Vector3(0.8f, 1.1f, 0.02f), ModelFactory.Mat(CardShade));
            Part(root, PrimitiveType.Cube, new Vector3(0, baseY + 0.02f, -size.z / 2f - 0.6f), new Vector3(size.x + 0.4f, 0.04f, 1.2f), ModelFactory.Mat(WhiteCard));
        }

        void AddWall(Vector3 pos, Vector3 scale, int side)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(wall.GetComponent<Collider>());
            wall.transform.SetParent(_roomRoot);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            // Foam-board cap on the cut edge (brief 4.2)
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(_roomRoot);
            cap.GetComponent<Renderer>().sharedMaterial = ModelFactory.Mat(FoamBoard);
            _walls.Add((wall.transform, side, cap.transform));
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

        void RebuildPractical()
        {
            _practicalDirty = false;
            foreach (var l in _practical) if (l != null) Destroy(l.gameObject);
            _practical.Clear();
            // Warm pools of light over the bar and the tables: the evening's real light sources (brief 10.2)
            foreach (var it in _g.S.items)
            {
                var d = Content.Item(it.id);
                if (d.Fn == ItemFn.BarCounter) AddPractical(ItemCentre(it) + Vector3.up * 2.1f, 5.5f, 1.25f);
            }
            foreach (var it in _g.S.items)
            {
                if (_practical.Count >= 8) break;
                var d = Content.Item(it.id);
                if (d.Fn == ItemFn.Seat && d.Capacity >= 2) AddPractical(ItemCentre(it) + Vector3.up * 1.7f, 3.4f, 0.9f);
            }
        }

        void AddPractical(Vector3 pos, float range, float weight)
        {
            var l = new GameObject("Practical").AddComponent<Light>();
            l.transform.SetParent(_fxRoot);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.range = range;
            l.color = new Color32(0xFF, 0xD2, 0x7A, 255);
            l.shadows = LightShadows.None;
            l.intensity = 0;
            l.bounceIntensity = weight; // stash the weight; bounce isn't used without GI
            // The bar's own lights are the hero (brief 10): keep them per-pixel on every tier.
            l.renderMode = weight >= 1.2f ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            _practical.Add(l);
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
                foreach (var (t, side, cap) in _walls) t.GetComponent<Renderer>().sharedMaterial = ModelFactory.Mat(wl.Color);
            }
            // Cut away the walls facing the camera, down to about knee height (brief 4.2), with foam caps.
            Rig.FrontWalls(out bool south, out bool west, out bool north, out bool east);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f);
            foreach (var (t, side, cap) in _walls)
            {
                bool cut = (side == 0 && south) || (side == 1 && west) || (side == 2 && north) || (side == 3 && east);
                bool lintel = t.localScale.x <= 1.01f && t.localPosition.y > 2f;
                if (lintel)
                {
                    t.gameObject.SetActive(!cut);
                    cap.gameObject.SetActive(!cut);
                    cap.localPosition = t.localPosition + Vector3.up * (t.localScale.y / 2f + 0.02f);
                    cap.localScale = new Vector3(t.localScale.x + 0.02f, 0.04f, t.localScale.z + 0.02f);
                    continue;
                }
                float targetH = cut ? 0.75f : 3f;
                var sc = t.localScale;
                sc.y = Mathf.Lerp(sc.y, targetH, k);
                t.localScale = sc;
                var p = t.localPosition; p.y = sc.y / 2f; t.localPosition = p;
                cap.localPosition = new Vector3(p.x, sc.y + 0.02f, p.z);
                cap.localScale = new Vector3(sc.x + 0.03f, 0.04f, sc.z + 0.03f);
            }

            // ---- Day / night (brief 10): cool even daylight for Prep; at Service the studio darkens and the
            // bar's own lights come on one by one, like a curtain rising.
            bool service = _g.Phase == Phase.Service;
            if (service && !_wasService) _nightStart = Time.unscaledTime;
            _wasService = service;
            _night = Mathf.MoveTowards(_night, service ? 1f : 0f, Time.unscaledDeltaTime / 2.5f);
            float n = Mathf.SmoothStep(0, 1, _night);
            float L = s.lighting / 100f;

            var cam = Rig.Cam;
            cam.backgroundColor = Color.Lerp(Color.Lerp(StudioDayTop, StudioDayFloor, 0.35f), Color.Lerp(StudioNightTop, StudioNightFloor, 0.4f), n);
            if (_studioFloor != null) _studioFloor.sharedMaterial.color = Color.Lerp(StudioDayFloor, StudioNightFloor, n);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var dayAmb = new Color(0.66f, 0.68f, 0.72f) * (0.92f + 0.16f * L);
            var nightAmb = Color.Lerp(new Color(0.25f, 0.19f, 0.17f), new Color(0.44f, 0.35f, 0.29f), L);
            RenderSettings.ambientLight = Color.Lerp(dayAmb, nightAmb, n);
            _sun.intensity = Mathf.Lerp(1.05f, 0.16f, n);
            _sun.color = Color.Lerp(new Color(1f, 0.98f, 0.95f), new Color(0.55f, 0.65f, 0.95f), n);
            _sun.shadowStrength = Mathf.Lerp(0.5f, 0.2f, n);
            if (_fill != null) _fill.intensity = Mathf.Lerp(0.12f, 0.3f, n);

            SyncPlinth();
            SyncRain();
            if (_practicalDirty) RebuildPractical();
            float since = Time.unscaledTime - _nightStart;
            for (int i = 0; i < _practical.Count; i++)
            {
                var l = _practical[i];
                bool on = service && since > 0.4f + i * 0.3f;
                float target = on ? (1.1f + 0.9f * L) * l.bounceIntensity * 1.7f : 0f;
                l.intensity = Mathf.MoveTowards(l.intensity, target, Time.unscaledDeltaTime * 3f);
            }
            if (_practicalLamp != null) _practicalLamp.intensity = Mathf.Lerp(0f, 1.5f, n);
            foreach (var gl in _lights) if (gl != null) gl.intensity = Mathf.Lerp(0.5f, 1.4f, n);
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

        readonly Dictionary<int, int> _wearBand = new Dictionary<int, int>();
        static MaterialPropertyBlock _mpb;

        /// <summary>Worn furniture looks faded and a bit grubby (visual brief 8: simplified wear cues, not grime).</summary>
        void ApplyWear(GameObject go, int band, int uid)
        {
            _wearBand[uid] = band;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (band == 0) { r.SetPropertyBlock(null); continue; }
                var m = r.sharedMaterial;
                if (m == null || m.IsKeywordEnabled("_EMISSION")) continue; // lamps and screens stay bright
                var c = m.color;
                float grey = c.grayscale;
                float fade = band == 2 ? 0.45f : 0.22f;
                var worn = Color.Lerp(c, new Color(grey, grey * 0.96f, grey * 0.9f), fade) * (band == 2 ? 0.8f : 0.9f);
                worn.a = 1;
                _mpb.SetColor("_Color", worn);
                r.SetPropertyBlock(_mpb);
            }
        }

        void SyncItems()
        {
            var seen = new HashSet<int>();
            foreach (var it in _g.S.items)
            {
                seen.Add(it.uid);
                string key = $"{it.id}:{it.x}:{it.y}:{it.rot}";
                int wearBand = it.wear >= 70 ? 2 : it.wear >= 45 ? 1 : 0;
                if (_items.TryGetValue(it.uid, out var go))
                {
                    if (!_wearBand.TryGetValue(it.uid, out int wb) || wb != wearBand) ApplyWear(go, wearBand, it.uid);
                    if (_itemKey[it.uid] == key) continue;
                    go.transform.position = ItemCentre(it);
                    go.transform.rotation = Quaternion.Euler(0, 90f * it.rot, 0);
                    _itemKey[it.uid] = key;
                    _practicalDirty = true;
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
                _practicalDirty = true;
                ApplyWear(go, wearBand, it.uid);
            }
            var remove = new List<int>();
            foreach (var kv in _items) if (!seen.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var uid in remove)
            {
                foreach (var l in _items[uid].GetComponentsInChildren<Light>()) _lights.Remove(l);
                Destroy(_items[uid]);
                _items.Remove(uid); _itemKey.Remove(uid);
                _practicalDirty = true;
            }
        }

        /// <summary>Where to pin a UI badge above a customer (the rendered figure, so seats and queues line up).</summary>
        public bool TryHeadPos(Customer c, out Vector3 pos)
        {
            if (_customers.TryGetValue(c, out var v) && v.Root.activeSelf) { pos = v.Root.transform.position + Vector3.up * 1.95f; return true; }
            pos = default;
            return false;
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
            if (_performer != null) _performer.Root.SetActive(false);
            if (_spot != null) _spot.gameObject.SetActive(false);
            _eventLive = false;
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
                v.Dress(m.role == Roles.Door ? 0x1b1b22u : m.shirt, m.skin, m.hair, true, m.role == Roles.Bartender);
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
                    v.Dress(c.shirt, c.skin, c.hair, c.tall, false, (int)c.seg);
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
                    v.Dress(!a.IsOwner && a.m.role == Roles.Door ? 0x1b1b22u : a.m.shirt, a.IsOwner ? 0xf1c27d : a.m.skin, a.IsOwner ? 0x4a3020 : a.m.hair, true, a.Duty == Roles.Bartender || a.IsOwner);
                    v.Tag.Staff = a;
                    v.Snap = true;
                    _staff[a] = v;
                }
                bool show = a.present && (!a.IsOwner || svc.OwnerBehindBar);
                v.Root.SetActive(show);
                if (!show) continue;
                var pos = new Vector3(a.x, 0, a.y);
                Vector3 face = Vector3.back;
                if (a.Duty == Roles.Bartender || a.IsOwner)
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
            _eventLive = svc.Event != null && svc.Event.kind != EventKinds.Match && (svc.EventPhase == 1 || svc.EventPhase == 3); // the match is on the screens
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
            if (svc.Event != null && svc.Event.kind >= 1)
                _spot.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.05f, 1f), 0.35f, 1f);
            else _spot.color = new Color(1f, 0.93f, 0.8f);
            _spot.transform.position = _stagePos + Vector3.up * 3.6f - face * 1.2f;
            _spot.transform.LookAt(_stagePos + Vector3.up * 0.8f);
        }

        static Material _spill;
        static Material SpillMat()
        {
            if (_spill == null) { _spill = new Material(ModelFactory.Mat(0xB0701E)); _spill.SetFloat("_Glossiness", 0.9f); }
            return _spill;
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
                    Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.75f, 0.01f, 0.55f), SpillMat());
                    Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.22f, 0.005f, 0.12f), new Vector3(0.3f, 0.012f, 0.25f), SpillMat());
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
                    var gc = root.AddComponent<BoxCollider>(); gc.size = new Vector3(0.7f, 0.4f, 0.7f); gc.center = new Vector3(0, 0.1f, 0);
                    break;
                }
                case IncidentKind.Toilet:
                {
                    var it = _g.S.Item(inc.itemUid);
                    root.transform.position = (it != null ? ItemCentre(it) : new Vector3(inc.cell.x, 0, inc.cell.y)) + Vector3.up * 2.4f;
                    // The visible marker is a UI badge (Hud); this just keeps a click target over the toilets.
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
            Transform _body, _head, _hair, _apron, _drink, _marker, _alert, _backpack, _tie, _cap, _scarf, _beanie;
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
                v._drink = Part(v.Root.transform, PrimitiveType.Cylinder, new Vector3(0.24f, 0.78f, -0.12f), new Vector3(0.1f, 0.1f, 0.1f), ModelFactory.Mat(0xE39B2E)).transform;
                // Beer reads by its cream foam cap from above (brief 8)
                Part(v._drink, PrimitiveType.Cylinder, new Vector3(0, 1.05f, 0), new Vector3(1.08f, 0.22f, 1.08f), ModelFactory.Mat(0xFFF1C9));
                // Segment silhouettes (brief 11.2): backpack, tie, flat cap, scarf, beanie
                v._backpack = Part(v.Root.transform, PrimitiveType.Cube, new Vector3(0, 0.7f, 0.2f), new Vector3(0.32f, 0.36f, 0.16f), ModelFactory.Mat(0x34345a)).transform;
                v._tie = Part(v.Root.transform, PrimitiveType.Cube, new Vector3(0, 0.82f, -0.165f), new Vector3(0.07f, 0.28f, 0.02f), ModelFactory.Mat(0x1a1a1a)).transform;
                v._cap = Part(v.Root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.39f, -0.02f), new Vector3(0.38f, 0.04f, 0.4f), ModelFactory.Mat(0x6b5a45)).transform;
                Part(v._cap, PrimitiveType.Cube, new Vector3(0, 0, -0.55f), new Vector3(0.7f, 1f, 0.35f), ModelFactory.Mat(0x6b5a45));
                v._scarf = Part(v.Root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.0f, 0), new Vector3(0.36f, 0.05f, 0.3f), ModelFactory.Mat(0xf2f2f2)).transform;
                v._beanie = Part(v.Root.transform, PrimitiveType.Sphere, new Vector3(0, 1.36f, 0.01f), new Vector3(0.37f, 0.27f, 0.37f), ModelFactory.Mat(0xE9C46A)).transform;
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

            public void Dress(uint shirt, uint skin, uint hair, bool tall, bool apron, int seg = -1)
            {
                // Segment silhouettes: the newer crowds reuse the same props in new combinations (task K5).
                _backpack.gameObject.SetActive(seg == (int)Seg.Students || seg == (int)Seg.Tourists);
                _tie.gameObject.SetActive(seg == (int)Seg.YoungPros || seg == (int)Seg.AfterWork);
                _cap.gameObject.SetActive(seg == (int)Seg.Locals || seg == (int)Seg.Tourists);
                _scarf.gameObject.SetActive(seg == (int)Seg.SportsFans || seg == (int)Seg.Couples);
                _beanie.gameObject.SetActive(seg == (int)Seg.Creatives);
                _hair.gameObject.SetActive(seg != (int)Seg.Locals && seg != (int)Seg.Creatives && seg != (int)Seg.Tourists);
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
                _alert.gameObject.SetActive(false);
                bool showMarker = false;
                _marker.gameObject.SetActive(showMarker);
                if (showMarker) _markerR.sharedMaterial = ModelFactory.Glow(mood < 25 ? 0xff4d4d : mood < 40 ? 0xffb347 : 0x3ddc84, 1.3f);
                if (rowdy) _alert.localRotation = Quaternion.Euler(0, Time.time * 180f, 0);
            }

            /// <summary>Optional stop-motion look (visual brief 11.3): figures step at 12 fps, camera stays smooth.</summary>
            public static bool StopMotion;
            float _lastStep = -1;

            public void Place(Vector3 target, Vector3 face, bool moving, float seatH, bool seated)
            {
                if (StopMotion && !Snap)
                {
                    float step = Mathf.Floor(Time.unscaledTime * 12f);
                    if (step == _lastStep) return;
                    _lastStep = step;
                }
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
