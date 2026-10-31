using System.Collections;
using Check.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Check.Game.Visuals
{
    public enum CardHighlight { None, Playable, Selected, Dimmed }

    /// <summary>
    /// Une carte 3D. CONTRAT public utilisé par la couche de jeu : garder ces signatures.
    /// Repère : la carte est posée à plat dans le plan XZ, face visible vers +Y quand FaceUp,
    /// largeur <see cref="Width"/> sur X, hauteur <see cref="Height"/> sur Z, pivot au centre
    /// (le haut de la face est du côté +Z local : lisible à l'endroit pour le joueur assis en -Z).
    ///
    /// Rendu : maillage procédural partagé par identité de carte (coins arrondis, tranche dorée) dont
    /// les UV pointent dans l'atlas unique de <see cref="CardAtlas"/> ; deux matériaux partagés par
    /// toutes les cartes (face, dos) → compatible SRP Batcher, pas de MaterialPropertyBlock.
    /// Hiérarchie : racine (pose donnée par le jeu, collider) → Lift (soulèvement de sélection /
    /// retournement, liseré lumineux) → Body (rotation de retournement autour de Z, maillage).
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public const float Width = 0.63f;
        public const float Height = 0.88f;
        public const float Thickness = 0.006f;

        /// <summary>Hauteur de soulèvement d'une carte sélectionnée (unités monde, axe +Y local).</summary>
        public const float SelectedLift = 0.11f;
        /// <summary>Décalage vers +Z local d'une carte sélectionnée (vers le centre de la table pour la main du joueur).</summary>
        public const float SelectedShift = 0.07f;
        /// <summary>Durée du retournement sur place (s).</summary>
        public const float FlipDuration = 0.34f;

        public Card? Card { get; private set; }
        public bool FaceUp { get; private set; }
        public CardHighlight Highlight { get; private set; }
        public bool IsMoving => moveRoutine != null;
        /// <summary>Vrai pendant un retournement animé (sur place ou pendant un MoveTo).</summary>
        public bool IsFlipping => flipRoutine != null || flippingInMove;

        Transform lift, bodyTf;
        MeshFilter filter;
        MeshRenderer body;
        MeshRenderer glow;
        Coroutine moveRoutine, flipRoutine;
        bool flippingInMove;
        float flipAngle;   // 0 = face visible, 180 = dos visible
        float flipLift;    // soulèvement dû au retournement
        float liftY, liftZ;

        static Material[] _normalMats, _dimMats;
        static int _pulseFrame = -1;

        /// <summary>
        /// Génère à l'avance l'atlas et les matériaux des cartes (sinon fait à la première carte créée).
        /// À appeler pendant un écran de chargement pour éviter un à-coup.
        /// </summary>
        public static void Prewarm() => CardAtlas.Ensure();

        /// <summary>Crée une carte ; <paramref name="card"/> nul = carte inconnue (dos seulement).</summary>
        public static CardView Create(Card? card, bool faceUp, Transform parent = null)
        {
            CardAtlas.Ensure();
            if (_normalMats == null)
            {
                _normalMats = new[] { CardAtlas.FaceMaterial, CardAtlas.BackMaterial };
                _dimMats = new[] { CardAtlas.FaceDimmedMaterial, CardAtlas.BackDimmedMaterial };
            }

            var go = new GameObject(card.HasValue ? "Card " + card.Value : "Card ?");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CardView>();
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Width, Mathf.Max(Thickness, 0.02f), Height);

            view.lift = new GameObject("Lift").transform;
            view.lift.SetParent(go.transform, false);

            var bodyGo = new GameObject("Body");
            view.bodyTf = bodyGo.transform;
            view.bodyTf.SetParent(view.lift, false);
            view.filter = bodyGo.AddComponent<MeshFilter>();
            view.body = bodyGo.AddComponent<MeshRenderer>();
            view.body.shadowCastingMode = ShadowCastingMode.On;
            view.body.receiveShadows = true;
            view.body.lightProbeUsage = LightProbeUsage.Off;
            view.body.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

            var glowGo = new GameObject("Highlight");
            glowGo.transform.SetParent(view.lift, false);
            glowGo.transform.localPosition = new Vector3(0f, -Thickness * 0.5f + 0.0004f, 0f);
            glowGo.AddComponent<MeshFilter>().sharedMesh = CardAtlas.HighlightMesh;
            view.glow = glowGo.AddComponent<MeshRenderer>();
            view.glow.shadowCastingMode = ShadowCastingMode.Off;
            view.glow.receiveShadows = false;
            view.glow.lightProbeUsage = LightProbeUsage.Off;
            view.glow.reflectionProbeUsage = ReflectionProbeUsage.Off;
            view.glow.enabled = false;

            view.Card = card;
            view.filter.sharedMesh = CardAtlas.MeshFor(card);
            view.body.sharedMaterials = _normalMats;
            view.SetFaceUp(faceUp, false);
            return view;
        }

        /// <summary>Révèle (ou change) la carte, par ex. quand le joueur humain pioche.</summary>
        public void SetCard(Card? card)
        {
            Card = card;
            name = card.HasValue ? "Card " + card.Value : "Card ?";
            if (filter != null) filter.sharedMesh = CardAtlas.MeshFor(card);
        }

        /// <summary>Retourne la carte ; animé = rotation autour de son grand axe avec un petit saut.</summary>
        public void SetFaceUp(bool faceUp, bool animate = true)
        {
            FaceUp = faceUp;
            float target = faceUp ? 0f : 180f;
            if (flipRoutine != null) { StopCoroutine(flipRoutine); flipRoutine = null; }
            if (!animate || !isActiveAndEnabled || Mathf.Abs(flipAngle - target) < 0.01f)
            {
                flipAngle = target;
                flipLift = 0f;
                ApplyPose();
                return;
            }
            flipRoutine = StartCoroutine(Flip(target));
        }

        public void SetHighlight(CardHighlight highlight)
        {
            Highlight = highlight;
            if (body == null) return;
            body.sharedMaterials = highlight == CardHighlight.Dimmed ? _dimMats : _normalMats;
            switch (highlight)
            {
                case CardHighlight.Playable:
                    glow.sharedMaterial = CardAtlas.PlayableMaterial;
                    glow.enabled = true;
                    break;
                case CardHighlight.Selected:
                    glow.sharedMaterial = CardAtlas.SelectedMaterial;
                    glow.enabled = true;
                    break;
                default:
                    glow.enabled = false;
                    break;
            }
        }

        /// <summary>
        /// Déplace la carte vers une pose monde en <paramref name="duration"/> s, avec un arc vertical
        /// de hauteur <paramref name="arc"/>. Retourne la carte en vol si <paramref name="flipTo"/> diffère de FaceUp
        /// (FaceUp change à mi-course).
        /// </summary>
        public Coroutine MoveTo(Vector3 position, Quaternion rotation, float duration, float arc = 0.15f, bool? flipTo = null)
        {
            if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
            if (!isActiveAndEnabled || duration <= 0f)
            {
                Snap(position, rotation);
                if (flipTo.HasValue) SetFaceUp(flipTo.Value, false);
                return null;
            }
            moveRoutine = StartCoroutine(Move(position, rotation, duration, arc, flipTo));
            return moveRoutine;
        }

        /// <summary>Place immédiatement (termine aussi un retournement en cours).</summary>
        public void Snap(Vector3 position, Quaternion rotation)
        {
            if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
            if (flipRoutine != null) { StopCoroutine(flipRoutine); flipRoutine = null; }
            flippingInMove = false;
            flipAngle = FaceUp ? 0f : 180f;
            flipLift = 0f;
            transform.SetPositionAndRotation(position, rotation);
            ApplyPose();
        }

        IEnumerator Move(Vector3 to, Quaternion toRot, float duration, float arc, bool? flipTo)
        {
            Vector3 from = transform.position;
            Quaternion fromRot = transform.rotation;
            bool flip = flipTo.HasValue && flipTo.Value != FaceUp;
            if (flipTo.HasValue && !flip) SetFaceUp(flipTo.Value, false);
            float angle0 = flipAngle, angle1 = flip ? (flipTo.Value ? 0f : 180f) : flipAngle;
            if (flip && flipRoutine != null) { StopCoroutine(flipRoutine); flipRoutine = null; }
            flippingInMove = flip;
            bool switched = false;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                float k = Mathf.SmoothStep(0f, 1f, u);
                transform.SetPositionAndRotation(Vector3.Lerp(from, to, k) + Vector3.up * (arc * Mathf.Sin(k * Mathf.PI)),
                    Quaternion.Slerp(fromRot, toRot, k));
                if (flip)
                {
                    float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.82f, u));
                    flipAngle = Mathf.Lerp(angle0, angle1, f);
                    flipLift = Mathf.Sin(f * Mathf.PI) * FlipLiftHeight * 0.75f;
                    if (!switched && f >= 0.5f) { FaceUp = flipTo.Value; switched = true; }
                    ApplyPose();
                }
                yield return null;
            }
            transform.SetPositionAndRotation(to, toRot);
            if (flip)
            {
                FaceUp = flipTo.Value;
                flipAngle = angle1;
                flipLift = 0f;
                flippingInMove = false;
                ApplyPose();
            }
            moveRoutine = null;
        }

        const float FlipLiftHeight = Width * 0.5f + 0.04f;

        IEnumerator Flip(float target)
        {
            float from = flipAngle;
            for (float t = 0f; t < FlipDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / FlipDuration);
                flipAngle = Mathf.Lerp(from, target, k);
                flipLift = Mathf.Sin(k * Mathf.PI) * FlipLiftHeight;
                ApplyPose();
                yield return null;
            }
            flipAngle = target;
            flipLift = 0f;
            ApplyPose();
            flipRoutine = null;
        }

        void OnDisable()
        {
            // Les coroutines meurent avec la désactivation : on termine proprement l'état.
            moveRoutine = null;
            flipRoutine = null;
            flippingInMove = false;
            flipAngle = FaceUp ? 0f : 180f;
            flipLift = 0f;
            ApplyPose();
        }

        void Update()
        {
            float targetY = 0f, targetZ = 0f;
            if (Highlight == CardHighlight.Selected) { targetY = SelectedLift; targetZ = SelectedShift; }
            else if (Highlight == CardHighlight.Playable) targetY = 0.012f;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 16f);
            if (Mathf.Abs(liftY - targetY) > 1e-4f || Mathf.Abs(liftZ - targetZ) > 1e-4f)
            {
                liftY = Mathf.Lerp(liftY, targetY, k);
                liftZ = Mathf.Lerp(liftZ, targetZ, k);
                ApplyPose();
            }
            if (glow != null && glow.enabled && _pulseFrame != Time.frameCount)
            {
                // Pulsation partagée (une seule mise à jour de matériau par image pour toutes les cartes).
                _pulseFrame = Time.frameCount;
                float t = Time.time;
                CardAtlas.PlayableMaterial.SetColor(NeonMaterials.BaseColorProperty, CardAtlas.PlayableColor * (0.78f + 0.22f * Mathf.Sin(t * 3.4f)));
                CardAtlas.SelectedMaterial.SetColor(NeonMaterials.BaseColorProperty, CardAtlas.SelectedColor * (0.9f + 0.1f * Mathf.Sin(t * 5.2f)));
            }
        }

        void ApplyPose()
        {
            if (lift == null) return;
            lift.localPosition = new Vector3(0f, liftY + flipLift, liftZ);
            bodyTf.localRotation = Quaternion.Euler(0f, 0f, flipAngle);
        }
    }
}
