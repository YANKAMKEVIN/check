using System.Collections;
using Check.Core;
using UnityEngine;

namespace Check.Game.Visuals
{
    public enum CardHighlight { None, Playable, Selected, Dimmed }

    /// <summary>
    /// Une carte 3D. CONTRAT public utilisé par la couche de jeu : garder ces signatures.
    /// Version provisoire (quad coloré) : le rendu définitif remplace l'intérieur.
    /// Repère : la carte est posée à plat dans le plan XZ, face visible vers +Y quand FaceUp,
    /// largeur <see cref="Width"/> sur X, hauteur <see cref="Height"/> sur Z, pivot au centre.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public const float Width = 0.63f;
        public const float Height = 0.88f;
        public const float Thickness = 0.006f;

        public Card? Card { get; private set; }
        public bool FaceUp { get; private set; }
        public CardHighlight Highlight { get; private set; }
        public bool IsMoving => moveRoutine != null;

        Renderer body;
        Coroutine moveRoutine;

        /// <summary>Crée une carte ; <paramref name="card"/> nul = carte inconnue (dos seulement).</summary>
        public static CardView Create(Card? card, bool faceUp, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = card.HasValue ? "Card " + card.Value : "Card ?";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CardView>();
            view.body = go.GetComponent<Renderer>();
            go.transform.localScale = new Vector3(Width, Thickness, Height);
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            view.Card = card;
            view.SetFaceUp(faceUp, false);
            return view;
        }

        /// <summary>Révèle (ou change) la carte, par ex. quand le joueur humain pioche.</summary>
        public void SetCard(Card? card)
        {
            Card = card;
            name = card.HasValue ? "Card " + card.Value : "Card ?";
            Refresh();
        }

        public void SetFaceUp(bool faceUp, bool animate = true)
        {
            FaceUp = faceUp;
            Refresh();
        }

        public void SetHighlight(CardHighlight highlight)
        {
            Highlight = highlight;
            Refresh();
        }

        /// <summary>
        /// Déplace la carte vers une pose monde en <paramref name="duration"/> s, avec un arc vertical
        /// de hauteur <paramref name="arc"/>. Retourne la carte à mi-course si <paramref name="flipTo"/> diffère de FaceUp.
        /// </summary>
        public Coroutine MoveTo(Vector3 position, Quaternion rotation, float duration, float arc = 0.15f, bool? flipTo = null)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(Move(position, rotation, duration, arc, flipTo));
            return moveRoutine;
        }

        /// <summary>Place immédiatement.</summary>
        public void Snap(Vector3 position, Quaternion rotation)
        {
            if (moveRoutine != null) { StopCoroutine(moveRoutine); moveRoutine = null; }
            transform.SetPositionAndRotation(position, rotation);
        }

        IEnumerator Move(Vector3 to, Quaternion toRot, float duration, float arc, bool? flipTo)
        {
            Vector3 from = transform.position;
            Quaternion fromRot = transform.rotation;
            bool flipped = false;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                transform.SetPositionAndRotation(Vector3.Lerp(from, to, k) + Vector3.up * (arc * Mathf.Sin(k * Mathf.PI)),
                    Quaternion.Slerp(fromRot, toRot, k));
                if (!flipped && flipTo.HasValue && k > 0.5f) { SetFaceUp(flipTo.Value, false); flipped = true; }
                yield return null;
            }
            transform.SetPositionAndRotation(to, toRot);
            if (flipTo.HasValue && !flipped) SetFaceUp(flipTo.Value, false);
            moveRoutine = null;
        }

        void Refresh()
        {
            if (body == null) return;
            Color c;
            if (!FaceUp || !Card.HasValue) c = new Color(0.2f, 0.1f, 0.4f);
            else c = Card.Value.Color == CardColor.Red ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.85f, 0.85f, 0.9f);
            if (Highlight == CardHighlight.Selected) c = Color.Lerp(c, Color.yellow, 0.5f);
            else if (Highlight == CardHighlight.Playable) c = Color.Lerp(c, Color.cyan, 0.25f);
            else if (Highlight == CardHighlight.Dimmed) c *= 0.4f;
            body.material.color = c;
        }
    }
}
