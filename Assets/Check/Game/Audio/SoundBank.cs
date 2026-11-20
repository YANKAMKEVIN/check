using System;
using Check.Core;
using UnityEngine;

namespace Check.Game.Audio
{
    public enum Sfx
    {
        Click, Select, Deselect, Slide, Place, Draw, Deal, Shuffle, Check, Games, Penalty, Skip,
        YourTurn, Suit, Victory, Defeat, Eliminated, Illegal,
    }

    /// <summary>
    /// Sons synthétisés au démarrage (papier, feutre, cloches) : aucun fichier audio.
    /// Les cartes : glissement (bruit filtré), pose (claquement sec + feutre), pioche, mélange (rafale de claquements).
    /// </summary>
    public sealed class SoundBank : MonoBehaviour
    {
        const int Rate = 44100;
        const string MuteKey = "check.muted";
        const string AmbienceKey = "check.ambience";
        /// <summary>Niveaux de l'ambiance du wagon (coupée, douce, forte).</summary>
        public static readonly float[] AmbienceLevels = { 0f, 0.35f, 0.7f };

        AudioClip[] clips;
        AudioSource[] voices;
        int nextVoice;
        readonly System.Random rng = new System.Random(7);

        AudioSource ambience;
        float tension, tensionTarget;

        public bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Niveau de l'ambiance du wagon (index dans <see cref="AmbienceLevels"/>), réglé dans la pause.</summary>
        public int AmbienceLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(AmbienceKey, 1), 0, AmbienceLevels.Length - 1);
            set { PlayerPrefs.SetInt(AmbienceKey, Mathf.Clamp(value, 0, AmbienceLevels.Length - 1)); PlayerPrefs.Save(); }
        }

        /// <summary>Tension de la partie (0 à 1, voir <see cref="RumbleSynth.Tension"/>) : la rame gronde un peu plus fort.</summary>
        public void SetTension(float value) => tensionTarget = Mathf.Clamp01(value);

        void Awake()
        {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }

            var values = (Sfx[])Enum.GetValues(typeof(Sfx));
            clips = new AudioClip[values.Length];
            foreach (var s in values) clips[(int)s] = Build(s);

            // Roulement de la rame en boucle, discret, sous tout le reste.
            const int ambienceRate = 22050;
            var rumble = RumbleSynth.Render(ambienceRate);
            var clip = AudioClip.Create("check_rumble", rumble.Length, 1, ambienceRate, false);
            clip.SetData(rumble, 0);
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.spatialBlend = 0f;
            ambience.loop = true;
            ambience.clip = clip;
            ambience.volume = 0f;
            ambience.Play();
        }

        void Update()
        {
            if (ambience == null) return;
            float dt = Time.unscaledDeltaTime;
            tension = Mathf.MoveTowards(tension, tensionTarget, dt * 0.5f);
            float level = Muted ? 0f : AmbienceLevels[AmbienceLevel];
            float target = level * (0.8f + 0.35f * tension);
            ambience.volume = Mathf.MoveTowards(ambience.volume, target, dt * 0.6f);
            ambience.pitch = 1f + 0.07f * tension;
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (Muted || clips == null) return;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
            v.PlayOneShot(clips[(int)sfx], volume);
        }

        AudioClip Build(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click: return Make("click", 0.06f, Mono(t => Bell(t, 2400f, 0.015f, 0.3f)));
                case Sfx.Select: return Make("select", 0.12f, Mono(t => Bell(t, 1568f, 0.04f, 0.3f) + 0.4f * Snap(t, 0.006f)));
                case Sfx.Deselect: return Make("deselect", 0.1f, Mono(t => Bell(t, 1175f, 0.035f, 0.25f)));
                case Sfx.Slide: return Make("slide", 0.28f, Swish(0.26f, 0.5f, 2400f, 6000f));
                case Sfx.Place: return Make("place", 0.22f, Layer(Swish(0.08f, 0.25f, 3000f, 5000f), Mono(t => Snap(t - 0.06f, 0.012f) * 0.9f + Thud(t - 0.06f, 180f, 0.03f, 0.35f))));
                case Sfx.Draw: return Make("draw", 0.25f, Swish(0.22f, 0.45f, 1800f, 4200f));
                case Sfx.Deal: return Make("deal", 0.12f, Layer(Swish(0.07f, 0.28f, 3500f, 6500f), Mono(t => Snap(t - 0.07f, 0.006f) * 0.5f)));
                case Sfx.Shuffle: return Make("shuffle", 0.9f, ShuffleRiffle());
                case Sfx.Check:
                    return Make("check", 0.9f, Mono(t => Bell(t, 1318.5f, 0.4f, 0.35f) + Bell(t - 0.08f, 1975.5f, 0.45f, 0.35f)
                                                        + Bell(t - 0.16f, 2637f, 0.35f, 0.2f)));
                case Sfx.Games:
                    return Make("games", 1.2f, Mono(t => Bell(t, 784f, 0.6f, 0.3f) + Bell(t - 0.1f, 988f, 0.6f, 0.3f)
                                                        + Bell(t - 0.2f, 1175f, 0.6f, 0.3f) + Bell(t - 0.3f, 1568f, 0.8f, 0.35f)));
                case Sfx.Penalty:
                    return Make("penalty", 0.55f, Mono(t => Buzz(t, 220f, 140f, 0.45f, 0.3f) + Thud(t, 90f, 0.08f, 0.4f)));
                case Sfx.Skip: return Make("skip", 0.4f, Mono(t => Sweep(t, 1400f, 500f, 0.32f, 0.25f)));
                case Sfx.YourTurn: return Make("turn", 0.5f, Mono(t => Bell(t, 1046.5f, 0.25f, 0.22f) + Bell(t - 0.09f, 1568f, 0.3f, 0.22f)));
                case Sfx.Suit: return Make("suit", 0.6f, Mono(t => Bell(t, 880f, 0.35f, 0.28f) + Bell(t - 0.06f, 1320f, 0.35f, 0.2f)));
                case Sfx.Victory:
                    return Make("victory", 1.8f, Mono(t => Bell(t, 523.25f, 1f, 0.3f) + Bell(t - 0.15f, 659.25f, 1f, 0.3f)
                                                          + Bell(t - 0.3f, 783.99f, 1f, 0.3f) + Bell(t - 0.45f, 1046.5f, 1.1f, 0.35f)));
                case Sfx.Defeat:
                    return Make("defeat", 1.8f, Mono(t => Bell(t, 659.25f, 1f, 0.3f) + Bell(t - 0.2f, 587.33f, 1f, 0.3f)
                                                         + Bell(t - 0.4f, 466.16f, 1.2f, 0.35f)));
                case Sfx.Eliminated:
                    return Make("eliminated", 1f, Mono(t => Bell(t, 523.25f, 0.6f, 0.3f) + Bell(t - 0.18f, 415.3f, 0.8f, 0.32f)));
                case Sfx.Illegal:
                    return Make("illegal", 0.2f, Mono(t => Thud(t, 95f, 0.07f, 0.5f) + Thud(t - 0.08f, 85f, 0.07f, 0.4f)));
                default: return Make("silence", 0.05f, Mono(t => 0f));
            }
        }

        // ---------------------------------------------------------------- Générateurs (échantillon par échantillon)

        /// <summary>Générateur d'échantillons : appelé dans l'ordre, une fois par échantillon (peut garder un état).</summary>
        delegate float Gen(float t);

        static Gen Mono(Func<float, float> f) => t => f(t);

        static Gen Layer(Gen a, Gen b) => t => a(t) + b(t);

        static AudioClip Make(string name, float seconds, Gen wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                data[i] = wave(i / (float)Rate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // Normalisation douce + fondu de sortie pour éviter les clics.
            float gain = peak > 0.9f ? 0.9f / peak : 1f;
            int fade = Mathf.Min(n, Rate / 100);
            for (int i = 0; i < n; i++)
            {
                data[i] *= gain;
                if (i > n - fade) data[i] *= (n - i) / (float)fade;
            }
            var clip = AudioClip.Create("check_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Bruit blanc reproductible.</summary>
        static float Noise(int i)
        {
            unchecked
            {
                uint x = (uint)i * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFF) / 32767.5f - 1f;
            }
        }

        /// <summary>Frottement de carte : bruit passe-bande dont la fréquence glisse, enveloppe en cloche.</summary>
        static Gen Swish(float duration, float amp, float f0, float f1)
        {
            float low = 0f, band = 0f;
            int i = 0;
            return t =>
            {
                i++;
                if (t > duration) return 0f;
                float u = t / duration;
                float f = Mathf.Lerp(f0, f1, u);
                // Filtre d'état variable (Chamberlin), résonance douce.
                float k = 2f * Mathf.Sin(Mathf.PI * f / Rate);
                float input = Noise(i);
                low += k * band;
                float high = input - low - 0.9f * band;
                band += k * high;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f));
                return amp * env * band;
            };
        }

        /// <summary>Mélange façon « riffle » : une rafale de petits claquements qui ralentit.</summary>
        static Gen ShuffleRiffle()
        {
            var ticks = new float[34];
            float acc = 0.02f;
            for (int k = 0; k < ticks.Length; k++)
            {
                ticks[k] = acc;
                acc += 0.012f + 0.012f * k / ticks.Length + 0.006f * (Noise(k * 31) * 0.5f + 0.5f);
            }
            var swish = Swish(0.85f, 0.18f, 1500f, 3500f);
            return t =>
            {
                float s = swish(t);
                foreach (var tk in ticks)
                {
                    float d = t - tk;
                    if (d >= 0f && d < 0.02f) s += Snap(d, 0.004f) * 0.45f;
                }
                return s;
            };
        }

        /// <summary>Claquement sec de carton : bruit très bref.</summary>
        static float Snap(float t, float decay)
        {
            if (t < 0f) return 0f;
            return Mathf.Exp(-t / decay) * Noise((int)(t * Rate) + 9173);
        }

        // Cloche : partiels légèrement inharmoniques.
        static float Bell(float t, float f, float decay, float amp)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 400f);
            return amp * env * (Mathf.Sin(2f * Mathf.PI * f * t)
                                + 0.45f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t / (decay * 0.5f))
                                + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 3.02f * t) * Mathf.Exp(-t / (decay * 0.3f)));
        }

        // Pose sur le feutre : sinus grave qui descend vite.
        static float Thud(float t, float f, float decay, float amp)
        {
            if (t < 0f) return 0f;
            float freq = f * (1f + 0.6f * Mathf.Exp(-t / 0.015f));
            return amp * Mathf.Exp(-t / decay) * Mathf.Sin(2f * Mathf.PI * freq * t);
        }

        // Bourdon descendant (pénalité) : dent de scie adoucie.
        static float Buzz(float t, float f0, float f1, float duration, float amp)
        {
            if (t < 0f || t > duration) return 0f;
            float u = t / duration;
            float phase = f0 * t + (f1 - f0) * t * u * 0.5f;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            return amp * (1f - u) * Mathf.Min(1f, t * 200f) * (0.6f * saw + 0.4f * Mathf.Sin(2f * Mathf.PI * phase));
        }

        static float Sweep(float t, float f0, float f1, float duration, float amp)
        {
            if (t < 0f || t > duration) return 0f;
            float u = t / duration;
            float phase = 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * u * 0.5f);
            return amp * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(phase);
        }
    }
}
