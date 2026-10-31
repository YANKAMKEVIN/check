# Check — état du projet

Jeu de cartes camerounais « Check » en 3D, pour Android et iOS (macOS pour tester). Direction artistique afrofuturiste mêlée au néon de KevChess, tout généré par code.

Dernière mise à jour : 2026-10-31.

## Décisions prises (avec Kevin)

| Sujet | Décision |
|---|---|
| Jeu | Check, version de Kevin : voir [regles.md](../regles.md) (points « à valider » = réglages de `RuleSet`) |
| Moteur | Unity 6000.6.3f1 + URP 17 + Input System, configuration reprise de `~/dev/kevchess` |
| Plateforme | Mobile d'abord, écran en paysage (jusqu'à 10 joueurs autour de la table) |
| Joueurs | 2 à 10 : le joueur humain + des IA (4 niveaux) |
| DA | Afrofuturisme (motifs ndop / wax, or et cuivre, indigo) + néon façon KevChess |
| Identifiant | `com.kevinyankam.check` |

## Architecture

```
Assets/Check/
  Core/    règles pures (C#, sans UnityEngine) : Card, RuleSet, TurnAction, GameEvent, Round (un tour), Match (tours + éliminations)
  AI/      IA des adversaires (pure C#)
  Game/
    Visuals/ CardView, TableView (contrats publics), outillage néon repris de KevChess
    Match/   GameController (boucle : moteur, IA hors thread, sauvegarde), HumanTurn (sélection, Check, Valet, pioche),
             IOpponentBrain (seul point de branchement de Check.AI), GameSettings (PlayerPrefs, noms), SavedGame
    View/    CardTable (cartes à l'écran + file d'animations des GameEvent), TableLayout (places de 2 à 10 joueurs)
    Camera/  TableCamera (caméra fixe cadrée automatiquement en paysage, respiration)
    UI/      Hud + HudScreens (uGUI par code : bandeau, journal, badges, boutons d'action, accueil, règles, pause…), UIKit
    Input/   PointerInput (tap, double-tap, glisser) ; Audio/ SoundBank (sons de cartes synthétisés)
    App/     CheckApp (point d'entrée, se crée seul), AutoTest (recette -check-autotest), Haptics (Android)
  Editor/  BuildScript (configuration + builds Android/macOS en ligne de commande)
tests/Check.Tests/  tests NUnit hors Unity (Core + AI)
```

## Tests

```bash
cd ~/dev/check/tests/Check.Tests && DOTNET_CLI_TELEMETRY_OPTOUT=1 /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet test
```

63 tests verts sur le moteur : chaque carte spéciale, cumuls et contres, poses multiples, pioche et remélange, Check / Games, éliminations, parties aléatoires de 2 à 10 joueurs (conservation des 54 cartes).

## Builds

```bash
/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/check -executeMethod Check.EditorTools.BuildScript.BuildMac -logFile -
/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/check -executeMethod Check.EditorTools.BuildScript.BuildAndroid -logFile -
```

## Recette automatique (boucle de jeu et interface)

```bash
Builds/macOS/Check.app/Contents/MacOS/Check -check-autotest <dossier> -logFile <dossier>.log   # code de sortie ≠ 0 si échec
```

Partie à 4 où le joueur humain est joué par les chemins du toucher (cartes touchées à l'écran, Check, Poser, Valet,
pioche, Passer, double-tap), retour Android, sauvegarde puis reprise en plein tour, rafale de touchers pendant les
animations, 3 formats d'écran (19,5:9, 16:9, 4:3), puis partie à 10 en accéléré jusqu'au bout ; vérifie l'absence
d'exception et d'écart vue/moteur. Captures : `docs/captures/jeu/`. Verte le 2026-10-31 avec l'IA réelle et le rendu définitif.

## En cours

Trois chantiers menés en parallèle sur des branches séparées, à intégrer : IA (`Assets/Check/AI`), rendu des cartes et de la table (`Game/Visuals`), boucle de jeu et interface (`Game/` hors Visuals).

## Points ouverts (boucle de jeu et interface)

- Passe QA sur téléphone (Galaxy) : taille de la main au doigt, haptique, encoches, bouton retour.
- Règle à valider : à 2 joueurs, deux As posés ensemble font sauter l'adversaire puis soi-même (le tour revient à l'adversaire).
- Glisser une carte vers le tas : implémenté, pas encore essayé au doigt.
- La reprise rejoue le « film » des actions (graine + actions) : une sauvegarde faite avec une version différente des règles est effacée si elle ne se rejoue pas.

## Pièges connus (hérités de KevChess)

- URP 17 supprimait toutes les variantes de `UberPost` sur Android (écran noir) : « Strip Unused Post Processing Variants » est désactivé dans `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (déjà fait ici).
- Mac : seul le module Mono est installé (pas IL2CPP).
- Ne jamais ouvrir deux éditeurs Unity sur le même dossier de projet (chaque worktree a le sien).
