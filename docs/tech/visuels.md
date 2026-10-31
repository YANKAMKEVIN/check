# Rendu des cartes et de la table (Assets/Check/Game/Visuals)

Direction artistique : décor « Dernier Métro » (wagon rétro, laiton, banquettes, ville de nuit qui défile ; voir `WagonDecor.cs`). Les cartes gardent pour l'instant leur style afrofuturiste d'origine (dos, figures, jokers). Tout est généré par code au lancement.

## Fichiers
- `Paint2D.cs` : palette `AfroPalette`, moteur de dessin 2D CPU (champs de distance), formes des couleurs (♠♥♦♣), police géométrique `StrokeFont`.
- `CardArt.cs` : dessin des 54 faces (index V/D/R/A/10, figures réversibles, jokers noir/rouge) et du dos.
- `CardAtlas.cs` : atlas unique 2048×1712, dos + carte de lueur, maillages par carte (UV dans l'atlas), matériaux partagés.
- `CardView.cs` / `TableView.cs` : contrats publics (signatures inchangées, membres ajoutés listés ci-dessous).
- `TableArt.cs` : forme du plateau (super-ellipse), emplacements, bande lumineuse du chant, vignette de tension.
- `WagonDecor.cs` : wagon (parois, fenêtres, banquettes, barres, porte du fond), ville qui défile en deux plans de parallaxe, bandes de lumière des lampadaires. Pas de plafond : la caméra est plus haute que le wagon.
- `NeonEffects.cs` : effets à particules partagés.
- `VisualsDemo.cs` : vitrine lancée par `-check-visuals-demo <dossier>` (captures + mesure d'images/s, puis quitte).

## API ajoutée
- `CardView.Prewarm()`, `CardView.IsFlipping`, constantes `SelectedLift`, `SelectedShift`, `FlipDuration`.
- `TableView.PlayCheckWave(Vector3)`, `PlayJokerBurst(bool rouge)`, `PlayGames(Vector3)`, `PulseDiscard()`, `PulseDrawPile()`, `TensionLevel`, `PendingDraw`.
- `NeonEffects.JokerBurst`, `CheckWave`, `GamesCelebration` (+ `Flash`, `LandingPulse`).

## Notes
- Le plateau visible est 3,5 mm sous y = 0 : poser les cartes à `SurfaceY + Thickness/2 + i × (Thickness + 0,0008)` pour une pile.
- Une main en éventail doit décaler chaque carte en hauteur (~1 cm) pour éviter le scintillement entre cartes qui se chevauchent.
- `TableView.CenterCardScale` (1,7) : échelle des cartes de la pioche et du tas ; `CardTable` crée chaque carte à cette taille.
- Captures : `docs/captures/metro/` (décor actuel), `docs/captures/visuels/` (ancien décor néon).
