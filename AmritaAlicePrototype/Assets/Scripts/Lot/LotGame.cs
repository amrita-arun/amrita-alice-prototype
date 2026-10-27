using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lot
{
    // Entry point. Spawns itself into whatever scene is playing, so no scene setup is needed.
    public class LotGame : MonoBehaviour
    {
        [Header("Run tuning")]
        public float runSeconds = 75f;     // one timer for the whole run of levels
        public float bonusPerLevel = 6f;   // time back for each car that escapes
        public float penaltySeconds = 2f;  // shoving a car into something it can't pass
        public float lockSeconds = 4f;     // making the level unwinnable (it rewinds to the start)

        [HideInInspector] public Camera cam;
        [HideInInspector] public LotSfx sfx;
        [HideInInspector] public LotParticles particles;

        enum State { Playing, Transition, Dead, Won }
        State state;
        List<LevelDef> levels;
        int levelIndex;
        LotBoard board;
        float timeLeft;
        bool ticking;

        // HUD: a single time bar above the board and one dot per level below it. No text.
        const float BarWidth = 6.4f, BarY = 3.78f, DotsY = -3.8f;
        SpriteRenderer barBack, barFill;
        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        float barShown = 1;
        Spring barPulse;
        float flashRed, flashWhite, heartbeatTimer;
        readonly List<Spring> dotPulse = new List<Spring>();

        float trauma;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<LotGame>() == null) new GameObject("LOT").AddComponent<LotGame>();
        }

        void Start()
        {
            Application.targetFrameRate = 120;
            cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = LotArt.Background;
            cam.transform.position = new Vector3(0, 0, -10);

            sfx = gameObject.AddComponent<LotSfx>();
            particles = new GameObject("fx").AddComponent<LotParticles>();

            var text = Resources.Load<TextAsset>("Levels");
            levels = LevelDef.Parse(text.text);

            barBack = LotArt.Sprite("time back", transform, LotArt.Rounded, LotArt.GridDot, new Vector2(BarWidth, 0.14f), 40);
            barBack.transform.position = new Vector2(0, BarY);
            barFill = LotArt.Sprite("time", transform, LotArt.Rounded, LotArt.Cream, new Vector2(BarWidth, 0.14f), 41);
            for (int i = 0; i < levels.Count; i++)
            {
                var d = LotArt.Sprite("dot", transform, LotArt.Circle, LotArt.GridDot, Vector2.one * 0.14f, 40);
                d.transform.position = new Vector2((i - (levels.Count - 1) / 2f) * 0.34f, DotsY);
                dots.Add(d);
                dotPulse.Add(new Spring());
            }

            StartRun();
        }

        void StartRun()
        {
            timeLeft = runSeconds;
            ticking = false;
            levelIndex = 0;
            LoadLevel(0);
        }

        void LoadLevel(int i)
        {
            levelIndex = i;
            board = new GameObject("board " + levels[i].name).AddComponent<LotBoard>();
            board.Build(levels[i], this);
            state = State.Playing;
            var p = dotPulse[i]; p.velocity += 6; dotPulse[i] = p;
        }

        // The clock only starts once the player first touches a car.
        public void NotifyFirstInput()
        {
            if (state == State.Playing) ticking = true;
        }

        public void Shake(float amount) => trauma = Mathf.Min(1, trauma + amount);

        public void Penalty(Vector2 where, float seconds = -1)
        {
            if (state != State.Playing || !ticking) return;
            float before = timeLeft;
            timeLeft -= seconds < 0 ? penaltySeconds : seconds;

            // the lost time snaps off the bar and falls away
            float x0 = -BarWidth / 2 + BarWidth * Mathf.Clamp01(timeLeft / runSeconds);
            float x1 = -BarWidth / 2 + BarWidth * Mathf.Clamp01(before / runSeconds);
            if (x1 - x0 > 0.01f)
            {
                var chunk = LotArt.Rect("lost", particles.transform, LotArt.Danger, new Vector2(x1 - x0, 0.14f), 42);
                chunk.transform.position = new Vector2((x0 + x1) / 2, BarY);
                particles.Debris(chunk, new Vector2(Random.Range(-1f, 1f), 3f), Random.Range(-300f, 300f), 0.9f);
            }
            barShown = Mathf.Clamp01(timeLeft / runSeconds);
            flashRed = 1;
            barPulse.velocity += 10;
            sfx.Play(sfx.buzz, 0.7f);
            Shake(0.35f);
            particles.Burst(where, LotArt.Danger, 6, 1.5f, 0.06f);
            if (timeLeft <= 0) Die();
        }

        public void LevelCleared()
        {
            if (state != State.Playing) return;
            state = State.Transition;
            sfx.Play(sfx.chime, 0.8f, 1 + 0.04f * levelIndex);
            if (ticking)
            {
                timeLeft = Mathf.Min(runSeconds, timeLeft + bonusPerLevel);
                flashWhite = 1;
                barPulse.velocity += 8;
            }
            var p = dotPulse[levelIndex]; p.velocity += 14; dotPulse[levelIndex] = p;
            particles.Burst(dots[levelIndex].transform.position, LotArt.Cream, 8, 2.5f, 0.06f);
            StartCoroutine(Next());
        }

        IEnumerator Next()
        {
            yield return new WaitForSeconds(0.25f);
            yield return board.Outro();
            if (levelIndex + 1 >= levels.Count) StartCoroutine(Win());
            else LoadLevel(levelIndex + 1);
        }

        void Die()
        {
            if (state != State.Playing) return;
            state = State.Dead;
            ticking = false;
            timeLeft = 0;
            sfx.Play(sfx.death, 0.8f);
            Shake(0.9f);
            flashRed = 1;
            StartCoroutine(DieRoutine());
        }

        IEnumerator DieRoutine()
        {
            yield return board.FallApart();
            yield return new WaitForSeconds(0.3f);
            // refill the bar before the next attempt so the restart reads clearly
            StartRun();
        }

        IEnumerator Win()
        {
            state = State.Won;
            ticking = false;
            sfx.Play(sfx.fanfare, 0.9f);
            for (int i = 0; i < dots.Count; i++)
            {
                var p = dotPulse[i]; p.velocity += 12; dotPulse[i] = p;
                particles.Burst(dots[i].transform.position, LotArt.Cream, 4, 2f, 0.06f);
                yield return new WaitForSeconds(0.05f);
            }
            for (int k = 0; k < 6; k++)
            {
                var c = LotArt.CarColor(new[] { "red", "blue", "teal", "yellow", "purple", "pink" }[k]);
                particles.Burst(new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(-1.5f, 2f)), c, 18, 6f, 0.12f, 6f);
                Shake(0.2f);
                yield return new WaitForSeconds(0.18f);
            }
            yield return new WaitForSeconds(0.6f);
            // tap anywhere to play again
            while (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) yield return null;
            StartRun();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (state == State.Playing && ticking)
            {
                timeLeft -= dt;
                if (timeLeft <= 10 && timeLeft > 0)
                {
                    heartbeatTimer -= dt;
                    if (heartbeatTimer <= 0)
                    {
                        heartbeatTimer = Mathf.Lerp(0.45f, 0.9f, timeLeft / 10);
                        sfx.Play(sfx.heartbeat, 0.7f);
                        barPulse.velocity += 6;
                    }
                }
                if (timeLeft <= 0) Die();
            }

            UpdateHud(dt);
            UpdateCamera(dt);

            var kb = Keyboard.current;
            if (kb != null && state == State.Playing && board != null)
            {
                if (kb.rKey.wasPressedThisFrame) { StopAllCoroutines(); Destroy(board.gameObject); StartRun(); }
                else if (kb.nKey.wasPressedThisFrame) { board.interactive = false; LevelCleared(); }
            }
        }

        void UpdateHud(float dt)
        {
            float frac = Mathf.Clamp01(timeLeft / runSeconds);
            barShown = Mathf.Lerp(barShown, frac, 1 - Mathf.Exp(-14 * dt));
            barPulse.Step(0, 300, 12, dt);
            flashRed = Mathf.MoveTowards(flashRed, 0, dt * 3);
            flashWhite = Mathf.MoveTowards(flashWhite, 0, dt * 3);

            float low = 1 - Mathf.Clamp01(timeLeft / 15f);
            var col = Color.Lerp(LotArt.Cream, LotArt.Danger, low);
            col = Color.Lerp(col, LotArt.Danger, flashRed);
            col = Color.Lerp(col, Color.white, flashWhite);
            barFill.color = col;
            float w = Mathf.Max(0.001f, BarWidth * barShown);
            float h = 0.14f * (1 + Mathf.Clamp(barPulse.value, -0.5f, 1.5f));
            barFill.size = new Vector2(Mathf.Max(w, h), h);
            barFill.enabled = barShown > 0.003f;
            barFill.transform.position = new Vector2(-BarWidth / 2 + barFill.size.x / 2, BarY);
            barBack.size = new Vector2(BarWidth, h);

            for (int i = 0; i < dots.Count; i++)
            {
                var p = dotPulse[i];
                p.Step(0, 260, 10, dt);
                dotPulse[i] = p;
                Color c;
                float s = 1 + Mathf.Clamp(p.value * 0.15f, -0.5f, 1.2f);
                if (state == State.Won || i < levelIndex || (i == levelIndex && state == State.Transition)) c = LotArt.Cream;
                else if (i == levelIndex) { c = Color.Lerp(LotArt.GridDot, LotArt.Cream, 0.45f + 0.15f * Mathf.Sin(Time.time * 4)); }
                else c = LotArt.GridDot;
                dots[i].color = c;
                dots[i].transform.localScale = Vector3.one * s;
            }

            // the whole lot gets a little redder as time runs out
            var bg = Color.Lerp(LotArt.Background, LotArt.Hex("2A1518"), ticking ? low * 0.8f : 0);
            cam.backgroundColor = Color.Lerp(bg, LotArt.Hex("3A1518"), flashRed * 0.5f);
        }

        void UpdateCamera(float dt)
        {
            // fit the board + HUD on any aspect ratio
            float aspect = Mathf.Max(0.1f, cam.aspect);
            cam.orthographicSize = Mathf.Max(4.5f, 3.9f / aspect);

            trauma = Mathf.Max(0, trauma - dt * 1.8f);
            float k = trauma * trauma;
            float t = Time.time * 30;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0) - 0.5f, Mathf.PerlinNoise(0, t) - 0.5f, 0) * 0.5f * k;
            cam.transform.position = new Vector3(0, 0, -10) + offset;
            cam.transform.rotation = Quaternion.Euler(0, 0, (Mathf.PerlinNoise(t, t) - 0.5f) * 3f * k);
        }
    }
}
