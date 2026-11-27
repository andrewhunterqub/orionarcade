using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    public struct Cell : IEquatable<Cell>
    {
        public int x, y;
        public Cell(int x, int y) { this.x = x; this.y = y; }
        public bool Equals(Cell o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => x * 73856093 ^ y * 19349663;
        public override string ToString() => $"({x},{y})";
        public static Cell operator +(Cell a, Cell b) => new Cell(a.x + b.x, a.y + b.y);
    }

    /// <summary>Grid occupancy, placement rules and pathfinding for the bar floor.</summary>
    public sealed class Layout
    {
        public readonly int W, H;
        public readonly Cell Door;     // inside cell next to the entrance
        public readonly Cell Outside;  // spawn/despawn point on the street
        readonly int[] _occ;           // item uid per cell, 0 = empty
        readonly bool[] _walk;
        readonly GameState _s;

        public Layout(GameState s)
        {
            _s = s;
            W = s.gridW; H = s.gridH;
            Door = new Cell(s.doorX, 0);
            Outside = new Cell(s.doorX, -1);
            _occ = new int[W * H];
            _walk = new bool[W * H];
            Rebuild();
        }

        public void Rebuild()
        {
            Array.Clear(_occ, 0, _occ.Length);
            for (int i = 0; i < _walk.Length; i++) _walk[i] = true;
            foreach (var it in _s.items)
            {
                var d = Content.Item(it.id);
                if (d == null) continue;
                foreach (var c in Footprint(it))
                {
                    if (!InBounds(c)) continue;
                    _occ[c.y * W + c.x] = it.uid;
                    if (!d.Walkable) _walk[c.y * W + c.x] = false;
                }
            }
        }

        public bool InBounds(Cell c) => c.x >= 0 && c.y >= 0 && c.x < W && c.y < H;
        public bool Walkable(Cell c) => (InBounds(c) && _walk[c.y * W + c.x]) || c.Equals(Outside);
        public int ItemAt(Cell c) => InBounds(c) ? _occ[c.y * W + c.x] : 0;

        public static void Size(ItemDef d, int rot, out int w, out int h)
        {
            if ((rot & 1) == 0) { w = d.W; h = d.H; } else { w = d.H; h = d.W; }
        }

        public static IEnumerable<Cell> Footprint(PlacedItem it)
        {
            var d = Content.Item(it.id);
            Size(d, it.rot, out int w, out int h);
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    yield return new Cell(it.x + dx, it.y + dy);
        }

        /// <summary>Direction the front of an item faces (rot 0 faces the door side, y-).</summary>
        public static Cell Facing(int rot) => rot switch
        {
            0 => new Cell(0, -1),
            1 => new Cell(-1, 0),
            2 => new Cell(0, 1),
            _ => new Cell(1, 0),
        };

        /// <summary>Cells where customers stand to be served at a bar counter.</summary>
        public static List<Cell> ServicePoints(PlacedItem it)
        {
            var d = Content.Item(it.id);
            Size(d, it.rot, out int w, out int h);
            var list = new List<Cell>();
            switch (it.rot)
            {
                case 0: for (int i = 0; i < w; i++) list.Add(new Cell(it.x + i, it.y - 1)); break;
                case 2: for (int i = 0; i < w; i++) list.Add(new Cell(it.x + i, it.y + h)); break;
                case 1: for (int i = 0; i < h; i++) list.Add(new Cell(it.x - 1, it.y + i)); break;
                default: for (int i = 0; i < h; i++) list.Add(new Cell(it.x + w, it.y + i)); break;
            }
            return list;
        }

        /// <summary>Walkable cells touching an item (where customers stand to use it).</summary>
        public List<Cell> Approaches(PlacedItem it)
        {
            var d = Content.Item(it.id);
            if (d.Fn == ItemFn.BarCounter) return ServicePoints(it).FindAll(Walkable);
            Size(d, it.rot, out int w, out int h);
            var list = new List<Cell>();
            for (int dx = -1; dx <= w; dx++)
                for (int dy = -1; dy <= h; dy++)
                {
                    bool edge = dx == -1 || dy == -1 || dx == w || dy == h;
                    bool corner = (dx == -1 || dx == w) && (dy == -1 || dy == h);
                    if (!edge || corner) continue;
                    var c = new Cell(it.x + dx, it.y + dy);
                    if (Walkable(c) && InBounds(c)) list.Add(c);
                }
            return list;
        }

        // ---- Placement ---------------------------------------------------------------------------

        public string CanPlace(string itemId, int x, int y, int rot, int ignoreUid = 0)
        {
            var d = Content.Item(itemId);
            if (d == null) return "Unknown item";
            Size(d, rot, out int w, out int h);
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                {
                    var c = new Cell(x + dx, y + dy);
                    if (!InBounds(c)) return "Doesn't fit there";
                    int o = ItemAt(c);
                    if (o != 0 && o != ignoreUid)
                    {
                        // Rugs can sit under things and things can sit on rugs.
                        var other = _s.Item(o);
                        bool rugInvolved = d.Walkable || (other != null && Content.Item(other.id).Walkable);
                        if (!rugInvolved) return "Something's already there";
                    }
                    if (!d.Walkable && (c.Equals(Door) || c.Equals(new Cell(Door.x, 1)))) return "Keep the entrance clear";
                }

            // Simulate the placement and make sure everything stays reachable.
            var temp = new PlacedItem { uid = -1, id = itemId, x = x, y = y, rot = rot };
            var saved = new List<PlacedItem>(_s.items);
            if (ignoreUid != 0) _s.items.RemoveAll(i => i.uid == ignoreUid);
            _s.items.Add(temp);
            Rebuild();
            string err = CheckReachability();
            _s.items.Clear();
            _s.items.AddRange(saved);
            Rebuild();
            return err;
        }

        /// <summary>Null if every functional item can be reached from the door.</summary>
        public string CheckReachability()
        {
            var reach = ReachableFromDoor();
            foreach (var it in _s.items)
            {
                var d = Content.Item(it.id);
                if (d.Walkable) continue;
                bool needsAccess = d.Fn != ItemFn.Decor && d.Fn != ItemFn.Speaker && d.Fn != ItemFn.Screen && d.Fn != ItemFn.Stage;
                if (!needsAccess) continue;
                var app = Approaches(it);
                if (!app.Exists(c => reach.Contains(c)))
                    return d.Fn == ItemFn.BarCounter ? "Customers couldn't reach the front of the bar" : $"Customers couldn't reach the {d.Name}";
            }
            return null;
        }

        public HashSet<Cell> ReachableFromDoor()
        {
            var seen = new HashSet<Cell>();
            if (!Walkable(Door)) return seen;
            var q = new Queue<Cell>();
            q.Enqueue(Door); seen.Add(Door);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var n in Neighbours4(c))
                    if (InBounds(n) && Walkable(n) && seen.Add(n)) q.Enqueue(n);
            }
            return seen;
        }

        static readonly Cell[] Dirs4 = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };
        static readonly Cell[] Dirs8 =
        {
            new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1),
            new Cell(1, 1), new Cell(1, -1), new Cell(-1, 1), new Cell(-1, -1),
        };

        static IEnumerable<Cell> Neighbours4(Cell c) { foreach (var d in Dirs4) yield return c + d; }

        // ---- A* -------------------------------------------------------------------------------------

        readonly Dictionary<long, List<Cell>> _pathCache = new Dictionary<long, List<Cell>>();
        public void ClearPathCache() => _pathCache.Clear();

        static long Key(Cell a, Cell b) => ((long)(a.x + 8) << 48) | ((long)(a.y + 8) << 32) | ((long)(b.x + 8) << 16) | (long)(b.y + 8);

        /// <summary>Path from a to b (excluding a, including b). Empty if a == b; null if unreachable.</summary>
        public List<Cell> Path(Cell a, Cell b)
        {
            if (a.Equals(b)) return new List<Cell>();
            long k = Key(a, b);
            if (_pathCache.TryGetValue(k, out var cached)) return cached;
            var result = AStar(a, b);
            _pathCache[k] = result;
            return result;
        }

        List<Cell> AStar(Cell a, Cell b)
        {
            if (!Walkable(b)) return null;
            int Idx(Cell c) => (c.y + 1) * W + c.x; // shift by one row so Outside (y = -1) fits
            int n = W * (H + 1);
            var g = new float[n];
            var from = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; from[i] = -1; }
            var open = new List<(float f, Cell c)>();
            g[Idx(a)] = 0;
            open.Add((Heur(a, b), a));
            while (open.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].f < open[best].f) best = i;
                var cur = open[best].c;
                open.RemoveAt(best);
                int ci = Idx(cur);
                if (closed[ci]) continue;
                closed[ci] = true;
                if (cur.Equals(b)) break;
                foreach (var d in Dirs8)
                {
                    var nb = cur + d;
                    if (!Walkable(nb)) continue;
                    if (d.x != 0 && d.y != 0 && (!Walkable(new Cell(cur.x + d.x, cur.y)) || !Walkable(new Cell(cur.x, cur.y + d.y)))) continue;
                    int ni = Idx(nb);
                    if (closed[ni]) continue;
                    float cost = g[ci] + (d.x != 0 && d.y != 0 ? 1.414f : 1f);
                    if (cost < g[ni])
                    {
                        g[ni] = cost; from[ni] = ci;
                        open.Add((cost + Heur(nb, b), nb));
                    }
                }
            }
            if (from[Idx(b)] < 0) return null;
            var path = new List<Cell>();
            int at = Idx(b), start = Idx(a);
            while (at != start)
            {
                path.Add(new Cell(at % W, at / W - 1));
                at = from[at];
            }
            path.Reverse();
            return path;
        }

        static float Heur(Cell a, Cell b)
        {
            int dx = Math.Abs(a.x - b.x), dy = Math.Abs(a.y - b.y);
            return Math.Max(dx, dy) + 0.414f * Math.Min(dx, dy);
        }

        public float Distance(Cell a, Cell b)
        {
            var p = Path(a, b);
            return p == null ? 999 : p.Count;
        }
    }
}
