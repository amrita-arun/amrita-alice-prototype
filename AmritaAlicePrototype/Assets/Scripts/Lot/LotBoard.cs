using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lot
{
    public class LotBoard : MonoBehaviour
    {
        const float WallThickness = 0.07f;
        const float ExitThickness = 0.13f;   // the "tell": the real exit is just a thicker wall
        const float Half = LotRules.N / 2f;

        public LevelDef level;
        public bool interactive;
        public bool finished;

        LotGame game;
        public LotGame Game => game;
        int[] fuel;
        readonly List<LotCar> cars = new List<LotCar>();
        readonly List<(Transform t, float delay)> pieces = new List<(Transform, float)>();
        SpriteRenderer exitWall;

        // drag state
        LotCar grabbed;
        List<int> group;
        LotRules.Limits lim;
        Vector2 dragStart, axis;
        Vector2Int axisInt;
        float disp, lastDisp, velocity, maxOver;
        int lastTickCell;
        bool bumpedPos, bumpedNeg;

        public Vector2 CellToWorld(Vector2Int c) => (Vector2)transform.position + new Vector2(c.x - Half + 0.5f, c.y - Half + 0.5f);

        public void Build(LevelDef lv, LotGame owner)
        {
            level = lv;
            fuel = LotRules.StartFuel(lv);
            game = owner;

            var bg = LotArt.Sprite("board", transform, LotArt.Rounded, LotArt.BoardColor, Vector2.one * (LotRules.N + 0.4f), 0);
            pieces.Add((bg.transform, 0));

            for (int x = 1; x < LotRules.N; x++)
            for (int y = 1; y < LotRules.N; y++)
            {
                var dot = LotArt.Sprite("dot", transform, LotArt.Circle, LotArt.GridDot, Vector2.one * 0.07f, 1);
                dot.transform.localPosition = new Vector2(x - Half, y - Half);
                pieces.Add((dot.transform, 0.02f + 0.012f * (Mathf.Abs(x - 3) + Mathf.Abs(y - 3))));
            }

            BuildWalls();

            for (int i = 0; i < lv.cars.Count; i++)
            {
                var go = new GameObject("car " + lv.cars[i].color);
                go.transform.SetParent(transform, false);
                var car = go.AddComponent<LotCar>();
                car.Build(this, lv.cars[i], i);
                cars.Add(car);
                var d = car.Center - (Vector2)transform.position;
                pieces.Add((go.transform, 0.12f + 0.035f * d.magnitude + 0.02f * i));
            }

            foreach (var (t, delay) in pieces) StartCoroutine(LotArt.PopIn(t, delay, 0.38f));
            StartCoroutine(EnableAfter(0.3f));
        }

        IEnumerator EnableAfter(float s)
        {
            yield return new WaitForSeconds(s);
            if (!finished) interactive = true;
        }

        void BuildWalls()
        {
            var sides = new[] { Side.L, Side.R, Side.B, Side.T };
            foreach (var side in sides)
            {
                for (int i = 0; i < LotRules.N; i++)
                {
                    bool isExit = level.exitSide == side && level.exitIndex == i;
                    bool isNotch = level.hasNotch && level.notchSide == side && level.notchIndex == i;
                    var (pos, normal) = EdgeSlot(side, i);
                    if (isNotch)
                    {
                        BuildNotch(pos, normal);
                        continue;
                    }
                    float w = isExit ? ExitThickness : WallThickness;
                    var size = normal.x != 0 ? new Vector2(w, 1) : new Vector2(1, w);
                    var sr = LotArt.Rect(isExit ? "exit" : "wall", transform, LotArt.Wall, size, 2);
                    sr.transform.localPosition = pos + normal * WallThickness / 2;
                    if (isExit) exitWall = sr;
                    pieces.Add((sr.transform, 0.06f + 0.015f * i));
                }
            }
            // corners
            for (int cx = -1; cx <= 1; cx += 2)
            for (int cy = -1; cy <= 1; cy += 2)
            {
                var sr = LotArt.Rect("corner", transform, LotArt.Wall, Vector2.one * WallThickness, 2);
                sr.transform.localPosition = new Vector2(cx * (Half + WallThickness / 2), cy * (Half + WallThickness / 2));
                pieces.Add((sr.transform, 0.06f));
            }
        }

        // A painted gap with door posts and a strip of "road" leading out. It is a wall.
        void BuildNotch(Vector2 pos, Vector2 normal)
        {
            var along = new Vector2(Mathf.Abs(normal.y), Mathf.Abs(normal.x));
            for (int s = -1; s <= 1; s += 2)
            {
                var post = LotArt.Rect("post", transform, LotArt.Wall, normal.x != 0 ? new Vector2(0.32f, WallThickness) : new Vector2(WallThickness, 0.32f), 2);
                post.transform.localPosition = pos + along * (0.5f - WallThickness / 2) * s + normal * 0.16f;
                pieces.Add((post.transform, 0.08f));
            }
            var road = LotArt.Sprite("road", transform, LotArt.Rounded, LotArt.BoardColor, normal.x != 0 ? new Vector2(0.5f, 0.86f) : new Vector2(0.86f, 0.5f), 0);
            road.transform.localPosition = pos + normal * 0.2f;
            pieces.Add((road.transform, 0.08f));
        }

        static (Vector2 pos, Vector2 normal) EdgeSlot(Side side, int i)
        {
            float c = i - Half + 0.5f;
            return side switch
            {
                Side.L => (new Vector2(-Half, c), Vector2.left),
                Side.R => (new Vector2(Half, c), Vector2.right),
                Side.B => (new Vector2(c, -Half), Vector2.down),
                _ => (new Vector2(c, Half), Vector2.up),
            };
        }

        Vector2Int[] Positions()
        {
            var p = new Vector2Int[cars.Count];
            for (int i = 0; i < cars.Count; i++) p[i] = cars[i].cell;
            return p;
        }

        void Update()
        {
            if (!interactive || finished) return;
            Pointer p = Pointer.current;
            if (p == null) p = Mouse.current;
            if (p == null) p = Touchscreen.current;
            if (p == null) return;
            Vector2 screen = p.position.ReadValue();
            Vector2 world = game.cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -game.cam.transform.position.z));

            if (p.press.wasPressedThisFrame)
            {
                TryGrab(world);
                if (grabbed == null)
                    game.particles.Burst(world, LotArt.Wall, 4, 1.2f, 0.05f); // a soft ripple where you clicked
                if (Application.isEditor)
                    Debug.Log($"[LOT] press {p.name} screen={screen} world={world} screenSize={Screen.width}x{Screen.height} hit={(grabbed != null ? grabbed.def.color : "nothing")}");
            }
            if (grabbed == null) return;
            if (p.press.isPressed) Drag(world);
            else EndDrag();
        }

        void TryGrab(Vector2 world)
        {
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                var car = cars[i];
                if (!car.Contains(world)) continue;

                game.NotifyFirstInput();
                grabbed = car;
                group = LotRules.GroupOf(level, i);
                lim = LotRules.GetLimits(level, Positions(), fuel, group);
                axis = car.def.horizontal ? Vector2.right : Vector2.up;
                axisInt = car.def.Axis;
                dragStart = world;
                disp = lastDisp = velocity = maxOver = 0;
                lastTickCell = 0;
                bumpedPos = bumpedNeg = false;
                bool alive = LotRules.HasFuel(fuel, group);
                foreach (int m in group)
                {
                    cars[m].dragging = true;
                    cars[m].SetLifted(alive); // parked cars don't lift: they're dead weight
                    if (m != i || !alive) cars[m].Kick(alive ? 1.2f : -2f); // linked cars twitch when you touch their twin
                }
                if (alive) game.sfx.Play(game.sfx.lift, 0.5f, 1 + 0.1f * (group.Count - 1));
                else game.sfx.Play(game.sfx.thunk, 0.35f, 0.7f);
                return;
            }
        }

        void Drag(Vector2 world)
        {
            float raw = Vector2.Dot(world - dragStart, axis);
            float hi = lim.max, lo = lim.min;

            // the target slides straight off the board through the real exit
            if (lim.exitDir != 0)
            {
                float edge = lim.exitDir > 0 ? hi : -lo;
                if (raw * lim.exitDir > edge + 0.55f)
                {
                    StartCoroutine(DriveOut());
                    return;
                }
                if (lim.exitDir > 0) hi = float.MaxValue; else lo = float.MinValue;
            }

            if (raw > hi) { disp = hi + Rubber(raw - hi); Bump(+1, raw - hi); }
            else if (raw < lo) { disp = lo - Rubber(lo - raw); Bump(-1, lo - raw); }
            else
            {
                disp = raw;
                if (raw < hi - 0.05f) bumpedPos = false;
                if (raw > lo + 0.05f) bumpedNeg = false;
            }

            int cellNow = Mathf.RoundToInt(Mathf.Clamp(disp, lim.min, lim.max));
            if (cellNow != lastTickCell)
            {
                game.sfx.Play(game.sfx.tick, 0.45f, 1 + 0.07f * Mathf.Abs(cellNow));
                lastTickCell = cellNow;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            velocity = Mathf.Lerp(velocity, (disp - lastDisp) / dt, 0.5f);
            lastDisp = disp;
            foreach (int m in group) cars[m].dragOffset = disp;
        }

        static float Rubber(float over) => 0.28f * (1 - Mathf.Exp(-over * 3.5f));

        void Bump(int dir, float over)
        {
            maxOver = Mathf.Max(maxOver, over);
            ref bool bumped = ref (dir > 0 ? ref bumpedPos : ref bumpedNeg);
            if (bumped || over < 0.06f) return;
            bumped = true;

            game.sfx.Play(game.sfx.thunk, 0.8f, Random.Range(0.9f, 1.05f));
            game.Shake(0.22f);
            foreach (int m in group)
            {
                cars[m].Kick(-3.5f);
                var light = cars[m].def.light;
                if (light.HasValue && light.Value != axisInt * dir) cars[m].FlashLights();
            }
            var front = grabbed.Center + axis * dir * (grabbed.def.len / 2f);
            game.particles.Burst(front, LotArt.Shade(grabbed.baseColor, 0.3f), 5, 2.2f, 0.07f, 0, -axis * dir * 0.6f);
        }

        void EndDrag()
        {
            float projected = disp + Mathf.Clamp(velocity * 0.05f, -0.6f, 0.6f); // flicks carry
            int n = Mathf.Clamp(Mathf.RoundToInt(projected), lim.min, lim.max);

            foreach (int m in group)
            {
                var car = cars[m];
                car.dragging = false;
                car.SetLifted(false);
                if (n != 0) car.Commit(car.cell + axisInt * n);
                else car.Release();
                car.Kick(-1.6f);
            }

            if (n != 0)
            {
                game.sfx.Play(game.sfx.snap, 0.7f, 1 + 0.05f * Mathf.Abs(n));
                SpendFuel(group);
                if (!LotRules.Solvable(level, Positions(), fuel)) StartCoroutine(LockOut());
            }
            else if (maxOver > 0.22f)
            {
                // you shoved something that doesn't move that way: it costs time
                game.Penalty(grabbed.Center);
            }
            grabbed = null;
        }

        void SpendFuel(List<int> members)
        {
            foreach (int m in members)
            {
                if (fuel[m] <= 0) continue;
                fuel[m]--;
                cars[m].SetFuel(fuel[m]);
            }
        }

        // The player made the level unwinnable (usually by burning fuel). Punish, then rewind.
        IEnumerator LockOut()
        {
            interactive = false;
            yield return new WaitForSeconds(0.4f); // let the fatal move land first
            if (finished) yield break;

            game.sfx.Play(game.sfx.thunk, 0.9f, 0.6f);
            game.sfx.Play(game.sfx.buzz, 0.8f, 0.8f);
            game.Shake(0.6f);
            game.Penalty(transform.position, game.lockSeconds);
            foreach (var c in cars) { StartCoroutine(c.Flash(LotArt.Danger)); c.Kick(-3f); }
            yield return new WaitForSeconds(0.55f);
            if (finished) yield break;

            // everything springs back to where the level started
            fuel = LotRules.StartFuel(level);
            for (int i = 0; i < cars.Count; i++)
            {
                var d = level.cars[i];
                cars[i].Commit(new Vector2Int(d.x, d.y));
                cars[i].SetFuel(fuel[i]);
            }
            game.sfx.Play(game.sfx.whoosh, 0.5f, 1.4f);
            yield return new WaitForSeconds(0.35f);
            if (!finished) interactive = true;
        }

        IEnumerator DriveOut()
        {
            interactive = false;
            finished = true;
            var car = grabbed;
            grabbed = null;
            car.dragging = false;
            car.SetLifted(false);
            car.driving = true;
            SpendFuel(new List<int> { car.index });

            var dir = axis * lim.exitDir;
            float speed = Mathf.Max(9f, velocity * lim.exitDir);
            game.sfx.Play(game.sfx.whoosh, 0.8f);
            game.Shake(0.3f);
            if (exitWall != null) StartCoroutine(FlashExit());

            var renderers = new List<SpriteRenderer>(car.GetComponentsInChildren<SpriteRenderer>());
            float trailTimer = 0;
            for (float t = 0; t < 0.55f; t += Time.deltaTime)
            {
                speed += 80 * Time.deltaTime;
                car.transform.position += (Vector3)(dir * speed * Time.deltaTime);
                // stretch with speed
                var s = Mathf.Min(1.5f, 1 + speed * 0.02f);
                car.transform.localScale = car.def.horizontal ? new Vector3(s, 1 / Mathf.Sqrt(s), 1) : new Vector3(1 / Mathf.Sqrt(s), s, 1);
                trailTimer -= Time.deltaTime;
                if (trailTimer <= 0)
                {
                    trailTimer = 0.02f;
                    var trail = LotArt.Sprite("trail", game.particles.transform, LotArt.Rounded,
                        new Color(car.baseColor.r, car.baseColor.g, car.baseColor.b, 0.35f), car.Size - Vector2.one * 0.18f, 8);
                    trail.transform.position = car.transform.position;
                    trail.transform.localScale = car.transform.localScale;
                    game.particles.Debris(trail, Vector2.zero, 0, 0.25f, 0);
                }
                yield return null;
            }
            foreach (var r in renderers) if (r != null) r.enabled = false;

            game.particles.Burst(exitWall != null ? (Vector2)exitWall.transform.position : car.Center, car.baseColor, 14, 5f, 0.1f, 0, dir * 0.8f);
            game.LevelCleared();
        }

        IEnumerator FlashExit()
        {
            var t = exitWall.transform;
            var baseScale = t.localScale;
            for (float e = 0; e < 0.45f; e += Time.deltaTime)
            {
                float k = e / 0.45f;
                exitWall.color = Color.Lerp(Color.white, LotArt.Wall, LotArt.EaseOutCubic(k));
                t.localScale = baseScale * (1 + 0.6f * (1 - LotArt.EaseOutCubic(k)));
                yield return null;
            }
            exitWall.color = LotArt.Wall;
            t.localScale = baseScale;
        }

        // Pop everything away, then destroy.
        public IEnumerator Outro()
        {
            interactive = false;
            finished = true;
            float max = 0;
            foreach (var (t, delay) in pieces)
            {
                if (t == null) continue;
                float d = Mathf.Max(0, 0.25f - delay * 0.6f);
                max = Mathf.Max(max, d);
                StartCoroutine(LotArt.PopOut(t, d));
            }
            yield return new WaitForSeconds(max + 0.25f);
            Destroy(gameObject);
        }

        // Time ran out: cars go gray and tumble off the screen.
        public IEnumerator FallApart()
        {
            interactive = false;
            finished = true;
            if (grabbed != null) { grabbed = null; }
            var vel = new Vector2[cars.Count];
            var spin = new float[cars.Count];
            for (int i = 0; i < cars.Count; i++)
            {
                cars[i].driving = true;
                vel[i] = new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 7f));
                spin[i] = Random.Range(-260f, 260f);
            }
            for (float e = 0; e < 1.3f; e += Time.deltaTime)
            {
                for (int i = 0; i < cars.Count; i++)
                {
                    var c = cars[i];
                    float delay = 0.04f * i;
                    if (e < delay) { c.Tint(LotArt.GridDot, e / delay); continue; }
                    c.Tint(LotArt.GridDot, 1);
                    vel[i].y -= 22 * Time.deltaTime;
                    c.transform.position += (Vector3)(vel[i] * Time.deltaTime);
                    c.transform.Rotate(0, 0, spin[i] * Time.deltaTime);
                }
                yield return null;
            }
            yield return Outro();
        }
    }
}
