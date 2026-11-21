# KEYCE-CODE

Éditeur de code de bureau Windows développé en C# pour le projet collaboratif
KEYCE-CODE. La fenêtre principale est Windows Forms ; les vues qui nécessitent
WPF (AvalonEdit, terminal, compilation et assistant) sont hébergées avec
`ElementHost`.

## Prérequis

- Windows 10 ou 11
- SDK .NET 10
- Pour le débogage : Visual Studio avec `vsdbg.exe` (ou le chemin défini dans
  `KEYCE_VSDBG_PATH`)

## Fonctionnalités disponibles

### Gestion des fichiers et projets

- créer, ouvrir, enregistrer et fermer des fichiers avec confirmation des
  modifications non enregistrées ;
- ouvrir une solution ou un projet C# (`.sln`, `.slnx`, `.csproj`) ;
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
- formatage C# avec Roslyn ;
- analyse en direct et diagnostics avec ligne, colonne et code d’erreur ;
- thèmes clair/sombre, retour à la ligne, taille de police et tabulations.

### Exécution et outils de développement

- compilation et exécution de projets C# avec le SDK .NET ;
- exécution de fichiers Python, JavaScript, PHP, Ruby, Perl, Lua, Go, Java,
  PowerShell, batch et shell lorsque l’outil correspondant est installé ;
- console de sortie avec diagnostics cliquables ;
- terminal intégré avec historique, saisie standard et arrêt de commande ;
- lancement des tests du projet ;
- points d’arrêt, lancement/arrêt du débogage, continuation et pas-à-pas avec
  `vsdbg.exe` ;
- affichage des frames et variables locales lors d’un arrêt.

### Git et assistant IA

- affichage de la branche et des fichiers modifiés ;
- stage, commit, pull et push depuis le menu Git ;
- conversation locale avec Ollama, configuration de l’endpoint et du modèle ;
- propositions de modifications de l’agent IA avec aperçu et confirmation ;
- restauration de la dernière modification proposée par l’agent.

## Organisation du code

- `Form1.cs` : fenêtre principale, menus, barre d’outils et orchestration ;
- `Views/CodeEditorSurface.cs` : surface AvalonEdit ;
- `Views/ExplorerView.cs` : explorateur de projet Windows Forms ;
- `Views/*.xaml` : vues WPF hébergées dans les onglets inférieurs ;
- `Services/` : fichiers, compilation, exécution, analyse, Git, IA, terminal,
  tests et débogage ;
- `Models/` : état métier et résultats structurés ;
- `KeyceCode.Tests/` : tests unitaires et tests de services.

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

Le test du débogueur nécessite un projet C# compilable et `vsdbg.exe`. Si
Visual Studio est installé dans un autre emplacement, définir
`KEYCE_VSDBG_PATH` vers l’exécutable avant de lancer KEYCE-CODE.

Le cahier des charges impose Windows ; l’application cible donc
`net10.0-windows` et utilise Windows Forms comme hôte principal, tout en
conservant WPF pour les composants qui en tirent parti.
