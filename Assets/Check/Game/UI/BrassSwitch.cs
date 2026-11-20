using UnityEngine;
using UnityEngine.UI;

namespace Check.Game.UI
{
    /// <summary>Interrupteur laiton : le bouton rond glisse à droite (oui, laiton) ou à gauche (non, éteint).</summary>
    public sealed class BrassSwitch : MonoBehaviour
    {
        public Image Track, Knob;
        public bool On { get; private set; }
        float pos;

        public void Set(bool on, bool instant)
        {
            On = on;
            if (instant) pos = on ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            float target = On ? 1f : 0f;
            if (Mathf.Approximately(pos, target)) return;
            pos = Mathf.MoveTowards(pos, target, Time.unscaledDeltaTime / 0.12f);
            Apply();
        }

        void Apply()
        {
            if (Track == null || Knob == null) return;
            Knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-23f, 23f, pos), 0f);
            Knob.color = Color.Lerp(UIKit.TextDim, UIKit.Gold, pos);
            Track.color = Color.Lerp(new Color(0.12f, 0.12f, 0.16f, 1f), UIKit.Copper * new Color(0.55f, 0.55f, 0.55f, 1f), pos);
        }
    }
}
