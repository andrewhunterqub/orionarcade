using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Loab.Game
{
    // Builds every bar prop at runtime out of Unity primitives with flat-coloured materials.
    // Conventions: 1 cell = 1 unit, model centred on the root origin, y = 0 is the floor,
    // front faces local -z. Seating parts line up with Service.SeatPos.
    public static class ModelFactory
    {
        static Material _lit, _glow;
        static readonly Dictionary<uint, Material> _mats = new Dictionary<uint, Material>();
        static readonly Dictionary<long, Material> _glows = new Dictionary<long, Material>();

        const uint Metal = 0x888888, Chrome = 0xc8c8c8, Black = 0x1a1a1a, White = 0xf2f2f2, Brass = 0xd4a64a,
            DarkWood = 0x4a2e18, MidWood = 0x8a5a33, Cream = 0xe8dcc8;

        // =====================================================================================
        //  Materials
        // =====================================================================================

        public static void Init(Material lit, Material glow)
        {
            _lit = lit;
            _glow = glow;
            _mats.Clear();
            _glows.Clear();
        }

        static Material BaseLit()
        {
            if (_lit == null) _lit = new Material(Shader.Find("Standard"));
            return _lit;
        }

        static Color ToColor(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static Material Mat(uint rgb)
        {
            rgb &= 0xffffff;
            if (_mats.TryGetValue(rgb, out var m) && m != null) return m;
            m = new Material(BaseLit()) { color = ToColor(rgb), name = "lit_" + rgb.ToString("x6") };
            _mats[rgb] = m;
            return m;
        }

        public static Material Glow(uint rgb, float intensity = 1.6f)
        {
            rgb &= 0xffffff;
            long key = ((long)rgb << 20) ^ Mathf.RoundToInt(intensity * 1000f);
            if (_glows.TryGetValue(key, out var m) && m != null) return m;
            var c = ToColor(rgb);
            m = new Material(_glow != null ? _glow : BaseLit()) { color = c, name = "glow_" + rgb.ToString("x6") };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            _glows[key] = m;
            return m;
        }

        // Convenience overloads so callers can pass non-constant int colours (e.g. `ok ? 0x3ddc84 : 0xff4d4d`).
        public static Material Mat(int rgb) => Mat((uint)rgb);
        public static Material Glow(int rgb, float intensity = 1.6f) => Glow((uint)rgb, intensity);

        static Material M(uint rgb) => Mat(rgb);
        static Material G(uint rgb, float i = 1.6f) => Glow(rgb, i);

        static uint Shade(uint rgb, float f)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(((rgb >> 16) & 255) * f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(((rgb >> 8) & 255) * f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt((rgb & 255) * f), 0, 255);
            return (uint)((r << 16) | (g << 8) | b);
        }

        // =====================================================================================
        //  Primitive helpers
        // =====================================================================================

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            var tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = Quaternion.Euler(euler);
            tr.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) < 0.09f) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static GameObject Box(Transform p, float x, float y, float z, float sx, float sy, float sz, Material m) =>
            Part(p, PrimitiveType.Cube, V(x, y, z), V(sx, sy, sz), m);

        // ---- Bevelled boxes (visual brief, task V3): hero props (bar counters, table tops, booth cushions)
        // get chamfered edges that catch the light. Built at the exact size so the bevel isn't stretched;
        // meshes are cached by size. Flat-shaded: 6 faces, 12 edge strips and 8 corner triangles.
        public static bool Bevels = true;
        static readonly Dictionary<long, Mesh> _bevelMeshes = new Dictionary<long, Mesh>();

        static GameObject BevelBox(Transform p, float x, float y, float z, float sx, float sy, float sz, Material m, float bevel = 0.025f)
        {
            float b = Mathf.Min(bevel, Mathf.Min(sx, Mathf.Min(sy, sz)) * 0.3f);
            if (!Bevels || b < 0.004f) return Box(p, x, y, z, sx, sy, sz, m);
            long key = ((long)Mathf.RoundToInt(sx * 500) << 40) ^ ((long)Mathf.RoundToInt(sy * 500) << 20) ^ Mathf.RoundToInt(sz * 500) ^ ((long)Mathf.RoundToInt(b * 1000) << 58);
            if (!_bevelMeshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = BevelMesh(sx * 0.5f, sy * 0.5f, sz * 0.5f, b);
                _bevelMeshes[key] = mesh;
            }
            var go = new GameObject("Bevel");
            go.transform.SetParent(p, false);
            go.transform.localPosition = V(x, y, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static Mesh BevelMesh(float hx, float hy, float hz, float b)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Poly(params Vector3[] pts)
            {
                // Fan-triangulate, wound to face outward (the box is convex and centred on the origin).
                var c = Vector3.zero; foreach (var q in pts) c += q; c /= pts.Length;
                var n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]);
                bool flip = Vector3.Dot(n, c) < 0;
                int i0 = verts.Count;
                verts.AddRange(pts);
                for (int i = 1; i < pts.Length - 1; i++)
                {
                    if (flip) { tris.Add(i0); tris.Add(i0 + i + 1); tris.Add(i0 + i); }
                    else { tris.Add(i0); tris.Add(i0 + i); tris.Add(i0 + i + 1); }
                }
            }
            var h = new Vector3(hx, hy, hz);
            var hi = h - Vector3.one * b;
            // Faces: axis a at ±h, other two axes spanning ±(h - b).
            for (int a = 0; a < 3; a++)
                for (int sa = -1; sa <= 1; sa += 2)
                {
                    int u = (a + 1) % 3, v = (a + 2) % 3;
                    var pts = new Vector3[4];
                    int k = 0;
                    foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        var q = Vector3.zero; q[a] = sa * h[a]; q[u] = su * hi[u]; q[v] = sv * hi[v];
                        pts[k++] = q;
                    }
                    Poly(pts);
                }
            // Edge strips between faces on axes a and c, running along axis e.
            for (int e = 0; e < 3; e++)
            {
                int a = (e + 1) % 3, c = (e + 2) % 3;
                for (int sa = -1; sa <= 1; sa += 2)
                    for (int sc = -1; sc <= 1; sc += 2)
                    {
                        Vector3 P(float ea, float ca, float ee) { var q = Vector3.zero; q[a] = ea; q[c] = ca; q[e] = ee; return q; }
                        Poly(P(sa * h[a], sc * hi[c], -hi[e]), P(sa * h[a], sc * hi[c], hi[e]), P(sa * hi[a], sc * h[c], hi[e]), P(sa * hi[a], sc * h[c], -hi[e]));
                    }
            }
            // Corner triangles.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Poly(new Vector3(sx * h.x, sy * hi.y, sz * hi.z), new Vector3(sx * hi.x, sy * h.y, sz * hi.z), new Vector3(sx * hi.x, sy * hi.y, sz * h.z));
            var mesh = new Mesh { name = "bevel" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static GameObject BoxR(Transform p, Vector3 pos, Vector3 size, Material m, Vector3 euler) =>
            Part(p, PrimitiveType.Cube, pos, size, m, euler);

        // Cylinder by centre, diameter and height (Unity's cylinder is 2 units tall at scale 1).
        static GameObject Cyl(Transform p, float x, float y, float z, float d, float h, Material m) =>
            Part(p, PrimitiveType.Cylinder, V(x, y, z), V(d, h * 0.5f, d), m);

        static GameObject CylR(Transform p, Vector3 pos, float d, float h, Material m, Vector3 euler) =>
            Part(p, PrimitiveType.Cylinder, pos, V(d, h * 0.5f, d), m, euler);

        static GameObject Sph(Transform p, float x, float y, float z, float sx, float sy, float sz, Material m) =>
            Part(p, PrimitiveType.Sphere, V(x, y, z), V(sx, sy, sz), m);

        // Cylinder running from a to b.
        static GameObject Rod(Transform p, Vector3 a, Vector3 b, float d, Material m)
        {
            var dir = b - a;
            float len = dir.magnitude;
            var go = Part(p, PrimitiveType.Cylinder, (a + b) * 0.5f, V(d, len * 0.5f, d), m);
            if (len > 1e-5f) go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            return go;
        }

        static Transform Group(Transform p, Vector3 pos, Vector3 euler)
        {
            var go = new GameObject("grp");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        static void Stool(Transform t, float x, float z, float seatTop, float d, Material seat, Material leg, float y0 = 0f, bool footrest = false)
        {
            float legH = seatTop - 0.06f;
            Cyl(t, x, y0 + seatTop - 0.03f, z, d, 0.06f, seat);
            Cyl(t, x, y0 + legH * 0.5f, z, 0.05f, legH, leg);
            Cyl(t, x, y0 + 0.015f, z, d * 0.85f, 0.03f, leg);
            if (footrest) Cyl(t, x, y0 + seatTop * 0.38f, z, d * 0.8f, 0.02f, leg);
        }

        // Local seat positions (rotation 0), mirroring Service.SeatPos.
        static Vector3[] Seats(Loab.Sim.ItemDef d)
        {
            int w = d.W, h = d.H, n = Mathf.Max(1, d.Capacity);
            bool oneRow = d.Model == "sofa" || d.Model == "armchair" || d.Model == "beanbag" || d.Model == "stool";
            bool alongX = w >= h;
            var res = new Vector3[n];
            for (int slot = 0; slot < n; slot++)
            {
                float along, across;
                if (n == 1) { along = 0; across = 0; }
                else if (oneRow)
                {
                    along = (slot - (n - 1) / 2f) * (Mathf.Max(w, h) / (float)n);
                    across = 0;
                }
                else
                {
                    int perSide = (n + 1) / 2, side = slot % 2, k = slot / 2;
                    float len = Mathf.Max(w, h);
                    along = perSide == 1 ? 0 : (k - (perSide - 1) / 2f) * (len / perSide);
                    across = (side == 0 ? -1 : 1) * Mathf.Max(0.32f, Mathf.Min(w, h) / 2f - 0.12f);
                }
                res[slot] = alongX ? V(along, 0, across) : V(across, 0, along);
            }
            return res;
        }

        // =====================================================================================
        //  Public API
        // =====================================================================================

        public static float SeatHeight(Loab.Sim.ItemDef def)
        {
            if (def == null) return 0.45f;
            switch (def.Model)
            {
                case "stool":
                case "hightable": return 0.75f;
                case "barrel": return 0.7f;
                case "beanbag": return 0.25f;
                default: return 0.45f; // pubtable, longtable, plastic, booth, armchair, sofa
            }
        }

        public static GameObject Build(Loab.Sim.ItemDef def)
        {
            var root = new GameObject(def != null ? def.Id : "item");
            if (def == null) return root;
            var t = root.transform;
            switch (def.Model)
            {
                case "bar": BuildBar(t, def); break;
                case "pubtable": PubTable(t, def); break;
                case "longtable": LongTable(t, def); break;
                case "stool": BarStool(t, def); break;
                case "hightable": HighTable(t, def); break;
                case "barrel": Barrel(t, def); break;
                case "plastic": Plastic(t, def); break;
                case "beanbag": Beanbag(t, def); break;
                case "booth": Booth(t, def); break;
                case "armchair": Couch(t, def, 0.86f, 1, false); break;
                case "sofa": Couch(t, def, 1.9f, 3, true); break;
                case "toilets": Toilets(t, def); break;
                case "darts": Darts(t, def); break;
                case "pool": Pool(t, def); break;
                case "foosball": Foosball(t, def); break;
                case "boardgames": BoardGames(t, def); break;
                case "arcade": Arcade(t, def); break;
                case "pinball": Pinball(t, def); break;
                case "stage": Stage(t, def); break;
                case "speaker": Speaker(t, def); break;
                case "jukebox": Jukebox(t, def); break;
                case "piano": Piano(t, def); break;
                case "tv": Tv(t, def); break;
                case "projector": Projector(t, def); break;
                case "edison": Edison(t, def); break;
                case "candles": Candles(t, def); break;
                case "neon": Neon(t, def); break;
                case "festoon": Festoon(t, def); break;
                case "discoball": DiscoBall(t, def); break;
                case "chandelier": Chandelier(t, def); break;
                case "plant": Plant(t, def); break;
                case "fern": Fern(t, def); break;
                case "frames": Frames(t, def); break;
                case "flamingo": Flamingo(t, def); break;
                case "bookshelf": Bookshelf(t, def); break;
                case "curtains": Curtains(t, def); break;
                case "pipes": Pipes(t, def); break;
                case "chalkboard": Chalkboard(t, def); break;
                case "fishtank": FishTank(t, def); break;
                case "fireplace": Fireplace(t, def); break;
                case "rug": Rug(t, def); break;
                case "dogbed": DogBed(t, def); break;
                case "tapwall": TapWall(t, def); break;
                case "kegs": Kegs(t, def); break;
                case "winerack": WineRack(t, def); break;
                case "still": CopperStill(t, def); break;
                case "trophies": TrophyCabinet(t, def); break;
                case "photobooth": PhotoBooth(t, def); break;
                default:
                    Box(t, 0, 0.4f, 0, Mathf.Max(0.1f, def.W - 0.1f), 0.8f, Mathf.Max(0.1f, def.H - 0.1f), M(def.Color));
                    break;
            }

            // One pick collider on the root covering the footprint.
            float top = 0.05f;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - t.position.y);
            var bc = root.AddComponent<BoxCollider>();
            bc.size = V(Mathf.Max(0.1f, def.W), top, Mathf.Max(0.1f, def.H));
            bc.center = V(0, top * 0.5f, 0);
            return root;
        }

        // =====================================================================================
        //  Bar counter (3x2)
        // =====================================================================================

        static void BuildBar(Transform t, Loab.Sim.ItemDef d)
        {
            bool neon = d.Id == "bar_neon", deco = d.Id == "bar_deco";
            var body = M(d.Color);
            var panel = M(Shade(d.Color, 0.78f));
            var topM = M(neon ? 0x15151cu : deco ? 0x111111u : d.Color2);
            var accent = neon ? G(d.Color2, 2.2f) : M(d.Color2);
            var chrome = M(Chrome);

            // Front counter: z -0.95..-0.15, top at ~1.06.
            BevelBox(t, 0, 0.5f, -0.56f, 2.86f, 1.0f, 0.72f, body, 0.03f);
            BevelBox(t, 0, 1.03f, -0.55f, 2.92f, 0.06f, 0.8f, topM, 0.02f);
            for (int i = -1; i <= 1; i++) Box(t, i * 0.95f, 0.48f, -0.925f, 0.8f, 0.58f, 0.02f, panel);
            Box(t, 0, 0.88f, -0.93f, 2.86f, 0.06f, 0.03f, accent);
            if (neon)
            {
                Box(t, 0, 0.06f, -0.93f, 2.86f, 0.04f, 0.03f, accent);       // underglow
                Box(t, 0, 1.0f, -0.955f, 2.92f, 0.025f, 0.02f, accent);      // top edge tube
            }
            if (deco)
            {
                var brass = M(Brass);
                Rod(t, V(-1.38f, 0.18f, -0.96f), V(1.38f, 0.18f, -0.96f), 0.045f, brass); // foot rail
                Box(t, -1.1f, 0.1f, -0.94f, 0.04f, 0.16f, 0.04f, brass);
                Box(t, 1.1f, 0.1f, -0.94f, 0.04f, 0.16f, 0.04f, brass);
                Box(t, 0, 1.0f, -0.955f, 2.92f, 0.03f, 0.02f, brass);              // brass top edge
            }

            // Beer taps: two T-bar fonts with coloured handles.
            uint[] handles = { 0xd62828, 0xffd166, 0x1a1a1a, 0x2e86de, 0xf2f2f2, 0x6ab04c };
            for (int f = 0; f < 2; f++)
            {
                float x = f == 0 ? -0.85f : 0.05f;
                Cyl(t, x, 1.21f, -0.32f, 0.07f, 0.3f, chrome);
                Box(t, x, 1.33f, -0.32f, 0.38f, 0.06f, 0.06f, chrome);
                for (int h = -1; h <= 1; h++)
                    Box(t, x + h * 0.13f, 1.43f, -0.34f, 0.035f, 0.15f, 0.035f, M(handles[f * 3 + h + 1]));
            }
            // Till.
            Box(t, 0.95f, 1.14f, -0.38f, 0.32f, 0.16f, 0.26f, M(0x2a2a2a));
            BoxR(t, V(0.95f, 1.29f, -0.33f), V(0.26f, 0.15f, 0.03f), M(0x7ad0e0), V(20, 0, 0));

            // Back bar along the rear edge (z ~0.71..0.95); bartender strip in between left empty.
            Box(t, 0, 0.45f, 0.83f, 2.86f, 0.9f, 0.24f, body);
            Box(t, 0, 1.5f, 0.93f, 2.86f, 1.2f, 0.04f, M(deco ? 0xb8ccd6u : Shade(d.Color, 0.7f)));
            Box(t, 0, 1.35f, 0.85f, 2.86f, 0.04f, 0.2f, topM);
            Box(t, 0, 1.8f, 0.85f, 2.86f, 0.04f, 0.2f, topM);
            Box(t, 0, 2.13f, 0.85f, 2.92f, 0.06f, 0.24f, deco ? M(Brass) : body);
            if (neon) Box(t, 0, 1.33f, 0.745f, 2.8f, 0.025f, 0.025f, accent);

            // Bottles.
            uint[] bottleCols = { 0x2e7d32, 0x8b4513, 0xd4a64a, 0x9ec5d6, 0xc0392b, 0x6c3483, 0xf2f2f2 };
            float[] xs = { -1.2f, -0.7f, -0.15f, 0.45f, 1.1f };
            for (int row = 0; row < 2; row++)
            {
                float baseY = row == 0 ? 0.9f : 1.37f;
                for (int i = 0; i < xs.Length; i++)
                {
                    float h = 0.24f + ((i + row) % 3) * 0.04f;
                    Cyl(t, xs[i] + row * 0.15f, baseY + h * 0.5f, 0.84f, 0.08f, h, M(bottleCols[(i + row * 3) % bottleCols.Length]));
                }
            }
        }

        // =====================================================================================
        //  Seating
        // =====================================================================================

        static void PubTable(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            var dark = M(d.Color2);
            BevelBox(t, 0, 0.74f, 0, 1.0f, 0.06f, 0.86f, wood, 0.02f);
            Cyl(t, 0, 0.36f, 0, 0.1f, 0.7f, dark);
            Cyl(t, 0, 0.02f, 0, 0.5f, 0.04f, dark);
            foreach (var s in Seats(d)) Stool(t, s.x, s.z, 0.45f, 0.26f, wood, dark);
        }

        static void LongTable(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            var bench = M(d.Color2);
            var seats = Seats(d);
            float acr = seats.Length > 1 ? Mathf.Abs(seats[0].z) : 0.38f;
            float len = d.W - 0.2f, legX = len / 2f - 0.3f;
            BevelBox(t, 0, 0.74f, 0, len, 0.06f, 0.42f, wood, 0.02f);
            Box(t, -legX, 0.355f, 0, 0.08f, 0.71f, 0.32f, wood);
            Box(t, legX, 0.355f, 0, 0.08f, 0.71f, 0.32f, wood);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, 0, 0.42f, s * acr, len, 0.06f, 0.22f, bench);
                Box(t, -legX, 0.195f, s * acr, 0.06f, 0.39f, 0.18f, bench);
                Box(t, legX, 0.195f, s * acr, 0.06f, 0.39f, 0.18f, bench);
            }
        }

        static void BarStool(Transform t, Loab.Sim.ItemDef d)
        {
            Stool(t, 0, 0, 0.75f, 0.36f, M(d.Color2), M(d.Color), 0f, true);
            Cyl(t, 0, 0.77f, 0, 0.3f, 0.04f, M(Shade(d.Color2, 1.15f))); // puffy cushion top
        }

        static void HighTable(Transform t, Loab.Sim.ItemDef d)
        {
            var frame = M(d.Color);
            var top = M(d.Color2);
            Cyl(t, 0, 1.03f, 0, 0.44f, 0.04f, top);
            Cyl(t, 0, 0.51f, 0, 0.06f, 1.0f, frame);
            Cyl(t, 0, 0.015f, 0, 0.34f, 0.03f, frame);
            foreach (var s in Seats(d)) Stool(t, s.x, s.z, 0.75f, 0.22f, frame, top, 0f, true);
        }

        static void Barrel(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            var hoop = M(d.Color2);
            Cyl(t, 0, 0.45f, 0, 0.48f, 0.9f, wood);
            Cyl(t, 0, 0.45f, 0, 0.53f, 0.5f, wood);
            Cyl(t, 0, 0.2f, 0, 0.545f, 0.04f, hoop);
            Cyl(t, 0, 0.7f, 0, 0.545f, 0.04f, hoop);
            Cyl(t, 0, 0.905f, 0, 0.46f, 0.02f, M(Shade(d.Color, 1.3f)));
            foreach (var s in Seats(d)) Stool(t, s.x, s.z, 0.7f, 0.22f, wood, hoop);
        }

        static void Plastic(Transform t, Loab.Sim.ItemDef d)
        {
            var white = M(d.Color);
            var acc = M(d.Color2);
            Cyl(t, 0, 0.7f, 0, 0.5f, 0.04f, white);
            Cyl(t, 0, 0.35f, 0, 0.05f, 0.7f, white);
            Rod(t, V(0, 0.72f, 0), V(0, 1.9f, 0), 0.025f, M(Metal));
            Cyl(t, 0, 1.86f, 0, 0.88f, 0.08f, acc);
            Sph(t, 0, 1.9f, 0, 0.06f, 0.06f, 0.06f, white);
            foreach (var s in Seats(d))
            {
                float sign = s.z >= 0 ? 1f : -1f;
                Box(t, s.x, 0.43f, s.z, 0.34f, 0.04f, 0.22f, white);
                Box(t, s.x, 0.64f, s.z + sign * 0.1f, 0.34f, 0.3f, 0.03f, white);
                Box(t, s.x - 0.15f, 0.21f, s.z, 0.03f, 0.42f, 0.2f, white);
                Box(t, s.x + 0.15f, 0.21f, s.z, 0.03f, 0.42f, 0.2f, white);
            }
        }

        static void Beanbag(Transform t, Loab.Sim.ItemDef d)
        {
            var seats = Seats(d);
            for (int i = 0; i < seats.Length; i++)
            {
                var m = M(i % 2 == 0 ? d.Color : d.Color2);
                float x = seats[i].x;
                Sph(t, x, 0.15f, 0, 0.46f, 0.3f, 0.5f, m);
                Sph(t, x, 0.3f, 0.14f, 0.4f, 0.34f, 0.24f, m);
            }
        }

        static void Booth(Transform t, Loab.Sim.ItemDef d)
        {
            var leather = M(d.Color);
            var wood = M(d.Color2);
            var seats = Seats(d);
            float acr = seats.Length > 1 ? Mathf.Abs(seats[0].z) : 0.88f;
            float wdt = d.W - 0.1f;
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, 0, 0.155f, s * (acr - 0.13f), wdt, 0.31f, 0.3f, wood);
                BevelBox(t, 0, 0.38f, s * (acr - 0.13f), wdt, 0.14f, 0.3f, leather, 0.04f);
                BevelBox(t, 0, 0.83f, s * (acr + 0.05f), wdt, 0.76f, 0.08f, leather, 0.03f);
                Box(t, 0, 1.23f, s * (acr + 0.05f), wdt + 0.04f, 0.05f, 0.12f, wood);
            }
            BevelBox(t, 0, 0.74f, 0, wdt - 0.4f, 0.06f, 0.7f, wood, 0.02f);
            Cyl(t, 0, 0.36f, 0, 0.12f, 0.7f, M(Black));
            Box(t, 0, 0.02f, 0, 0.6f, 0.04f, 0.4f, M(Black));
            Cyl(t, 0.35f, 0.79f, 0, 0.08f, 0.04f, M(Brass));
            Sph(t, 0.35f, 0.86f, 0, 0.12f, 0.1f, 0.12f, M(0xffe0a0));
        }

        static void Couch(Transform t, Loab.Sim.ItemDef d, float wdt, int cushions, bool chester)
        {
            var fab = M(d.Color);
            var cushion = M(Shade(d.Color, 1.12f));
            var leg = M(d.Color2);
            float hw = wdt / 2f, aw = 0.16f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Cyl(t, sx * (hw - 0.08f), 0.04f, -0.3f, 0.07f, 0.08f, leg);
                Cyl(t, sx * (hw - 0.08f), 0.04f, 0.3f, 0.07f, 0.08f, leg);
            }
            Box(t, 0, 0.22f, 0, wdt, 0.28f, 0.8f, fab);
            float inner = wdt - 2 * aw, cw = inner / cushions;
            for (int i = 0; i < cushions; i++)
                Box(t, -inner / 2f + cw * (i + 0.5f), 0.405f, -0.06f, cw - 0.02f, 0.09f, 0.6f, cushion);
            Box(t, 0, 0.66f, 0.31f, wdt, 0.6f, 0.18f, fab);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float ax = sx * (hw - aw / 2f);
                Box(t, ax, 0.5f, -0.02f, aw, 0.28f, 0.76f, fab);
                CylR(t, V(ax, 0.64f, -0.02f), 0.2f, 0.76f, fab, V(90, 0, 0)); // rolled arm
            }
            if (chester)
            {
                CylR(t, V(0, 0.96f, 0.31f), 0.18f, wdt, fab, V(0, 0, 90));
                var btn = M(Shade(d.Color, 0.6f));
                for (int i = -1; i <= 1; i++) Sph(t, i * inner / 3f, 0.62f, 0.215f, 0.05f, 0.05f, 0.03f, btn);
                for (int s = -1; s <= 1; s += 2) Sph(t, s * inner / 6f, 0.8f, 0.215f, 0.05f, 0.05f, 0.03f, btn);
            }
        }

        // =====================================================================================
        //  Toilets
        // =====================================================================================

        static void Toilets(Transform t, Loab.Sim.ItemDef d)
        {
            var wall = M(d.Color);
            var door = M(d.Color2);
            var trim = M(Shade(d.Color, 0.75f));
            float wdt = d.W - 0.1f, dep = d.H - 0.14f;
            Box(t, 0, 1.1f, 0.02f, wdt, 2.2f, dep, wall);
            Box(t, 0, 2.24f, 0.02f, wdt + 0.04f, 0.08f, dep + 0.04f, door);
            int n = Mathf.Clamp(d.Capacity, 1, 4);
            float span = wdt - 0.1f, cell = span / n, front = 0.02f - dep / 2f;
            for (int i = 0; i < n; i++)
            {
                float x = -span / 2f + cell * (i + 0.5f), dw = cell - 0.12f;
                Box(t, x, 0.95f, front - 0.015f, dw, 1.7f, 0.04f, door);
                Sph(t, x + dw / 2f - 0.07f, 0.95f, front - 0.045f, 0.05f, 0.05f, 0.05f, M(Chrome));
                Box(t, x, 1.6f, front - 0.04f, 0.1f, 0.06f, 0.01f, M(0x3ddc84)); // vacant indicator
            }
            for (int i = 1; i < n; i++) Box(t, -span / 2f + cell * i, 0.95f, front - 0.015f, 0.04f, 1.9f, 0.05f, trim);
            Box(t, 0, 1.98f, front - 0.02f, 0.5f, 0.22f, 0.03f, M(White));
            Part(t, PrimitiveType.Capsule, V(-0.09f, 1.98f, front - 0.04f), V(0.06f, 0.07f, 0.02f), M(0x2e86de));
            Part(t, PrimitiveType.Capsule, V(0.09f, 1.98f, front - 0.04f), V(0.08f, 0.07f, 0.02f), M(0xd62828));
        }

        // =====================================================================================
        //  Games
        // =====================================================================================

        static void Darts(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(0x5a3a22);
            Box(t, 0, 0.03f, 0.15f, 0.6f, 0.06f, 0.5f, wood);
            Box(t, 0, 0.85f, 0.3f, 0.1f, 1.6f, 0.08f, wood);
            Box(t, 0, 1.6f, 0.24f, 0.72f, 0.72f, 0.06f, wood);
            var face = V(90, 0, 0);
            CylR(t, V(0, 1.6f, 0.19f), 0.54f, 0.03f, M(d.Color), face);
            CylR(t, V(0, 1.6f, 0.17f), 0.42f, 0.02f, M(0xe8dcc0), face);
            CylR(t, V(0, 1.6f, 0.155f), 0.3f, 0.02f, M(d.Color2), face);
            CylR(t, V(0, 1.6f, 0.14f), 0.17f, 0.02f, M(0x2a7a3a), face);
            CylR(t, V(0, 1.6f, 0.125f), 0.06f, 0.02f, M(d.Color2), face);
            Rod(t, V(0.07f, 1.66f, 0.13f), V(0.07f, 1.66f, -0.02f), 0.015f, M(0xffd166));
            Rod(t, V(-0.1f, 1.52f, 0.13f), V(-0.1f, 1.52f, -0.02f), 0.015f, M(0x2e86de));
            Box(t, 0, 1.06f, 0.22f, 0.4f, 0.24f, 0.03f, M(0x222222));
            Box(t, 0, 1.06f, 0.2f, 0.3f, 0.02f, 0.01f, M(White));
            Box(t, 0, 0.005f, -0.4f, 0.6f, 0.01f, 0.04f, M(White)); // oche
        }

        static void Pool(Transform t, Loab.Sim.ItemDef d)
        {
            var cloth = M(d.Color);
            var wood = M(d.Color2);
            var black = M(0x111111);
            Box(t, 0, 0.66f, 0, 1.85f, 0.2f, 0.88f, wood);
            Box(t, 0, 0.77f, 0, 1.7f, 0.04f, 0.74f, cloth);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, 0, 0.79f, s * 0.41f, 1.85f, 0.06f, 0.08f, wood);
                Box(t, s * 0.885f, 0.79f, 0, 0.08f, 0.06f, 0.88f, wood);
                Box(t, s * 0.78f, 0.28f, -0.32f, 0.12f, 0.56f, 0.12f, wood);
                Box(t, s * 0.78f, 0.28f, 0.32f, 0.12f, 0.56f, 0.12f, wood);
            }
            for (int i = -1; i <= 1; i++)
                for (int s = -1; s <= 1; s += 2)
                    Cyl(t, i * 0.83f, 0.8f, s * 0.36f, 0.08f, 0.025f, black);
            Sph(t, -0.45f, 0.82f, 0.02f, 0.07f, 0.07f, 0.07f, M(White));
            Sph(t, 0.4f, 0.82f, 0, 0.07f, 0.07f, 0.07f, M(0xd62828));
            Sph(t, 0.47f, 0.82f, 0.05f, 0.07f, 0.07f, 0.07f, M(0xffd166));
            Sph(t, 0.47f, 0.82f, -0.05f, 0.07f, 0.07f, 0.07f, black);
            Rod(t, V(-0.75f, 0.83f, 0.22f), V(0.25f, 0.83f, 0.12f), 0.025f, M(0xd9b382));
        }

        static void Foosball(Transform t, Loab.Sim.ItemDef d)
        {
            var body = M(0x333333);
            var line = M(d.Color2);
            Box(t, 0, 0.78f, 0, 1.4f, 0.24f, 0.72f, body);
            Box(t, 0, 0.9f, 0, 1.3f, 0.02f, 0.62f, M(d.Color));
            Box(t, 0, 0.912f, 0, 0.02f, 0.006f, 0.62f, line);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(t, sx * 0.6f, 0.33f, -0.28f, 0.1f, 0.66f, 0.1f, body);
                Box(t, sx * 0.6f, 0.33f, 0.28f, 0.1f, 0.66f, 0.1f, body);
            }
            var rod = M(Chrome);
            var handle = M(Black);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.45f + 0.3f * i;
                var team = M(i % 2 == 0 ? 0xd62828u : 0x2e86deu);
                Rod(t, V(x, 0.98f, -0.47f), V(x, 0.98f, 0.47f), 0.025f, rod);
                float hz = i % 2 == 0 ? -0.43f : 0.43f;
                CylR(t, V(x, 0.98f, hz), 0.05f, 0.08f, handle, V(90, 0, 0));
                Box(t, x, 0.94f, -0.15f, 0.04f, 0.1f, 0.05f, team);
                Box(t, x, 0.94f, 0.15f, 0.04f, 0.1f, 0.05f, team);
            }
        }

        static void BoardGames(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            Box(t, 0, 0.8f, 0.42f, 0.84f, 1.6f, 0.04f, wood);
            Box(t, -0.41f, 0.8f, 0.24f, 0.04f, 1.6f, 0.4f, wood);
            Box(t, 0.41f, 0.8f, 0.24f, 0.04f, 1.6f, 0.4f, wood);
            Box(t, 0, 1.58f, 0.24f, 0.86f, 0.04f, 0.42f, wood);
            Box(t, 0, 0.05f, 0.24f, 0.78f, 0.1f, 0.38f, wood);
            Box(t, 0, 0.58f, 0.24f, 0.78f, 0.03f, 0.36f, wood);
            Box(t, 0, 1.08f, 0.24f, 0.78f, 0.03f, 0.36f, wood);
            uint[] cols = { d.Color2, 0xd62828, 0x2e86de, 0x6ab04c, 0x9b5de5, 0xf2f2f2 };
            // Stacks of flat game boxes.
            float[] ys = { 0.1f, 0.595f, 1.095f };
            for (int s = 0; s < 3; s++)
            {
                float y = ys[s];
                Box(t, -0.17f, y + 0.05f, 0.24f, 0.36f, 0.1f, 0.3f, M(cols[(s * 2) % cols.Length]));
                Box(t, -0.17f, y + 0.14f, 0.24f, 0.32f, 0.08f, 0.28f, M(cols[(s * 2 + 1) % cols.Length]));
                Box(t, 0.2f, y + 0.18f, 0.24f, 0.3f, 0.36f, 0.08f, M(cols[(s * 2 + 3) % cols.Length])); // standing box
            }
            Box(t, -0.15f, 1.66f, 0.2f, 0.1f, 0.1f, 0.1f, M(White)); // a big die on top
            Sph(t, 0.15f, 1.65f, 0.22f, 0.12f, 0.1f, 0.12f, M(d.Color2));
        }

        static void Arcade(Transform t, Loab.Sim.ItemDef d)
        {
            var body = M(d.Color);
            var acc = M(d.Color2);
            Box(t, 0, 0.5f, 0.05f, 0.62f, 1.0f, 0.6f, body);
            Box(t, 0, 1.03f, -0.12f, 0.62f, 0.08f, 0.36f, M(Shade(d.Color, 1.6f)));
            Cyl(t, -0.12f, 1.11f, -0.2f, 0.025f, 0.1f, M(Black));
            Sph(t, -0.12f, 1.17f, -0.2f, 0.065f, 0.065f, 0.065f, M(0xd62828));
            Cyl(t, 0.06f, 1.075f, -0.2f, 0.05f, 0.02f, acc);
            Cyl(t, 0.16f, 1.075f, -0.18f, 0.05f, 0.02f, M(0xff3fa4));
            Box(t, 0, 1.42f, 0.15f, 0.62f, 0.78f, 0.4f, body);
            Box(t, 0, 1.4f, -0.055f, 0.5f, 0.42f, 0.02f, G(0x2541b2, 1.1f));
            Box(t, 0.06f, 1.43f, -0.068f, 0.08f, 0.08f, 0.006f, G(0xffd166, 1.8f));
            Box(t, -0.1f, 1.3f, -0.068f, 0.06f, 0.04f, 0.006f, G(0xff3fa4, 1.8f));
            Box(t, 0, 1.92f, 0.1f, 0.64f, 0.22f, 0.5f, body);
            Box(t, 0, 1.92f, -0.155f, 0.56f, 0.15f, 0.02f, G(d.Color2, 1.8f));
            for (int s = -1; s <= 1; s += 2)
                BoxR(t, V(s * 0.315f, 0.95f, 0.05f), V(0.01f, 1.2f, 0.08f), acc, V(25, 0, 0));
        }

        static void Pinball(Transform t, Loab.Sim.ItemDef d)
        {
            var body = M(d.Color);
            var chrome = M(Chrome);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(t, sx * 0.2f, 0.41f, -0.34f, 0.05f, 0.82f, 0.05f, chrome);
                Box(t, sx * 0.2f, 0.43f, 0.34f, 0.05f, 0.86f, 0.05f, chrome);
            }
            BoxR(t, V(0, 0.93f, -0.02f), V(0.5f, 0.2f, 0.84f), body, V(-5, 0, 0));
            BoxR(t, V(0, 1.035f, -0.02f), V(0.44f, 0.01f, 0.78f), G(d.Color2, 0.7f), V(-5, 0, 0));
            Cyl(t, -0.1f, 1.06f, 0.12f, 0.08f, 0.03f, G(0xff3fa4, 1.8f));
            Cyl(t, 0.1f, 1.065f, 0.16f, 0.08f, 0.03f, G(0x4cc9f0, 1.8f));
            Cyl(t, 0, 1.05f, 0.0f, 0.08f, 0.03f, G(0xd62828, 1.8f));
            Box(t, 0, 1.36f, 0.36f, 0.52f, 0.64f, 0.14f, body);
            Box(t, 0, 1.38f, 0.285f, 0.44f, 0.46f, 0.02f, G(d.Color2, 1.5f));
            BoxR(t, V(0, 1.4f, 0.272f), V(0.18f, 0.18f, 0.006f), G(0xff3fa4, 2f), V(0, 0, 45));
            Box(t, 0, 0.88f, -0.445f, 0.14f, 0.08f, 0.01f, chrome);
        }

        // =====================================================================================
        //  Stage & music
        // =====================================================================================

        static void Stage(Transform t, Loab.Sim.ItemDef d)
        {
            float wdt = d.W - 0.1f, dep = d.H - 0.1f;
            var black = M(Black);
            var metal = M(Metal);
            Box(t, 0, 0.165f, 0, wdt, 0.33f, dep, M(d.Color));
            Box(t, 0, 0.34f, 0, wdt, 0.02f, dep, M(d.Color2));
            Box(t, 0, 0.3f, -dep / 2f - 0.005f, wdt, 0.04f, 0.01f, M(Brass));
            // Mic stand.
            Cyl(t, 0, 0.365f, -0.45f, 0.22f, 0.03f, black);
            Rod(t, V(0, 0.38f, -0.45f), V(0, 1.45f, -0.45f), 0.025f, metal);
            Rod(t, V(0, 1.45f, -0.45f), V(0, 1.52f, -0.6f), 0.02f, metal);
            Sph(t, 0, 1.54f, -0.63f, 0.07f, 0.07f, 0.1f, M(0x333333));
            // Comedian's stool and a monitor wedge.
            Stool(t, 0.55f, -0.15f, 0.72f, 0.3f, M(0xa33a2a), black, 0.35f, true);
            BoxR(t, V(-0.7f, 0.46f, -0.7f), V(0.42f, 0.2f, 0.26f), black, V(20, 0, 0));
            // Lighting truss.
            float px = wdt / 2f - 0.1f, pz = dep / 2f - 0.1f;
            Rod(t, V(-px, 0.35f, pz), V(-px, 2.4f, pz), 0.07f, black);
            Rod(t, V(px, 0.35f, pz), V(px, 2.4f, pz), 0.07f, black);
            Rod(t, V(-px - 0.04f, 2.4f, pz), V(px + 0.04f, 2.4f, pz), 0.08f, black);
            float[] spots = { -0.8f, 0f, 0.8f };
            uint[] lens = { 0xfff1c1, 0xff6fb5, 0x4cc9f0 };
            for (int i = 0; i < spots.Length; i++)
            {
                var c = V(spots[i], 2.22f, pz - 0.05f);
                CylR(t, c, 0.16f, 0.24f, M(0x2a2a2a), V(45, 0, 0));
                CylR(t, c + V(0, -0.085f, -0.085f), 0.13f, 0.02f, G(lens[i], 2f), V(45, 0, 0));
            }
        }

        static void Speaker(Transform t, Loab.Sim.ItemDef d)
        {
            var black = M(Black);
            for (int k = 0; k < 3; k++)
            {
                float a = (90f + 120f * k) * Mathf.Deg2Rad;
                Rod(t, V(Mathf.Cos(a) * 0.32f, 0.015f, Mathf.Sin(a) * 0.32f), V(0, 0.6f, 0), 0.03f, black);
            }
            Rod(t, V(0, 0.5f, 0), V(0, 1.22f, 0), 0.045f, M(Metal));
            Box(t, 0, 1.55f, 0, 0.46f, 0.7f, 0.36f, M(d.Color));
            var face = V(90, 0, 0);
            CylR(t, V(0, 1.45f, -0.18f), 0.3f, 0.03f, M(d.Color2), face);
            CylR(t, V(0, 1.45f, -0.195f), 0.1f, 0.02f, M(0x222222), face);
            CylR(t, V(0, 1.76f, -0.185f), 0.12f, 0.02f, M(d.Color2), face);
            Box(t, 0, 1.91f, 0, 0.2f, 0.03f, 0.05f, black); // handle
        }

        static void Jukebox(Transform t, Loab.Sim.ItemDef d)
        {
            var body = M(d.Color);
            var face = V(90, 0, 0);
            Box(t, 0, 0.04f, 0.1f, 0.8f, 0.08f, 0.54f, M(Black));
            Box(t, 0, 0.64f, 0.1f, 0.76f, 1.12f, 0.5f, body);
            CylR(t, V(0, 1.2f, 0.1f), 0.76f, 0.5f, body, face);
            CylR(t, V(0, 1.2f, -0.16f), 0.66f, 0.04f, G(d.Color2, 2f), face);
            CylR(t, V(0, 1.2f, -0.175f), 0.5f, 0.04f, M(Shade(d.Color, 0.55f)), face);
            Box(t, 0, 1.14f, -0.2f, 0.36f, 0.22f, 0.02f, G(0xfff1c1, 1.4f));
            for (int s = -1; s <= 1; s += 2)
                Box(t, s * 0.31f, 0.62f, -0.165f, 0.07f, 0.95f, 0.03f, G(0xff3fa4, 2f));
            Box(t, 0, 0.4f, -0.16f, 0.44f, 0.4f, 0.02f, M(Brass));
            Box(t, 0, 0.78f, -0.17f, 0.44f, 0.06f, 0.04f, G(0x4cc9f0, 1.6f));
        }

        static void Piano(Transform t, Loab.Sim.ItemDef d)
        {
            var body = M(d.Color);
            float wdt = d.W - 0.5f;
            Box(t, 0, 0.66f, 0.17f, wdt, 1.26f, 0.46f, body);
            Box(t, 0, 1.31f, 0.15f, wdt + 0.06f, 0.04f, 0.52f, body);
            Box(t, 0, 0.72f, -0.2f, wdt - 0.08f, 0.1f, 0.3f, body);
            Box(t, 0, 0.785f, -0.22f, wdt - 0.18f, 0.03f, 0.18f, M(d.Color2));
            var blk = M(0x111111);
            float span = wdt - 0.28f;
            for (int i = 0; i < 9; i++)
                Box(t, -span / 2f + span * (i + 0.5f) / 9f, 0.81f, -0.18f, 0.035f, 0.025f, 0.1f, blk);
            for (int s = -1; s <= 1; s += 2) Cyl(t, s * (wdt / 2f - 0.1f), 0.34f, -0.2f, 0.06f, 0.68f, body);
            BoxR(t, V(0, 1.0f, -0.08f), V(0.36f, 0.24f, 0.02f), M(Cream), V(-10, 0, 0));
            Cyl(t, wdt / 2f - 0.15f, 1.4f, 0.2f, 0.06f, 0.14f, M(Brass));
            Sph(t, wdt / 2f - 0.15f, 1.5f, 0.2f, 0.05f, 0.08f, 0.05f, M(0xfff1c1));
            // Bench.
            var bench = M(Shade(d.Color, 1.3f));
            Box(t, 0, 0.45f, -0.37f, 0.8f, 0.06f, 0.24f, bench);
            Box(t, -0.34f, 0.21f, -0.37f, 0.05f, 0.42f, 0.2f, bench);
            Box(t, 0.34f, 0.21f, -0.37f, 0.05f, 0.42f, 0.2f, bench);
        }

        // =====================================================================================
        //  Screens
        // =====================================================================================

        static void Tv(Transform t, Loab.Sim.ItemDef d)
        {
            var frame = M(d.Color);
            Box(t, 0, 0.02f, 0.1f, 0.6f, 0.04f, 0.36f, frame);
            Box(t, 0, 0.7f, 0.12f, 0.08f, 1.36f, 0.06f, frame);
            Box(t, 0, 1.48f, 0.05f, 0.95f, 0.58f, 0.06f, frame);
            Box(t, 0, 1.48f, 0.012f, 0.87f, 0.5f, 0.02f, G(0x3cb043, 1.2f));          // the pitch
            Box(t, 0, 1.48f, 0.0f, 0.015f, 0.5f, 0.006f, G(White, 1.4f));
            Box(t, -0.3f, 1.68f, -0.002f, 0.2f, 0.06f, 0.006f, G(d.Color2, 1.8f));     // score bug
            Box(t, 0, 1.15f, 0.05f, 0.6f, 0.07f, 0.08f, M(0x222222));                  // soundbar
        }

        static void Projector(Transform t, Loab.Sim.ItemDef d)
        {
            var white = M(d.Color);
            var black = M(Black);
            var metal = M(Metal);
            float px = d.W / 2f - 0.12f;
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(t, V(s * px, 0.04f, 0.2f), V(s * px, 2.25f, 0.2f), 0.05f, metal);
                Box(t, s * px, 0.02f, 0.2f, 0.07f, 0.04f, 0.5f, black);
            }
            float sw = px * 2f - 0.1f;
            CylR(t, V(0, 2.2f, 0.2f), 0.1f, sw + 0.08f, black, V(0, 0, 90));
            Box(t, 0, 1.55f, 0.22f, sw, 1.2f, 0.02f, white);
            Box(t, 0, 1.6f, 0.205f, sw - 0.14f, 0.92f, 0.01f, G(0x3cb043, 1.2f));
            Box(t, 0, 1.6f, 0.198f, 0.02f, 0.92f, 0.004f, G(White, 1.4f));
            Box(t, -sw / 2f + 0.3f, 1.98f, 0.197f, 0.3f, 0.08f, 0.004f, G(d.Color2, 1.8f));
            CylR(t, V(0, 0.95f, 0.22f), 0.05f, sw, black, V(0, 0, 90));
            // Projector on a little stand.
            Box(t, 0, 0.25f, -0.3f, 0.3f, 0.5f, 0.26f, M(DarkWood));
            Box(t, 0, 0.56f, -0.3f, 0.28f, 0.12f, 0.22f, M(0x333333));
            CylR(t, V(0, 0.56f, -0.18f), 0.08f, 0.04f, G(White, 2f), V(90, 0, 0));
        }

        // =====================================================================================
        //  Lighting
        // =====================================================================================

        static void Edison(Transform t, Loab.Sim.ItemDef d)
        {
            var black = M(Black);
            var brass = M(Brass);
            var bulb = G(d.Color, 2f);
            Cyl(t, 0, 0.02f, 0, 0.4f, 0.04f, black);
            Rod(t, V(0, 0.04f, 0), V(0, 2.3f, 0), 0.05f, black);
            Rod(t, V(-0.42f, 2.3f, 0), V(0.42f, 2.3f, 0), 0.07f, M(0x5a3a22));
            float[] xs = { -0.36f, -0.16f, 0.16f, 0.36f };
            float[] ys = { 1.85f, 1.7f, 1.95f, 1.78f };
            for (int i = 0; i < 4; i++)
            {
                Rod(t, V(xs[i], 2.27f, 0), V(xs[i], ys[i] + 0.12f, 0), 0.012f, black);
                Cyl(t, xs[i], ys[i] + 0.09f, 0, 0.06f, 0.06f, brass);
                Sph(t, xs[i], ys[i], 0, 0.12f, 0.16f, 0.12f, bulb);
            }
        }

        static void Candles(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(DarkWood);
            Cyl(t, 0, 0.62f, 0, 0.55f, 0.04f, wood);
            Cyl(t, 0, 0.31f, 0, 0.07f, 0.6f, wood);
            Cyl(t, 0, 0.02f, 0, 0.36f, 0.04f, wood);
            Cyl(t, 0, 0.65f, 0, 0.36f, 0.02f, M(Brass));
            var wax = M(d.Color);
            var flame = G(d.Color2, 2.2f);
            float[,] c = { { -0.08f, -0.05f, 0.22f }, { 0.07f, -0.06f, 0.15f }, { 0f, 0.08f, 0.3f }, { 0.11f, 0.07f, 0.1f }, { -0.11f, 0.07f, 0.12f } };
            for (int i = 0; i < 5; i++)
            {
                float x = c[i, 0], z = c[i, 1], h = c[i, 2];
                Cyl(t, x, 0.66f + h / 2f, z, 0.07f, h, wax);
                Sph(t, x, 0.66f + h + 0.04f, z, 0.04f, 0.07f, 0.04f, flame);
            }
        }

        static void NeonSeg(Transform t, float x, float y, float sx, float sy, Material m, float rotZ = 0f) =>
            BoxR(t, V(x, y, 0.015f), V(sx, sy, 0.02f), m, V(0, 0, rotZ));

        static void Neon(Transform t, Loab.Sim.ItemDef d)
        {
            Box(t, 0, 0.02f, 0.05f, 0.5f, 0.04f, 0.3f, M(Black));
            Rod(t, V(0, 0.04f, 0.05f), V(0, 1.3f, 0.05f), 0.05f, M(Metal));
            Box(t, 0, 1.5f, 0.05f, 0.86f, 0.4f, 0.04f, M(0x1a1a1a));
            var m = G(d.Color, 2.2f);
            float lw = 0.15f, lh = 0.24f, s = 0.028f, y = 1.5f;
            // O
            float o = -0.3f;
            NeonSeg(t, o - lw / 2 + s / 2, y, s, lh, m); NeonSeg(t, o + lw / 2 - s / 2, y, s, lh, m);
            NeonSeg(t, o, y + lh / 2 - s / 2, lw, s, m); NeonSeg(t, o, y - lh / 2 + s / 2, lw, s, m);
            // P
            o = -0.1f;
            NeonSeg(t, o - lw / 2 + s / 2, y, s, lh, m); NeonSeg(t, o, y + lh / 2 - s / 2, lw, s, m);
            NeonSeg(t, o, y, lw, s, m); NeonSeg(t, o + lw / 2 - s / 2, y + lh / 4, s, lh / 2, m);
            // E
            o = 0.1f;
            NeonSeg(t, o - lw / 2 + s / 2, y, s, lh, m); NeonSeg(t, o, y + lh / 2 - s / 2, lw, s, m);
            NeonSeg(t, o - lw * 0.1f, y, lw * 0.8f, s, m); NeonSeg(t, o, y - lh / 2 + s / 2, lw, s, m);
            // N
            o = 0.3f;
            NeonSeg(t, o - lw / 2 + s / 2, y, s, lh, m); NeonSeg(t, o + lw / 2 - s / 2, y, s, lh, m);
            float dx = lw - s;
            NeonSeg(t, o, y, s, Mathf.Sqrt(dx * dx + lh * lh), m, Mathf.Atan2(dx, lh) * Mathf.Rad2Deg);
        }

        static void Festoon(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(DarkWood);
            var black = M(Black);
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(t, V(s * 0.42f, 0.02f, 0), V(s * 0.42f, 2.2f, 0), 0.045f, wood);
                Box(t, s * 0.42f, 0.02f, 0, 0.16f, 0.04f, 0.3f, wood);
                Sph(t, s * 0.42f, 2.22f, 0, 0.07f, 0.07f, 0.07f, M(Brass));
            }
            System.Func<float, float> sag = x => 1.92f + 0.24f * (x / 0.42f) * (x / 0.42f);
            for (int i = 0; i < 4; i++)
            {
                float x0 = -0.42f + 0.21f * i, x1 = x0 + 0.21f;
                Rod(t, V(x0, sag(x0), 0), V(x1, sag(x1), 0), 0.012f, black);
            }
            var bulb = G(d.Color, 2f);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.3f + 0.15f * i;
                Sph(t, x, sag(x) - 0.07f, 0, 0.09f, 0.11f, 0.09f, bulb);
            }
        }

        static void DiscoBall(Transform t, Loab.Sim.ItemDef d)
        {
            var black = M(Black);
            Cyl(t, 0, 0.02f, 0, 0.42f, 0.04f, black);
            Rod(t, V(0, 0.04f, 0), V(0, 1.65f, 0), 0.04f, M(Chrome));
            Cyl(t, 0, 1.7f, 0, 0.1f, 0.1f, black);
            var c = V(0, 1.99f, 0);
            float r = 0.225f;
            Part(t, PrimitiveType.Sphere, c, Vector3.one * (r * 2f), G(d.Color, 1.1f));
            uint[] cols = { White, 0xff6fb5, 0x4cc9f0 };
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad, e = (k % 2 == 0 ? 25f : -20f) * Mathf.Deg2Rad;
                var p = c + r * V(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
                Part(t, PrimitiveType.Cube, p, Vector3.one * 0.07f, G(cols[k % 3], 2.4f), V(0, k * 60f, 0));
            }
        }

        static void Chandelier(Transform t, Loab.Sim.ItemDef d)
        {
            var brass = M(d.Color);
            // Arc-lamp style brass stand so it can sit anywhere on the floor.
            Cyl(t, 0.3f, 0.03f, 0.3f, 0.3f, 0.06f, brass);
            Rod(t, V(0.3f, 0.06f, 0.3f), V(0.3f, 2.45f, 0.3f), 0.05f, brass);
            Rod(t, V(0.3f, 2.45f, 0.3f), V(0, 2.45f, 0), 0.04f, brass);
            Sph(t, 0.3f, 2.48f, 0.3f, 0.07f, 0.07f, 0.07f, brass);
            Rod(t, V(0, 2.45f, 0), V(0, 2.08f, 0), 0.02f, brass);
            Sph(t, 0, 1.98f, 0, 0.16f, 0.24f, 0.16f, brass);
            Cyl(t, 0, 1.9f, 0, 0.62f, 0.03f, brass);
            var candle = M(Cream);
            var flame = G(d.Color2, 2f);
            for (int k = 0; k < 5; k++)
            {
                float a = (k * 72f + 18f) * Mathf.Deg2Rad;
                float x = Mathf.Cos(a) * 0.26f, z = Mathf.Sin(a) * 0.26f;
                Cyl(t, x, 1.96f, z, 0.04f, 0.1f, candle);
                Sph(t, x, 2.04f, z, 0.06f, 0.09f, 0.06f, flame);
            }
            var crystal = G(0xe6f4ff, 0.6f);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                Sph(t, Mathf.Cos(a) * 0.18f, 1.82f, Mathf.Sin(a) * 0.18f, 0.05f, 0.08f, 0.05f, crystal);
            }
            Sph(t, 0, 1.8f, 0, 0.07f, 0.12f, 0.07f, crystal);
        }

        // =====================================================================================
        //  Decor
        // =====================================================================================

        static void Plant(Transform t, Loab.Sim.ItemDef d)
        {
            var pot = M(d.Color2);
            var leaf = M(d.Color);
            var leaf2 = M(Shade(d.Color, 1.25f));
            Cyl(t, 0, 0.21f, 0, 0.42f, 0.42f, pot);
            Cyl(t, 0, 0.43f, 0, 0.48f, 0.06f, pot);
            Cyl(t, 0, 0.45f, 0, 0.4f, 0.02f, M(0x3b2a1a));
            var top = V(0.03f, 1.25f, 0.02f);
            Rod(t, V(0, 0.44f, 0), top, 0.08f, M(0x7a5a3a));
            for (int i = 0; i < 7; i++)
            {
                float yaw = i * 360f / 7f, a = yaw * Mathf.Deg2Rad;
                var dir = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                Part(t, PrimitiveType.Sphere, top + dir * 0.22f + V(0, -0.07f, 0), V(0.2f, 0.05f, 0.5f), i % 2 == 0 ? leaf : leaf2, V(25, yaw, 0));
            }
            Sph(t, top.x, top.y + 0.03f, top.z, 0.2f, 0.18f, 0.2f, leaf);
        }

        static void Fern(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color2);
            var leaf = M(d.Color);
            var leaf2 = M(Shade(d.Color, 1.2f));
            Cyl(t, 0, 0.8f, 0, 0.5f, 0.04f, wood);
            Cyl(t, 0, 0.3f, 0, 0.4f, 0.03f, wood);
            for (int k = 0; k < 3; k++)
            {
                float a = (90f + 120f * k) * Mathf.Deg2Rad;
                Rod(t, V(Mathf.Cos(a) * 0.24f, 0, Mathf.Sin(a) * 0.24f), V(Mathf.Cos(a) * 0.19f, 0.8f, Mathf.Sin(a) * 0.19f), 0.04f, wood);
            }
            Cyl(t, 0, 0.94f, 0, 0.32f, 0.24f, M(Cream));
            for (int i = 0; i < 7; i++)
            {
                float yaw = i * 360f / 7f + 10f, a = yaw * Mathf.Deg2Rad;
                var dir = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                Part(t, PrimitiveType.Sphere, V(0, 1.03f, 0) + dir * 0.2f, V(0.14f, 0.05f, 0.5f), i % 2 == 0 ? leaf : leaf2, V(40, yaw, 0));
            }
            Sph(t, 0, 1.1f, 0, 0.3f, 0.16f, 0.3f, leaf);
        }

        static void Frames(Transform t, Loab.Sim.ItemDef d)
        {
            var leg = M(0x3a2a1a);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, s * 0.33f, 0.85f, 0.09f, 0.05f, 1.7f, 0.05f, leg);
                Box(t, s * 0.33f, 0.02f, 0.09f, 0.07f, 0.04f, 0.5f, leg);
            }
            Box(t, 0, 0.5f, 0.09f, 0.62f, 0.04f, 0.03f, leg);
            Box(t, 0, 1.3f, 0.05f, 0.62f, 0.56f, 0.05f, M(d.Color));
            Box(t, 0, 1.3f, 0.02f, 0.5f, 0.44f, 0.02f, M(d.Color2));
            const float fz = 0.006f;
            switch (d.Id)
            {
                case "photos":
                    var sil = M(Shade(d.Color2, 0.6f));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Box(t, s * 0.1f, 1.24f, fz, 0.12f, 0.2f, 0.01f, sil);
                        Sph(t, s * 0.1f, 1.39f, fz, 0.08f, 0.08f, 0.01f, sil);
                    }
                    break;
                case "mirror":
                    BoxR(t, V(-0.1f, 1.34f, fz), V(0.04f, 0.3f, 0.005f), M(White), V(0, 0, 30));
                    BoxR(t, V(0.0f, 1.36f, fz), V(0.02f, 0.2f, 0.005f), M(White), V(0, 0, 30));
                    Box(t, 0, 1.15f, fz, 0.36f, 0.04f, 0.005f, M(Brass));
                    break;
                case "fish":
                    Part(t, PrimitiveType.Capsule, V(-0.03f, 1.3f, fz), V(0.12f, 0.17f, 0.04f), M(0x8fa36b), V(0, 0, 90));
                    BoxR(t, V(0.17f, 1.3f, fz), V(0.09f, 0.09f, 0.03f), M(0x8fa36b), V(0, 0, 45));
                    Sph(t, -0.15f, 1.32f, -0.012f, 0.025f, 0.025f, 0.01f, M(0x111111));
                    break;
                case "pennants":
                    for (int i = -1; i <= 1; i++)
                        BoxR(t, V(i * 0.14f, 1.3f, fz), V(0.1f, 0.1f, 0.005f), M(i == 0 ? White : 0xffd166), V(0, 0, 45));
                    Box(t, 0, 1.4f, fz, 0.44f, 0.015f, 0.005f, M(White));
                    break;
                case "gallery_prints":
                    Box(t, -0.1f, 1.36f, fz, 0.18f, 0.22f, 0.005f, M(0xe76f51));
                    Box(t, 0.08f, 1.24f, fz, 0.2f, 0.14f, 0.005f, M(0x2a9d8f));
                    Sph(t, 0.12f, 1.42f, fz, 0.12f, 0.12f, 0.005f, M(0xe9c46a));
                    break;
                case "records":
                    for (int s = -1; s <= 1; s += 2)
                    {
                        CylR(t, V(s * 0.12f, 1.3f, fz), 0.2f, 0.01f, M(0x111111), V(90, 0, 0));
                        CylR(t, V(s * 0.12f, 1.3f, fz - 0.007f), 0.07f, 0.006f, M(s < 0 ? 0xd62828u : 0x2e86deu), V(90, 0, 0));
                    }
                    break;
            }
        }

        // ---- Craft Beer Hall and Lounge pieces (task K1) ----------------------------------------------

        // A dark board against the wall with a row of chrome taps and chalk labels.
        static void TapWall(Transform t, Loab.Sim.ItemDef d)
        {
            var board = M(d.Color);
            var chrome = M(d.Color2);
            var chalk = M(0xf1f1f1);
            const float z = 0.38f;
            Box(t, 0, 1.2f, z, 1.86f, 1.5f, 0.06f, board);
            Box(t, 0, 0.45f, z - 0.12f, 1.86f, 0.9f, 0.3f, M(Shade(d.Color, 1.4f)));       // drip tray cabinet
            Box(t, 0, 0.91f, z - 0.14f, 1.8f, 0.03f, 0.34f, chrome);
            for (int i = 0; i < 8; i++)
            {
                float x = -0.77f + i * 0.22f;
                Rod(t, V(x, 1.0f, z - 0.04f), V(x, 1.0f, z - 0.2f), 0.035f, chrome);
                Box(t, x, 1.12f, z - 0.2f, 0.03f, 0.16f, 0.03f, M(i % 3 == 0 ? 0xe9c46au : i % 3 == 1 ? 0xb5651du : 0x2a2a2au)); // tap handles
                Box(t, x, 1.45f, z - 0.035f, 0.16f, 0.07f, 0.005f, chalk);
            }
            Box(t, 0, 1.82f, z - 0.035f, 1.2f, 0.12f, 0.005f, M(0xffd166));
        }

        // A pyramid of steel kegs.
        static void Kegs(Transform t, Loab.Sim.ItemDef d)
        {
            var steel = M(d.Color);
            var band = M(d.Color2);
            Vector3[] at = { V(-0.2f, 0.25f, -0.15f), V(0.2f, 0.25f, -0.15f), V(0, 0.25f, 0.22f), V(0, 0.75f, 0.0f) };
            foreach (var p in at)
            {
                Cyl(t, p.x, p.y, p.z, 0.38f, 0.5f, steel);
                Cyl(t, p.x, p.y + 0.17f, p.z, 0.395f, 0.04f, band);
                Cyl(t, p.x, p.y - 0.17f, p.z, 0.395f, 0.04f, band);
            }
        }

        // Floor-to-ceiling wine shelving: a wooden frame with rows of bottle ends.
        static void WineRack(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            var wine = M(d.Color2);
            var white = M(0xe9e2c8);
            const float z = 0.3f;
            Box(t, 0, 1.1f, z + 0.12f, 1.86f, 2.2f, 0.05f, wood);
            for (int s = -1; s <= 1; s += 2) Box(t, s * 0.91f, 1.1f, z, 0.05f, 2.2f, 0.3f, wood);
            Box(t, 0, 2.18f, z, 1.86f, 0.05f, 0.3f, wood);
            for (int row = 0; row < 6; row++)
            {
                float y = 0.25f + row * 0.34f;
                Box(t, 0, y - 0.11f, z, 1.78f, 0.03f, 0.3f, wood);
                for (int i = 0; i < 9; i++)
                {
                    float x = -0.8f + i * 0.2f;
                    var m = (i + row) % 4 == 0 ? white : wine;
                    CylR(t, V(x, y, z - 0.14f), 0.08f, 0.06f, m, V(90, 0, 0));
                }
            }
        }

        // ---- K2 hero pieces ---------------------------------------------------------------------------

        // A copper pot still: a round pot, a neck and a coiled pipe on a wooden stand.
        static void CopperStill(Transform t, Loab.Sim.ItemDef d)
        {
            var copper = M(d.Color);
            var wood = M(d.Color2);
            Box(t, 0, 0.15f, 0, 0.8f, 0.3f, 0.8f, wood);
            Sph(t, 0, 0.62f, 0, 0.62f, 0.62f, 0.62f, copper);
            Cyl(t, 0, 1.05f, 0, 0.22f, 0.35f, copper);
            Sph(t, 0, 1.28f, 0, 0.3f, 0.26f, 0.3f, copper);
            Rod(t, V(0, 1.35f, 0), V(0.32f, 1.25f, 0.1f), 0.07f, copper);
            Rod(t, V(0.32f, 1.25f, 0.1f), V(0.32f, 0.55f, 0.1f), 0.07f, copper);
            for (int i = 0; i < 4; i++) CylR(t, V(0.32f, 0.65f + i * 0.14f, 0.1f), 0.2f, 0.04f, copper, V(0, 0, 0));
        }

        // A glass-fronted cabinet of trophies.
        static void TrophyCabinet(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            var gold = M(d.Color2);
            const float z = 0.25f;
            Box(t, 0, 0.95f, z + 0.13f, 0.86f, 1.9f, 0.04f, wood);
            for (int s = -1; s <= 1; s += 2) Box(t, s * 0.42f, 0.95f, z, 0.04f, 1.9f, 0.3f, wood);
            Box(t, 0, 1.88f, z, 0.88f, 0.04f, 0.3f, wood);
            Box(t, 0, 0.06f, z, 0.84f, 0.12f, 0.3f, wood);
            float[] shelves = { 0.62f, 1.2f };
            foreach (var y in shelves) Box(t, 0, y, z, 0.8f, 0.03f, 0.28f, wood);
            float[] rows = { 0.14f, 0.64f, 1.22f };
            for (int r = 0; r < 3; r++)
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 0.24f, y = rows[r];
                    Cyl(t, x, y + 0.04f, z, 0.1f, 0.08f, wood);
                    Cyl(t, x, y + 0.16f, z, 0.04f, 0.16f, gold);
                    Sph(t, x, y + 0.3f, z, 0.16f, 0.12f, 0.16f, gold);
                }
            Box(t, 0, 0.95f, z - 0.15f, 0.8f, 1.8f, 0.01f, M(0xcfe8ff));
        }

        // A photo booth: a red box with a curtain, a little screen and a light.
        static void PhotoBooth(Transform t, Loab.Sim.ItemDef d)
        {
            var body = M(d.Color);
            var trim = M(d.Color2);
            Box(t, 0, 1.0f, 0.05f, 0.9f, 2.0f, 0.8f, body);
            Box(t, 0, 2.05f, 0.05f, 0.94f, 0.1f, 0.84f, trim);
            Box(t, -0.2f, 1.0f, -0.36f, 0.44f, 1.6f, 0.03f, M(0x2b1b10));      // curtain
            Box(t, 0.28f, 1.35f, -0.36f, 0.22f, 0.16f, 0.03f, G(0x4cc9f0, 1.6f)); // screen
            Box(t, 0.28f, 1.7f, -0.36f, 0.22f, 0.12f, 0.03f, trim);
            Box(t, 0.28f, 1.05f, -0.36f, 0.12f, 0.2f, 0.03f, M(0x111111));      // photo slot
        }

        static void Flamingo(Transform t, Loab.Sim.ItemDef d)
        {
            Box(t, 0, 0.02f, 0.06f, 0.5f, 0.04f, 0.3f, M(Black));
            Rod(t, V(0, 0.04f, 0.06f), V(0, 0.95f, 0.06f), 0.05f, M(Metal));
            Box(t, 0, 1.4f, 0.06f, 0.7f, 0.9f, 0.04f, M(0x1a1a1a));
            var neon = G(d.Color, 2.2f);
            const float z = 0.025f;
            Part(t, PrimitiveType.Sphere, V(0.06f, 1.3f, z), V(0.3f, 0.17f, 0.04f), neon, V(0, 0, -12));
            Rod(t, V(-0.06f, 1.34f, z), V(-0.13f, 1.55f, z), 0.03f, neon);
            Rod(t, V(-0.13f, 1.55f, z), V(-0.05f, 1.72f, z), 0.03f, neon);
            Sph(t, -0.05f, 1.75f, z, 0.08f, 0.08f, 0.04f, neon);
            Rod(t, V(-0.07f, 1.75f, z), V(-0.15f, 1.69f, z), 0.025f, G(0xffd166, 2f));
            Rod(t, V(0.05f, 1.22f, z), V(0.05f, 0.98f, z), 0.02f, neon);
            Rod(t, V(0.1f, 1.22f, z), V(0.14f, 1.1f, z), 0.02f, neon);
            Rod(t, V(0.14f, 1.1f, z), V(0.05f, 1.1f, z), 0.02f, neon);
            Part(t, PrimitiveType.Sphere, V(0.2f, 1.33f, z), V(0.1f, 0.05f, 0.03f), neon, V(0, 0, 25)); // tail flick
        }

        static void Bookshelf(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(d.Color);
            Box(t, 0, 1.0f, 0.43f, 0.88f, 2.0f, 0.04f, wood);
            Box(t, -0.42f, 1.0f, 0.25f, 0.04f, 2.0f, 0.4f, wood);
            Box(t, 0.42f, 1.0f, 0.25f, 0.04f, 2.0f, 0.4f, wood);
            Box(t, 0, 1.98f, 0.25f, 0.92f, 0.04f, 0.42f, wood);
            Box(t, 0, 0.05f, 0.25f, 0.84f, 0.1f, 0.4f, wood);
            float[] shelves = { 0.6f, 1.1f, 1.55f };
            foreach (var y in shelves) Box(t, 0, y, 0.25f, 0.8f, 0.03f, 0.38f, wood);
            uint[] cols = { d.Color2, 0x2a4d69, 0xe9c46a, 0x386641, 0x6d597a };
            float[] bases = { 0.1f, 0.615f, 1.115f };
            float[] widths = { 0.24f, 0.28f, 0.2f };
            for (int r = 0; r < 3; r++)
            {
                float x = -0.38f;
                for (int b = 0; b < 3; b++)
                {
                    float w = widths[(b + r) % 3], h = 0.32f + ((b + r * 2) % 3) * 0.04f;
                    Box(t, x + w / 2f, bases[r] + h / 2f, 0.27f, w - 0.02f, h, 0.28f, M(cols[(b + r * 2) % cols.Length]));
                    x += w + 0.01f;
                }
            }
            Sph(t, 0.18f, 1.65f, 0.25f, 0.16f, 0.16f, 0.16f, M(0x9ec5d6)); // globe
            BoxR(t, V(-0.18f, 1.72f, 0.27f), V(0.06f, 0.3f, 0.24f), M(cols[1]), V(0, 0, 12)); // leaning book
        }

        static void Curtains(Transform t, Loab.Sim.ItemDef d)
        {
            var fabric = M(d.Color);
            var fold = M(Shade(d.Color, 0.8f));
            var brass = M(d.Color2);
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(t, V(s * 0.44f, 0.04f, 0.1f), V(s * 0.44f, 2.35f, 0.1f), 0.04f, brass);
                Box(t, s * 0.44f, 0.02f, 0.1f, 0.08f, 0.04f, 0.4f, brass);
            }
            Rod(t, V(-0.46f, 2.32f, 0.05f), V(0.46f, 2.32f, 0.05f), 0.04f, brass);
            Box(t, 0, 2.22f, 0.02f, 0.86f, 0.18f, 0.06f, fabric);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = s * (0.38f - 0.11f * i);
                    Cyl(t, x, 1.08f, 0.05f - (i % 2) * 0.03f, 0.13f, 2.08f, i % 2 == 0 ? fabric : fold);
                }
                Sph(t, s * 0.3f, 0.95f, 0.0f, 0.1f, 0.1f, 0.1f, brass); // tie-back
            }
        }

        static void Pipes(Transform t, Loab.Sim.ItemDef d)
        {
            var pipe = M(d.Color);
            var cop = M(d.Color2);
            const float z = 0.05f, pd = 0.09f;
            Box(t, 0, 0.03f, z, 0.8f, 0.06f, 0.5f, M(0x333333));
            Rod(t, V(-0.3f, 0.06f, z), V(-0.3f, 1.7f, z), pd, pipe);
            Rod(t, V(0.3f, 0.06f, z), V(0.3f, 1.2f, z), pd, pipe);
            Rod(t, V(-0.3f, 1.7f, z), V(0.1f, 1.7f, z), pd, pipe);
            Rod(t, V(0.1f, 1.7f, z), V(0.1f, 1.38f, z), pd, pipe);
            Rod(t, V(0.3f, 1.2f, z), V(-0.3f, 1.2f, z), pd, pipe);
            Sph(t, -0.3f, 1.7f, z, 0.13f, 0.13f, 0.13f, cop);
            Sph(t, 0.1f, 1.7f, z, 0.13f, 0.13f, 0.13f, cop);
            Sph(t, 0.3f, 1.2f, z, 0.13f, 0.13f, 0.13f, cop);
            Sph(t, -0.3f, 1.2f, z, 0.13f, 0.13f, 0.13f, cop);
            CylR(t, V(0.1f, 1.38f, z), 0.16f, 0.06f, cop, Vector3.zero);                 // pipe end flange
            Rod(t, V(-0.3f, 0.6f, z), V(0.3f, 0.6f, z), 0.06f, cop);
            Rod(t, V(0, 1.2f, z), V(0, 1.2f, -0.07f), 0.03f, M(Metal));
            CylR(t, V(0, 1.2f, -0.08f), 0.2f, 0.03f, M(0xc0392b), V(90, 0, 0));         // valve wheel
            CylR(t, V(0.3f, 0.9f, -0.0f), 0.17f, 0.04f, cop, V(90, 0, 0));
            CylR(t, V(0.3f, 0.9f, -0.015f), 0.14f, 0.02f, M(White), V(90, 0, 0));       // gauge face
            Box(t, 0.3f, 0.92f, -0.03f, 0.012f, 0.05f, 0.005f, M(0xd62828));
        }

        static void Chalkboard(Transform t, Loab.Sim.ItemDef d)
        {
            var wood = M(MidWood);
            var board = M(d.Color);
            var chalk = M(d.Color2);
            var f = Group(t, V(0, 0.5f, -0.1f), V(14, 0, 0));
            Box(f, 0, 0, 0, 0.62f, 0.98f, 0.04f, wood);
            Box(f, 0, 0.02f, -0.022f, 0.52f, 0.8f, 0.01f, board);
            Box(f, 0, 0.3f, -0.03f, 0.34f, 0.04f, 0.005f, chalk);
            Box(f, -0.02f, 0.16f, -0.03f, 0.4f, 0.02f, 0.005f, chalk);
            Box(f, -0.06f, 0.08f, -0.03f, 0.32f, 0.02f, 0.005f, chalk);
            Box(f, 0.0f, 0.0f, -0.03f, 0.36f, 0.02f, 0.005f, chalk);
            Box(f, -0.12f, -0.22f, -0.03f, 0.08f, 0.12f, 0.005f, M(0xffd166));
            Box(f, -0.12f, -0.145f, -0.03f, 0.08f, 0.03f, 0.005f, chalk);
            Sph(f, 0.12f, -0.22f, -0.03f, 0.08f, 0.08f, 0.005f, M(0xff6fb5));
            var b = Group(t, V(0, 0.5f, 0.1f), V(-14, 0, 0));
            Box(b, 0, 0, 0, 0.62f, 0.98f, 0.04f, wood);
            Box(b, 0, 0.02f, 0.022f, 0.52f, 0.8f, 0.01f, board);
        }

        static void FishTank(Transform t, Loab.Sim.ItemDef d)
        {
            var frame = M(0x1a1a1a);
            float wdt = d.W - 0.2f, dep = d.H - 0.4f, fz = -dep / 2f - 0.008f;
            Box(t, 0, 0.35f, 0, wdt, 0.7f, dep + 0.1f, M(d.Color2));
            Box(t, 0, 1.1f, 0, wdt - 0.1f, 0.78f, dep, G(d.Color, 0.9f));
            Box(t, 0, 1.52f, 0, wdt - 0.02f, 0.06f, dep + 0.08f, frame);
            Box(t, 0, 0.72f, 0, wdt - 0.02f, 0.04f, dep + 0.08f, frame);
            for (int s = -1; s <= 1; s += 2)
                Box(t, s * (wdt / 2f - 0.065f), 1.1f, -dep / 2f + 0.01f, 0.04f, 0.78f, 0.04f, frame);
            Box(t, 0, 0.8f, fz + 0.003f, wdt - 0.1f, 0.12f, 0.01f, M(0xd9c9a3));
            var weed = M(0x2d6a4f);
            Box(t, -0.6f, 0.98f, fz, 0.06f, 0.36f, 0.01f, weed);
            Box(t, -0.5f, 0.92f, fz, 0.05f, 0.24f, 0.01f, weed);
            Box(t, 0.62f, 0.96f, fz, 0.06f, 0.32f, 0.01f, weed);
            float[,] fish = { { -0.2f, 1.2f }, { 0.25f, 1.05f }, { 0.42f, 1.3f } };
            uint[] fc = { 0xff8c42, 0xffd166, 0xff4d6d };
            for (int i = 0; i < 3; i++)
            {
                var m = G(fc[i], 1.2f);
                Sph(t, fish[i, 0], fish[i, 1], fz - 0.002f, 0.14f, 0.08f, 0.01f, m);
                BoxR(t, V(fish[i, 0] + 0.08f, fish[i, 1], fz - 0.002f), V(0.06f, 0.06f, 0.01f), m, V(0, 0, 45));
            }
            Sph(t, 0.05f, 1.36f, fz, 0.04f, 0.04f, 0.01f, G(White, 1.2f));
            Sph(t, 0.08f, 1.26f, fz, 0.03f, 0.03f, 0.01f, G(White, 1.2f));
        }

        static void Fireplace(Transform t, Loab.Sim.ItemDef d)
        {
            var brick = M(d.Color);
            var wood = M(DarkWood);
            float wdt = d.W - 0.2f;
            Box(t, 0, 0.58f, 0.2f, wdt, 1.16f, 0.45f, brick);
            Box(t, 0, 0.42f, -0.04f, 0.9f, 0.7f, 0.04f, M(0x151010));
            Box(t, 0, 1.2f, 0.13f, wdt + 0.1f, 0.08f, 0.55f, wood);
            Box(t, 0, 1.85f, 0.25f, wdt * 0.6f, 1.2f, 0.34f, brick);
            Box(t, 0, 0.03f, -0.3f, wdt - 0.2f, 0.06f, 0.36f, M(Metal));
            var log = M(0x5a3a22);
            CylR(t, V(0, 0.12f, -0.12f), 0.1f, 0.6f, log, V(0, 15, 90));
            CylR(t, V(0, 0.13f, -0.13f), 0.1f, 0.6f, log, V(0, -15, 90));
            Sph(t, -0.14f, 0.3f, -0.12f, 0.18f, 0.32f, 0.12f, G(d.Color2, 2.2f));
            Sph(t, 0.13f, 0.28f, -0.12f, 0.16f, 0.28f, 0.12f, G(d.Color2, 2.2f));
            Sph(t, 0, 0.36f, -0.14f, 0.12f, 0.3f, 0.1f, G(0xffd166, 2.4f));
            // Mantel bits: clock and two vases.
            Box(t, 0, 1.35f, 0.0f, 0.2f, 0.22f, 0.1f, wood);
            CylR(t, V(0, 1.37f, -0.055f), 0.14f, 0.01f, M(Cream), V(90, 0, 0));
            for (int s = -1; s <= 1; s += 2) Cyl(t, s * (wdt / 2f - 0.15f), 1.32f, 0.05f, 0.08f, 0.16f, M(Brass));
        }

        static void Rug(Transform t, Loab.Sim.ItemDef d)
        {
            float w = d.W - 0.1f, h = d.H - 0.1f;
            Box(t, 0, 0.01f, 0, w, 0.02f, h, M(d.Color2));
            Box(t, 0, 0.013f, 0, w - 0.2f, 0.026f, h - 0.2f, M(d.Color));
            BoxR(t, V(0, 0.016f, 0), V(0.6f, 0.032f, 0.6f), M(d.Color2), V(0, 45, 0));
            BoxR(t, V(0, 0.019f, 0), V(0.32f, 0.038f, 0.32f), M(Shade(d.Color, 0.7f)), V(0, 45, 0));
        }

        static void DogBed(Transform t, Loab.Sim.ItemDef d)
        {
            var bed = M(d.Color);
            var dog = M(d.Color2);
            var ear = M(Shade(d.Color2, 0.6f));
            Box(t, 0, 0.04f, 0, 0.8f, 0.08f, 0.76f, bed);
            Box(t, 0, 0.1f, -0.02f, 0.6f, 0.06f, 0.5f, M(Cream));
            Part(t, PrimitiveType.Capsule, V(0, 0.12f, 0.28f), V(0.18f, 0.38f, 0.18f), bed, V(0, 0, 90));
            Part(t, PrimitiveType.Capsule, V(-0.32f, 0.12f, 0.0f), V(0.16f, 0.3f, 0.16f), bed, V(90, 0, 0));
            Part(t, PrimitiveType.Capsule, V(0.32f, 0.12f, 0.0f), V(0.16f, 0.3f, 0.16f), bed, V(90, 0, 0));
            // Biscuit, curled up asleep.
            Sph(t, 0.03f, 0.22f, 0.0f, 0.42f, 0.22f, 0.3f, dog);
            Sph(t, -0.18f, 0.25f, -0.1f, 0.18f, 0.16f, 0.18f, dog);
            Sph(t, -0.27f, 0.23f, -0.15f, 0.1f, 0.08f, 0.1f, M(Shade(d.Color2, 1.15f)));
            Sph(t, -0.31f, 0.24f, -0.18f, 0.035f, 0.035f, 0.035f, M(0x111111));
            Part(t, PrimitiveType.Sphere, V(-0.13f, 0.31f, -0.04f), V(0.06f, 0.03f, 0.1f), ear, V(0, 30, 20));
            Part(t, PrimitiveType.Sphere, V(-0.23f, 0.31f, -0.02f), V(0.06f, 0.03f, 0.1f), ear, V(0, -20, -20));
            Part(t, PrimitiveType.Capsule, V(0.24f, 0.2f, -0.1f), V(0.05f, 0.09f, 0.05f), dog, V(70, -30, 0));
        }
    }
}
