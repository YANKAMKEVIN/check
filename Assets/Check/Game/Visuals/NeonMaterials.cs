using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>Couleurs néon génériques (héritées de KevChess) ; la palette des cartes est <see cref="AfroPalette"/>.</summary>
    public static class NeonPalette
    {
        /// <summary>Lueur des pièces blanches (cyan froid).</summary>
        public static readonly Color WhiteGlow = new Color(0.35f, 0.92f, 1.00f);
        /// <summary>Lueur des pièces noires (magenta / violet).</summary>
        public static readonly Color BlackGlow = new Color(1.00f, 0.22f, 0.85f);
        public static readonly Color Selection = new Color(0.45f, 1.00f, 0.95f);
        public static readonly Color LegalMove = new Color(0.55f, 0.85f, 1.00f);
        public static readonly Color Capture = new Color(1.00f, 0.38f, 0.55f);
        public static readonly Color LastMove = new Color(1.00f, 0.72f, 0.30f);
        public static readonly Color Check = new Color(1.00f, 0.08f, 0.12f);
        public static readonly Color Indigo = new Color(0.50f, 0.42f, 1.00f);
        public static readonly Color Fog = new Color(0.040f, 0.018f, 0.060f);

        public static Color Glow(bool white) => white ? WhiteGlow : BlackGlow;
    }

    /// <summary>
    /// Création des matériaux URP par code. Les matériaux renvoyés par les propriétés sont
    /// partagés (ne pas les détruire) ; utiliser <c>new Material(m)</c> pour une instance animée.
    ///
    /// Stripping en build : si la <see cref="NeonShaderLibrary"/> existe dans Resources,
    /// les matériaux sont clonés depuis ses gabarits (shader + mots-clés garantis dans le build).
    /// Sinon on retombe sur <see cref="Shader.Find"/> (fonctionne dans l'éditeur, risqué en build).
    /// </summary>
    public static class NeonMaterials
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string ParticleUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";
        public const string SkyboxShaderName = "Skybox/Cubemap";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int BlendId = Shader.PropertyToID("_Blend");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int WorkflowId = Shader.PropertyToID("_WorkflowMode");
        static readonly int PreserveSpecId = Shader.PropertyToID("_BlendModePreserveSpecular");
        static readonly int ReceiveShadowsId = Shader.PropertyToID("_ReceiveShadows");
        static readonly int EnvReflectionsId = Shader.PropertyToID("_EnvironmentReflections");
        static readonly int SpecHighlightsId = Shader.PropertyToID("_SpecularHighlights");
        static readonly int SkyTexId = Shader.PropertyToID("_Tex");
        static readonly int SkyTintId = Shader.PropertyToID("_Tint");
        static readonly int SkyExposureId = Shader.PropertyToID("_Exposure");

        /// <summary>Identifiant de la propriété d'émission (pour animer une instance).</summary>
        public static int EmissionColorProperty => EmissionColorId;
        /// <summary>Identifiant de la couleur de base.</summary>
        public static int BaseColorProperty => BaseColorId;

        static Material _sparks, _shards, _rings, _motes, _skybox;

        // ------------------------------------------------------------------ effets

        /// <summary>Étincelles / halos (particules billboard).</summary>
        public static Material Sparks => _sparks != null ? _sparks : (_sparks = CreateAdditive("Check_Sparks", NeonTextures.RadialGlow, Color.white * 4f, 40));

        /// <summary>Éclats de cristal (particules maillage).</summary>
        public static Material Shards => _shards != null ? _shards : (_shards = CreateAdditive("Check_Shards", null, Color.white * 2.5f, 39));

        /// <summary>Anneaux d'onde au sol.</summary>
        public static Material Rings => _rings != null ? _rings : (_rings = CreateAdditive("Check_Rings", NeonTextures.Ring, Color.white * 2.5f, 38));

        /// <summary>Poussières lumineuses d'ambiance.</summary>
        public static Material Motes => _motes != null ? _motes : (_motes = CreateAdditive("Check_Motes", NeonTextures.RadialGlow, Color.white * 1.6f, 30));

        /// <summary>Skybox nébuleuse générée par code.</summary>
        public static Material Skybox
        {
            get
            {
                if (_skybox != null) return _skybox;
                var lib = NeonShaderLibrary.Load();
                _skybox = lib != null && lib.skyboxCubemap != null
                    ? new Material(lib.skyboxCubemap)
                    : new Material(FindShader(SkyboxShaderName));
                _skybox.name = "Check_Skybox";
                _skybox.SetTexture(SkyTexId, NeonTextures.NebulaSky);
                _skybox.SetColor(SkyTintId, new Color(0.5f, 0.5f, 0.5f, 1f));
                _skybox.SetFloat(SkyExposureId, 1.6f);
                return _skybox;
            }
        }

        // ------------------------------------------------------------------ fabriques

        /// <summary>URP/Lit opaque émissif.</summary>
        public static Material CreateLitOpaque(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            var lib = NeonShaderLibrary.Load();
            Material m = lib != null && lib.litOpaqueEmissive != null ? new Material(lib.litOpaqueEmissive) : new Material(FindShader(LitShaderName));
            ConfigureLitOpaqueEmissive(m);
            m.name = name;
            m.SetColor(BaseColorId, baseColor);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothness);
            m.SetColor(EmissionColorId, emission);
            return m;
        }

        /// <summary>URP/Lit transparent (alpha, spéculaire et reflets préservés) émissif.</summary>
        public static Material CreateLitTransparent(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            var lib = NeonShaderLibrary.Load();
            Material m = lib != null && lib.litTransparentEmissive != null ? new Material(lib.litTransparentEmissive) : new Material(FindShader(LitShaderName));
            ConfigureLitTransparentEmissive(m);
            m.name = name;
            m.SetColor(BaseColorId, baseColor);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothness);
            m.SetColor(EmissionColorId, emission);
            return m;
        }

        /// <summary>URP/Particles/Unlit additif (couleur HDR × couleur de sommet × texture).</summary>
        public static Material CreateAdditive(string name, Texture texture, Color color, int queueOffset)
        {
            var lib = NeonShaderLibrary.Load();
            Material m = lib != null && lib.particleAdditive != null ? new Material(lib.particleAdditive) : new Material(FindShader(ParticleUnlitShaderName));
            ConfigureParticleAdditive(m);
            m.name = name;
            m.SetColor(BaseColorId, color);
            if (texture != null) m.SetTexture(BaseMapId, texture);
            m.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            return m;
        }

        // ------------------------------------------------------------------ configuration des gabarits
        // Ces fonctions sont aussi utilisées par le générateur d'assets de l'éditeur, pour que
        // les gabarits du build aient exactement les mêmes mots-clés que les matériaux runtime.

        public static void ConfigureLitOpaqueEmissive(Material m)
        {
            m.SetFloat(WorkflowId, 1f);
            m.SetFloat(SurfaceId, 0f);
            m.SetFloat(BlendId, 0f);
            m.SetFloat(SrcBlendId, (float)BlendMode.One);
            m.SetFloat(DstBlendId, (float)BlendMode.Zero);
            m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(DstBlendAlphaId, (float)BlendMode.Zero);
            m.SetFloat(ZWriteId, 1f);
            m.SetFloat(CullId, (float)CullMode.Back);
            m.SetFloat(ReceiveShadowsId, 1f);
            m.SetFloat(EnvReflectionsId, 1f);
            m.SetFloat(SpecHighlightsId, 1f);
            m.SetOverrideTag("RenderType", "Opaque");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.renderQueue = (int)RenderQueue.Geometry;
        }

        public static void ConfigureLitTransparentEmissive(Material m)
        {
            m.SetFloat(WorkflowId, 1f);
            m.SetFloat(SurfaceId, 1f);
            m.SetFloat(BlendId, 0f); // Alpha
            m.SetFloat(PreserveSpecId, 1f);
            // Spéculaire préservé : la couleur est prémultipliée dans le shader.
            m.SetFloat(SrcBlendId, (float)BlendMode.One);
            m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(DstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(CullId, (float)CullMode.Back);
            m.SetFloat(ReceiveShadowsId, 1f);
            m.SetFloat(EnvReflectionsId, 1f);
            m.SetFloat(SpecHighlightsId, 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static void ConfigureParticleAdditive(Material m)
        {
            m.SetFloat(SurfaceId, 1f);
            m.SetFloat(BlendId, 2f); // Additive
            m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, (float)BlendMode.One);
            m.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(DstBlendAlphaId, (float)BlendMode.One);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(CullId, (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.DisableKeyword("_EMISSION");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static Shader FindShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogError("[Check] Shader introuvable : " + name +
                               ". Vérifiez que URP est actif et que la NeonShaderLibrary est générée (menu Check > Visuals).");
                s = Shader.Find("Hidden/InternalErrorShader");
            }
            return s;
        }
    }
}
