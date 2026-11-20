using System.Collections.Generic;
using System.Diagnostics;
using Check.Core;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Ressources partagées des cartes, générées une seule fois :
    /// - un atlas unique 2048 × 1712 (54 faces + une face « inconnue »), sans copie CPU après génération ;
    /// - la texture du dos et sa carte de lueur néon ;
    /// - un maillage par carte (coins arrondis, épaisseur, tranche dorée) dont les UV pointent
    ///   dans l'atlas : toutes les cartes partagent donc les mêmes matériaux (SRP Batcher, aucun
    ///   MaterialPropertyBlock) ;
    /// - les matériaux (normal / assombri) et ceux du liseré de surbrillance.
    /// </summary>
    public static class CardAtlas
    {
        const int Cols = 10, Rows = 6;
        /// <summary>Taille de l'atlas : 10 × 6 cellules de 204 × 284 px (≈ 14 Mo GPU + mipmaps).</summary>
        public const int AtlasWidth = 2048, AtlasHeight = 1712;
        const int TexH = AtlasHeight;
        /// <summary>Emplacement de la face « carte inconnue » (dos reproduit).</summary>
        const int UnknownSlot = 54;

        /// <summary>Rayon des coins arrondis (unités monde).</summary>
        public const float CornerRadius = 0.042f;
        /// <summary>Marge du liseré de surbrillance autour de la carte (unités monde).</summary>
        public const float HighlightMargin = 0.11f;

        /// <summary>Si vrai avant la génération, l'atlas reste lisible (export PNG de contrôle).</summary>
        public static bool KeepReadable;

        static Texture2D _atlas, _back, _backGlow, _highlightTex;
        static Material _face, _faceDim, _backMat, _backDim, _playable, _selected;
        static readonly Mesh[] _meshes = new Mesh[55];
        static Mesh _highlightMesh;

        public static bool IsReady => _atlas != null;
        public static Texture2D Atlas { get { Ensure(); return _atlas; } }
        public static Texture2D BackTexture { get { Ensure(); return _back; } }

        public static Material FaceMaterial { get { Ensure(); return _face; } }
        public static Material FaceDimmedMaterial { get { Ensure(); return _faceDim; } }
        public static Material BackMaterial { get { Ensure(); return _backMat; } }
        public static Material BackDimmedMaterial { get { Ensure(); return _backDim; } }
        public static Material PlayableMaterial { get { Ensure(); return _playable; } }
        public static Material SelectedMaterial { get { Ensure(); return _selected; } }
        public static Mesh HighlightMesh { get { Ensure(); return _highlightMesh; } }

        /// <summary>Couleur HDR du liseré « jouable » (cyan néon).</summary>
        public static readonly Color PlayableColor = new Color(0.30f, 0.95f, 1.00f) * 1.9f;
        /// <summary>Couleur HDR de la carte sélectionnée (or ambré).</summary>
        public static readonly Color SelectedColor = new Color(1.00f, 0.72f, 0.28f) * 3.4f;

        /// <summary>Maillage de la carte (null = inconnue : dos des deux côtés).</summary>
        public static Mesh MeshFor(Card? card)
        {
            Ensure();
            int slot = card.HasValue ? card.Value.Id : UnknownSlot;
            if (_meshes[slot] == null) _meshes[slot] = BuildCardMesh(slot);
            return _meshes[slot];
        }

        /// <summary>Génère tout (≈ quelques centaines de ms) ; appelé automatiquement à la première carte.</summary>
        public static void Ensure()
        {
            if (_atlas != null) return;
            var sw = Stopwatch.StartNew();
            GenerateTextures();
            CreateMaterials();
            _highlightMesh = BuildQuad(CardView.Width + 2f * HighlightMargin, CardView.Height + 2f * HighlightMargin);
            Debug.Log($"[Check] Atlas des cartes généré en {sw.ElapsedMilliseconds} ms ({AtlasWidth}x{TexH}).");
        }

        // ------------------------------------------------------------------ textures

        static void GenerateTextures()
        {
            // Dos d'abord (copié aussi dans l'emplacement « inconnue » de l'atlas).
            var back = new Paint2D(CardArt.BackW, CardArt.BackH);
            var glow = new Paint2D(CardArt.BackW, CardArt.BackH);
            CardArt.PaintBack(back, glow);
            _back = ToTexture(back, "Check_CardBack");
            _backGlow = ToTexture(glow, "Check_CardBackGlow");

            _atlas = new Texture2D(AtlasWidth, TexH, TextureFormat.RGBA32, true, false)
            {
                name = "Check_CardAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                mipMapBias = -0.4f, // index plus nets vus de loin / en biais
            };
            NativeArray<Color32> data = _atlas.GetPixelData<Color32>(0);
            Color32 fill = AfroPalette.Ivory;
            for (int i = 0; i < data.Length; i++) data[i] = fill;

            var cell = CardArt.NewCell();
            for (int id = 0; id < Card.DeckSize; id++)
            {
                CardArt.PaintFace(cell, new Card(id));
                Blit(cell.Px, CardArt.CellW, CardArt.CellH, data, id);
            }
            // Face « inconnue » : le dos rééchantillonné.
            for (int y = 0; y < CardArt.CellH; y++)
            for (int x = 0; x < CardArt.CellW; x++)
            {
                float u = (x + 0.5f) / CardArt.CellW, v = (y + 0.5f) / CardArt.CellH;
                cell.Px[y * CardArt.CellW + x] = back.Px[Mathf.Min(CardArt.BackH - 1, (int)(v * CardArt.BackH)) * CardArt.BackW + Mathf.Min(CardArt.BackW - 1, (int)(u * CardArt.BackW))];
            }
            Blit(cell.Px, CardArt.CellW, CardArt.CellH, data, UnknownSlot);
            _atlas.Apply(true, !KeepReadable);

            _highlightTex = CardArt.HighlightTexture(CardView.Width, CardView.Height, CornerRadius, HighlightMargin);
        }

        static void Blit(Color[] src, int w, int h, NativeArray<Color32> dst, int slot)
        {
            int ox = (slot % Cols) * w, oy = (slot / Cols) * h;
            for (int y = 0; y < h; y++)
            {
                int row = (oy + y) * AtlasWidth + ox;
                int s = y * w;
                for (int x = 0; x < w; x++) dst[row + x] = src[s + x];
            }
        }

        static Texture2D ToTexture(Paint2D p, string name)
        {
            var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, true, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, mipMapBias = -0.3f,
            };
            var px = new Color32[p.W * p.H];
            for (int i = 0; i < px.Length; i++) px[i] = p.Px[i];
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Rectangle de la face de <paramref name="card"/> dans <see cref="Atlas"/> (pour l'afficher dans l'interface).</summary>
        public static Rect FaceUv(Card card) => CellUv(card.Id);

        static Rect CellUv(int slot)
        {
            int col = slot % Cols, row = slot / Cols;
            float u0 = (col * CardArt.CellW + CardArt.Bleed) / (float)AtlasWidth;
            float v0 = (row * CardArt.CellH + CardArt.Bleed) / (float)TexH;
            float du = CardArt.DW / AtlasWidth, dv = CardArt.DH / TexH;
            return new Rect(u0, v0, du, dv);
        }

        // ------------------------------------------------------------------ matériaux

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

        static void CreateMaterials()
        {
            // Face : un peu auto-éclairée par l'atlas pour rester lisible quel que soit l'éclairage.
            _face = NeonMaterials.CreateLitOpaque("Check_CardFace", new Color(0.94f, 0.94f, 0.94f), 0f, 0.42f, new Color(0.36f, 0.36f, 0.36f));
            _face.SetTexture(BaseMapId, _atlas);
            _face.SetTexture(EmissionMapId, _atlas);
            _faceDim = NeonMaterials.CreateLitOpaque("Check_CardFaceDim", new Color(0.36f, 0.36f, 0.42f), 0f, 0.5f, new Color(0.07f, 0.07f, 0.09f));
            _faceDim.SetTexture(BaseMapId, _atlas);
            _faceDim.SetTexture(EmissionMapId, _atlas);

            // Dos : indigo verni, pistes néon émissives (bloom).
            _backMat = NeonMaterials.CreateLitOpaque("Check_CardBack", Color.white, 0f, 0.42f, Color.white * 2.3f);
            _backMat.SetTexture(BaseMapId, _back);
            _backMat.SetTexture(EmissionMapId, _backGlow);
            _backDim = NeonMaterials.CreateLitOpaque("Check_CardBackDim", new Color(0.4f, 0.4f, 0.45f), 0.2f, 0.6f, Color.white * 0.5f);
            _backDim.SetTexture(BaseMapId, _back);
            _backDim.SetTexture(EmissionMapId, _backGlow);

            _playable = NeonMaterials.CreateAdditive("Check_CardPlayable", _highlightTex, PlayableColor, 5);
            _selected = NeonMaterials.CreateAdditive("Check_CardSelected", _highlightTex, SelectedColor, 6);
        }

        // ------------------------------------------------------------------ maillages

        /// <summary>Contour arrondi de la carte dans le plan XZ (sens trigonométrique vu de dessus).</summary>
        static List<Vector2> Outline()
        {
            const int seg = 6;
            float hx = CardView.Width * 0.5f - CornerRadius, hz = CardView.Height * 0.5f - CornerRadius;
            var pts = new List<Vector2>(4 * (seg + 1));
            Vector2[] centers = { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
            for (int c = 0; c < 4; c++)
            for (int i = 0; i <= seg; i++)
            {
                float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * CornerRadius);
            }
            return pts;
        }

        static Mesh BuildCardMesh(int slot)
        {
            var outline = Outline();
            int n = outline.Count;
            float ht = CardView.Thickness * 0.5f;
            float w = CardView.Width, h = CardView.Height;
            Rect face = CellUv(slot);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var front = new List<int>();
            var backTris = new List<int>();

            // Face avant (+Y) → atlas.
            int c0 = verts.Count;
            verts.Add(new Vector3(0f, ht, 0f)); norms.Add(Vector3.up); uvs.Add(face.center);
            foreach (var p in outline)
            {
                verts.Add(new Vector3(p.x, ht, p.y)); norms.Add(Vector3.up);
                uvs.Add(new Vector2(face.x + (p.x / w + 0.5f) * face.width, face.y + (p.y / h + 0.5f) * face.height));
            }
            for (int i = 0; i < n; i++) AddTri(front, verts, c0, c0 + 1 + i, c0 + 1 + (i + 1) % n, Vector3.up);

            // Dos (-Y) → texture du dos, lue à l'endroit une fois la carte retournée autour de Z.
            int b0 = verts.Count;
            verts.Add(new Vector3(0f, -ht, 0f)); norms.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
            foreach (var p in outline)
            {
                verts.Add(new Vector3(p.x, -ht, p.y)); norms.Add(Vector3.down);
                uvs.Add(new Vector2(0.5f - p.x / w, p.y / h + 0.5f));
            }
            for (int i = 0; i < n; i++) AddTri(backTris, verts, b0, b0 + 1 + i, b0 + 1 + (i + 1) % n, Vector3.down);

            // Tranche dorée (normales lissées), UV dans la bordure or du dos.
            var edgeUv = new Vector2(3f / CardArt.BackW, 0.5f);
            int s0 = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = outline[(i + n - 1) % n], next = outline[(i + 1) % n];
                Vector2 t = (next - prev).normalized;
                var nrm = new Vector3(t.y, 0f, -t.x); // extérieur pour un contour trigonométrique
                verts.Add(new Vector3(outline[i].x, ht, outline[i].y)); norms.Add(nrm); uvs.Add(edgeUv);
                verts.Add(new Vector3(outline[i].x, -ht, outline[i].y)); norms.Add(nrm); uvs.Add(edgeUv);
            }
            for (int i = 0; i < n; i++)
            {
                int a = s0 + i * 2, b = s0 + ((i + 1) % n) * 2;
                Vector3 outward = norms[a] + norms[b];
                AddTri(backTris, verts, a, b, a + 1, outward);
                AddTri(backTris, verts, b, b + 1, a + 1, outward);
            }

            var mesh = new Mesh { name = slot == UnknownSlot ? "Card ?" : "Card " + new Card(slot) };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0, false);
            mesh.SetTriangles(backTris, 1, false);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Ajoute un triangle dont la face avant regarde vers <paramref name="expected"/>.</summary>
        static void AddTri(List<int> tris, List<Vector3> v, int a, int b, int c, Vector3 expected)
        {
            Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, expected) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }

        /// <summary>Quad horizontal (face vers +Y) centré, UV 0..1.</summary>
        public static Mesh BuildQuad(float width, float depth)
        {
            float hx = width * 0.5f, hz = depth * 0.5f;
            var mesh = new Mesh { name = "Check_Quad" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz) });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Exporte l'atlas en PNG (seulement si <see cref="KeepReadable"/> était vrai à la génération).</summary>
        public static bool ExportAtlasPng(string path)
        {
            if (_atlas == null || !_atlas.isReadable) return false;
            System.IO.File.WriteAllBytes(path, _atlas.EncodeToPNG());
            return true;
        }
    }
}
