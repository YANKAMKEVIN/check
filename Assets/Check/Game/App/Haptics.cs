using System;
using UnityEngine;

namespace Check.Game.App
{
    /// <summary>
    /// Retour haptique léger (Android : vibration de 12 ms à faible amplitude via VibrationEffect).
    /// Handheld.Vibrate est bien trop fort : ailleurs (iOS, Mac), on ne fait rien.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effectClass;
        static bool failed;
#endif

        public static void Light() => Pulse(12, 60);

        public static void Medium() => Pulse(22, 140);

        static void Pulse(long milliseconds, int amplitude)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (failed) return;
            try
            {
                if (vibrator == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
                vibrator.Call("vibrate", effect);
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning("[Check] Haptique indisponible : " + e.Message);
            }
#endif
        }

        /// <summary>
        /// Jamais appelé : la simple présence de Handheld.Vibrate fait ajouter par Unity la permission
        /// Android VIBRATE au manifeste, nécessaire à <see cref="Light"/>.
        /// </summary>
        [Obsolete("Ne pas appeler : sert seulement à déclarer la permission VIBRATE")]
        static void DeclareVibratePermission()
        {
#if UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }
    }
}
