# Check — état du projet

Jeu de cartes camerounais « Check » en 3D, pour Android et iOS (macOS pour tester). Direction artistique afrofuturiste mêlée au néon de KevChess, tout généré par code.

Dernière mise à jour : 2026-10-30.

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
    App/     CheckApp : point d'entrée, se crée seul au chargement de la scène
    Input/ Audio/ UI/   repris de KevChess
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

## En cours

Trois chantiers menés en parallèle sur des branches séparées, à intégrer : IA (`Assets/Check/AI`), rendu des cartes et de la table (`Game/Visuals`), boucle de jeu et interface (`Game/` hors Visuals).
