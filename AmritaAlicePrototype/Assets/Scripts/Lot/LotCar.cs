using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lot
{
    public class LotCar : MonoBehaviour
    {
        const float Inset = 0.09f;
        static readonly Color ParkedColor = LotArt.Hex("3A3D47");

        public CarDef def;
        public int index;
        public Vector2Int cell;

        public Color baseColor;
        public float dragOffset;      // where the board wants the car along its axis (cells)
        public bool dragging;
        public bool driving;          // leaving the board; position is fully scripted

        LotBoard board;
        Transform body;
        SpriteRenderer shadow, fill, roof;
        readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();

        float shown;                  // displayed offset along axis
        float lastShown;
        Spring snapSpring;            // offset left over after committing a move, springs to 0
        Spring squash;                // + stretches along axis, - squashes
        Spring liftSpring;
        float liftTarget;
        float breathePhase;
        float parked, parkedTarget;   // 0 = normal colors, 1 = out of fuel
        Vector2 Axis => def.horizontal ? Vector2.right : Vector2.up;
        public Vector2 Size => def.horizontal ? new Vector2(def.len, 1) : new Vector2(1, def.len);

        public void Build(LotBoard owner, CarDef d, int i)
        {
            board = owner;
            def = d;
            index = i;
            cell = new Vector2Int(d.x, d.y);
            baseColor = LotArt.CarColor(d.color);
            breathePhase = Random.value * 10;

            body = new GameObject("body").transform;
            body.SetParent(transform, false);
            var size = Size - Vector2.one * Inset * 2;

            shadow = LotArt.Sprite("shadow", transform, LotArt.Rounded, new Color(0, 0, 0, 0.35f), size, 5);
            fill = LotArt.Sprite("fill", body, LotArt.Rounded, baseColor, size, 10);
            roof = LotArt.Sprite("roof", body, LotArt.Rounded, LotArt.Shade(baseColor, 0.16f), size - Vector2.one * 0.26f, 11);
            roof.transform.localPosition = new Vector3(0, 0.03f, 0);
            tinted.Add(fill);
            tinted.Add(roof);
            if (d.light.HasValue) BuildHeadlights(size, d.light.Value);
            if (d.fuel > 0) BuildPips(d.fuel);

            transform.position = Rest;
        }

        // ---- headlights ----
        readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        SpriteRenderer beam;
        float lightFlash;

        void BuildHeadlights(Vector2 size, Vector2Int dir)
        {
            var fwd = (Vector2)dir;
            var side = new Vector2(-fwd.y, fwd.x);
            float halfLen = Vector2.Dot(size / 2, new Vector2(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y)));
            for (int s = -1; s <= 1; s += 2)
            {
                var sr = LotArt.Sprite("light", body, LotArt.Circle, LotArt.Headlight, Vector2.one * 0.14f, 12);
                sr.transform.localPosition = fwd * (halfLen - 0.11f) + side * 0.22f * s;
                lights.Add(sr);
            }
            // light spilling onto the floor ahead: this end is the front
            float width = Vector2.Dot(size, new Vector2(Mathf.Abs(side.x), Mathf.Abs(side.y)));
            beam = LotArt.Sprite("beam", body, LotArt.Beam, BeamColor(0), new Vector2(0.95f, width + 0.15f), 4);
            beam.transform.localPosition = fwd * (halfLen - 0.04f);
            beam.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg);
        }

        static Color BeamColor(float flash) => new Color(LotArt.Headlight.r, LotArt.Headlight.g, LotArt.Headlight.b, 0.2f + 0.45f * flash);

        // Pushed against its headlights: blink them.
        public void FlashLights() => lightFlash = 1;

        // ---- fuel ----
        readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
        int fuelShown;

        void BuildPips(int count)
        {
            const float spacing = 0.22f;
            var color = LotArt.Shade(baseColor, -0.45f);
            for (int i = 0; i < count; i++)
            {
                var sr = LotArt.Sprite("pip", body, LotArt.Circle, color, Vector2.one * 0.13f, 12);
                sr.transform.localPosition = Axis * ((i - (count - 1) / 2f) * spacing) + new Vector2(0, 0.03f);
                pips.Add(sr);
            }
            fuelShown = count;
        }

        // Pop pips off (or back on after a reset). At zero the car goes dark: parked for good.
        public void SetFuel(int fuel)
        {
            if (fuel < 0 || pips.Count == 0) return;
            for (int i = 0; i < pips.Count; i++)
            {
                bool on = i < fuel;
                bool wasOn = i < fuelShown;
                if (wasOn && !on)
                {
                    board.Game.particles.Burst(pips[i].transform.position, LotArt.Shade(baseColor, 0.3f), 5, 2f, 0.06f, 4f, Vector2.up);
                    StartCoroutine(LotArt.PopOut(pips[i].transform, 0.05f, 0.18f));
                }
                else if (on && !wasOn)
                {
                    pips[i].transform.localScale = Vector3.one;
                    StartCoroutine(LotArt.PopIn(pips[i].transform, 0.05f * i, 0.3f));
                }
            }
            fuelShown = fuel;
            parkedTarget = fuel == 0 ? 1 : 0;
            if (fuel == 0) squash.velocity -= 4;
        }

        public Vector2 Rest => board.CellToWorld(cell) + Axis * (def.len - 1) * 0.5f;

        public Vector2 Center => (Vector2)transform.position;

        public bool Contains(Vector2 world)
        {
            var d = world - Center;
            var half = Size / 2;
            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y;
        }

        // Move the logical cell; keep the visual where it is and spring the difference away.
        public void Commit(Vector2Int newCell)
        {
            var before = Center;
            cell = newCell;
            dragOffset = 0;
            shown = 0;
            snapSpring.value = Vector2.Dot(before - Rest, Axis);
            lastShown = snapSpring.value;
        }

        public void Release()
        {
            // board did not move us: let the leftover drag offset spring home
            snapSpring.value += shown;
            shown = 0;
            dragOffset = 0;
        }

        public void SetLifted(bool up) => liftTarget = up ? 1 : 0;

        public void Kick(float amount) => squash.velocity += amount;

        public void Tint(Color c, float amount)
        {
            foreach (var sr in tinted)
            {
                if (sr == null) continue;
                var baseC = sr == roof ? LotArt.Shade(baseColor, 0.16f) : baseColor;
                var col = Color.Lerp(baseC, c, amount);
                col.a = sr.color.a;
                sr.color = col;
            }
        }

        // Brief red flash used when the player locks the board.
        public IEnumerator Flash(Color c)
        {
            for (float e = 0; e < 0.5f; e += Time.deltaTime)
            {
                Tint(c, 0.8f * (1 - e / 0.5f));
                yield return null;
            }
            Tint(ParkedColor, parked * 0.75f);
        }

        void Update()
        {
            if (driving) return;
            float dt = Time.deltaTime;

            if (dragging)
            {
                // follow the finger tightly but not rigidly
                shown = Mathf.Lerp(shown, dragOffset, 1 - Mathf.Exp(-30 * dt));
            }
            snapSpring.Step(0, 420, 16, dt);
            squash.Step(0, 380, 14, dt);
            liftSpring.Step(liftTarget, 500, 22, dt);

            float total = shown + snapSpring.value;
            transform.position = Rest + Axis * total;

            float vel = (total - lastShown) / Mathf.Max(dt, 1e-4f);
            lastShown = total;
            float stretch = Mathf.Clamp(Mathf.Abs(vel) * 0.012f, 0, 0.14f) + squash.value;

            float breathe = 1;
            if (def.target && parked < 0.5f) breathe = 1 + 0.045f * Mathf.Sin((Time.time + breathePhase) * 2.6f);

            float lift = liftSpring.value;
            float along = (1 + stretch) * breathe * (1 + lift * 0.05f);
            float across = (1 - stretch * 0.6f) * breathe * (1 + lift * 0.05f);
            body.localScale = def.horizontal ? new Vector3(along, across, 1) : new Vector3(across, along, 1);
            body.localPosition = new Vector3(0, lift * 0.06f, 0);

            shadow.transform.localPosition = new Vector3(0.03f + lift * 0.04f, -0.06f - lift * 0.07f, 0);
            shadow.transform.localScale = body.localScale;
            var sc = shadow.color;
            sc.a = 0.35f - lift * 0.12f;
            shadow.color = sc;

            fill.sortingOrder = dragging ? 15 : 10;
            roof.sortingOrder = dragging ? 16 : 11;
            foreach (var p in pips) p.sortingOrder = dragging ? 17 : 12;

            if (!Mathf.Approximately(parked, parkedTarget))
            {
                parked = Mathf.MoveTowards(parked, parkedTarget, dt * 4);
                Tint(ParkedColor, parked * 0.75f);
            }

            if (beam != null)
            {
                lightFlash = Mathf.MoveTowards(lightFlash, 0, dt * 3.5f);
                // double blink, like a car flashing its brights; lights die with the fuel
                float blink = lightFlash > 0 && Mathf.Sin(lightFlash * Mathf.PI * 4) > 0 ? lightFlash : 0;
                var bc = BeamColor(blink);
                bc.a *= 1 - parked;
                beam.color = bc;
                foreach (var l in lights) l.transform.localScale = Vector3.one * (1 + 0.6f * blink);
            }
        }
    }
}
