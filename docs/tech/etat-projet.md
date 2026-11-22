# Check — état du projet

Jeu de cartes camerounais « Check » en 3D, pour Android et iOS (macOS pour tester). Décor « Dernier Métro » (wagon rétro aménagé en salon qui traverse une ville la nuit) : wagon peint en fond, adversaires illustrés assis autour de la table 3D.

Dernière mise à jour : 2026-11-22.

Dépôt : https://github.com/YANKAMKEVIN/check (privé), branche `main` suivie sur `origin`.

## Décisions prises (avec Kevin)

| Sujet | Décision |
|---|---|
| Jeu | Check, version de Kevin : voir [regles.md](../regles.md) (toutes les règles validées par Kevin le 21/11 ; chacune reste un réglage de `RuleSet`) |
| Pioche | Règle de Kevin du 21/11 : « Si tu pioches, c'est le tour de la personne qui suit. » Piocher termine le tour, la carte piochée ne se pose pas tout de suite (`RuleSet.MayPlayDrawnCard` = faux ; l'ancienne variante reste disponible et testée) |
| Moteur | Unity 6000.6.3f1 + URP 17 + Input System, configuration reprise de `~/dev/kevchess` |
| Plateforme | Mobile d'abord, écran en paysage (jusqu'à 10 joueurs autour de la table) |
| Joueurs | 2 à 10 : le joueur humain + des IA (4 niveaux) |
| DA | « Dernier Métro » (choisi par Kevin le 2026-10-31 parmi 6 univers proposés par Codex ; il ne voulait plus de thème Cameroun ni des tours néon) : wagon, banquettes, laiton, ville de nuit qui défile ; palette pétrole #173F46, ambre #D99B59, nuit #243047, crème #F3E5CE. Captures : `docs/captures/metro/` |
| Carte du centre | Pioche et tas agrandis (`TableView.CenterCardScale` = 1,7), plateau mat et uni, rotation du tas réduite : demande de Kevin (« on a du mal à voir la carte au centre ») |
| Identifiant | `com.kevinyankam.check` |

## Architecture

```
Assets/Check/
  Core/    règles pures (C#, sans UnityEngine) : Card, RuleSet, TurnAction, GameEvent, Round (un tour), Match (tours + éliminations),
           SaveReplay (film de sauvegarde : codage des actions, version, rejeu vérifié)
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

180 tests verts (~20 s à 1 min 30 selon la charge) : moteur (chaque carte spéciale, cumuls et contres, poses multiples, pioche et remélange, Check / Games, éliminations, parties aléatoires de 2 à 10 joueurs, indices publics sur les mains, film de sauvegarde) et IA (légalité, budget de temps, pas de triche, tournois : Moyen bat Facile, Difficile bat Moyen, Expert bat Difficile). La CI GitHub (`.github/workflows/tests.yml`) lance la même commande avec `dotnet test tests/Check.Tests/Check.Tests.csproj`.

Mesure longue de l'Expert, hors CI : `dotnet test tests/Check.Tests/Check.Tests.csproj --filter Name=Measure_ExpertAgainstHard`.

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

- [x] Kevin a validé les règles « à valider » de `docs/regles.md` (21/11) et changé la pioche : piocher termine le tour (branche `claude/regle-pioche`).
  Moteur : `ApplyDraw` passe directement au suivant, sans événement « passe » ; plus jamais de « Passer » proposé. IA, interface et autotest suivent le moteur
  (leurs branches « après une pioche » ne servent plus qu'à la variante). Fiche « À ton tour » mise à jour.
  Indices de l'Expert (`Round.MissingCards`) : piocher reste un signal (on pioche quand on ne peut ou ne veut pas jouer), l'indice couvre
  les cartes d'avant la pioche ; la carte piochée, cachée, reste libre (avant, la passe automatique montrait qu'elle ne se posait pas ;
  désormais ce n'est plus visible, l'étendre aurait fait lire à l'IA une information secrète). Si le joueur pose ensuite une carte exclue,
  c'était la carte piochée : toute sa main redevient indicée. Mesure longue après le changement : Expert 53,9 % contre Difficile
  sur 1 000 parties (51,4 % sans indices), contre 56,1 % / 56,7 % avant ; le tournoi de la CI (> 54 % sur 400 parties) passe.
  Les sauvegardes faites avant le changement ne se rejouent plus : elles sont effacées à la reprise (voir « Points ouverts »).
- [x] Image cible choisie par Kevin : `docs/captures/cible/cible_3.jpg` (vue dans l'axe du wagon, enseignes néon, adversaires assis)
- [x] Refonte « hybride » choisie par Kevin (2026-11-02) et faite : décor peint par défaut, 9 personnages illustrés, réactions, 2 à 10 joueurs ; recette Mac OK (captures `docs/captures/peint/`). Le prototype « tout 3D » a été écarté (visible dans l'historique, commit 0567c7a)
- [ ] Vérifier le décor peint sur téléphone (pas encore fait : le coordinateur de l'A17, session kevinyankam-91, n'existe plus ; demander à Kevin qui attribue l'A17)
- [ ] Plus de portraits masculins (4 seulement : à 9 adversaires, des voyageuses remplacent des hommes) ; le grand-père a été refusé par le filtre du générateur. **Pour Kevin** : la génération (Kling) et le détourage (script Python) ne sont pas versionnés dans le dépôt. Pour chaque nouveau personnage : `Resources/Painted/perso_N.png` (fond détouré, RVB prémultiplié par l'alpha, hauteur 1024, même cadrage que perso_8/9), une entrée `Portrait(N, false, regard, mains, hauteur)` dans `PaintedScene.Cast` et son cadrage de visage dans `FaceUv` (`PortraitCount` suit). Idéalement 4 hommes de plus (5 + 9 = parité à 9 adversaires) et des prénoms masculins dans `OpponentNames`
- [ ] Kevin teste le décor « Dernier Métro » sur téléphone (vérifié sur Mac et sur le Galaxy A17, cartes Art déco comprises). Le S24 n'est plus disponible : l'A17 se partage entre sessions, à demander au coordinateur (session kevinyankam-91)
- [x] Cartes accordées au métro (Art déco) : dos bleu nuit « CHECK », figures en personnel de gare, jokers saltimbanques
- [ ] Passe QA complète sur téléphone (partie entière à 4 et à 10, Valet, Check oublié, glisser vers le tas, haptique, mesure des fps)
- [x] Expert : tient compte des couleurs qu'un adversaire n'a pas (quand il pioche) — 2026-11-21, branche `claude/suite`.
  `Round.MissingCards` / `MissingCardsHeld` / `IsKnownToLack` : quand un joueur pioche sans pénalité, on note les cartes
  qu'il n'avait pas (tout ce qui se posait), pour les cartes de sa main à ce moment-là (toute la main si la carte piochée
  ne se pose pas non plus : passe automatique, visible de tous). L'indice s'efface s'il pose ensuite une carte exclue
  (il avait pioché exprès). L'Expert (`AISettings.UseHandClues`) tire ses mondes simulés en respectant ces indices et
  ses heuristiques favorisent le dessus que le suivant ne peut pas couvrir (`Heuristics.BlockedShare`).
  Mesure à deux, donnes jouées deux fois en échangeant les places, simulations dans le rapport du jeu (×3) : en CI,
  Expert 62 % de victoires sur 400 parties (graine 31) ; mesure longue, 1 000 parties : 56,1 % avec indices (réglage final ; 56 à 58 % selon les variantes essayées),
  56,7 % sans. Expert bat donc Difficile, surtout grâce à son budget : l'apport propre des indices reste dans le bruit (écart type ≈ 1,6 point). Piste : modéliser aussi le 7 et le joker gardés en main.
- [ ] iOS : module iOS + build Xcode + simulateur
- [ ] Idées : jeu en ligne entre amis (backend à choisir), thèmes de table (pas encore de mécanisme : `DecorStyle` ne choisit que le décor peint ou classique), voix « Check ! » / « Games ! » (enregistrements audio à fournir)
- [x] Statistiques : carnet du joueur fait (`Core/Ux/PlayerStats`, `StatsStore` en PlayerPrefs, écran « Mon carnet » `HudStats`, tests dans `UxTests`)

## Points ouverts (boucle de jeu et interface)

- Passe QA sur téléphone : faite en partie sur le S24 (voir ci-dessus) ; haptique, encoches et bouton retour non vérifiés au doigt.
- As : on ne se saute jamais soi-même (à deux, un ou deux As = on rejoue), corrigé dans le moteur.
- Glisser une carte vers le tas : implémenté, pas encore essayé au doigt.
- La reprise rejoue le « film » des actions (graine + actions) avec `Core/SaveReplay.TryReplay` (testé hors Unity) : chaque coup est vérifié avant
  d'être joué, et une sauvegarde d'une autre version (`SavedGame.Format` ≠ `SaveReplay.CurrentFormat`), illisible ou illégale est effacée
  avec une raison dans le journal ; l'accueil affiche « Ta partie en cours n'a pas pu être reprise après la mise à jour » (au lieu de
  « Aucune partie à reprendre »). Les sauvegardes d'avant ce champ (format 0, avant la règle de la pioche du 21/11) sont toutes refusées.
  **Règle** : incrémenter `SaveReplay.CurrentFormat` à chaque changement de `Round.Apply`, du tirage ou des règles par défaut.

## Pièges connus (hérités de KevChess)

- URP 17 supprimait toutes les variantes de `UberPost` sur Android (écran noir) : « Strip Unused Post Processing Variants » est désactivé dans `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (déjà fait ici).
- Mac : seul le module Mono est installé (pas IL2CPP).
- Ne jamais ouvrir deux éditeurs Unity sur le même dossier de projet (chaque worktree a le sien).
- **Android, écran noir derrière l'UI** : la liste `m_List` des ressources URP de `UniversalRenderPipelineGlobalSettings.asset` doit rester remplie et commitée (Unity la remplit au build ; la vider retire `UberPost` du build). Ne pas annuler ce fichier après un build.
- **Android, symboles ♠ ♥ ♦ ♣ invisibles** : les polices Android ne les ont pas → police `Game/UI/Resources/CheckSuits.ttf` (Noto Sans Symbols 2, OFL) utilisée sur mobile, lettres en repli sur la police système (réglé dans son .meta).
- Unity modifie tout seul les gabarits `.mat` de `Visuals/Resources/NeonTemplates` à chaque build : les restaurer (`git checkout`).
- Un build Mac fait juste après un build Android peut donner un joueur cassé (exceptions SSAO / Blitter) : refaire le build Mac.
