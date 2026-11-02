# Rendu des cartes et de la table (Assets/Check/Game/Visuals)

Direction artistique : décor « Dernier Métro ». Depuis le 2026-11-02, le décor par défaut est **peint** (refonte « hybride » choisie par Kevin d'après l'image cible `docs/captures/cible/cible_3.jpg`) : table et cartes en 3D, wagon peint en fond, adversaires illustrés assis autour de la table (`PaintedScene.cs`). L'ancien wagon généré par code (`WagonDecor.cs`) reste disponible avec `-check-style classique`. Les cartes suivent le même style (Art déco des années 30) : face crème à cadre laiton et éventails de coin ; Roi = chef de gare (képi, moustache), Dame = voyageuse (chapeau cloche, col de fourrure), Valet = contrôleur (casquette, lanterne), panneau pétrole (noires) ou bordeaux (rouges) ; jokers saltimbanques (bleu nuit / bordeaux) ; dos bleu nuit à croisillons laiton et rosace « CHECK ». Tout est généré par code au lancement.

## Fichiers
- `Paint2D.cs` : palette `AfroPalette`, moteur de dessin 2D CPU (champs de distance), formes des couleurs (♠♥♦♣), police géométrique `StrokeFont`.
- `CardArt.cs` : dessin des 54 faces (index V/D/R/A/10, figures réversibles, jokers noir/rouge) et du dos.
- `CardAtlas.cs` : atlas unique 2048×1712, dos + carte de lueur, maillages par carte (UV dans l'atlas), matériaux partagés.
- `CardView.cs` / `TableView.cs` : contrats publics (signatures inchangées, membres ajoutés listés ci-dessous).
- `TableArt.cs` : forme du plateau (super-ellipse), emplacements, bande lumineuse du chant, vignette de tension.
- `WagonDecor.cs` : wagon (parois, fenêtres, banquettes, barres, porte du fond), ville qui défile en deux plans de parallaxe, bandes de lumière des lampadaires. Pas de plafond : la caméra est plus haute que le wagon.
- `DecorStyle.cs` : choix du décor (`-check-style classique`), réactions des adversaires (`SeatReaction`), place d'un adversaire assis (`OpponentSpot`).
- `PaintedScene.cs` : fond peint qui suit la caméra (recadrage « cover », sans brouillard), personnages détourés (quads tournés vers la caméra, retournés pour regarder la table) : respiration, se redressent à leur tour, se penchent pour poser, plongent pour piocher, sautillent sur « Games », s'éteignent quand ils sont éliminés. Distribution par genre du prénom (`OpponentNames.IsFemale`) : 5 femmes, 4 hommes ; au-delà, un personnage de l'autre genre complète.
- `Resources/Painted/` : `fond.jpg` (wagon vide, 2048 px) et `perso_1..9.png` (fond vert détouré, RVB prémultiplié par l'alpha, hauteur 1024). Images générées avec Kling (Nano Banana Pro) à partir de l'image cible ; le détourage se fait hors Unity (script Python : clé verte, dé-débordement, recadrage).
- `NeonEffects.cs` : effets à particules partagés.
- `VisualsDemo.cs` : vitrine lancée par `-check-visuals-demo <dossier>` (captures + mesure d'images/s, puis quitte).

## API ajoutée
- `CardView.Prewarm()`, `CardView.IsFlipping`, constantes `SelectedLift`, `SelectedShift`, `FlipDuration`.
- `TableView.SetPlayers(bool[] femmes)`, `SetActivePlayer(int)`, `ReactPlayer(int, SeatReaction)` (sans effet en décor classique).
- `TableView.PlayCheckWave(Vector3)`, `PlayJokerBurst(bool rouge)`, `PlayGames(Vector3)`, `PulseDiscard()`, `PulseDrawPile()`, `TensionLevel`, `PendingDraw`.
- `NeonEffects.JokerBurst`, `CheckWave`, `GamesCelebration` (+ `Flash`, `LandingPulse`).

## Notes
- Le plateau visible est 3,5 mm sous y = 0 : poser les cartes à `SurfaceY + Thickness/2 + i × (Thickness + 0,0008)` pour une pile.
- Une main en éventail doit décaler chaque carte en hauteur (~1 cm) pour éviter le scintillement entre cartes qui se chevauchent.
- `TableView.CenterCardScale` (1,7) : échelle des cartes de la pioche et du tas ; `CardTable` crée chaque carte à cette taille.
- Caméra en décor peint : `Pitch` 36°, `Frame` (0,14 ; 0,31 ; 0,72 ; 0,36) pour laisser la place aux têtes sous le bandeau.
- Captures : `docs/captures/peint/` (décor peint), `docs/captures/metro/` (décor classique), `docs/captures/visuels/` (ancien décor néon).
