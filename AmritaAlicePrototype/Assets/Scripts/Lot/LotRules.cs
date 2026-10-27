using System.Collections.Generic;
using UnityEngine;

namespace Lot
{
    // Pure puzzle logic. Tools/solve_levels.py mirrors these rules; keep them in sync.
    public static class LotRules
    {
        public const int N = 6;
        public const int Unlimited = -1;

        public static bool InBounds(Vector2Int c) => c.x >= 0 && c.x < N && c.y >= 0 && c.y < N;

        public static IEnumerable<Vector2Int> Cells(CarDef car, Vector2Int pos)
        {
            for (int i = 0; i < car.len; i++) yield return pos + car.Axis * i;
        }

        // Same-colored cars move together. The target is always alone.
        public static List<int> GroupOf(LevelDef lv, int index)
        {
            var group = new List<int>();
            var car = lv.cars[index];
            if (car.target) { group.Add(index); return group; }
            for (int i = 0; i < lv.cars.Count; i++)
            {
                var o = lv.cars[i];
                if (!o.target && o.color == car.color) group.Add(i);
            }
            return group;
        }

        public struct Limits
        {
            public int min, max;  // how many cells the group may slide (min <= 0 <= max)
            public int exitDir;   // 0, or +1/-1: sliding past the limit this way leaves through the exit
        }

        // One drag of any distance costs each car in the group 1 fuel. Out of fuel = parked for good.
        public static bool HasFuel(int[] fuel, List<int> group)
        {
            foreach (int m in group) if (fuel[m] == 0) return false;
            return true;
        }

        public static Limits GetLimits(LevelDef lv, Vector2Int[] pos, int[] fuel, List<int> group)
        {
            var lim = new Limits();
            if (!HasFuel(fuel, group)) return lim;

            var blocked = new HashSet<Vector2Int>();
            for (int i = 0; i < lv.cars.Count; i++)
            {
                if (group.Contains(i)) continue;
                foreach (var c in Cells(lv.cars[i], pos[i])) blocked.Add(c);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                int dist = int.MaxValue;
                bool allReachEdge = true;
                foreach (int m in group)
                {
                    var car = lv.cars[m];
                    var dir = car.Axis * s;
                    if (car.light.HasValue && car.light.Value != dir) { dist = 0; allReachEdge = false; break; }
                    int k = 0;
                    while (true)
                    {
                        bool ok = true;
                        foreach (var c in Cells(car, pos[m] + dir * (k + 1)))
                            if (!InBounds(c) || blocked.Contains(c)) { ok = false; break; }
                        if (!ok) break;
                        k++;
                    }
                    // did this car stop because of the board edge (not another car)?
                    bool edge = false;
                    foreach (var c in Cells(car, pos[m] + dir * (k + 1)))
                        if (!InBounds(c)) edge = true;
                    allReachEdge &= edge;
                    dist = Mathf.Min(dist, k);
                }
                if (s < 0) lim.min = -dist; else lim.max = dist;

                if (group.Count == 1 && lv.cars[group[0]].target && allReachEdge &&
                    ExitsThrough(lv, lv.cars[group[0]], pos[group[0]], s))
                    lim.exitDir = s;
            }
            return lim;
        }

        // Is the board edge this car hits when sliding in direction s the real exit?
        static bool ExitsThrough(LevelDef lv, CarDef car, Vector2Int pos, int s)
        {
            if (car.horizontal)
            {
                var side = s > 0 ? Side.R : Side.L;
                return lv.exitSide == side && lv.exitIndex == pos.y;
            }
            else
            {
                var side = s > 0 ? Side.T : Side.B;
                return lv.exitSide == side && lv.exitIndex == pos.x;
            }
        }

        public static int[] StartFuel(LevelDef lv)
        {
            var f = new int[lv.cars.Count];
            for (int i = 0; i < f.Length; i++) f[i] = lv.cars[i].fuel;
            return f;
        }

        // Can the target still get out from here? Used to catch the player locking themselves in.
        public static bool Solvable(LevelDef lv, Vector2Int[] pos, int[] fuel, int maxStates = 200000)
        {
            var groups = new List<List<int>>();
            var seenGroup = new HashSet<string>();
            for (int i = 0; i < lv.cars.Count; i++)
            {
                var g = GroupOf(lv, i);
                if (seenGroup.Add(string.Join(",", g))) groups.Add(g);
            }

            var seen = new HashSet<string> { Key(pos, fuel) };
            var queue = new Queue<(Vector2Int[] p, int[] f)>();
            queue.Enqueue((pos, fuel));
            while (queue.Count > 0)
            {
                if (seen.Count > maxStates) return true; // too big to search: assume the player is fine
                var (p, f) = queue.Dequeue();
                foreach (var g in groups)
                {
                    var lim = GetLimits(lv, p, f, g);
                    if (lim.exitDir != 0) return true;
                    for (int n = lim.min; n <= lim.max; n++)
                    {
                        if (n == 0) continue;
                        var np = (Vector2Int[])p.Clone();
                        var nf = (int[])f.Clone();
                        foreach (int m in g)
                        {
                            np[m] += lv.cars[m].Axis * n;
                            if (nf[m] > 0) nf[m]--;
                        }
                        if (seen.Add(Key(np, nf))) queue.Enqueue((np, nf));
                    }
                }
            }
            return false;
        }

        static string Key(Vector2Int[] p, int[] f)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < p.Length; i++) sb.Append(p[i].x).Append(p[i].y).Append(f[i] + 1).Append(';');
            return sb.ToString();
        }
    }
}
