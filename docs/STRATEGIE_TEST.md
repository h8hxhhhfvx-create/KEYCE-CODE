# KEYCE CODE — Stratégie de test

## Niveaux

1. Unitaire : modèles et services sans interface.
2. Intégration : services avec fichiers temporaires et faux outils.
3. UI manuel : clics, raccourcis, redimensionnement, thèmes et messages.
4. Système : vrais SDK et interpréteurs Windows.
5. Régression : build Release et tous les tests après chaque lot.

## Commandes

```powershell
dotnet restore
dotnet build -c Release -p:UseSharedCompilation=false
dotnet test KeyceCode.Tests/KeyceCode.Tests.csproj -c Release --no-restore -p:UseSharedCompilation=false
dotnet run --project KeyceCode.csproj
git diff --check
```

## Recette minimale

- Démarrer et fermer sans exception.
- Créer, modifier, enregistrer et fermer avec confirmation.
- Ouvrir plusieurs fichiers et fermer par X, clic milieu, Ctrl+W.
- Restaurer une session.
- Introduire une erreur C# : soulignement, gouttière, Problems, navigation.
- Compiler correctement puis avec erreur.
- Utiliser une commande interactive dans le vrai terminal et l’arrêter.
- Vérifier Git sans compte puis GitHub facultatif.
- Vérifier IA avec faux fournisseurs, clé invalide et bascule.
- Tester thèmes, DPI, clavier et résolution réduite.

## Sortie d’un lot

Build Release réussi, tests automatisés réussis, parcours manuel documenté, aucune régression bloquante et comportement compréhensible en cas de dépendance absente.
