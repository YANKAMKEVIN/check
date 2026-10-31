using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Effets lumineux ponctuels sans asset : flash, onde d'atterrissage, explosion de joker,
    /// onde « Check », confettis « Games ».
    /// Quatre systèmes de particules partagés (créés à la demande) sont réutilisés via
    /// <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> : aucun objet n'est instancié par effet.
    /// </summary>
    public static class NeonEffects
    {
        static GameObject _root;
        static ParticleSystem _sparks, _shards, _rings;
        static NeonFxRunner _runner;
        static Light _flashLight;

        /// <summary>La pièce se brise en éclats de lumière.</summary>
        public static void Shatter(Vector3 basePosition, float height, Color color)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams { startColor = Brighten(color), applyShapeToPosition = true };
            // Éclats répartis sur la hauteur de la pièce.
            for (int i = 0; i < 4; i++)
            {
                ep.position = basePosition + Vector3.up * (height * (0.15f + 0.22f * i));
                _shards.Emit(ep, 6);
            }
            var sp = new ParticleSystem.EmitParams { startColor = Brighten(color), applyShapeToPosition = true, position = basePosition + Vector3.up * height * 0.5f };
            _sparks.Emit(sp, 28);
            // Halo bref au cœur de l'explosion.
            var glow = new ParticleSystem.EmitParams
            {
                position = basePosition + Vector3.up * height * 0.45f,
                startColor = color,
                startSize = 1.6f,
                startLifetime = 0.35f,
                velocity = Vector3.zero,
            };
            _sparks.Emit(glow, 1);
            Ring(basePosition, color, 1.8f, 0.6f);
            Pulse(basePosition + Vector3.up * height * 0.5f, color, 5f, 0.35f);
        }

        /// <summary>Flash de lumière (promotion, mise en valeur).</summary>
        public static void Flash(Vector3 basePosition, Color color, float height = 1f)
        {
            Ensure();
            var glow = new ParticleSystem.EmitParams
            {
                position = basePosition + Vector3.up * height * 0.55f,
                startColor = color,
                startSize = 2.6f,
                startLifetime = 0.5f,
                velocity = Vector3.zero,
            };
            _sparks.Emit(glow, 1);
            var sp = new ParticleSystem.EmitParams { startColor = Brighten(color), applyShapeToPosition = true, position = basePosition + Vector3.up * height * 0.5f };
            _sparks.Emit(sp, 36);
            Ring(basePosition, color, 2.4f, 0.7f);
            Ring(basePosition, Color.white, 1.4f, 0.45f);
            Pulse(basePosition + Vector3.up * height * 0.6f, color, 9f, 0.5f);
        }

        /// <summary>Onde discrète quand une pièce se pose.</summary>
        public static void LandingPulse(Vector3 basePosition, Color color)
        {
            Ensure();
            Ring(basePosition, color, 1.1f, 0.4f);
        }


        // ------------------------------------------------------------------ effets du jeu de cartes

        static readonly Color[] Festive =
        {
            new Color(1.00f, 0.78f, 0.30f), new Color(0.30f, 0.95f, 1.00f), new Color(1.00f, 0.30f, 0.80f),
            new Color(1.00f, 0.95f, 0.85f), new Color(1.00f, 0.25f, 0.18f),
        };

        /// <summary>
        /// Explosion de lueur quand un joker tombe (sur le tas en général) : flash, gerbe d'étincelles
        /// et d'éclats, trois anneaux au sol et éclair de lumière. Rouge/or pour le joker rouge,
        /// violet/cyan pour le joker noir.
        /// </summary>
        public static void JokerBurst(Vector3 position, bool redJoker)
        {
            Ensure();
            Color a = redJoker ? new Color(1f, 0.18f, 0.14f) : new Color(0.50f, 0.32f, 1f);
            Color b = redJoker ? new Color(1f, 0.76f, 0.30f) : new Color(0.30f, 0.95f, 1f);
            _sparks.Emit(new ParticleSystem.EmitParams { position = position + Vector3.up * 0.25f, startColor = a, startSize = 2.2f, startLifetime = 0.5f, velocity = Vector3.zero }, 1);
            _sparks.Emit(new ParticleSystem.EmitParams { position = position + Vector3.up * 0.25f, startColor = new Color(1f, 1f, 1f, 0.6f), startSize = 0.9f, startLifetime = 0.25f, velocity = Vector3.zero }, 1);
            for (int i = 0; i < 80; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.8f + 0.15f;
                _sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.15f,
                    velocity = d.normalized * Random.Range(1.8f, 5.2f),
                    startColor = Brighten(i % 3 == 0 ? b : a),
                    startSize = Random.Range(0.04f, 0.13f),
                    startLifetime = Random.Range(0.5f, 1.1f),
                }, 1);
            }
            for (int i = 0; i < 26; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) + 0.3f;
                _shards.Emit(new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.2f,
                    velocity = d.normalized * Random.Range(1.5f, 3.6f),
                    startColor = Brighten(i % 2 == 0 ? b : a),
                    startSize = Random.Range(0.05f, 0.1f),
                    startLifetime = Random.Range(0.7f, 1.2f),
                }, 1);
            }
            Ring(position, a, 4.2f, 0.75f);
            Ring(position, b, 2.6f, 0.55f);
            Ring(position, Color.white, 1.3f, 0.35f);
            Pulse(position + Vector3.up * 0.6f, a, 12f, 0.7f);
        }

        /// <summary>Onde de choc dorée en trois vagues quand un joueur annonce « Check ».</summary>
        public static void CheckWave(Vector3 position)
        {
            Ensure();
            _runner.StartCoroutine(Wave(position));
            Pulse(position + Vector3.up * 0.8f, new Color(1f, 0.72f, 0.3f), 9f, 0.9f);
        }

        static IEnumerator Wave(Vector3 position)
        {
            Color[] colors = { new Color(1f, 0.75f, 0.3f), new Color(0.3f, 0.95f, 1f), Color.white };
            for (int w = 0; w < 3; w++)
            {
                Ring(position, new Color(colors[w].r, colors[w].g, colors[w].b, 0.16f), 7f + w * 2.2f, 1.0f);
                const int n = 36;
                for (int i = 0; i < n; i++)
                {
                    float ang = (i + w * 0.33f) * Mathf.PI * 2f / n;
                    var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    _sparks.Emit(new ParticleSystem.EmitParams
                    {
                        position = position + d * 0.3f + Vector3.up * 0.05f,
                        velocity = d * (4.2f + w) + Vector3.up * 0.6f,
                        startColor = Brighten(colors[w]),
                        startSize = 0.09f,
                        startLifetime = 0.9f,
                    }, 1);
                }
                yield return new WaitForSeconds(0.16f);
            }
        }

        /// <summary>Célébration « Games » : quatre gerbes de confettis lumineux multicolores et anneaux.</summary>
        public static void GamesCelebration(Vector3 position)
        {
            Ensure();
            _runner.StartCoroutine(Celebrate(position));
        }

        static IEnumerator Celebrate(Vector3 position)
        {
            for (int burst = 0; burst < 4; burst++)
            {
                Vector3 origin = position + new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, Random.Range(-0.4f, 0.4f));
                for (int i = 0; i < 26; i++)
                {
                    Vector3 d = new Vector3(Random.Range(-0.7f, 0.7f), 1f, Random.Range(-0.7f, 0.7f)).normalized;
                    Color c = Festive[Random.Range(0, Festive.Length)];
                    _shards.Emit(new ParticleSystem.EmitParams
                    {
                        position = origin,
                        velocity = d * Random.Range(3.2f, 5.6f),
                        startColor = Brighten(c),
                        startSize = Random.Range(0.06f, 0.12f),
                        startLifetime = Random.Range(1.4f, 2.1f),
                    }, 1);
                    if (i % 2 == 0)
                        _sparks.Emit(new ParticleSystem.EmitParams
                        {
                            position = origin,
                            velocity = d * Random.Range(2.5f, 5f),
                            startColor = Brighten(c),
                            startSize = Random.Range(0.05f, 0.1f),
                            startLifetime = Random.Range(0.8f, 1.4f),
                        }, 1);
                }
                Ring(origin, Festive[burst % Festive.Length] * 0.7f, 2.4f, 0.7f);
                Pulse(origin + Vector3.up * 1.5f, Festive[burst % Festive.Length], 3.5f, 0.4f);
                yield return new WaitForSeconds(0.28f);
            }
        }

        static void Ring(Vector3 position, Color color, float size, float lifetime)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = position + Vector3.up * 0.02f,
                startColor = color,
                startSize = size,
                startLifetime = lifetime,
                velocity = Vector3.zero,
                rotation3D = new Vector3(90f, 0f, 0f),
            };
            _rings.Emit(ep, 1);
        }

        static Coroutine _pulse;

        static void Pulse(Vector3 position, Color color, float intensity, float duration)
        {
            if (_runner == null) return;
            if (_pulse != null) _runner.StopCoroutine(_pulse);
            _pulse = _runner.StartCoroutine(PulseLight(position, color, intensity, duration));
        }

        static IEnumerator PulseLight(Vector3 position, Color color, float intensity, float duration)
        {
            _flashLight.transform.position = position;
            _flashLight.color = color;
            _flashLight.enabled = true;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                _flashLight.intensity = intensity * k * k;
                yield return null;
            }
            _flashLight.enabled = false;
            _pulse = null;
        }

        static Color32 Brighten(Color c)
        {
            // Les couleurs de particules sont 8 bits : on éclaircit vers le blanc, l'intensité HDR vient du matériau.
            Color b = Color.Lerp(c, Color.white, 0.25f);
            b.a = 1f;
            return b;
        }

        // ------------------------------------------------------------------ création

        static void Ensure()
        {
            if (_root != null) return;
            _root = new GameObject("Check FX");
            _runner = _root.AddComponent<NeonFxRunner>();

            var lightGo = new GameObject("Flash Light");
            lightGo.transform.SetParent(_root.transform, false);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 5f;
            _flashLight.shadows = LightShadows.None;
            _flashLight.enabled = false;

            _sparks = CreateSystem("Sparks", NeonMaterials.Sparks);
            {
                var main = _sparks.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
                main.gravityModifier = 0.35f;
                main.maxParticles = 900;
                var shape = _sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.18f;
                var limit = _sparks.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 6f;
                limit.dampen = 0.12f;
                var col = _sparks.colorOverLifetime;
                col.enabled = true;
                col.color = FadeGradient();
                var size = _sparks.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            }

            _shards = CreateSystem("Shards", NeonMaterials.Shards);
            {
                var main = _shards.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 1.1f;
                main.maxParticles = 400;
                var shape = _shards.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;
                var rot = _shards.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                var col = _shards.colorOverLifetime;
                col.enabled = true;
                col.color = FadeGradient();
                var size = _shards.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                var pr = _shards.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Mesh;
                pr.mesh = Shard();
                pr.alignment = ParticleSystemRenderSpace.Local;
            }

            _rings = CreateSystem("Rings", NeonMaterials.Rings);
            {
                var main = _rings.main;
                main.startLifetime = 0.5f;
                main.startSpeed = 0f;
                main.startSize = 1f;
                main.startRotation3D = true;
                main.maxParticles = 64;
                var shape = _rings.shape;
                shape.enabled = false;
                var col = _rings.colorOverLifetime;
                col.enabled = true;
                col.color = FadeGradient();
                var size = _rings.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
                var pr = _rings.GetComponent<ParticleSystemRenderer>();
                pr.alignment = ParticleSystemRenderSpace.World; // anneau couché au sol (rotation3D X = 90°)
            }

            _sparks.Play();
            _shards.Play();
            _rings.Play();
        }

        static ParticleSystem CreateSystem(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true; // le système reste « en lecture » sans rien émettre : Emit() suffit
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.enabled = false; // émission uniquement via Emit()
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = material;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.enableGPUInstancing = false;
            pr.sortingFudge = -10f;
            return ps;
        }

        static ParticleSystem.MinMaxGradient FadeGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static Mesh _shard;

        /// <summary>Éclat de cristal pour les particules.</summary>
        static Mesh Shard()
        {
            if (_shard != null) return _shard;
            var b = new MeshBuilder(1);
            b.GemAlong(0, Vector3.zero, new Vector3(0.2f, 1f, 0.1f), 0.32f, 1f, 4);
            _shard = b.ToMesh("Check_Shard");
            return _shard;
        }
    }
}
