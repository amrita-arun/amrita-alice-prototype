using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lot
{
    // Procedural sprites, palette and small juice helpers. No imported assets.
    public static class LotArt
    {
        public static readonly Color Background = Hex("15161B");
        public static readonly Color BoardColor = Hex("1E2027");
        public static readonly Color GridDot = Hex("2E313B");
        public static readonly Color Wall = Hex("4A4F5E");
        public static readonly Color Cream = Hex("EDE6D6");
        public static readonly Color Danger = Hex("F2545B");
        public static readonly Color Headlight = Hex("FFF4D6");

        static readonly Dictionary<string, Color> carColors = new Dictionary<string, Color>
        {
            { "red", Hex("F2545B") },
            { "blue", Hex("4D8EF7") },
            { "teal", Hex("2EC4B6") },
            { "yellow", Hex("FFC857") },
            { "purple", Hex("9B6BF2") },
            { "orange", Hex("FF8C42") },
            { "green", Hex("7BD389") },
            { "pink", Hex("F78FB3") },
            { "gray", Hex("8A8F9C") },
        };

        public static Color CarColor(string name) => carColors.TryGetValue(name, out var c) ? c : Color.white;

        static Sprite rounded, circle, square;
        static Material unlit;

        // Unity 6 enters Play Mode without a domain reload, so these statics outlive the textures
        // Unity destroys between sessions. Use Unity's == null (not ??=) so they get rebuilt.
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 14);
        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32, false);
        public static Sprite Square => square != null ? square : square = MakeSquare();
        public static Sprite Beam => beam != null ? beam : beam = MakeBeam();
        static Sprite beam;

        // A soft cone of light pointing along +x, pivot at its narrow end.
        static Sprite MakeBeam()
        {
            const int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                float v = Mathf.Abs((y - (h - 1) / 2f) / ((h - 1) / 2f));
                float spread = 0.45f + 0.55f * u;
                float edge = Mathf.Clamp01((spread - v) / 0.3f);
                float a = Mathf.Pow(1 - u, 1.4f) * edge * Mathf.Clamp01(u * 8);
                cols[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(cols);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0, 0.5f), 100, 0, SpriteMeshType.FullRect);
        }

        static Material Unlit
        {
            get
            {
                if (unlit == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    if (sh != null) unlit = new Material(sh);
                }
                return unlit;
            }
        }

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Color color, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (Unlit != null) sr.sharedMaterial = Unlit;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer Rect(string name, Transform parent, Color color, Vector2 size, int order)
            => Sprite(name, parent, Square, color, size, order);

        static Sprite MakeRounded(int px, int radius, bool sliced = true)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[px * px];
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                // signed distance to a rounded square, for anti-aliased edges
                float cx = Mathf.Max(Mathf.Abs(x + 0.5f - px / 2f) - (px / 2f - radius), 0);
                float cy = Mathf.Max(Mathf.Abs(y + 0.5f - px / 2f) - (px / 2f - radius), 0);
                float d = Mathf.Sqrt(cx * cx + cy * cy) - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255);
                cols[y * px + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(cols);
            tex.Apply();
            var border = sliced ? new Vector4(radius + 1, radius + 1, radius + 1, radius + 1) : Vector4.zero;
            return UnityEngine.Sprite.Create(tex, new UnityEngine.Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var cols = new Color32[16];
            for (int i = 0; i < 16; i++) cols[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(cols);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new UnityEngine.Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4, 0, SpriteMeshType.FullRect);
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        public static Color Shade(Color c, float amount) =>
            amount >= 0 ? Color.Lerp(c, Color.white, amount) : Color.Lerp(c, Color.black, -amount);

        // ---- easing ----
        public static float EaseOutBack(float t, float s = 1.70158f)
        {
            t -= 1;
            return t * t * ((s + 1) * t + s) + 1;
        }

        public static float EaseOutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);
        public static float EaseInCubic(float t) => t * t * t;

        // Pop a transform in from scale 0 after a delay.
        public static IEnumerator PopIn(Transform t, float delay, float duration = 0.35f)
        {
            if (t == null) yield break;
            var target = t.localScale;
            t.localScale = Vector3.zero;
            if (delay > 0) yield return new WaitForSeconds(delay);
            for (float e = 0; e < duration; e += Time.deltaTime)
            {
                if (t == null) yield break;
                t.localScale = target * EaseOutBack(e / duration);
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        public static IEnumerator PopOut(Transform t, float delay, float duration = 0.22f)
        {
            if (t == null) yield break;
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (t == null) yield break;
            var start = t.localScale;
            for (float e = 0; e < duration; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = e / duration;
                // tiny anticipation swell, then shrink
                float s = k < 0.25f ? 1 + 0.12f * (k / 0.25f) : 1.12f * (1 - EaseInCubic((k - 0.25f) / 0.75f));
                t.localScale = start * s;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.zero;
        }
    }

    // Damped spring, used for every bit of squash / snap / pulse so motion feels physical.
    public struct Spring
    {
        public float value, velocity;

        public void Step(float target, float stiffness, float damping, float dt)
        {
            velocity += (target - value) * stiffness * dt;
            velocity *= Mathf.Exp(-damping * dt);
            value += velocity * dt;
        }
    }

    // Fire-and-forget square particles.
    public class LotParticles : MonoBehaviour
    {
        class P { public Transform t; public SpriteRenderer sr; public Vector2 v; public float life, max, spin, gravity, alpha = 1; }
        readonly List<P> live = new List<P>();

        public void Burst(Vector2 pos, Color color, int count, float speed, float size = 0.09f, float gravity = 0, Vector2? bias = null)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = LotArt.Rect("p", transform, color, Vector2.one * size * Random.Range(0.6f, 1.2f), 30);
                sr.transform.position = pos;
                sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 90f));
                var dir = Random.insideUnitCircle.normalized + (bias ?? Vector2.zero);
                live.Add(new P
                {
                    t = sr.transform, sr = sr, v = dir * speed * Random.Range(0.5f, 1.2f),
                    max = Random.Range(0.3f, 0.55f), spin = Random.Range(-720f, 720f), gravity = gravity,
                });
            }
        }

        // A sprite that keeps falling and fading (bits of timer bar, dead cars...).
        public void Debris(SpriteRenderer sr, Vector2 velocity, float spin, float life, float gravity = 14)
        {
            live.Add(new P { t = sr.transform, sr = sr, v = velocity, max = life, spin = spin, gravity = gravity, alpha = sr.color.a });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.life += dt;
                if (p.t == null || p.life >= p.max)
                {
                    if (p.t != null) Destroy(p.t.gameObject);
                    live.RemoveAt(i);
                    continue;
                }
                p.v *= Mathf.Exp(-3f * dt * (p.gravity > 0 ? 0.2f : 1f));
                p.v.y -= p.gravity * dt;
                p.t.position += (Vector3)(p.v * dt);
                p.t.Rotate(0, 0, p.spin * dt);
                var c = p.sr.color;
                c.a = p.alpha * (1 - Mathf.Pow(p.life / p.max, 2));
                p.sr.color = c;
            }
        }

        public void Clear()
        {
            foreach (var p in live) if (p.t != null) Destroy(p.t.gameObject);
            live.Clear();
        }
    }
}
