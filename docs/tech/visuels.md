# Rendu des cartes et de la table (Assets/Check/Game/Visuals)

Direction artistique : afrofuturisme néon (ndop indigo et blanc, wax, or et cuivre, cases musgoum, accents cyan / magenta / ambre). Tout est généré par code au lancement.

## Fichiers
- `Paint2D.cs` : palette `AfroPalette`, moteur de dessin 2D CPU (champs de distance), formes des couleurs (♠♥♦♣), police géométrique `StrokeFont`.
- `CardArt.cs` : dessin des 54 faces (index V/D/R/A/10, figures réversibles, jokers noir/rouge) et du dos.
- `CardAtlas.cs` : atlas unique 2048×1712, dos + carte de lueur, maillages par carte (UV dans l'atlas), matériaux partagés.
- `CardView.cs` / `TableView.cs` : contrats publics (signatures inchangées, membres ajoutés listés ci-dessous).
- `TableArt.cs` : textures du plateau, emplacements, tube néon, sol.
- `NeonEffects.cs` : effets à particules partagés.
- `VisualsDemo.cs` : vitrine lancée par `-check-visuals-demo <dossier>` (captures + mesure d'images/s, puis quitte).

## API ajoutée
- `CardView.Prewarm()`, `CardView.IsFlipping`, constantes `SelectedLift`, `SelectedShift`, `FlipDuration`.
- `TableView.PlayCheckWave(Vector3)`, `PlayJokerBurst(bool rouge)`, `PlayGames(Vector3)`, `PulseDiscard()`, `PulseDrawPile()`, `TensionLevel`, `PendingDraw`.
- `NeonEffects.JokerBurst`, `CheckWave`, `GamesCelebration` (+ `Flash`, `LandingPulse`).

## Notes
- Le plateau visible est 3,5 mm sous y = 0 : poser les cartes à `SurfaceY + Thickness/2 + i × (Thickness + 0,0008)` pour une pile.
- Une main en éventail doit décaler chaque carte en hauteur (~1 cm) pour éviter le scintillement entre cartes qui se chevauchent.
- Captures : `docs/captures/visuels/`.
