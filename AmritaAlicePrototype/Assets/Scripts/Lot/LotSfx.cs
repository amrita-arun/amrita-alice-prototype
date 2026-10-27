using UnityEngine;

namespace Lot
{
    // All sounds are synthesized at startup so the project has no audio assets.
    public class LotSfx : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource[] voices;
        int next;

        public AudioClip tick, lift, snap, thunk, buzz, whoosh, chime, death, fanfare, heartbeat;

        void Awake()
        {
            voices = new AudioSource[10];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }

            tick = Make("tick", 0.03f, (t, k) => Mathf.Sin(t * 2 * Mathf.PI * 1900) * Mathf.Exp(-t * 160) * 0.5f);
            lift = Make("lift", 0.07f, (t, k) => Mathf.Sin(t * 2 * Mathf.PI * (500 + 900 * k)) * Mathf.Exp(-t * 45) * 0.35f);
            snap = Make("snap", 0.08f, (t, k) =>
                (Mathf.Sin(t * 2 * Mathf.PI * 320) * 0.6f + Noise() * Mathf.Exp(-t * 400) * 0.5f) * Mathf.Exp(-t * 55) * 0.6f);
            thunk = Make("thunk", 0.14f, (t, k) =>
                (Mathf.Sin(2 * Mathf.PI * (160 * t - 300 * t * t)) + Noise() * Mathf.Exp(-t * 200) * 0.6f) * Mathf.Exp(-t * 32) * 0.8f);
            buzz = Make("buzz", 0.22f, (t, k) =>
                Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 92)) * 0.18f * Mathf.Exp(-t * 9) * (1 + 0.5f * Mathf.Sin(t * 2 * Mathf.PI * 30)));
            whoosh = Make("whoosh", 0.5f, (t, k) =>
                (Noise() * 0.5f + Mathf.Sin(2 * Mathf.PI * (180 * t + 900 * t * t)) * 0.4f) * Mathf.Sin(Mathf.PI * k) * 0.5f);
            chime = Make("chime", 0.6f, (t, k) =>
                (Bell(t, 1046.5f) + (t > 0.09f ? Bell(t - 0.09f, 1568f) : 0)) * 0.35f);
            death = Make("death", 0.9f, (t, k) =>
                Mathf.Sin(2 * Mathf.PI * (440 * t - 180 * t * t)) * (1 - k) * 0.4f * (0.7f + 0.3f * Mathf.Sin(t * 2 * Mathf.PI * 12)));
            fanfare = Make("fanfare", 1.1f, (t, k) =>
                (Bell(t, 523f) + Bell(t - 0.12f, 659f) + Bell(t - 0.24f, 784f) + Bell(t - 0.36f, 1046f)) * 0.3f);
            heartbeat = Make("heartbeat", 0.35f, (t, k) =>
                (Mathf.Sin(t * 2 * Mathf.PI * 55) * Mathf.Exp(-t * 30) + (t > 0.16f ? Mathf.Sin((t - 0.16f) * 2 * Mathf.PI * 50) * Mathf.Exp(-(t - 0.16f) * 30) * 0.7f : 0)) * 0.9f);
        }

        static float Noise() => Random.value * 2 - 1;

        static float Bell(float t, float f) =>
            t < 0 ? 0 : (Mathf.Sin(t * 2 * Mathf.PI * f) + 0.3f * Mathf.Sin(t * 2 * Mathf.PI * f * 2)) * Mathf.Exp(-t * 7);

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> f)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float fade = Mathf.Min(1, (n - i) / (Rate * 0.005f)); // avoid end clicks
                data[i] = Mathf.Clamp(f(t, i / (float)n) * fade, -1, 1);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(AudioClip clip, float volume = 1, float pitch = 1)
        {
            if (clip == null) return;
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.pitch = pitch * Random.Range(0.97f, 1.03f);
            v.PlayOneShot(clip, volume);
        }
    }
}
