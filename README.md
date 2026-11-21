# KEYCE-CODE

Éditeur de code de bureau développé en C# et Windows Forms pour le projet collaboratif KEYCE-CODE.

## Prérequis

- Windows 10 ou 11
- SDK .NET 10

## Lancer l’application

Depuis Git Bash ou PowerShell, dans le dossier du projet :

```bash
dotnet run --project KeyceCode.csproj
```

## Gestion des fichiers et projets

- Créer un fichier vierge ou un fichier depuis l’explorateur
- Ouvrir un fichier existant et éviter d’ouvrir deux fois le même chemin dans des onglets différents
- Enregistrer ou utiliser « Enregistrer sous »
- Détecter les modifications non enregistrées et demander confirmation avant fermeture
- Ouvrir une solution ou un projet C# (`.sln`, `.slnx`, `.csproj`) et parcourir les dossiers/fichiers
- Créer, renommer et supprimer des fichiers et dossiers depuis le menu contextuel de l’explorateur
- Afficher les erreurs d’accès ou de chemin dans l’interface

## Organisation du code

- `Models/` : `Project`, `CodeFile`, `EditorTab`, `Diagnostic` (analyse), `BuildDiagnostic` et `ConsoleBuffer` (console)
- `Services/` :
  - fichiers et projets : `FileService`, `ProjectService`
  - éditeur : `SyntaxHighlightingService`
  - analyse : `CodeAnalysisService` (Roslyn)
  - compilation et exécution : `CompilerService`, `ProcessSession`, `TerminalService`
- `ViewModels/` : `ProjectViewModel`, `EditorViewModel`, `OutputViewModel`, `TerminalViewModel`, `ViewModelBase`
- `Views/` : `ExplorerView` (Windows Forms), `EditorView` / `CodeEditorSurface` (AvalonEdit),
  `OutputView` et `TerminalView` (WPF hébergés dans la fenêtre Windows Forms)
- `Resources/` : coloration syntaxique C# (`Syntax/`) et styles de la console (`ConsoleTemplates.xaml`)

## Éditeur de code

La zone d’édition utilise AvalonEdit, hébergé dans Windows Forms grâce à
l’interopérabilité WPF. Cela permet de conserver la fenêtre actuelle du projet
tout en disposant d’un vrai moteur d’édition WPF extensible.

La partie éditeur comprend :

- coloration syntaxique C# ;
- numéros de lignes ;
- onglets et indicateur de modification ;
- annuler et rétablir ;
- indentation automatique ;
- fermeture automatique des parenthèses, crochets, accolades et guillemets ;
- mini-carte de navigation à droite de l’éditeur ;
- modèles `EditorTab` et `EditorViewModel` prêts pour les futurs services.

Les tests de gestion des onglets se trouvent dans `KeyceCode.Tests` et se
lancent avec :

```bash
dotnet test KeyceCode.Tests/KeyceCode.Tests.csproj
```

Le cahier des charges impose Windows Forms ; l’explorateur est donc un contrôle C# et non une vue WPF/XAML.

## Analyse du code

Le bouton « 🔍 Analyser » (menu Exécuter) utilise Roslyn pour détecter les erreurs et avertissements C# du
fichier ouvert. Les résultats s’affichent dans l’onglet **Analyse** ; un double-clic sur un problème place le
curseur à la ligne et à la colonne concernées.

## Compilation, exécution, console et terminal

Le panneau du bas contient trois onglets : **Console**, **Terminal** et **Analyse**.

- **Compiler** (`Ctrl+Maj+B`) : `dotnet build` sur le projet ouvert (ou sur le fichier `.cs` courant).
- **Exécuter** (`F5`) : `dotnet run`. Les fichiers `.py`, `.js`, `.ps1`, `.bat` et `.cmd` sont lancés dans la même console.
- **Arrêter** (`Maj+F5`) : arrête le programme (et tous ses sous-processus) ou la commande en cours.
- Les fichiers modifiés sont enregistrés automatiquement avant chaque compilation ou exécution.
- La console affiche en direct la sortie et les erreurs ; la zone « Saisie » envoie du texte au programme
  (`Console.ReadLine`, etc.).
- Les erreurs de compilation sont listées sous la console : un double-clic ouvre le fichier à la bonne ligne.
- Le **terminal** exécute des commandes via `cmd.exe` dans le dossier du projet (`cd`, `cls`, changement de lecteur et
  historique ↑/↓ pris en charge).

La cible compilée est le projet ouvert (`.csproj` ou dossier) ; sans projet ouvert, c’est le fichier `.cs` courant
(avec son `.csproj` s’il se trouve dans le même dossier). Le SDK .NET doit être dans le `PATH`.
