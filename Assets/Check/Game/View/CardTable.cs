using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Check.Core;
using Check.Game.Audio;
using Check.Game.Visuals;
using UnityEngine;

namespace Check.Game.View
{
    /// <summary>Événement du moteur prêt à animer : le siège est déjà traduit en joueur (le tour peut changer ensuite).</summary>
    public sealed class ViewEvent
    {
        public GameEvent E;
        /// <summary>Joueur concerné (identifiant de partie), -1 si aucun.</summary>
        public int Player = -1;
        /// <summary>Siège → joueur pour le tour de l'événement.</summary>
        public int[] SeatPlayers;
        /// <summary>RoundStarted : main du joueur humain dans l'ordre de distribution (vide s'il ne joue pas ce tour).</summary>
        public Card[] HumanHand;
        /// <summary>Dernier événement d'un lot : taille réelle de la pioche après le lot (-1 sinon).</summary>
        public int PileAfter = -1;
        public int RoundNumber;
        public GameEventKind Kind => E.Kind;
    }

    /// <summary>
    /// Toutes les cartes à l'écran : mains, pioche, tas. Joue les événements du moteur dans l'ordre, à son rythme
    /// (le moteur, lui, avance instantanément). La main du joueur humain est tenue devant la caméra en grand éventail ;
    /// celles des adversaires sont de petits éventails face cachée devant leur place.
    /// </summary>
    public sealed class CardTable : MonoBehaviour
    {
        public const int HumanId = 0;
        const int MaxDiscardVisible = 16;

        enum Zone { None, Hand, Discard, Pile }

        sealed class Item
        {
            public CardView View;
            public Vector3 BaseScale;
            public float Scale = 1f, TargetScale = 1f;
            public Zone Zone;
            public int Owner = -1;
            public bool Follow;
            public float FollowAt = -1f;
            public bool Under;
            public CardHighlight LastHighlight = (CardHighlight)(-1);
            public Card? Card => View != null ? View.Card : null;
        }

        Camera cam;
        TableView table;
        SoundBank sfx;
        int playerCount;
        List<Item>[] hands = Array.Empty<List<Item>>();
        readonly List<Item> discard = new List<Item>();
        readonly List<Item> pile = new List<Item>();
        readonly List<Item> all = new List<Item>();
        readonly Queue<ViewEvent> queue = new Queue<ViewEvent>();
        bool running;
        int pileCount;
        Item dragged;
        Vector2 dragScreen;

        /// <summary>Multiplicateur des durées (1 = normal, plus petit = plus rapide).</summary>
        public float Speed = 1f;
        /// <summary>Distance caméra → main du joueur.</summary>
        public float HandDistance = 4.2f;
        /// <summary>Hauteur d'une carte de la main, en fraction de la hauteur d'écran.</summary>
        public float HandCardHeight = 0.3f;
        /// <summary>Largeur maximale de l'éventail (fraction de l'écran), ajustée par l'interface.</summary>
        public float HandMaxWidth = 0.62f;

        /// <summary>Surbrillance d'une carte de la main humaine (fournie par l'interaction).</summary>
        public Func<Card, CardHighlight> HumanHighlight;
        /// <summary>Carte sélectionnée (soulevée) dans la main humaine.</summary>
        public Func<Card, bool> IsSelected;

        public event Action<ViewEvent> EventShown;
        public event Action QueueDrained;

        // ---------------------------------------------------------------- État visible (en retard sur le moteur)
        public int ActivePlayer { get; private set; } = -1;
        public int PendingDraw { get; private set; }
        public Suit? RequestedSuit { get; private set; }
        public Card? TopCard => discard.Count > 0 ? discard[discard.Count - 1].Card : null;
        public int PileCount => pileCount;
        public bool[] Qualified { get; private set; } = Array.Empty<bool>();
        public bool[] Eliminated { get; private set; } = Array.Empty<bool>();
        public int PlayerCount => playerCount;
        public bool IsBusy => running || queue.Count > 0;
        /// <summary>Écarts corrigés par <see cref="Reconcile"/> (doit rester à 0 : sert à la recette).</summary>
        public int ReconcileFixes { get; private set; }

        public void Init(Camera camera, TableView tableView, SoundBank sounds)
        {
            cam = camera;
            table = tableView;
            sfx = sounds;
        }

        bool[] female;

        /// <summary>
        /// Nouvelle partie : table vide, places pour <paramref name="players"/> joueurs. <paramref name="isFemale"/>
        /// (par joueur) choisit les personnages assis ; sans lui, on garde ceux de la partie en cours.
        /// </summary>
        public void Setup(int players, bool[] isFemale = null)
        {
            if (isFemale != null) female = isFemale;
            else if (female == null || female.Length != players) female = DefaultGenders(players);
            table.SetPlayers(female);
            StopAllCoroutines();
            running = false;
            queue.Clear();
            foreach (var it in all.ToList()) DestroyItem(it);
            all.Clear();
            discard.Clear();
            pile.Clear();
            dragged = null;
            playerCount = players;
            hands = new List<Item>[players];
            for (int i = 0; i < players; i++) hands[i] = new List<Item>();
            Qualified = new bool[players];
            Eliminated = new bool[players];
            ActivePlayer = -1;
            PendingDraw = 0;
            RequestedSuit = null;
            pileCount = Card.DeckSize;
            RefreshPile();
            table.SetTension(0);
        }

        static bool[] DefaultGenders(int players)
        {
            var g = new bool[players];
            for (int i = 0; i < players; i++) g[i] = i % 2 == 0;
            return g;
        }

        public int HandCount(int player) => player >= 0 && player < hands.Length ? hands[player].Count : 0;

        public IEnumerable<Card> HumanCards => hands.Length > 0
            ? hands[HumanId].Where(i => i.Card.HasValue).Select(i => i.Card.Value)
            : Enumerable.Empty<Card>();

        // ---------------------------------------------------------------- File d'événements

        /// <summary>Ajoute un lot d'événements du moteur ; <paramref name="round"/> = tour qui vient de les produire.</summary>
        public void Enqueue(IReadOnlyList<GameEvent> events, Round round, Check.Core.Match match)
        {
            if (events == null || events.Count == 0) return;
            var seatPlayers = new int[round.SeatCount];
            for (int s = 0; s < seatPlayers.Length; s++) seatPlayers[s] = round.PlayerId(s);
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                var ve = new ViewEvent { E = e, SeatPlayers = seatPlayers, RoundNumber = match.RoundNumber };
                if (e.Kind == GameEventKind.Eliminated || e.Kind == GameEventKind.MatchWon) ve.Player = e.PlayerId;
                else if (e.Seat >= 0 && e.Seat < seatPlayers.Length) ve.Player = seatPlayers[e.Seat];
                if (e.Kind == GameEventKind.RoundStarted)
                {
                    int hs = round.SeatOf(HumanId);
                    ve.HumanHand = hs >= 0 ? round.Hand(hs).Take(e.Count).ToArray() : Array.Empty<Card>();
                }
                if (i == events.Count - 1) ve.PileAfter = round.DrawPileCount;
                queue.Enqueue(ve);
            }
            if (!running) StartCoroutine(RunQueue());
        }

        IEnumerator RunQueue()
        {
            running = true;
            while (queue.Count > 0)
            {
                var ev = queue.Dequeue();
                IEnumerator anim = null;
                try
                {
                    EventShown?.Invoke(ev);
                    anim = Animate(ev);
                }
                catch (Exception e) { Debug.LogException(e); }
                if (anim != null) yield return RunSafe(anim);
                if (ev.PileAfter >= 0)
                {
                    pileCount = ev.PileAfter;
                    RefreshPile();
                }
            }
            running = false;
            try { QueueDrained?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Exécute une animation en avalant ses exceptions : la file ne doit jamais se bloquer.</summary>
        static IEnumerator RunSafe(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    yield break;
                }
                yield return current;
            }
        }

        float D(float seconds) => seconds * Speed;

        IEnumerator Animate(ViewEvent ev)
        {
            var e = ev.E;
            switch (e.Kind)
            {
                case GameEventKind.RoundStarted: return DealRound(ev);
                case GameEventKind.StartCard: return FlipStartCard(e.Cards[0]);
                case GameEventKind.TurnStarted:
                    ActivePlayer = ev.Player;
                    table.SetActiveSeat(TableLayout.Seat(ev.Player, playerCount).BadgePoint);
                    table.SetActivePlayer(ev.Player);
                    if (ev.Player == HumanId) sfx.Play(Sfx.YourTurn, 0.6f);
                    return Wait(0.12f);
                case GameEventKind.Played:
                    table.ReactPlayer(ev.Player, SeatReaction.Played);
                    return PlayCards(ev.Player, e.Cards);
                case GameEventKind.PlayedUnder: return PlayUnder(ev.Player, e.Cards);
                case GameEventKind.Drew:
                    table.ReactPlayer(ev.Player, SeatReaction.Drew);
                    return DrawCards(ev.Player, e.Cards, 0.13f, 0.34f);
                case GameEventKind.PenaltyDrawn:
                    PendingDraw = 0;
                    table.SetTension(0);
                    sfx.Play(Sfx.Penalty, 0.8f);
                    return DrawCards(ev.Player, e.Cards, 0.09f, 0.32f);
                case GameEventKind.CheckForgotten:
                    sfx.Play(Sfx.Penalty, 0.8f);
                    return DrawCards(ev.Player, e.Cards, 0.12f, 0.34f, 0.5f);
                case GameEventKind.PenaltyStacked:
                    PendingDraw = e.Count;
                    table.SetTension(e.Count);
                    sfx.Play(Sfx.Penalty, 0.45f);
                    return Wait(0.3f);
                case GameEventKind.Skipped:
                    sfx.Play(Sfx.Skip);
                    return Wait(0.55f);
                case GameEventKind.SuitRequested:
                    RequestedSuit = e.Suit;
                    sfx.Play(Sfx.Suit);
                    return Wait(0.55f);
                case GameEventKind.Passed: return Wait(0.25f);
                case GameEventKind.CheckDeclared:
                    sfx.Play(Sfx.Check);
                    table.PlayCheckWave(SeatPoint(ev.Player));
                    return Wait(0.6f);
                case GameEventKind.Finished:
                    if (ev.Player >= 0 && ev.Player < Qualified.Length) Qualified[ev.Player] = true;
                    sfx.Play(Sfx.Games);
                    table.PlayGames(SeatPoint(ev.Player));
                    table.ReactPlayer(ev.Player, SeatReaction.Finished);
                    return Wait(0.8f);
                case GameEventKind.Eliminated:
                    if (ev.Player >= 0 && ev.Player < Eliminated.Length) Eliminated[ev.Player] = true;
                    table.ReactPlayer(ev.Player, SeatReaction.Eliminated);
                    ActivePlayer = -1;
                    sfx.Play(Sfx.Eliminated);
                    return Wait(1.1f);
                case GameEventKind.Reshuffled: return Reshuffle(e.Count);
                case GameEventKind.MatchWon:
                    ActivePlayer = -1;
                    return Wait(0.5f);
            }
            return null;
        }

        IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(D(seconds));
        }

        // ---------------------------------------------------------------- Animations

        IEnumerator DealRound(ViewEvent ev)
        {
            ActivePlayer = -1;
            PendingDraw = 0;
            RequestedSuit = null;
            table.SetTension(0);
            for (int p = 0; p < Qualified.Length; p++) Qualified[p] = false;

            // On ramasse tout vers la pioche.
            var leftovers = all.Where(i => i.Zone != Zone.Pile).ToList();
            foreach (var it in leftovers)
            {
                RemoveFromZones(it);
                it.Follow = false;
                it.TargetScale = CenterScale;
                it.View.MoveTo(PilePose(pile.Count).pos, PilePose(0).rot, D(0.35f), 0.2f, false);
            }
            if (leftovers.Count > 0) yield return new WaitForSeconds(D(0.38f));
            foreach (var it in leftovers) DestroyItem(it);
            pileCount = Card.DeckSize;
            RefreshPile();
            sfx.Play(Sfx.Shuffle, 0.8f);
            yield return new WaitForSeconds(D(0.55f));

            var e = ev.E;
            int n = ev.SeatPlayers.Length;
            for (int r = 0; r < e.Count; r++)
            {
                for (int s = 0; s < n; s++)
                {
                    int seat = (e.Seat + s) % n;
                    int p = ev.SeatPlayers[seat];
                    Card? card = p == HumanId && ev.HumanHand != null && r < ev.HumanHand.Length ? ev.HumanHand[r] : (Card?)null;
                    var it = SpawnAtPile(card);
                    AddToHand(p, it);
                    pileCount--;
                    RefreshPile();
                    var pose = ZonePose(it);
                    float dur = D(0.3f);
                    it.View.MoveTo(pose.pos, pose.rot, dur, 0.25f, p == HumanId ? true : (bool?)null);
                    it.FollowAt = Time.time + dur;
                    sfx.Play(Sfx.Deal, 0.45f);
                    yield return new WaitForSeconds(D(0.06f));
                }
            }
            yield return new WaitForSeconds(D(0.3f));
        }

        IEnumerator FlipStartCard(Card card)
        {
            pileCount--;
            RefreshPile();
            var it = SpawnAtPile(card);
            it.Zone = Zone.Discard;
            discard.Add(it);
            var pose = ZonePose(it);
            float dur = D(0.4f);
            it.View.MoveTo(pose.pos, pose.rot, dur, 0.3f, true);
            it.FollowAt = Time.time + dur;
            sfx.Play(Sfx.Slide, 0.7f);
            yield return new WaitForSeconds(dur);
            sfx.Play(Sfx.Place);
            yield return new WaitForSeconds(D(0.15f));
        }

        Item TakeFromHand(int player, Card card)
        {
            if (player < 0 || player >= hands.Length) return null;
            var hand = hands[player];
            Item it = hand.FirstOrDefault(i => i.Card.HasValue && i.Card.Value == card);
            if (it == null && player != HumanId && hand.Count > 0)
            {
                // Main adverse : on prend une carte cachée près du milieu et on lui donne son identité.
                it = hand.FirstOrDefault(i => !i.Card.HasValue) ?? hand[hand.Count / 2];
                it.View.SetCard(card);
            }
            if (it == null)
            {
                // Désynchronisation (ne devrait pas arriver) : on crée la carte à la place du joueur.
                it = Spawn(card, false, TableLayout.Seat(Math.Max(0, player), playerCount).HandCenter);
            }
            else hand.Remove(it);
            it.Zone = Zone.None;
            if (it == dragged) dragged = null;
            return it;
        }

        IEnumerator PlayCards(int player, IReadOnlyList<Card> cards)
        {
            RequestedSuit = null;
            float last = 0f;
            for (int k = 0; k < cards.Count; k++)
            {
                var it = TakeFromHand(player, cards[k]);
                it.Follow = false;
                it.TargetScale = CenterScale;
                it.Zone = Zone.Discard;
                it.Under = false;
                discard.Add(it);
                var pose = ZonePose(it);
                float dur = D(player == HumanId ? 0.3f : 0.38f);
                it.View.MoveTo(pose.pos, pose.rot, dur, 0.35f, true);
                it.FollowAt = Time.time + dur;
                sfx.Play(Sfx.Slide, 0.8f);
                last = dur;
                if (k < cards.Count - 1) { yield return new WaitForSeconds(D(0.16f)); last -= D(0.16f); }
            }
            yield return new WaitForSeconds(Mathf.Max(0f, last));
            sfx.Play(Sfx.Place);
            table.PulseDiscard();
            var top = cards[cards.Count - 1];
            if (top.IsJoker) table.PlayJokerBurst(top.Color == CardColor.Red);
            TrimDiscard();
            yield return new WaitForSeconds(D(0.12f));
        }

        IEnumerator PlayUnder(int player, IReadOnlyList<Card> cards)
        {
            foreach (var card in cards)
            {
                var it = TakeFromHand(player, card);
                it.Follow = false;
                it.TargetScale = CenterScale;
                // 1) la carte arrive à côté du tas, face visible…
                var beside = DiscardBase() + new Vector3(0.6f, 0.02f, -0.08f) * CenterScale;
                var rot = Quaternion.Euler(0f, 14f, 0f);
                float dur = D(0.3f);
                it.View.MoveTo(beside, rot, dur, 0.3f, true);
                sfx.Play(Sfx.Slide, 0.8f);
                yield return new WaitForSeconds(dur + D(0.12f));
                // 2) … puis se glisse dessous : elle dépasse un peu du tas.
                it.Zone = Zone.Discard;
                it.Under = true;
                discard.Insert(0, it);
                var pose = ZonePose(it);
                float slide = D(0.32f);
                it.View.MoveTo(pose.pos, pose.rot, slide, 0f);
                it.FollowAt = Time.time + slide;
                sfx.Play(Sfx.Slide, 0.5f);
                yield return new WaitForSeconds(slide);
            }
            TrimDiscard();
            yield return new WaitForSeconds(D(0.15f));
        }

        IEnumerator DrawCards(int player, IReadOnlyList<Card> cards, float stagger, float flight, float preDelay = 0f)
        {
            if (preDelay > 0f) yield return new WaitForSeconds(D(preDelay));
            float dur = D(flight);
            foreach (var card in cards)
            {
                pileCount = Math.Max(0, pileCount - 1);
                RefreshPile();
                var it = SpawnAtPile(player == HumanId ? card : (Card?)null);
                AddToHand(player, it);
                var pose = ZonePose(it);
                it.View.MoveTo(pose.pos, pose.rot, dur, 0.3f, player == HumanId ? true : (bool?)null);
                it.FollowAt = Time.time + dur;
                sfx.Play(Sfx.Draw, 0.7f);
                table.PulseDrawPile();
                yield return new WaitForSeconds(D(stagger));
            }
            yield return new WaitForSeconds(Mathf.Max(0f, dur - D(stagger)) + D(0.08f));
        }

        IEnumerator Reshuffle(int newPileCount)
        {
            var moving = discard.Take(Math.Max(0, discard.Count - 1)).ToList();
            foreach (var it in moving)
            {
                discard.Remove(it);
                it.Zone = Zone.None;
                it.Follow = false;
                it.View.MoveTo(PilePose(pile.Count).pos, PilePose(0).rot, D(0.3f), 0.25f, false);
            }
            sfx.Play(Sfx.Shuffle);
            yield return new WaitForSeconds(D(0.35f));
            foreach (var it in moving) DestroyItem(it);
            pileCount = newPileCount;
            RefreshPile();
            // Petit tassement de la nouvelle pioche.
            foreach (var p in pile) p.FollowAt = -1f;
            yield return new WaitForSeconds(D(0.45f));
        }

        // ---------------------------------------------------------------- Création, zones

        Item Spawn(Card? card, bool faceUp, Vector3 position)
        {
            var view = CardView.Create(card, faceUp, transform);
            // Une carte naît toujours sur la table (pioche, tas) : à la taille du centre.
            var it = new Item { View = view, BaseScale = view.transform.localScale, Scale = CenterScale, TargetScale = CenterScale };
            view.transform.localScale = it.BaseScale * CenterScale;
            view.Snap(position, Quaternion.identity);
            all.Add(it);
            return it;
        }

        Item SpawnAtPile(Card? card)
        {
            var pose = PilePose(pile.Count);
            var it = Spawn(card, false, pose.pos);
            it.View.Snap(pose.pos + Vector3.up * 0.01f, pose.rot);
            return it;
        }

        void AddToHand(int player, Item it)
        {
            it.Zone = Zone.Hand;
            it.Owner = player;
            it.Follow = false;
            if (player < 0 || player >= hands.Length) return;
            hands[player].Add(it);
            if (player == HumanId)
            {
                it.TargetScale = HumanScale();
                SortHumanHand();
            }
            else it.TargetScale = OpponentScale();
        }

        void RemoveFromZones(Item it)
        {
            foreach (var h in hands) h.Remove(it);
            discard.Remove(it);
            pile.Remove(it);
            it.Zone = Zone.None;
        }

        void DestroyItem(Item it)
        {
            RemoveFromZones(it);
            all.Remove(it);
            if (it == dragged) dragged = null;
            if (it.View != null) Destroy(it.View.gameObject);
        }

        void TrimDiscard()
        {
            while (discard.Count > MaxDiscardVisible) DestroyItem(discard[0]);
        }

        /// <summary>Ajuste la pile de dos de cartes visible à la taille de la pioche.</summary>
        void RefreshPile()
        {
            if (table == null) return;
            int wanted = pileCount <= 0 ? 0 : Mathf.Clamp(1 + pileCount / 4, 1, 14);
            while (pile.Count < wanted)
            {
                var pose = PilePose(pile.Count);
                var it = Spawn(null, false, pose.pos);
                it.View.Snap(pose.pos, pose.rot);
                it.Zone = Zone.Pile;
                it.Follow = true;
                pile.Add(it);
            }
            while (pile.Count > wanted) DestroyItem(pile[pile.Count - 1]);
        }

        /// <summary>Tri automatique de la main : par couleur (♠ ♥ ♣ ♦, noir et rouge alternés) puis par valeur, jokers à la fin.</summary>
        void SortHumanHand()
        {
            if (hands.Length == 0) return;
            hands[HumanId].Sort((a, b) => SortKey(a.Card).CompareTo(SortKey(b.Card)));
        }

        static int SortKey(Card? c)
        {
            if (!c.HasValue) return 1000;
            var card = c.Value;
            if (card.IsJoker) return 900 + card.Id;
            int suitOrder = card.Suit switch { Suit.Spades => 0, Suit.Hearts => 1, Suit.Clubs => 2, _ => 3 };
            return suitOrder * 20 + (int)card.Rank;
        }

        // ---------------------------------------------------------------- Poses

        float HumanScale()
        {
            if (cam == null) return 1f;
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return HandCardHeight * 2f * HandDistance * tanV / CardView.Height;
        }

        float OpponentScale() => playerCount <= 5 ? 0.78f : playerCount <= 7 ? 0.66f : 0.56f;

        Vector3 DiscardBase() => table != null ? table.DiscardPosition : Vector3.zero;

        static float CenterScale => TableView.CenterCardScale;

        (Vector3 pos, Quaternion rot) PilePose(int index)
        {
            var basePos = table != null ? table.DrawPilePosition : Vector3.zero;
            float jitter = Mathf.Sin(index * 12.9898f) * 2.5f;
            // Chaque carte visible de la pioche en représente ~4 : pas de pile un peu plus grand que l'épaisseur.
            return (basePos + Vector3.up * ((CardView.Thickness * 0.5f + index * (CardView.Thickness * 2f + 0.0008f)) * CenterScale), Quaternion.Euler(0f, jitter, 0f));
        }

        (Vector3 pos, Quaternion rot) ZonePose(Item it)
        {
            switch (it.Zone)
            {
                case Zone.Hand:
                    var hand = it.Owner >= 0 && it.Owner < hands.Length ? hands[it.Owner] : null;
                    int idx = hand != null ? hand.IndexOf(it) : 0;
                    int count = hand?.Count ?? 1;
                    return it.Owner == HumanId ? HumanPose(idx, count, it) : OpponentPose(it.Owner, idx, count);
                case Zone.Discard: return DiscardPose(discard.IndexOf(it), it);
                case Zone.Pile: return PilePose(pile.IndexOf(it));
            }
            return (it.View.transform.position, it.View.transform.rotation);
        }

        (Vector3 pos, Quaternion rot) DiscardPose(int index, Item it)
        {
            int id = it.Card.HasValue ? it.Card.Value.Id : index;
            float h = Mathf.Sin(id * 78.233f) * 43758.5453f;
            h -= Mathf.Floor(h);
            float h2 = Mathf.Sin(id * 12.9898f) * 24634.6345f;
            h2 -= Mathf.Floor(h2);
            var basePos = DiscardBase();
            if (it.Under)
            {
                // Le 2 glissé dessous dépasse en bas à droite.
                return (basePos + new Vector3(0.2f + h * 0.06f, CardView.Thickness * 0.5f, -0.16f) * CenterScale, Quaternion.Euler(0f, 16f + h2 * 10f, 0f));
            }
            var offset = new Vector3((h - 0.5f) * 0.1f * CenterScale,
                (CardView.Thickness * 0.5f + (Mathf.Max(0, index) + 1) * (CardView.Thickness + 0.0008f)) * CenterScale, (h2 - 0.5f) * 0.1f * CenterScale);
            // Rotation modérée : la carte du dessus doit se lire d'un coup d'œil.
            return (basePos + offset, Quaternion.Euler(0f, (h - 0.5f) * 16f, 0f));
        }

        /// <summary>Pose d'une carte de la main humaine : grand éventail tenu devant la caméra, en bas de l'écran.</summary>
        (Vector3 pos, Quaternion rot) HumanPose(int index, int count, Item it)
        {
            if (cam == null) return (Vector3.zero, Quaternion.identity);
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;
            float scale = HumanScale();
            float cardW = CardView.Width * scale / (2f * HandDistance * tanH);   // largeur d'une carte (fraction d'écran)
            float maxW = Mathf.Max(cardW * 1.5f, HandMaxWidth);
            float step = count <= 1 ? 0f : Mathf.Min(cardW * 0.7f, (maxW - cardW) / (count - 1));
            float x = (index - (count - 1) * 0.5f) * step;
            float half = Mathf.Max(0.001f, (count - 1) * 0.5f * step + cardW * 0.5f);
            float u = Mathf.Clamp(x / half, -1f, 1f);
            bool selected = it.Card.HasValue && IsSelected != null && IsSelected(it.Card.Value);
            float vy = HandCardHeight * 0.5f + 0.012f - 0.035f * u * u + (selected ? 0.035f : 0f);
            float depth = HandDistance - index * 0.01f;
            var pos = cam.ViewportToWorldPoint(new Vector3(0.5f + x, vy, depth));
            float roll = -u * Mathf.Min(9f, 2.5f + count * 0.6f);
            var local = Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(10f, Vector3.right) *
                        Quaternion.LookRotation(Vector3.up, Vector3.back);
            return (pos, cam.transform.rotation * local);
        }

        /// <summary>Pose d'une carte d'une main adverse : petit éventail face cachée, incliné vers le centre.</summary>
        (Vector3 pos, Quaternion rot) OpponentPose(int player, int index, int count)
        {
            var seat = TableLayout.Seat(player, playerCount);
            float s = OpponentScale();
            var right = Vector3.Cross(Vector3.up, seat.Facing);
            float step = count <= 1 ? 0f : Mathf.Min(0.2f, 1.25f / (count - 1));
            float off = (index - (count - 1) * 0.5f) * step;
            float tilt = 32f;
            var pos = seat.HandCenter + right * (off * s / 0.6f)
                      - seat.Facing * (off * off * 0.12f)
                      + Vector3.up * (Mathf.Sin(tilt * Mathf.Deg2Rad) * CardView.Height * s * 0.5f + 0.02f + index * 0.01f);
            var rot = Quaternion.AngleAxis(tilt, right) * Quaternion.AngleAxis(off * 22f, Vector3.up) *
                      Quaternion.LookRotation(seat.Facing, Vector3.up);
            return (pos, rot);
        }

        // ---------------------------------------------------------------- Mise à jour par image

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-16f * Time.deltaTime);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var it = all[i];
                if (it.View == null) { all.RemoveAt(i); continue; }

                // Échelle (petits éventails adverses, grande main du joueur).
                if (!Mathf.Approximately(it.Scale, it.TargetScale))
                {
                    it.Scale = Mathf.MoveTowards(it.Scale, it.TargetScale, Time.deltaTime * 2.2f / Mathf.Max(0.2f, Speed));
                    it.View.transform.localScale = it.BaseScale * it.Scale;
                }

                if (!it.Follow && it.FollowAt >= 0f && Time.time >= it.FollowAt && !it.View.IsMoving)
                {
                    it.Follow = true;
                    it.FollowAt = -1f;
                }
                if (it == dragged)
                {
                    PlaceDragged();
                    continue;
                }
                if (it.Follow && !it.View.IsMoving && it.Zone != Zone.None)
                {
                    var pose = ZonePose(it);
                    var tr = it.View.transform;
                    tr.SetPositionAndRotation(Vector3.Lerp(tr.position, pose.pos, k), Quaternion.Slerp(tr.rotation, pose.rot, k));
                }

                if (it.Zone == Zone.Hand && it.Owner == HumanId && it.Card.HasValue)
                {
                    var hl = HumanHighlight != null ? HumanHighlight(it.Card.Value) : CardHighlight.None;
                    if (hl != it.LastHighlight)
                    {
                        it.LastHighlight = hl;
                        it.View.SetHighlight(hl);
                    }
                }
                else if (it.LastHighlight != CardHighlight.None)
                {
                    it.LastHighlight = CardHighlight.None;
                    it.View.SetHighlight(CardHighlight.None);
                }
            }
        }

        // ---------------------------------------------------------------- Toucher

        /// <summary>Carte de la main humaine sous le point d'écran (la plus au-dessus d'abord).</summary>
        public bool TryPickHumanCard(Vector2 screen, out Card card)
        {
            card = default;
            if (cam == null || hands.Length == 0) return false;
            var hand = hands[HumanId];
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var it = hand[i];
                if (!it.Card.HasValue || it.View == null) continue;
                var tr = it.View.transform;
                if (ScreenQuadContains(tr.position, tr.rotation, CardView.Width * it.Scale, CardView.Height * it.Scale, screen))
                {
                    card = it.Card.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Point d'écran où toucher une carte de la main humaine : son coin haut gauche (index de la carte),
        /// toujours visible dans l'éventail. Sert à la recette automatique.
        /// </summary>
        public bool TryGetHumanCardScreenPoint(Card card, out Vector2 screen)
        {
            screen = default;
            if (cam == null || hands.Length == 0) return false;
            var it = hands[HumanId].FirstOrDefault(i => i.Card.HasValue && i.Card.Value == card);
            if (it?.View == null) return false;
            var tr = it.View.transform;
            var local = new Vector3(-CardView.Width * 0.38f, 0f, CardView.Height * 0.3f) * it.Scale;
            screen = cam.WorldToScreenPoint(tr.position + tr.rotation * local);
            return true;
        }

        public bool IsOverDrawPile(Vector2 screen)
        {
            if (cam == null || table == null) return false;
            var top = PilePose(Mathf.Max(0, pile.Count - 1)).pos;
            return ScreenQuadContains(top, Quaternion.identity, CardView.Width * 1.3f * CenterScale, CardView.Height * 1.25f * CenterScale, screen);
        }

        bool ScreenQuadContains(Vector3 center, Quaternion rot, float width, float height, Vector2 p)
        {
            var r = rot * Vector3.right * (width * 0.5f);
            var f = rot * Vector3.forward * (height * 0.5f);
            Vector2 a = cam.WorldToScreenPoint(center - r - f);
            Vector2 b = cam.WorldToScreenPoint(center + r - f);
            Vector2 c = cam.WorldToScreenPoint(center + r + f);
            Vector2 d = cam.WorldToScreenPoint(center - r + f);
            return SameSide(a, b, c, d, p);
        }

        static bool SameSide(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 p)
        {
            float c1 = Cross(b - a, p - a), c2 = Cross(c - b, p - b), c3 = Cross(d - c, p - c), c4 = Cross(a - d, p - d);
            return (c1 >= 0 && c2 >= 0 && c3 >= 0 && c4 >= 0) || (c1 <= 0 && c2 <= 0 && c3 <= 0 && c4 <= 0);
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        public void BeginDrag(Card card, Vector2 screen)
        {
            if (hands.Length == 0) return;
            dragged = hands[HumanId].FirstOrDefault(i => i.Card.HasValue && i.Card.Value == card);
            dragScreen = screen;
        }

        public void DragTo(Vector2 screen) => dragScreen = screen;

        /// <summary>Lâche la carte : elle retourne dans l'éventail (sauf si elle est posée entre-temps).</summary>
        public void EndDrag()
        {
            if (dragged != null) dragged.Follow = true;
            dragged = null;
        }

        public bool IsDragging => dragged != null;

        void PlaceDragged()
        {
            if (dragged?.View == null || cam == null) return;
            var tr = dragged.View.transform;
            var vp = cam.ScreenToViewportPoint(new Vector3(dragScreen.x, dragScreen.y, 0f));
            var pos = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, HandDistance - 0.4f));
            var rot = cam.transform.rotation * Quaternion.AngleAxis(18f, Vector3.right) * Quaternion.LookRotation(Vector3.up, Vector3.back);
            float k = 1f - Mathf.Exp(-30f * Time.deltaTime);
            tr.SetPositionAndRotation(Vector3.Lerp(tr.position, pos, k), Quaternion.Slerp(tr.rotation, rot, k));
        }

        /// <summary>Point de la table devant un joueur (effets) ; le joueur humain : bord avant de la table.</summary>
        Vector3 SeatPoint(int player) =>
            player >= 0 && player < playerCount ? TableLayout.Seat(player, playerCount).HandCenter : DiscardBase();

        // ---------------------------------------------------------------- Points d'écran pour l'interface

        public Vector3 WorldToScreen(Vector3 world) => cam != null ? cam.WorldToScreenPoint(world) : Vector3.zero;
        public Vector3 BadgeScreenPoint(int player) => WorldToScreen(TableLayout.Seat(player, playerCount).BadgePoint);

        /// <summary>
        /// Repères d'écran d'une main adverse : son centre, et le bord de l'éventail côté joueur (vers l'extérieur).
        /// L'interface pose le badge au-delà de ce bord pour ne jamais cacher les cartes.
        /// </summary>
        public bool SeatScreenAnchors(int player, out Vector3 center, out Vector3 outerEdge)
        {
            var seat = TableLayout.Seat(player, playerCount);
            float reach = CardView.Height * OpponentScale() * 0.5f + 0.12f;
            center = WorldToScreen(seat.HandCenter + Vector3.up * 0.15f);
            outerEdge = WorldToScreen(seat.HandCenter - seat.Facing * reach + Vector3.up * 0.32f);
            return center.z > 0f && outerEdge.z > 0f;
        }
        public Vector3 DiscardScreenPoint => WorldToScreen(DiscardBase());
        public Vector3 DrawPileScreenPoint => WorldToScreen(table != null ? table.DrawPilePosition : Vector3.zero);

        // ---------------------------------------------------------------- Synchronisation directe

        /// <summary>
        /// Remet la vue exactement dans l'état du moteur, sans animation (reprise d'une partie sauvegardée).
        /// </summary>
        public void ResetTo(Check.Core.Match match)
        {
            Setup(match.PlayerCount);
            foreach (var p in match.Eliminated)
            {
                Eliminated[p] = true;
                table.ReactPlayer(p, SeatReaction.Eliminated);
            }
            var round = match.Round;
            if (round == null) return;
            for (int s = 0; s < round.SeatCount; s++)
            {
                int p = round.PlayerId(s);
                Qualified[p] = round.IsFinished(s);
                foreach (var c in round.Hand(s))
                {
                    var it = Spawn(p == HumanId ? c : (Card?)null, p == HumanId, Vector3.zero);
                    AddToHand(p, it);
                    it.Scale = it.TargetScale;
                    it.View.transform.localScale = it.BaseScale * it.Scale;
                }
            }
            var top = round.Discard.Skip(Math.Max(0, round.Discard.Count - 8)).ToList();
            foreach (var c in top)
            {
                var it = Spawn(c, true, Vector3.zero);
                it.Zone = Zone.Discard;
                discard.Add(it);
            }
            pileCount = round.DrawPileCount;
            RefreshPile();
            ActivePlayer = round.IsOver ? -1 : round.PlayerId(round.Current);
            PendingDraw = round.PendingDraw;
            RequestedSuit = round.RequestedSuit;
            table.SetTension(PendingDraw);
            if (ActivePlayer >= 0) table.SetActiveSeat(TableLayout.Seat(ActivePlayer, playerCount).BadgePoint);
            foreach (var it in all)
            {
                var pose = ZonePose(it);
                it.View.Snap(pose.pos, pose.rot);
                it.Follow = true;
            }
        }

        /// <summary>
        /// Filet de sécurité quand la file est vide : corrige tout écart entre la vue et le moteur
        /// (cartes de la main humaine, nombre de cartes adverses, dessus du tas, pioche). Compte les corrections.
        /// </summary>
        public void Reconcile(Check.Core.Match match)
        {
            var round = match?.Round;
            if (round == null || IsBusy || hands.Length != match.PlayerCount) return;
            int fixes = 0;
            for (int p = 0; p < hands.Length; p++)
            {
                int seat = round.SeatOf(p);
                var truth = seat >= 0 ? round.Hand(seat) : (IReadOnlyList<Card>)Array.Empty<Card>();
                var hand = hands[p];
                if (p == HumanId)
                {
                    foreach (var it in hand.Where(i => !i.Card.HasValue || !truth.Contains(i.Card.Value)).ToList()) { DestroyItem(it); fixes++; }
                    foreach (var c in truth)
                    {
                        if (hand.Any(i => i.Card.HasValue && i.Card.Value == c)) continue;
                        var it = Spawn(c, true, PilePose(pile.Count).pos);
                        AddToHand(p, it);
                        it.Follow = true;
                        fixes++;
                    }
                }
                else
                {
                    while (hand.Count > truth.Count) { DestroyItem(hand[hand.Count - 1]); fixes++; }
                    while (hand.Count < truth.Count)
                    {
                        var it = Spawn(null, false, PilePose(pile.Count).pos);
                        AddToHand(p, it);
                        it.Follow = true;
                        fixes++;
                    }
                }
            }
            if (round.Discard.Count > 0 && (!TopCard.HasValue || TopCard.Value != round.Top))
            {
                var it = Spawn(round.Top, true, DiscardBase());
                it.Zone = Zone.Discard;
                it.Follow = true;
                discard.Add(it);
                fixes++;
            }
            if (pileCount != round.DrawPileCount)
            {
                pileCount = round.DrawPileCount;
                RefreshPile();
            }
            if (!round.IsOver)
            {
                if (PendingDraw != round.PendingDraw) { PendingDraw = round.PendingDraw; table.SetTension(PendingDraw); fixes++; }
                if (RequestedSuit != round.RequestedSuit) { RequestedSuit = round.RequestedSuit; fixes++; }
            }
            if (fixes > 0)
            {
                ReconcileFixes += fixes;
                Debug.LogWarning($"[Check] Vue resynchronisée avec le moteur ({fixes} correction(s))");
            }
        }
    }
}
