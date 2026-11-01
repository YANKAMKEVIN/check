# Check — état du projet

Jeu de cartes camerounais « Check » en 3D, pour Android et iOS (macOS pour tester). Décor « Dernier Métro » (wagon rétro aménagé en salon qui traverse une ville la nuit), tout généré par code.

Dernière mise à jour : 2026-10-31.

Dépôt : https://github.com/YANKAMKEVIN/check (privé), branche `main` suivie sur `origin`.

## Décisions prises (avec Kevin)

| Sujet | Décision |
|---|---|
| Jeu | Check, version de Kevin : voir [regles.md](../regles.md) (points « à valider » = réglages de `RuleSet`) |
| Moteur | Unity 6000.6.3f1 + URP 17 + Input System, configuration reprise de `~/dev/kevchess` |
| Plateforme | Mobile d'abord, écran en paysage (jusqu'à 10 joueurs autour de la table) |
| Joueurs | 2 à 10 : le joueur humain + des IA (4 niveaux) |
| DA | « Dernier Métro » (choisi par Kevin le 2026-10-31 parmi 6 univers proposés par Codex ; il ne voulait plus de thème Cameroun ni des tours néon) : wagon, banquettes, laiton, ville de nuit qui défile ; palette pétrole #173F46, ambre #D99B59, nuit #243047, crème #F3E5CE. Captures : `docs/captures/metro/` |
| Carte du centre | Pioche et tas agrandis (`TableView.CenterCardScale` = 1,7), plateau mat et uni, rotation du tas réduite : demande de Kevin (« on a du mal à voir la carte au centre ») |
| Identifiant | `com.kevinyankam.check` |

## Architecture

```
Assets/Check/
  Core/    règles pures (C#, sans UnityEngine) : Card, RuleSet, TurnAction, GameEvent, Round (un tour), Match (tours + éliminations)
  AI/      IA des adversaires (pure C#)
  Game/
    Visuals/ CardView, TableView (contrats publics), WagonDecor (wagon + ville), outillage repris de KevChess
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

102 tests verts (~10 à 50 s selon la charge) : moteur (chaque carte spéciale, cumuls et contres, poses multiples, pioche et remélange, Check / Games, éliminations, parties aléatoires de 2 à 10 joueurs) et IA (légalité, budget de temps, pas de triche, tournois : Moyen bat Facile, Difficile bat Moyen ; Expert ≈ Difficile pour l'instant).

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

## État au 2026-10-31

Jeu complet jouable : accueil, 2 à 10 joueurs, 4 niveaux d'IA, règles réglables, animations, éliminations, victoire, sauvegarde.
Installé et essayé au doigt sur le **Galaxy S24** (`RFCX80Y0FVX`) : rendu 3D, sélection, pose, tours des IA, cumul de pénalités OK.
L'APK Android : `Builds/Android/Check.apk` (36 Mo). Toujours passer `-buildTarget Android` (ou `OSXUniversal`) en batchmode. Pas encore de build iOS (module iOS à installer dans Unity Hub).

## Prochaines étapes

- [ ] Kevin valide les règles marquées « à valider » dans `docs/regles.md`
- [ ] Kevin choisit une image cible (`docs/captures/cible/cible_1..3.jpg`, générées avec Kling / Nano Banana Pro le 2026-11-01) pour la refonte visuelle poussée : vraies textures et modèles 3D, figures illustrées, adversaires assis, interface Art déco
- [ ] Kevin teste le décor « Dernier Métro » sur téléphone (vérifié sur Mac et sur le Galaxy A17, cartes Art déco comprises). Le S24 n'est plus disponible : l'A17 se partage entre sessions, à demander au coordinateur (session kevinyankam-91)
- [x] Cartes accordées au métro (Art déco) : dos bleu nuit « CHECK », figures en personnel de gare, jokers saltimbanques
- [ ] Passe QA complète sur téléphone (partie entière à 4 et à 10, Valet, Check oublié, glisser vers le tas, haptique, mesure des fps)
- [ ] Expert : tenir compte des couleurs qu'un adversaire n'a pas (quand il pioche) pour qu'il batte Difficile
- [ ] iOS : module iOS + build Xcode + simulateur
- [ ] Idées : jeu en ligne entre amis, statistiques, thèmes de table, voix « Check ! » / « Games ! »

## Points ouverts (boucle de jeu et interface)

- Passe QA sur téléphone : faite en partie sur le S24 (voir ci-dessus) ; haptique, encoches et bouton retour non vérifiés au doigt.
- As : on ne se saute jamais soi-même (à deux, un ou deux As = on rejoue), corrigé dans le moteur.
- Glisser une carte vers le tas : implémenté, pas encore essayé au doigt.
- La reprise rejoue le « film » des actions (graine + actions) : une sauvegarde faite avec une version différente des règles est effacée si elle ne se rejoue pas.

## Pièges connus (hérités de KevChess)

- URP 17 supprimait toutes les variantes de `UberPost` sur Android (écran noir) : « Strip Unused Post Processing Variants » est désactivé dans `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (déjà fait ici).
- Mac : seul le module Mono est installé (pas IL2CPP).
- Ne jamais ouvrir deux éditeurs Unity sur le même dossier de projet (chaque worktree a le sien).
- **Android, écran noir derrière l'UI** : la liste `m_List` des ressources URP de `UniversalRenderPipelineGlobalSettings.asset` doit rester remplie et commitée (Unity la remplit au build ; la vider retire `UberPost` du build). Ne pas annuler ce fichier après un build.
- **Android, symboles ♠ ♥ ♦ ♣ invisibles** : les polices Android ne les ont pas → police `Game/UI/Resources/CheckSuits.ttf` (Noto Sans Symbols 2, OFL) utilisée sur mobile, lettres en repli sur la police système (réglé dans son .meta).
- Unity modifie tout seul les gabarits `.mat` de `Visuals/Resources/NeonTemplates` à chaque build : les restaurer (`git checkout`).
- Un build Mac fait juste après un build Android peut donner un joueur cassé (exceptions SSAO / Blitter) : refaire le build Mac.
