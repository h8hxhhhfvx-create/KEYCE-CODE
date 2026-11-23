# KEYCE-CODE

Éditeur de code de bureau Windows développé en C# pour le projet collaboratif
KEYCE-CODE. La fenêtre principale est Windows Forms ; les vues qui nécessitent
WPF (AvalonEdit, terminal, compilation et assistant) sont hébergées avec
`ElementHost`.

## Prérequis

- Windows 10 ou 11
- SDK .NET 10
- Pour le débogage : Visual Studio avec `vsdbg.exe` (ou le chemin défini dans
  `KEYCE_VSDBG_PATH`, ou directement dans les paramètres KEYCE CODE)

## Fonctionnalités disponibles

### Gestion des fichiers et projets

- créer des projets C# de démarrage, ouvrir, enregistrer et fermer des fichiers avec confirmation des
  modifications non enregistrées ;
- enregistrer tous les onglets ou les fermer en une seule commande avec
  confirmation des modifications ;
- ouvrir une solution ou un projet C# (`.sln`, `.slnx`, `.csproj`) ;
- retrouver les dix derniers projets dans le menu « Projets récents » ;
- restaurer au démarrage le dernier dossier et les fichiers encore existants ;
- parcourir les dossiers et fichiers dans l’explorateur ;
- créer, renommer et supprimer des fichiers et dossiers ;
- afficher les erreurs d’accès ou de chemin dans l’interface.

### Éditeur

- coloration syntaxique C# et coloration intégrée pour plusieurs extensions ;
- numéros de lignes, onglets, indicateur de modification, annuler et rétablir ;
- indentation automatique et fermeture des parenthèses, crochets, accolades et
  guillemets ;
- mini-carte de navigation ;
- auto-complétion C# (`Ctrl+Espace`) ;
- recherche, remplacement, navigation vers une ligne et recherche dans les
  fichiers ;
- remplacement global écrit les fichiers de façon atomique et resynchronise les
  onglets ouverts sans écraser les modifications locales ;
- navigation vers les symboles (`Ctrl+Maj+O`), vers la définition (`F12`) et
  renommage contrôlé (`F2`) ;
- formatage C# avec Roslyn en respectant la taille de tabulation choisie ;
- analyse en direct et diagnostics avec ligne, colonne et code d’erreur ;
- thèmes clair/sombre, retour à la ligne, taille de police et tabulations.
- boîte de paramètres unifiée pour modifier ces options et la configuration
  Ollama sans quitter l’éditeur.

### Exécution et outils de développement

- compilation et exécution de projets C# avec le SDK .NET, y compris les solutions
  .sln et .slnx (l’exécution d’une solution choisit le premier projet
  exécutable trouvé) ;
- exécution de fichiers Python, JavaScript, PHP, Ruby, Perl, Lua, Go, Java,
  PowerShell, batch et shell lorsque l’outil correspondant est installé ;
- console de sortie avec diagnostics cliquables ;
- terminal intégré avec historique, saisie standard et arrêt de commande ;
- lancement des tests du projet ;
- points d’arrêt, lancement/arrêt du débogage, continuation et pas-à-pas avec
  `vsdbg.exe` ;
- affichage des frames et variables locales lors d’un arrêt.
- mode apprentissage et mode examen, avec protections adaptées pour un contexte
  pédagogique.

### Git et assistant IA

- affichage de la branche et des fichiers modifiés ;
- stage, commit, pull et push depuis le menu Git ;
- conversation locale avec Ollama, configuration de l’endpoint et du modèle ;
- propositions de modifications de l’agent IA avec aperçu et confirmation ;
- restauration de la dernière modification proposée par l’agent.
- application transactionnelle des plans IA, chemins confinés au projet et
  lecture du contexte sans suivre les jonctions ou liens symboliques ;
- palette de commandes (`Ctrl+Maj+P`) et système d’extensions DLL chargé depuis
  le dossier utilisateur `KeyceCode/Extensions`.

## Organisation du code

- `Form1.cs` : fenêtre principale, menus, barre d’outils et orchestration ;
- `Views/CodeEditorSurface.cs` : surface AvalonEdit ;
- `Views/ExplorerView.cs` : explorateur de projet Windows Forms ;
- `Views/*.xaml` : vues WPF hébergées dans les onglets inférieurs ;
- `Services/` : fichiers, compilation, exécution, analyse, Git, IA, terminal,
  tests et débogage ;
- `Models/` : état métier et résultats structurés ;
- `KeyceCode.Tests/` : tests unitaires et tests de services.

## Documents de pilotage

- `docs/CAHIER_DES_CHARGES_COMPLET.md` : vision, périmètre, fonctionnalités et architecture cible ;
- `docs/TODO_PROFESSIONNEL.md` : réalisation par lots, critères d’acceptation et validations ;
- `docs/STRATEGIE_TEST.md` : niveaux de test, commandes et recette manuelle.
- `docs/SIMULATIONS_PARCOURS.md` : scénarios de premier lancement, GitHub, Git, IA, terminal et erreurs.
- `outputs/KEYCE_CODE_TODO_PROFESSIONNEL.xlsx` : suivi opérationnel avec statuts, criticité, preuves et prérequis.

## Vérifier le projet

Depuis Git Bash ou PowerShell, dans le dossier du projet :

```bash
dotnet build -c Release -p:UseSharedCompilation=false
dotnet test KeyceCode.Tests/KeyceCode.Tests.csproj --no-restore \
  -p:UseSharedCompilation=false
```

Pour lancer l’application :

```bash
dotnet run --project KeyceCode.csproj
```

Le test du débogueur nécessite un projet C# compilable et un `vsdbg.exe` compatible avec l’hôte DAP. Le chemin peut être défini dans les paramètres, champ « Chemin vsdbg.exe », ou avec `KEYCE_VSDBG_PATH`. Certaines versions de `vsdbg` installées avec Visual Studio refusent les points d’arrêt lorsqu’elles sont pilotées hors de leur hôte officiel ; KEYCE CODE affiche alors l’erreur retournée par l’adaptateur. Si
Visual Studio est installé dans un autre emplacement, définir
`KEYCE_VSDBG_PATH` vers l’exécutable avant de lancer KEYCE-CODE.

Le cahier des charges impose Windows ; l’application cible donc
`net10.0-windows` et utilise Windows Forms comme hôte principal, tout en
conservant WPF pour les composants qui en tirent parti.
