using System.Collections.Generic;
using UnityEngine;

namespace Lot
{
    public enum Side { L, R, T, B }

    public class CarDef
    {
        public int x, y, len;
        public bool horizontal, target;
        public string color;
        public Vector2Int? light; // headlight: the only direction this car can drive. null = both ways.
        public int fuel = LotRules.Unlimited; // moves left (shown as pips on the roof). -1 = unlimited.

        public Vector2Int Axis => horizontal ? Vector2Int.right : Vector2Int.up;
    }

    public class LevelDef
    {
        public string name;
        public Side exitSide;
        public int exitIndex;
        public bool hasNotch;
        public Side notchSide;
        public int notchIndex;
        public readonly List<CarDef> cars = new List<CarDef>();

        // Format is documented at the top of Assets/Resources/Levels.txt.
        public static List<LevelDef> Parse(string text)
        {
            var levels = new List<LevelDef>();
            LevelDef cur = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                var t = line.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 0) continue;

                switch (t[0])
                {
                    case "level":
                        cur = new LevelDef { name = string.Join(" ", t, 1, t.Length - 1) };
                        break;
                    case "exit":
                        cur.exitSide = ParseSide(t[1]);
                        cur.exitIndex = int.Parse(t[2]);
                        break;
                    case "notch":
                        cur.hasNotch = true;
                        cur.notchSide = ParseSide(t[1]);
                        cur.notchIndex = int.Parse(t[2]);
                        break;
                    case "car":
                        var c = new CarDef
                        {
                            x = int.Parse(t[1]),
                            y = int.Parse(t[2]),
                            len = int.Parse(t[3]),
                            horizontal = t[4] == "H",
                            color = t[5],
                        };
                        for (int i = 6; i < t.Length; i++)
                        {
                            switch (t[i])
                            {
                                case "target": c.target = true; break;
                                case var f when f.StartsWith("fuel"): c.fuel = int.Parse(f.Substring(4)); break;
                                case "left": c.light = Vector2Int.left; break;
                                case "right": c.light = Vector2Int.right; break;
                                case "up": c.light = Vector2Int.up; break;
                                case "down": c.light = Vector2Int.down; break;
                            }
                        }
                        cur.cars.Add(c);
                        break;
                    case "end":
                        levels.Add(cur);
                        cur = null;
                        break;
                }
            }
            return levels;
        }

        static Side ParseSide(string s) => s switch { "L" => Side.L, "R" => Side.R, "T" => Side.T, _ => Side.B };
    }
}
