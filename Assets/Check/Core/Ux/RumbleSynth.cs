using System;
using System.Collections.Generic;

namespace Check.Core
{
    /// <summary>
    /// Roulement de rame synthétisé (aucun fichier audio) : grondement grave de bruit filtré, ronronnement des
    /// moteurs, et passage des joints de rails (« ta-dam ta-dam » des deux bogies) à intervalle régulier.
    /// La boucle se raccorde sans clic : la fin est fondue dans le début.
    /// </summary>
    public static class RumbleSynth
    {
        /// <summary>Intervalle entre deux passages de joint (secondes) ; la boucle en contient un nombre entier.</summary>
        public const float JointInterval = 1.6f;
        public const int JointsPerLoop = 5;
        public static float LoopSeconds => JointInterval * JointsPerLoop;

        /// <summary>Instants des chocs de roues dans la boucle : deux essieux par bogie, deux bogies par joint.</summary>
        public static List<float> WheelHits()
        {
            var hits = new List<float>();
            for (int j = 0; j < JointsPerLoop; j++)
            {
                float t = 0.3f + j * JointInterval;
                hits.Add(t);
                hits.Add(t + 0.11f);
                hits.Add(t + 0.62f);
                hits.Add(t + 0.73f);
            }
            return hits;
        }

        /// <summary>Échantillons mono de la boucle, entre -1 et 1.</summary>
        public static float[] Render(int sampleRate)
        {
            int n = (int)Math.Round(LoopSeconds * sampleRate);
            int fade = sampleRate / 2;
            var raw = new float[n + fade];
            var hits = WheelHits();
            var rng = new Random(1931);
            double low1 = 0, low2 = 0, band = 0, bandLow = 0;
            float dt = 1f / sampleRate;
            double kLow = 1 - Math.Exp(-2 * Math.PI * 90.0 / sampleRate);
            double kBand = 2 * Math.Sin(Math.PI * 420.0 / sampleRate);
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i * dt;
                double white = rng.NextDouble() * 2 - 1;
                // Grondement : bruit passe-bas (deux pôles) qui ondule lentement.
                low1 += kLow * (white - low1);
                low2 += kLow * (low1 - low2);
                double rumble = low2 * 3.2 * (0.85 + 0.15 * Math.Sin(2 * Math.PI * t / LoopSeconds * 2));
                // Souffle de la caisse : bruit passe-bande médium, très discret.
                bandLow += kBand * band;
                double high = white - bandLow - 1.2 * band;
                band += kBand * high;
                double hiss = band * 0.05;
                // Moteur : bourdon à 48 Hz et son harmonique.
                double hum = 0.06 * Math.Sin(2 * Math.PI * 48 * t) + 0.025 * Math.Sin(2 * Math.PI * 96 * t + 0.7);
                raw[i] = (float)(rumble + hiss + hum);
            }
            // Chocs de roues sur les joints (et leur écho dans la boucle suivante pour le fondu).
            foreach (var h0 in hits)
                for (int rep = 0; rep < 2; rep++)
                {
                    float h = h0 + rep * LoopSeconds;
                    int start = (int)(h * sampleRate);
                    int len = (int)(0.09f * sampleRate);
                    for (int k = 0; k < len && start + k < raw.Length; k++)
                    {
                        float u = k * dt;
                        double env = Math.Exp(-u / 0.018);
                        double thump = Math.Sin(2 * Math.PI * (75 + 60 * Math.Exp(-u / 0.01)) * u);
                        double click = Math.Sin(2 * Math.PI * 1150 * u) * Math.Exp(-u / 0.004) * 0.35;
                        raw[start + k] += (float)(env * (thump * 0.55 + click));
                    }
                }
            // Raccord : la queue (après la boucle) est fondue dans le début.
            var data = new float[n];
            Array.Copy(raw, data, n);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                data[i] = data[i] * w + raw[n + i] * (1f - w);
            }
            float peak = 0f;
            foreach (var v in data) peak = Math.Max(peak, Math.Abs(v));
            if (peak > 0f)
                for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;
            return data;
        }

        /// <summary>
        /// Tension de la partie (0 à 1) : monte quand un joueur encore en course n'a plus que deux cartes,
        /// et au maximum quand il n'en a plus qu'une.
        /// </summary>
        public static float Tension(IEnumerable<int> activeHandCounts)
        {
            float t = 0f;
            foreach (var c in activeHandCounts)
            {
                if (c == 1) return 1f;
                if (c == 2) t = 0.4f;
            }
            return t;
        }
    }
}
