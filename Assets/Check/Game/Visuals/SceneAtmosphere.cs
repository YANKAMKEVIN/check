using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Met en place l'ambiance au runtime (« Dernier Métro ») : caméra en perspective cadrée sur le plateau,
    /// plafonnier ambré à ombres douces au-dessus de la table, lueur froide de la ville par les fenêtres,
    /// nuit bleutée en fond, éclairage ambiant, brouillard léger, sonde de réflexion,
    /// poussière dans la lumière et post-traitement URP (dans SceneAtmosphere.Urp.cs).
    /// À placer une fois dans la scène (sur un objet vide). Tout est créé dans Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SceneAtmosphere : MonoBehaviour
    {
        [Header("Caméra")]
        [SerializeField, Tooltip("Caméra à configurer (Camera.main si vide).")]
        Camera targetCamera;
        [SerializeField] bool configureCamera = true;
        [SerializeField, Range(30f, 85f), Tooltip("Inclinaison de la caméra au-dessus du plateau (degrés).")]
        float cameraPitch = 56f;
        [SerializeField, Range(25f, 70f), Tooltip("FOV vertical en paysage (macOS).")] float fieldOfView = 40f;
        [SerializeField, Range(25f, 80f), Tooltip("FOV vertical en portrait (téléphone) : plus large pour garder le plateau proche.")] float portraitFieldOfView = 58f;
        [SerializeField, Tooltip("Marge autour du plateau lors du cadrage automatique.")]
        float framingMargin = 0.35f;
        [SerializeField, Tooltip("Demi-largeur de la zone à cadrer (table).")]
        float framingExtent = 4.5f;
        [SerializeField, Tooltip("Vue côté blancs (sinon côté noirs).")]
        bool viewFromWhite = true;

        [Header("Lumières")]
        [SerializeField] bool createLights = true;
        [SerializeField] float sunIntensity = 1.1f;
        [SerializeField] float accentLightIntensity = 7f;

        [Header("Environnement")]
        [SerializeField] bool useNebulaSky = false;
        [SerializeField] bool enableFog = true;
        [SerializeField, Range(0f, 0.08f)] float fogDensity = 0.014f;
        [SerializeField, Tooltip("Sonde de réflexion temps réel rendue une seule fois (reflets du plateau dans le cristal).")]
        bool useReflectionProbe = true;
        [SerializeField, Tooltip("Couches capturées par la sonde (excluez celle des pièces si vous en créez une).")]
        LayerMask reflectionMask = ~0;
        [SerializeField] bool floatingMotes = true;

        [Header("URP")]
        [SerializeField] bool postProcessing = true;
        [SerializeField, Tooltip("Active le HDR sur l'asset URP si nécessaire (modifie l'asset du projet !).")]
        bool forcePipelineHdr = false;

        /// <summary>Lumière directionnelle principale créée.</summary>
        public Light Sun { get; private set; }
        public ReflectionProbe Probe { get; private set; }
        public Camera Camera => targetCamera;

        /// <summary>
        /// Désactive le cadrage automatique quand une autre caméra (orbite) gère la position.
        /// À régler avant Awake : créer l'objet inactif, ajouter le composant, régler, puis activer.
        /// </summary>
        public bool ManageCameraFraming
        {
            get => configureCamera;
            set => configureCamera = value;
        }

        public void SetTargetCamera(Camera cam) => targetCamera = cam;

        int _lastScreenW, _lastScreenH;
        bool _probeRequested;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (createLights) SetupLights();
            SetupEnvironment();
            if (useReflectionProbe) SetupReflectionProbe();
            if (floatingMotes) SetupMotes();
            SetupUrpPipeline();
            if (targetCamera != null)
            {
                if (configureCamera) ConfigureCamera(targetCamera);
                else
                {
                    targetCamera.allowHDR = true;
                    targetCamera.allowMSAA = true;
                    targetCamera.clearFlags = useNebulaSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    targetCamera.backgroundColor = WagonDecor.Sky;
                }
                SetupUrpCamera(targetCamera);
            }
            if (postProcessing) SetupPostProcessing();
        }

        void Start()
        {
            // Rendu de la sonde après la construction du plateau (qui a lieu en général dans Awake/Start).
            if (useReflectionProbe) _probeRequested = true;
        }

        void LateUpdate()
        {
            if (_probeRequested && Probe != null)
            {
                _probeRequested = false;
                Probe.RenderProbe();
            }
            if (configureCamera && targetCamera != null && (Screen.width != _lastScreenW || Screen.height != _lastScreenH))
                FrameBoard();
        }

        // ------------------------------------------------------------------ API

        /// <summary>Re-rend la sonde de réflexion (à appeler après un changement de décor).</summary>
        public void RefreshReflections()
        {
            if (Probe != null) Probe.RenderProbe();
        }

        /// <summary>Place la caméra du côté des blancs ou des noirs.</summary>
        public void SetViewSide(bool white)
        {
            viewFromWhite = white;
            FrameBoard();
        }

        /// <summary>Recadre la caméra pour que le plateau tienne à l'écran (portrait ou paysage).</summary>
        public void FrameBoard()
        {
            if (targetCamera == null) return;
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            Camera cam = targetCamera;
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float fov = aspect < 1f ? portraitFieldOfView : fieldOfView;
            cam.fieldOfView = fov;
            float halfV = fov * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            float extent = framingExtent + framingMargin;

            // Largeur : le bord proche du plateau est le plus large à l'écran ; approximation conservatrice.
            float distH = extent / Mathf.Tan(halfH) + extent * Mathf.Cos(cameraPitch * Mathf.Deg2Rad) * 0.6f;
            // Profondeur : le plateau vu incliné occupe ~ extent * sin(pitch) en vertical.
            float distV = extent * Mathf.Sin(cameraPitch * Mathf.Deg2Rad) / Mathf.Tan(halfV) + extent * 0.5f;
            float dist = Mathf.Max(distH, distV);

            float yaw = viewFromWhite ? 0f : 180f;
            Quaternion rot = Quaternion.Euler(cameraPitch, yaw, 0f);
            Vector3 focus = new Vector3(0f, 0f, viewFromWhite ? 0.25f : -0.25f);
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * dist, rot);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
        }

        // ------------------------------------------------------------------ mise en place

        void ConfigureCamera(Camera cam)
        {
            cam.orthographic = false;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = useNebulaSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = WagonDecor.Sky;
            FrameBoard();
        }

        void SetupLights()
        {
            // Lumière principale : le plafonnier, chaud, presque à la verticale de la table, ombres douces.
            var sunGo = new GameObject("Ceiling Light");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(72f, -20f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.88f, 0.72f);
            Sun.intensity = sunIntensity;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.85f;
            Sun.shadowBias = 0.03f;
            Sun.shadowNormalBias = 0.25f;
            RenderSettings.sun = Sun;
            SetupUrpLight(Sun);

            // Appoints : ambre sous le plafond (boiseries, laiton), bleu froid de la ville par les fenêtres.
            CreatePointLight("Lamp Amber", new Vector3(0f, 7.5f, 1.5f), new Color(1f, 0.72f, 0.42f), accentLightIntensity * 0.6f, 14f);
            CreatePointLight("City Left", new Vector3(-6.0f, 2.2f, 3.0f), new Color(0.45f, 0.62f, 1f), accentLightIntensity * 0.45f, 9f);
            CreatePointLight("City Right", new Vector3(6.0f, 2.2f, 3.0f), new Color(0.45f, 0.62f, 1f), accentLightIntensity * 0.45f, 9f);
        }

        void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None; // ombres réservées à la lumière principale (perf mobile)
        }

        void SetupEnvironment()
        {
            if (useNebulaSky)
            {
                RenderSettings.skybox = NeonMaterials.Skybox;
                // Réflexion d'environnement : le même ciel, pour que le cristal reflète la nébuleuse.
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = NeonTextures.NebulaSky;
                RenderSettings.reflectionIntensity = 1f;
            }

            // Ambiance : nuit bleutée en haut, chaleur des boiseries au milieu, sol sombre.
            Color sky = new Color(0.12f, 0.15f, 0.22f);
            Color equator = new Color(0.13f, 0.10f, 0.09f);
            Color ground = new Color(0.03f, 0.03f, 0.035f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(equator);
            sh.AddDirectionalLight(Vector3.up, sky - equator * 0.5f, 0.9f);
            sh.AddDirectionalLight(Vector3.down, ground, 0.3f);
            RenderSettings.ambientProbe = sh;

            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = WagonDecor.Sky;
        }

        void SetupReflectionProbe()
        {
            var go = new GameObject("Neon Reflection Probe");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0.6f, 0f);
            Probe = go.AddComponent<ReflectionProbe>();
            Probe.mode = ReflectionProbeMode.Realtime;
            Probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            Probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            Probe.resolution = 128;
            Probe.hdr = true;
            Probe.boxProjection = true; // effectif si « Box Projection » est coché sur l'asset URP
            Probe.size = new Vector3(10f, 4f, 10f);
            Probe.center = Vector3.zero;
            Probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            Probe.cullingMask = reflectionMask;
            Probe.nearClipPlane = 0.05f;
            Probe.farClipPlane = 80f;
            Probe.intensity = 1f;
            Probe.importance = 1;
        }

        void SetupMotes()
        {
            var go = new GameObject("Neon Motes");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 1.6f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.82f, 0.6f, 0.35f), new Color(1f, 0.9f, 0.75f, 0.25f));
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.004f; // dérive très lente vers le haut

            var emission = ps.emission;
            emission.rateOverTime = 4f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(15f, 3.2f, 15f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.y = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = NeonMaterials.Motes;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.enableGPUInstancing = false;
            ps.Play();
        }

        // ------------------------------------------------------------------ parties URP (SceneAtmosphere.Urp.cs)

        partial void SetupUrpPipeline();
        partial void SetupUrpCamera(Camera cam);
        partial void SetupUrpLight(Light light);
        partial void SetupPostProcessing();
    }
}
