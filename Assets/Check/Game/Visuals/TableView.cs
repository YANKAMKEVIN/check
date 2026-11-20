using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    /// <summary>
    /// La table et son décor. CONTRAT public utilisé par la couche de jeu : garder ces signatures.
    /// Repère : table centrée en (0, 0, 0), plateau à y = <see cref="SurfaceY"/>, le joueur humain
    /// est assis côté -Z (bas de l'écran), les adversaires répartis sur l'arc du fond.
    ///
    /// Rendu (« Dernier Métro ») : plateau en linoléum bleu pétrole mat et uni, pour que les cartes
    /// ressortent ; chant en bois verni filé de laiton, discrète lueur ambrée vers la place active,
    /// emplacements crème de la pioche et du tas (agrandis : <see cref="CenterCardScale"/>),
    /// vignette rouge pulsée quand une pénalité s'accumule.
    /// Décor : wagon rétro aménagé en salon (voir <see cref="WagonDecor"/>). Aucune lumière ajoutée ici.
    /// Le visuel du plateau est 3,5 mm sous y = 0 : une carte centrée en y = 0 repose dessus sans clignoter.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        public const float SurfaceY = 0f;
        /// <summary>Demi-largeur (X) et demi-profondeur (Z) de la surface de jeu.</summary>
        public const float HalfWidth = 3.2f;
        public const float HalfDepth = 4.2f;

        /// <summary>Échelle des cartes de la pioche et du tas (plus grandes que les mains adverses, pour bien lire la carte du dessus).</summary>
        public const float CenterCardScale = 1.7f;

        public Vector3 DrawPilePosition => new Vector3(-0.78f, SurfaceY, 0.4f);
        public Vector3 DiscardPosition => new Vector3(0.78f, SurfaceY, 0.4f);

        /// <summary>Niveau de tension affiché (0..1, lissé).</summary>
        public float TensionLevel => tension;
        /// <summary>Pénalité en attente transmise au dernier <see cref="SetTension"/>.</summary>
        public int PendingDraw { get; private set; }

        const float TopY = SurfaceY - 0.0035f;
        const float FloorY = -2.4f;
        const int RimSamples = 192;

        static readonly Color StripBase = new Color(0.30f, 0.19f, 0.09f) * 0.35f;
        static readonly Color StripSeat = new Color(1.00f, 0.66f, 0.30f);
        static readonly Color StripColor = Color.white * 0.9f;
        static readonly Color SeatHaloColor = new Color(1.0f, 0.70f, 0.38f) * 0.35f;
        static readonly Color SlotColor = new Color(0.95f, 0.88f, 0.74f) * 0.32f;

        bool built;
        Material surfaceMat, stripMat, drawSlotMat, discardSlotMat, haloMat, tensionMat;
        Material[] accentMats;
        Color[] accentBase;
        Mesh stripMesh;
        Color[] stripColors;
        float[] stripParams;
        Transform halo, ringA, ringB;
        float seatParam, seatTarget, haloAlpha, haloAlphaTarget;
        bool seatDirty;
        float tension, tensionTarget;
        float surfaceFlash, drawFlash, discardFlash;
        Color flashColor = Color.white;
        Color baseFog;
        bool fogCaptured;

        public void Build()
        {
            if (built) return;
            built = true;
            BuildSurface();
            BuildRim();
            BuildSlots();
            BuildHaloAndTension();
            BuildBackdrop();
            SetActiveSeat(new Vector3(0f, 0f, -HalfDepth));
            haloAlpha = haloAlphaTarget;
        }

        PaintedScene painted;

        /// <summary>Nouvelle partie : un personnage assis par adversaire (index 0 = joueur humain, ignoré).</summary>
        public void SetPlayers(bool[] female, int humanPortrait = 0) { if (painted != null) painted.SetPlayers(female, humanPortrait); }

        /// <summary>Haut de la tête du personnage peint de l'adversaire (monde) ; faux sans décor peint.</summary>
        public bool TryGetHeadPoint(int playerId, out Vector3 world)
        {
            world = Vector3.zero;
            return painted != null && painted.TryGetHeadPoint(playerId, out world);
        }

        /// <summary>Portrait peint de l'adversaire (visage cadré) ; faux sans décor peint ou pour le joueur humain.</summary>
        public bool TryGetPortrait(int playerId, out Texture2D texture, out Rect face)
        {
            texture = null;
            face = new Rect(0f, 0f, 1f, 1f);
            return painted != null && painted.TryGetPortrait(playerId, out texture, out face);
        }

        /// <summary>Le personnage dont c'est le tour se redresse ; -1 : personne.</summary>
        public void SetActivePlayer(int playerId) { if (painted != null) painted.SetActive(playerId); }

        public void ReactPlayer(int playerId, SeatReaction reaction) { if (painted != null) painted.React(playerId, reaction); }

        /// <summary>Indique au décor à qui est le tour (halo, lumière…). Seat = place à table, n = joueurs.</summary>
        public void SetActiveSeat(Vector3 seatPosition)
        {
            Vector3 local = transform.InverseTransformPoint(seatPosition);
            if (new Vector2(local.x, local.z).sqrMagnitude < 1e-4f) { haloAlphaTarget = 0f; return; }
            seatTarget = TableArt.Param(local.x, local.z, HalfWidth, HalfDepth);
            haloAlphaTarget = 1f;
            seatDirty = true;
        }

        /// <summary>Teinte d'ambiance selon la pénalité en attente (0 = aucune).</summary>
        public void SetTension(int pendingDraw)
        {
            PendingDraw = Mathf.Max(0, pendingDraw);
            tensionTarget = PendingDraw <= 0 ? 0f : Mathf.Clamp01(0.32f + 0.085f * PendingDraw);
            if (tensionTarget > 0f && !fogCaptured)
            {
                baseFog = RenderSettings.fogColor;
                fogCaptured = true;
            }
        }

        // ------------------------------------------------------------------ effets (API ajoutée)

        /// <summary>Onde dorée qui balaie la table quand un joueur annonce « Check ».</summary>
        public void PlayCheckWave(Vector3 origin)
        {
            NeonEffects.CheckWave(origin);
            Flash(new Color(1f, 0.75f, 0.35f), 0.6f);
        }

        /// <summary>Explosion de lueur sur le tas quand un joker tombe (rouge ou noir).</summary>
        public void PlayJokerBurst(bool redJoker)
        {
            NeonEffects.JokerBurst(DiscardPosition, redJoker);
            Flash(redJoker ? new Color(1f, 0.25f, 0.2f) : new Color(0.55f, 0.4f, 1f), 0.9f);
            discardFlash = 1.6f;
        }

        /// <summary>Célébration « Games » (fin de manche) au-dessus de la place du gagnant.</summary>
        public void PlayGames(Vector3 winnerPosition)
        {
            NeonEffects.GamesCelebration(winnerPosition);
            Flash(new Color(1f, 0.8f, 0.4f), 0.6f);
        }

        /// <summary>Petite impulsion lumineuse de l'emplacement du tas (carte posée).</summary>
        public void PulseDiscard() { discardFlash = 1f; NeonEffects.LandingPulse(DiscardPosition, new Color(1f, 0.7f, 0.3f)); }

        /// <summary>Petite impulsion lumineuse de l'emplacement de la pioche (carte piochée).</summary>
        public void PulseDrawPile() => drawFlash = 1f;

        void Flash(Color c, float amount)
        {
            flashColor = c;
            surfaceFlash = Mathf.Max(surfaceFlash, amount);
        }

        // ------------------------------------------------------------------ animation

        void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime, t = Time.time;

            // Place active : le halo glisse le long du bord.
            float k = 1f - Mathf.Exp(-dt * 6f);
            if (seatDirty || Mathf.Abs(Mathf.DeltaAngle(seatParam * Mathf.Rad2Deg, seatTarget * Mathf.Rad2Deg)) > 0.05f)
            {
                seatParam = Mathf.LerpAngle(seatParam * Mathf.Rad2Deg, seatTarget * Mathf.Rad2Deg, seatDirty && haloAlpha < 0.01f ? 1f : k) * Mathf.Deg2Rad;
                seatDirty = false;
                Vector2 e = TableArt.SuperEllipse(seatParam, HalfWidth, HalfDepth) * 0.80f;
                halo.localPosition = new Vector3(e.x, TopY + 0.002f, e.y);
                halo.localRotation = Quaternion.Euler(0f, 90f - seatParam * Mathf.Rad2Deg, 0f);
                UpdateStripColors();
            }
            haloAlpha = Mathf.MoveTowards(haloAlpha, haloAlphaTarget, dt * 3f);

            tension = Mathf.MoveTowards(tension, tensionTarget, dt * 0.8f);
            float pulse = tension > 0f ? 0.5f + 0.5f * Mathf.Sin(t * (3f + 4f * tension)) : 0f;
            surfaceFlash = Mathf.MoveTowards(surfaceFlash, 0f, dt * 2.2f);
            drawFlash = Mathf.MoveTowards(drawFlash, 0f, dt * 2.5f);
            discardFlash = Mathf.MoveTowards(discardFlash, 0f, dt * 2.5f);

            // Plateau mat : il ne s'allume qu'un instant (Check, joker…), et rougit à peine sous la pression.
            surfaceMat.SetColor(NeonMaterials.EmissionColorProperty, flashColor * (surfaceFlash * 0.12f) + new Color(0.10f, 0.0f, 0.0f) * (tension * pulse));
            stripMat.SetColor(NeonMaterials.BaseColorProperty, Color.Lerp(StripColor, NeonPalette.Check * 3.2f, tension) * (1f + 0.5f * tension * pulse) + flashColor * surfaceFlash);
            float breathe = 0.85f + 0.15f * Mathf.Sin(t * 1.7f);
            haloMat.SetColor(NeonMaterials.BaseColorProperty, Color.Lerp(SeatHaloColor, NeonPalette.Check * 1.4f, tension * 0.6f) * (haloAlpha * breathe));
            tensionMat.SetColor(NeonMaterials.BaseColorProperty, NeonPalette.Check * (tension * (0.45f + 0.55f * pulse) * 1.3f));
            drawSlotMat.SetColor(NeonMaterials.BaseColorProperty, SlotColor * (1f + 3f * drawFlash));
            discardSlotMat.SetColor(NeonMaterials.BaseColorProperty, SlotColor * (1f + 3f * discardFlash));

            for (int i = 0; i < accentMats.Length; i++)
                accentMats[i].SetColor(NeonMaterials.EmissionColorProperty, Color.Lerp(accentBase[i], NeonPalette.Check * 2.6f, tension * 0.85f) * (1f + 0.25f * tension * pulse));
            if (fogCaptured)
                RenderSettings.fogColor = Color.Lerp(baseFog, new Color(0.12f, 0.012f, 0.025f), tension * 0.85f);

            if (ringA != null)
            {
                ringA.localRotation = Quaternion.Euler(0f, 0f, t * 4f) ;
                ringB.localRotation = Quaternion.Euler(0f, 0f, -t * 6.5f);
            }
        }

        void UpdateStripColors()
        {
            for (int i = 0; i < stripParams.Length; i++)
            {
                float d = Mathf.DeltaAngle(stripParams[i] * Mathf.Rad2Deg, seatParam * Mathf.Rad2Deg) / 30f;
                float g = Mathf.Exp(-d * d) * haloAlphaTarget;
                Color c = StripBase + StripSeat * g;
                c.a = 1f;
                stripColors[i * 2] = c;
                stripColors[i * 2 + 1] = c;
            }
            stripMesh.SetColors(stripColors);
        }

        // ------------------------------------------------------------------ construction : table

        void BuildSurface()
        {
            surfaceMat = NeonMaterials.CreateLitOpaque("Check_TableSurface", Color.white, 0f, 0.18f, Color.black);
            surfaceMat.SetTexture("_BaseMap", LinoleumTexture());

            var verts = new List<Vector3> { new Vector3(0f, TopY, 0f) };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            for (int i = 0; i < RimSamples; i++)
            {
                Vector2 e = TableArt.SuperEllipse(i * Mathf.PI * 2f / RimSamples, HalfWidth, HalfDepth);
                verts.Add(new Vector3(e.x, TopY, e.y));
                uvs.Add(new Vector2(e.x / (2f * HalfWidth) + 0.5f, e.y / (2f * HalfDepth) + 0.5f));
            }
            var tris = new List<int>();
            for (int i = 0; i < RimSamples; i++) AddTri(tris, verts, 0, 1 + i, 1 + (i + 1) % RimSamples, Vector3.up);
            var mesh = new Mesh { name = "Check_TableSurface" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AddRenderer("Surface", mesh, new[] { surfaceMat }, false, true);
        }

        // Profil de la bordure : (décalage vers l'extérieur, hauteur).
        static readonly Vector2[] RimProfile =
        {
            new Vector2(0.000f, TopY), new Vector2(0.000f, 0.030f), new Vector2(0.030f, 0.062f), new Vector2(0.110f, 0.080f),
            new Vector2(0.250f, 0.072f), new Vector2(0.360f, 0.035f), new Vector2(0.385f, -0.040f), new Vector2(0.365f, -0.200f),
            new Vector2(0.250f, -0.250f),
        };

        void BuildRim()
        {
            int n = RimSamples;
            var pts = new Vector2[n];
            var nrm = new Vector2[n];
            for (int i = 0; i < n; i++) pts[i] = TableArt.SuperEllipse(i * Mathf.PI * 2f / n, HalfWidth, HalfDepth);
            for (int i = 0; i < n; i++)
            {
                Vector2 tng = (pts[(i + 1) % n] - pts[(i + n - 1) % n]).normalized;
                nrm[i] = new Vector2(tng.y, -tng.x);
            }

            // Normales lissées du profil.
            int m = RimProfile.Length;
            var pn = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                Vector2 a = RimProfile[Mathf.Max(0, j - 1)], b = RimProfile[Mathf.Min(m - 1, j + 1)];
                Vector2 tg = (b - a).normalized;
                pn[j] = new Vector2(tg.y, -tg.x); // vers l'extérieur / le haut pour un profil parcouru de l'intérieur vers le bas
                if (j < 2) pn[j] = Vector2.Lerp(new Vector2(-1f, 0f), pn[j], 0.6f).normalized;
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                int ii = i % n;
                for (int j = 0; j < m; j++)
                {
                    Vector2 o = pts[ii] + nrm[ii] * RimProfile[j].x;
                    verts.Add(new Vector3(o.x, RimProfile[j].y, o.y));
                    var nn = new Vector3(nrm[ii].x * pn[j].x, pn[j].y, nrm[ii].y * pn[j].x).normalized;
                    norms.Add(nn);
                    uvs.Add(new Vector2(i / (float)n * 16f, j / (float)(m - 1)));
                }
            }
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            for (int j = 0; j < m - 1; j++)
            {
                int a = i * m + j, b = (i + 1) * m + j;
                Vector3 expected = norms[a] + norms[b + 1];
                AddTri(tris, verts, a, b, b + 1, expected);
                AddTri(tris, verts, a, b + 1, a + 1, expected);
            }

            // Incrustations d'émail indigo (losanges) sur le dessus de la bordure.
            var inlay = new List<int>();
            const int count = 72;
            for (int k = 0; k < count; k++)
            {
                float tt = (k + 0.5f) * Mathf.PI * 2f / count;
                int i0 = Mathf.RoundToInt(tt / (Mathf.PI * 2f) * n) % n;
                Vector2 c = TableArt.SuperEllipse(tt, HalfWidth, HalfDepth);
                Vector2 tg = new Vector2(-nrm[i0].y, nrm[i0].x);
                Vector2 center = c + nrm[i0] * 0.22f;
                float y = 0.0775f;
                float hl = k % 2 == 0 ? 0.075f : 0.04f, hwid = 0.03f;
                Vector3 V(Vector2 p) => new Vector3(p.x, y, p.y);
                int s = verts.Count;
                verts.Add(V(center + tg * hl)); verts.Add(V(center + nrm[i0] * hwid)); verts.Add(V(center - tg * hl)); verts.Add(V(center - nrm[i0] * hwid));
                for (int q = 0; q < 4; q++) { norms.Add(Vector3.up); uvs.Add(Vector2.zero); }
                AddTri(inlay, verts, s, s + 1, s + 2, Vector3.up);
                AddTri(inlay, verts, s, s + 2, s + 3, Vector3.up);
            }

            var mesh = new Mesh { name = "Check_TableRim" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris, 0);
            mesh.SetTriangles(inlay, 1);
            mesh.RecalculateBounds();
            var wood = NeonMaterials.CreateLitOpaque("Check_TableWood", new Color(0.40f, 0.22f, 0.11f), 0f, 0.62f, Color.black);
            var brass = NeonMaterials.CreateLitOpaque("Check_TableBrass", WagonDecor.Amber, 0.9f, 0.75f, WagonDecor.Amber * 0.05f);
            AddRenderer("Rim", mesh, new[] { wood, brass }, true, true);

            // Tube néon (additif, couleurs par sommet pour la lueur de la place active).
            stripParams = new float[n + 1];
            var sv = new List<Vector3>();
            var su = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                int ii = i % n;
                stripParams[i] = ii * Mathf.PI * 2f / n;
                Vector2 o0 = pts[ii] + nrm[ii] * 0.035f, o1 = pts[ii] + nrm[ii] * 0.165f;
                sv.Add(new Vector3(o0.x, 0.0745f, o0.y));
                sv.Add(new Vector3(o1.x, 0.0835f, o1.y));
                su.Add(new Vector2(i / (float)n * 40f, 0f));
                su.Add(new Vector2(i / (float)n * 40f, 1f));
            }
            var st = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = (i + 1) * 2;
                AddTri(st, sv, a, b, b + 1, Vector3.up);
                AddTri(st, sv, a, b + 1, a + 1, Vector3.up);
            }
            stripMesh = new Mesh { name = "Check_TableNeon" };
            stripMesh.MarkDynamic();
            stripMesh.SetVertices(sv);
            stripMesh.SetUVs(0, su);
            stripMesh.SetTriangles(st, 0);
            stripMesh.RecalculateBounds();
            stripColors = new Color[sv.Count];
            stripMat = NeonMaterials.CreateAdditive("Check_TableNeon", TableArt.NeonBand(), StripColor, 2);
            AddRenderer("Neon", stripMesh, new[] { stripMat }, false, false);
        }

        void BuildSlots()
        {
            const float margin = 0.09f;
            var tex = TableArt.Slot(CardView.Width, CardView.Height, CardAtlas.CornerRadius, margin);
            var quad = CardAtlas.BuildQuad(CardView.Width + 2f * margin, CardView.Height + 2f * margin);
            drawSlotMat = NeonMaterials.CreateAdditive("Check_DrawSlot", tex, SlotColor, 1);
            discardSlotMat = NeonMaterials.CreateAdditive("Check_DiscardSlot", tex, SlotColor, 1);
            var scale = Vector3.one * CenterCardScale;
            AddDecal("Draw Slot", quad, drawSlotMat, DrawPilePosition + Vector3.up * (TopY + 0.0015f - SurfaceY), scale);
            AddDecal("Discard Slot", quad, discardSlotMat, DiscardPosition + Vector3.up * (TopY + 0.0015f - SurfaceY), scale);
        }

        void BuildHaloAndTension()
        {
            haloMat = NeonMaterials.CreateAdditive("Check_SeatHalo", NeonTextures.RadialGlow, SeatHaloColor, 0);
            halo = AddDecal("Seat Halo", CardAtlas.BuildQuad(1f, 1f), haloMat, new Vector3(0f, TopY + 0.002f, -HalfDepth * 0.8f), new Vector3(3.4f, 1f, 1.7f)).transform;

            tensionMat = NeonMaterials.CreateAdditive("Check_Tension", TableArt.TensionVignette(), Color.black, 3);
            AddDecal("Tension", CardAtlas.BuildQuad(2f * HalfWidth, 2f * HalfDepth), tensionMat, new Vector3(0f, TopY + 0.0025f, 0f), Vector3.one);
        }

        // ------------------------------------------------------------------ construction : décor

        void BuildBackdrop()
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(transform, false);
            accentMats = new Material[0];
            accentBase = new Color[0];

            // Pied de table en bois sombre cerclé de laiton.
            var woodDark = NeonMaterials.CreateLitOpaque("Check_PedestalWood", new Color(0.20f, 0.11f, 0.06f), 0f, 0.5f, Color.black);
            var brass = NeonMaterials.CreateLitOpaque("Check_PedestalBrass", WagonDecor.Amber, 0.9f, 0.7f, Color.black);
            var ped = new MeshBuilder(3);
            // Profil de bas en haut (le segment qui part d'un point prend son sous-maillage).
            ped.Lathe(new List<ProfilePoint>
            {
                new ProfilePoint(1.6f, FloorY, 0f, 1), new ProfilePoint(1.55f, FloorY + 0.06f), new ProfilePoint(1.1f, FloorY + 0.25f),
                new ProfilePoint(0.55f, -1.68f, 0f, 2), new ProfilePoint(0.53f, -1.6f), new ProfilePoint(0.66f, -0.98f, 0f, 1),
                new ProfilePoint(0.64f, -0.9f), new ProfilePoint(1.1f, -0.55f), new ProfilePoint(1.5f, -0.26f),
            }, 24, Vector3.zero);
            AddRenderer("Pedestal", ped.ToMesh("Check_Pedestal"), new[] { woodDark, brass, brass }, false, true, root);

            if (DecorStyles.Painted) painted = PaintedScene.Build(root);
            else WagonDecor.Build(root, FloorY);
        }

        /// <summary>Linoléum bleu pétrole : léger moucheté et fin liseré crème en retrait du bord.</summary>
        static Texture2D LinoleumTexture()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Check_Linoleum", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color32[size * size];
            var rnd = new System.Random(7);
            // Un peu plus bleu que la palette : la lumière chaude du plafonnier le verdirait.
            Color petrol = new Color(0.075f, 0.215f, 0.285f), cream = WagonDecor.Cream;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float wx = (u - 0.5f) * 2f * HalfWidth, wz = (v - 0.5f) * 2f * HalfDepth;
                float rho = TableArt.Rho(wx, wz, HalfWidth, HalfDepth);
                float speck = ((float)rnd.NextDouble() - 0.5f) * 0.06f;
                float grain = Mathf.PerlinNoise(u * 18f, v * 18f) * 0.05f;
                Color c = petrol * (0.92f + speck + grain);
                // Liseré crème (double filet) près du bord.
                float d1 = Mathf.Abs(rho - 0.90f) * HalfWidth, d2 = Mathf.Abs(rho - 0.925f) * HalfWidth;
                float line = Mathf.Clamp01(1f - d1 / 0.012f) * 0.55f + Mathf.Clamp01(1f - d2 / 0.006f) * 0.35f;
                c = Color.Lerp(c, cream * 0.8f, line);
                c.a = 1f;
                px[y * size + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // ------------------------------------------------------------------ utilitaires

        MeshRenderer AddRenderer(string name, Mesh mesh, Material[] mats, bool castShadows, bool receiveShadows, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            return r;
        }

        MeshRenderer AddDecal(string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale)
        {
            var r = AddRenderer(name, mesh, new[] { mat }, false, false);
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.transform.localPosition = localPos;
            r.transform.localScale = scale;
            return r;
        }

        static void AddTri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 expected)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, expected) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }
    }
}
