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

- `Models/` : `Project` et `CodeFile`
- `Services/` : opérations sur les fichiers et chargement de projets
- `ViewModels/` : état et actions du projet courant
- `Views/ExplorerView.cs` : explorateur de projet Windows Forms

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
