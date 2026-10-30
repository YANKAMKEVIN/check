using Check.Game.Visuals;
using UnityEngine;

namespace Check.Game.App
{
    /// <summary>
    /// Point d'entrée : construit caméra, ambiance, table et interface.
    /// La scène peut être vide : l'app se crée toute seule au chargement.
    /// </summary>
    public sealed class CheckApp : MonoBehaviour
    {
        Camera mainCamera;
        TableView table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<CheckApp>() == null) new GameObject("Check").AddComponent<CheckApp>();
        }

        void Awake()
        {
            if (Application.isMobilePlatform)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            mainCamera = camGo.AddComponent<Camera>();
            mainCamera.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 7.5f, -6.5f), Quaternion.Euler(52f, 0f, 0f));

            table = new GameObject("Table").AddComponent<TableView>();
            table.transform.SetParent(transform, false);
            table.Build();

            var atmoGo = new GameObject("Atmosphere");
            atmoGo.SetActive(false);
            atmoGo.transform.SetParent(transform, false);
            var atmosphere = atmoGo.AddComponent<SceneAtmosphere>();
            atmosphere.ManageCameraFraming = false;
            atmosphere.SetTargetCamera(mainCamera);
            atmoGo.SetActive(true);
        }
    }
}
