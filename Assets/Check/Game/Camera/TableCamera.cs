using Check.Game.View;
using UnityEngine;

namespace Check.Game.CameraRig
{
    /// <summary>
    /// Caméra fixe inclinée au-dessus de la table, cadrée automatiquement pour le paysage
    /// (téléphone 19.5:9 ou 16:9, tablette 4:3, Mac) : la table utile tient dans la zone laissée libre
    /// entre le bandeau du haut et la main du joueur. Légère respiration pour que la scène vive.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TableCamera : MonoBehaviour
    {
        public Vector3 Target = new Vector3(0f, 0f, 1.6f);
        public float Pitch = 52f;
        public float FieldOfView = 38f;
        /// <summary>Zone d'écran (viewport, origine en bas) où doit tenir la table : au-dessus de la main, sous le bandeau.</summary>
        public Rect Frame = new Rect(0.12f, 0.33f, 0.76f, 0.41f);
        public bool Breathing = true;

        Camera cam;
        float distance = 12f;
        Vector3 offset;
        int lastWidth, lastHeight;

        public Camera Camera => cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            Refit();
            Apply(0f);
        }

        public void Refit()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            cam.fieldOfView = FieldOfView;
            distance = FitView(out offset);
        }

        float FitView(out Vector3 focusOffset)
        {
            focusOffset = Vector3.zero;
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.3f, cam.aspect);
            float d = 12f;
            for (int pass = 0; pass < 3; pass++)
            {
                float lo = 2f, hi = 80f;
                for (int i = 0; i < 30; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(rot, Target + focusOffset, mid, tanH, tanV, out _, out _)) hi = mid;
                    else lo = mid;
                }
                d = hi;
                // Recentrage vertical dans le cadre (haut et bas de l'écran ne sont pas symétriques).
                Fits(rot, Target + focusOffset, d, tanH, tanV, out var min, out var max);
                float shift = (min.y + max.y) * 0.5f - Frame.center.y;
                focusOffset += rot * Vector3.up * (shift * 2f * d * tanV);
            }
            return d;
        }

        bool Fits(Quaternion rot, Vector3 focus, float dist, float tanH, float tanV, out Vector2 min, out Vector2 max)
        {
            var camPos = focus + rot * new Vector3(0f, 0f, -dist);
            var inv = Quaternion.Inverse(rot);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in TableLayout.FitPoints)
            {
                var local = inv * (p - camPos);
                if (local.z <= 0.1f) return false;
                var v = new Vector2(0.5f + 0.5f * local.x / (local.z * tanH), 0.5f + 0.5f * local.y / (local.z * tanV));
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return min.x >= Frame.xMin && max.x <= Frame.xMax && max.y - min.y <= Frame.height;
        }

        void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight) Refit();
            Apply(Breathing ? Time.unscaledTime : 0f);
        }

        void Apply(float time)
        {
            // Respiration très légère : la scène ne paraît jamais figée, sans gêner la lecture.
            float yaw = Mathf.Sin(time * 0.21f) * 0.45f;
            float pitch = Pitch + Mathf.Sin(time * 0.29f + 1.3f) * 0.3f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(Target + offset + rot * new Vector3(0f, 0f, -distance), rot);
        }
    }
}
