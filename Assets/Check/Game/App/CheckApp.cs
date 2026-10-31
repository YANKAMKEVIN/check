using System;
using Check.Core;
using Check.Game.Audio;
using Check.Game.CameraRig;
using Check.Game.Input;
using Check.Game.Match;
using Check.Game.UI;
using Check.Game.View;
using Check.Game.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Check.Game.App
{
    /// <summary>
    /// Point d'entrée : construit caméra, ambiance, table, cartes, boucle de jeu, interface, et branche les entrées.
    /// La scène peut être vide : l'app se crée toute seule au chargement.
    /// </summary>
    public sealed class CheckApp : MonoBehaviour
    {
        Camera mainCamera;
        TableCamera tableCamera;
        TableView table;
        CardTable cards;
        GameController game;
        HumanTurn human;
        Hud hud;
        SoundBank sfx;
        PointerInput pointer;
        Card? draggedCard;

        public GameController Game => game;
        public HumanTurn Human => human;
        public Hud Hud => hud;
        public CardTable Cards => cards;
        public Camera Camera => mainCamera;

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

            DisableForeignSceneObjects();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            mainCamera = camGo.AddComponent<Camera>();
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = WagonDecor.Sky;
            mainCamera.allowHDR = true;
            mainCamera.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            tableCamera = camGo.AddComponent<TableCamera>();

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

            sfx = new GameObject("Sound").AddComponent<SoundBank>();
            sfx.transform.SetParent(transform, false);

            cards = new GameObject("Cards").AddComponent<CardTable>();
            cards.transform.SetParent(transform, false);
            cards.Init(mainCamera, table, sfx);

            game = gameObject.AddComponent<GameController>();
            game.Init(cards);
            human = new HumanTurn(game, cards, sfx);

            BuildEventSystem();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(game, cards, human, sfx);

            pointer = gameObject.AddComponent<PointerInput>();
            pointer.Tapped += p => Guard(() => OnTap(p));
            pointer.DoubleTapped += p => Guard(() => OnDoubleTap(p));
            pointer.DragBegan += p => Guard(() => OnDragBegan(p));
            pointer.DragMoved += p => Guard(() => { if (draggedCard.HasValue) cards.DragTo(p); });
            pointer.DragEnded += p => Guard(() => OnDragEnded(p));
        }

        void Start()
        {
            CardView.Prewarm(); // atlas des 54 faces : généré une fois, pendant l'accueil
            cards.Setup(4); // une pioche sur la table derrière l'accueil
            hud.ShowHome();
            if (AutoTest.Requested(out var folder)) gameObject.AddComponent<AutoTest>().Run(this, folder);
        }

        /// <summary>Lancé depuis une autre scène (caméra, lumière…) : on coupe ce qui doublonnerait.</summary>
        void DisableForeignSceneObjects()
        {
            foreach (var cam in FindObjectsByType<Camera>())
                if (!cam.transform.IsChildOf(transform)) cam.gameObject.SetActive(false);
            foreach (var light in FindObjectsByType<Light>())
                if (!light.transform.IsChildOf(transform)) light.enabled = false;
            foreach (var listener in FindObjectsByType<AudioListener>())
                if (!listener.transform.IsChildOf(transform)) listener.enabled = false;
        }

        static void BuildEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ---------------------------------------------------------------- Entrées sur la table

        /// <summary>Toucher : une carte de la main (sélection), la pioche (piocher), le tas (poser la sélection).</summary>
        public void OnTap(Vector2 screen)
        {
            if (hud.IsBlockingOverlayOpen || !game.HasGame) return;
            if (cards.TryPickHumanCard(screen, out var card)) { human.TapCard(card); return; }
            if (cards.IsOverDrawPile(screen))
            {
                if (human.HasDrawn && human.PendingDraw == 0) hud.Flash("Tu as déjà pioché : pose la carte ou passe");
                else human.Draw();
                return;
            }
            if (human.Selection.Count > 0 && NearDiscard(screen)) human.PressPlay();
        }

        public void OnDoubleTap(Vector2 screen)
        {
            if (hud.IsBlockingOverlayOpen || !game.HasGame) return;
            if (cards.TryPickHumanCard(screen, out var card)) human.QuickPlay(card);
        }

        void OnDragBegan(Vector2 start)
        {
            draggedCard = null;
            if (hud.IsBlockingOverlayOpen || !human.CanAct) return;
            if (!cards.TryPickHumanCard(start, out var card)) return;
            draggedCard = card;
            cards.BeginDrag(card, start);
        }

        void OnDragEnded(Vector2 end)
        {
            if (!draggedCard.HasValue) return;
            var card = draggedCard.Value;
            draggedCard = null;
            cards.EndDrag();
            // Lâchée au-dessus de la main (vers la table) : on pose.
            if (end.y / Mathf.Max(1, Screen.height) > 0.36f) human.QuickPlay(card);
        }

        bool NearDiscard(Vector2 screen)
        {
            var p = cards.DiscardScreenPoint;
            return p.z > 0f && Vector2.Distance(screen, p) < Screen.height * 0.17f;
        }

        void Update()
        {
            // Bouton retour Android / Échap : ouvre ou ferme le menu.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Guard(hud.HandleBack);
            if (draggedCard.HasValue && !human.CanAct)
            {
                draggedCard = null;
                cards.EndDrag();
            }
        }
    }
}
