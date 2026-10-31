using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Check.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Check.Game.Visuals
{
    /// <summary>
    /// Vitrine autonome du rendu (cartes, table, effets), lancée par l'argument
    /// « -check-visuals-demo [dossier] » : remplace l'app normale par une scène de démonstration,
    /// joue une courte séquence (pioche avec retournement, pose, joker, tension, tour adverse,
    /// « Check », « Games »), prend des captures PNG dans le dossier, mesure les images/s puis quitte.
    /// Sans l'argument, ne fait rien.
    /// </summary>
    public sealed class VisualsDemo : MonoBehaviour
    {
        const string Arg = "-check-visuals-demo";

        string folder;
        int shot;
        Camera cam;
        TableView table;
        readonly List<CardView> hand = new List<CardView>();
        readonly List<CardView> drawPile = new List<CardView>();
        readonly List<CardView> discard = new List<CardView>();
        readonly List<CardView>[] opponents = { new List<CardView>(), new List<CardView>(), new List<CardView>() };
        int purgeFrames = 30;

        public static bool Requested(out string folder)
        {
            folder = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != Arg) continue;
                folder = i + 1 < args.Length && !args[i + 1].StartsWith("-")
                    ? args[i + 1]
                    : Path.Combine(Application.persistentDataPath, "visuals-demo");
                return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (!Requested(out string outFolder)) return;
            PurgeApp();
            Scene old = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("VisualsDemo");
            SceneManager.SetActiveScene(scene);
            var go = new GameObject("Visuals Demo");
            go.AddComponent<VisualsDemo>().folder = outFolder;
            if (old.IsValid() && old.isLoaded) SceneManager.UnloadSceneAsync(old);
        }

        /// <summary>Supprime l'app normale (composant « CheckApp », assemblage non référencé ici).</summary>
        static void PurgeApp()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Name != "CheckApp") continue;
                mb.gameObject.SetActive(false);
                Destroy(mb.gameObject);
            }
        }

        void Update()
        {
            // L'amorçage de CheckApp peut s'exécuter après notre crochet : on le neutralise quelques images.
            if (purgeFrames-- > 0) PurgeApp();
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Log("début, dossier " + folder + ", écran " + Screen.width + "x" + Screen.height);

            CardAtlas.KeepReadable = true;
            float t0 = Time.realtimeSinceStartup;
            CardView.Prewarm();
            Log($"atlas prêt en {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
            if (CardAtlas.ExportAtlasPng(Path.Combine(folder, "atlas.png"))) Log("atlas exporté");

            BuildScene();
            yield return null;
            DealLayout();
            yield return new WaitForSeconds(1.5f);
            yield return Capture("table");

            // Pioche : la carte du dessus vole vers la main en se retournant.
            CardView drawn = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            drawn.SetCard(new Card(Rank.Ten, Suit.Spades));
            table.PulseDrawPile();
            HandPose(hand.Count, hand.Count + 1, out Vector3 hp, out Quaternion hr);
            drawn.MoveTo(hp + Vector3.up * 0.05f, hr, 0.9f, 0.45f, true);
            yield return new WaitForSeconds(0.3f);
            yield return Capture("pioche_en_vol");
            yield return new WaitForSeconds(0.12f);
            yield return new WaitForSeconds(0.6f);
            hand.Add(drawn);
            ArrangeHand(0.35f);
            yield return new WaitForSeconds(0.5f);

            // Le joueur pose la carte sélectionnée sur le tas.
            CardView played = hand[3];
            hand.RemoveAt(3);
            played.SetHighlight(CardHighlight.None);
            played.MoveTo(DiscardPose(discard.Count, out Quaternion dr), dr, 0.55f, 0.35f);
            discard.Add(played);
            ArrangeHand(0.35f);
            yield return new WaitForSeconds(0.6f);
            table.PulseDiscard();
            yield return Capture("pose_sur_le_tas");

            // Un adversaire joue un joker rouge : explosion de lueur et tension.
            table.SetActiveSeat(SeatPosition(1));
            yield return new WaitForSeconds(0.6f);
            CardView joker = opponents[1][2];
            opponents[1].RemoveAt(2);
            joker.SetCard(Card.RedJoker);
            joker.MoveTo(DiscardPose(discard.Count, out Quaternion jr), jr, 0.7f, 0.4f, true);
            discard.Add(joker);
            yield return new WaitForSeconds(0.72f);
            table.PlayJokerBurst(true);
            table.SetTension(4);
            yield return new WaitForSeconds(0.12f);
            yield return Capture("joker_explosion");
            yield return new WaitForSeconds(1.4f);
            yield return Capture("tension");

            // Tour de l'adversaire de droite.
            table.SetActiveSeat(SeatPosition(2));
            yield return new WaitForSeconds(1.2f);
            yield return Capture("tour_adverse");
            table.SetTension(0);

            // Retour au joueur : « Check » puis « Games ».
            table.SetActiveSeat(SeatPosition(-1));
            yield return new WaitForSeconds(1.5f);
            table.PlayCheckWave(new Vector3(0f, 0f, 0.35f));
            yield return new WaitForSeconds(0.3f);
            yield return Capture("check");
            yield return new WaitForSeconds(1.2f);
            table.PlayGames(new Vector3(0f, 0f, -2.6f));
            yield return new WaitForSeconds(0.65f);
            yield return Capture("games");
            yield return new WaitForSeconds(1.5f);

            // Mesure des performances (Mac, indicatif) avec toutes les cartes à l'écran.
            int cards = FindObjectsByType<CardView>(FindObjectsSortMode.None).Length;
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 3f) { frames++; yield return null; }
            float fps = frames / (Time.realtimeSinceStartup - start);
            Log($"performance : {fps:0} images/s avec {cards} cartes (vsync coupé)");

            // Gros plan sur la main.
            Vector3 camPos = cam.transform.position;
            Quaternion camRot = cam.transform.rotation;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.4f, -6.0f), Quaternion.LookRotation(new Vector3(0f, -0.08f, -2.9f) - new Vector3(0f, 3.4f, -6.0f)));
            yield return new WaitForSeconds(0.4f);
            yield return Capture("main_gros_plan");

            // Gros plan sur le tas et la pioche.
            cam.transform.SetPositionAndRotation(new Vector3(0.6f, 2.6f, -1.6f), Quaternion.LookRotation(new Vector3(0.1f, 0f, 0.4f) - new Vector3(0.6f, 2.6f, -1.6f)));
            yield return new WaitForSeconds(0.4f);
            yield return Capture("tas_gros_plan");

            // Planche des 54 faces + dos (vue de dessus, loin de la table).
            cam.transform.SetPositionAndRotation(camPos, camRot);
            yield return FaceSheet();

            Log("TERMINÉ");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(0);
        }

        // ------------------------------------------------------------------ scène

        void BuildScene()
        {
            var camGo = new GameObject("Demo Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.allowHDR = true;
            cam.fieldOfView = 44f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 140f;
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 8.0f, -7.2f), Quaternion.Euler(52f, 0f, 0f));

            table = new GameObject("Table").AddComponent<TableView>();
            table.transform.SetParent(transform, false);
            table.Build();

            var atmoGo = new GameObject("Atmosphere");
            atmoGo.SetActive(false);
            atmoGo.transform.SetParent(transform, false);
            var atmosphere = atmoGo.AddComponent<SceneAtmosphere>();
            atmosphere.ManageCameraFraming = false;
            atmosphere.SetTargetCamera(cam);
            atmoGo.SetActive(true);
        }

        static readonly Card[] HandCards =
        {
            new Card(Rank.Ace, Suit.Spades), new Card(Rank.Seven, Suit.Hearts), new Card(Rank.Queen, Suit.Diamonds),
            new Card(Rank.King, Suit.Clubs), new Card(Rank.Two, Suit.Clubs), new Card(Rank.Jack, Suit.Hearts),
            Card.BlackJoker, new Card(Rank.Nine, Suit.Diamonds),
        };

        void DealLayout()
        {
            var rnd = new System.Random(5);
            for (int i = 0; i < 24; i++)
            {
                var c = CardView.Create(null, false, transform);
                c.Snap(table.DrawPilePosition + Vector3.up * (CardView.Thickness * 0.5f + i * (CardView.Thickness + 0.0008f)),
                    Quaternion.Euler(0f, (float)(rnd.NextDouble() - 0.5) * 5f, 0f));
                drawPile.Add(c);
            }
            Card[] under = { new Card(Rank.Five, Suit.Hearts), new Card(Rank.Eight, Suit.Spades), new Card(Rank.Three, Suit.Diamonds), new Card(Rank.Queen, Suit.Spades), new Card(Rank.Seven, Suit.Clubs) };
            foreach (var card in under)
            {
                var c = CardView.Create(card, true, transform);
                c.Snap(DiscardPose(discard.Count, out Quaternion r), r);
                discard.Add(c);
            }
            foreach (var card in HandCards)
            {
                var c = CardView.Create(card, true, transform);
                hand.Add(c);
            }
            ArrangeHand(0f);
            for (int i = 0; i < hand.Count; i++)
                hand[i].SetHighlight(i == 3 ? CardHighlight.Selected : (i == 1 || i == 2 || i == 6) ? CardHighlight.Playable : CardHighlight.Dimmed);

            for (int s = 0; s < 3; s++)
            {
                int n = s == 1 ? 7 : 5;
                for (int i = 0; i < n; i++)
                {
                    var c = CardView.Create(new Card((i * 7 + s * 13) % 52), false, transform);
                    OpponentPose(s, i, n, out Vector3 p, out Quaternion r);
                    c.Snap(p, r);
                    opponents[s].Add(c);
                }
            }
        }

        void ArrangeHand(float duration)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                HandPose(i, hand.Count, out Vector3 p, out Quaternion r);
                if (duration <= 0f) hand[i].Snap(p, r);
                else hand[i].MoveTo(p, r, duration, 0.04f);
            }
        }

        /// <summary>Éventail du joueur humain, légèrement incliné vers la caméra.</summary>
        static void HandPose(int i, int n, out Vector3 pos, out Quaternion rot)
        {
            const float spread = 6.5f, radius = 3.2f;
            float a = (i - (n - 1) * 0.5f) * spread;
            var center = new Vector3(0f, 0f, -2.85f - radius);
            float rad = a * Mathf.Deg2Rad;
            pos = center + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radius + Vector3.up * (0.03f + i * 0.012f);
            rot = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(-16f, 0f, 0f);
        }

        Vector3 DiscardPose(int index, out Quaternion rot)
        {
            float yaw = Mathf.Sin(index * 2.3f) * 22f;
            rot = Quaternion.Euler(0f, yaw, 0f);
            Vector2 off = new Vector2(Mathf.Cos(index * 1.7f), Mathf.Sin(index * 1.7f)) * 0.04f;
            return table.DiscardPosition + new Vector3(off.x, CardView.Thickness * 0.5f + index * (CardView.Thickness + 0.0008f), off.y);
        }

        static Vector3 SeatPosition(int seat)
        {
            switch (seat)
            {
                case 0: return new Vector3(-TableView.HalfWidth, 0f, 0.6f);
                case 1: return new Vector3(0f, 0f, TableView.HalfDepth);
                case 2: return new Vector3(TableView.HalfWidth, 0f, 0.6f);
                default: return new Vector3(0f, 0f, -TableView.HalfDepth);
            }
        }

        static void OpponentPose(int seat, int i, int n, out Vector3 pos, out Quaternion rot)
        {
            float yaw = seat == 0 ? 90f : seat == 1 ? 180f : -90f;
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 seatPos = SeatPosition(seat);
            Vector3 inward = facing * Vector3.forward;
            const float spread = 7f, radius = 2.2f;
            float a = (i - (n - 1) * 0.5f) * spread;
            Vector3 center = seatPos + inward * (0.75f - radius);
            Quaternion q = facing * Quaternion.Euler(0f, a, 0f);
            pos = center + q * Vector3.forward * radius + Vector3.up * (0.004f + i * 0.0075f);
            rot = q;
        }

        IEnumerator FaceSheet()
        {
            var root = new GameObject("Face Sheet").transform;
            root.SetParent(transform, false);
            Vector3 origin = new Vector3(120f, 0f, 0f);
            const int cols = 11;
            const float gx = 0.70f, gz = 0.96f;
            for (int id = 0; id <= Card.DeckSize; id++)
            {
                int col = id % cols, row = id / cols;
                bool back = id == Card.DeckSize;
                var c = CardView.Create(back ? (Card?)null : new Card(id), !back, root);
                c.Snap(origin + new Vector3((col - (cols - 1) * 0.5f) * gx, 0f, -(row - 2f) * gz), Quaternion.identity);
            }
            var keep = cam.orthographic;
            cam.orthographic = true;
            cam.orthographicSize = 2.65f;
            cam.transform.SetPositionAndRotation(origin + Vector3.up * 10f, Quaternion.Euler(90f, 0f, 0f));
            yield return new WaitForSeconds(0.5f);
            yield return Capture("planche_faces", 2);
            cam.orthographic = keep;
        }

        // ------------------------------------------------------------------ utilitaires

        IEnumerator Capture(string name, int superSize = 1)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path, superSize);
            Log("capture " + path);
            yield return null;
            yield return null;
        }

        static void Log(string message) => Debug.Log("[VisualsDemo] " + message);
    }
}
