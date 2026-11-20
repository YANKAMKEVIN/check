using Check.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>
    /// Sens du jeu : une flèche laiton en pointillés va du badge du joueur actif vers le suivant, avec un point sur
    /// le badge « suivant ». Après un As, elle saute en arc par-dessus le joueur sauté.
    /// </summary>
    public sealed partial class Hud
    {
        const int ArrowDots = 14;

        RectTransform arrowLayer, arrowHead;
        Image[] arrowDots;
        Image nextDot;
        int skipFrom = -1, skipOver = -1;
        float skipUntil;

        void BuildNextArrow()
        {
            arrowLayer = UIKit.Rect("NextArrow", badgeLayer);
            UIKit.Stretch(arrowLayer);
            arrowLayer.SetAsFirstSibling(); // sous les badges
            arrowDots = new Image[ArrowDots];
            for (int i = 0; i < ArrowDots; i++)
            {
                var d = new GameObject("Dot", typeof(RectTransform)).AddComponent<Image>();
                d.transform.SetParent(arrowLayer, false);
                d.sprite = UIKit.Circle;
                d.raycastTarget = false;
                d.rectTransform.sizeDelta = new Vector2(12, 12);
                arrowDots[i] = d;
            }
            var head = new GameObject("Head", typeof(RectTransform)).AddComponent<Image>();
            head.transform.SetParent(arrowLayer, false);
            head.sprite = UIKit.Rounded;
            head.type = Image.Type.Sliced;
            head.color = UIKit.Gold;
            head.raycastTarget = false;
            arrowHead = head.rectTransform;
            arrowHead.sizeDelta = new Vector2(26, 26);
            nextDot = new GameObject("NextDot", typeof(RectTransform)).AddComponent<Image>();
            nextDot.transform.SetParent(arrowLayer, false);
            nextDot.sprite = UIKit.Circle;
            nextDot.color = UIKit.Gold;
            nextDot.raycastTarget = false;
            nextDot.rectTransform.sizeDelta = new Vector2(24, 24);
            arrowLayer.gameObject.SetActive(false);
        }

        /// <summary>Un As vient de sauter <paramref name="skipped"/> : la flèche passe un moment par-dessus lui.</summary>
        void NoteSkipped(int skipped)
        {
            skipFrom = table.ActivePlayer;
            skipOver = skipped;
            skipUntil = Time.unscaledTime + 1.4f;
        }

        bool IsOutOfRound(int p) =>
            (p < table.Eliminated.Length && table.Eliminated[p]) || (p < table.Qualified.Length && table.Qualified[p]);

        Vector2 BadgeCenter(int p) => arrowLayer.InverseTransformPoint(badges[p].Root.position);

        void RefreshNextArrow()
        {
            if (arrowLayer == null) return;
            int n = table.PlayerCount;
            bool skipping = Time.unscaledTime < skipUntil && skipFrom >= 0 && skipOver >= 0;
            int from = skipping ? skipFrom : table.ActivePlayer;
            int to = from >= 0 ? TurnOrder.Next(skipping ? skipOver : from, n, IsOutOfRound) : -1;
            bool show = n >= 3 && from >= 0 && to >= 0 && to != from && from < badges.Length && to < badges.Length
                        && badges[from] != null && badges[to] != null && game.Round != null && !game.Round.IsOver;
            arrowLayer.gameObject.SetActive(show);
            if (!show) return;

            Vector2 a = BadgeCenter(from), b = BadgeCenter(to);
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 1f) { arrowLayer.gameObject.SetActive(false); return; }
            dir /= len;
            // On part du bord des badges, pas de leur centre.
            a += dir * 70f;
            b -= dir * 70f;
            // Arc vers le centre de l'écran ; bien plus haut quand on saute un joueur.
            var normal = new Vector2(-dir.y, dir.x);
            if (Vector2.Dot(normal, -(a + b) * 0.5f) < 0f) normal = -normal;
            Vector2 control = (a + b) * 0.5f + normal * (skipping ? Mathf.Max(160f, len * 0.45f) : len * 0.18f);
            if (skipping) control = Vector2.Lerp(control, BadgeCenter(skipOver) + normal * 140f, 0.5f);

            float t0 = Time.unscaledTime;
            for (int i = 0; i < ArrowDots; i++)
            {
                float u = (i + 1f) / (ArrowDots + 1f);
                var p = Bezier(a, control, b, u);
                var dot = arrowDots[i];
                dot.rectTransform.anchoredPosition = p;
                // Les points défilent vers le suivant.
                float wave = 0.5f + 0.5f * Mathf.Sin((u * 6f - t0 * 4f) * Mathf.PI);
                dot.color = (skipping ? UIKit.Magenta : UIKit.Gold) * new Color(1, 1, 1, 0.35f + 0.5f * wave);
                dot.rectTransform.localScale = Vector3.one * (0.8f + 0.4f * wave);
            }
            var end = Bezier(a, control, b, 1f);
            var tangent = (end - Bezier(a, control, b, 0.94f)).normalized;
            arrowHead.anchoredPosition = end;
            arrowHead.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 45f);
            arrowHead.GetComponent<Image>().color = skipping ? UIKit.Magenta : UIKit.Gold;

            // Point « suivant » au coin haut-droit du badge.
            var corner = badges[to].Root.TransformPoint(new Vector3(badges[to].Root.rect.xMax - 4f, badges[to].Root.rect.yMax - 4f, 0f));
            nextDot.rectTransform.anchoredPosition = arrowLayer.InverseTransformPoint(corner);
            nextDot.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t0 * 5f));
        }

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float u)
        {
            float v = 1f - u;
            return v * v * a + 2f * v * u * c + u * u * b;
        }
    }
}
