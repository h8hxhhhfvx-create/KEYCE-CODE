# KEYCE CODE — Todo professionnel de réalisation

## Méthode

Réaliser une tranche courte, la tester, la corriger puis la valider. Statuts : `À faire`, `Prêt`, `En cours`, `Bloqué`, `À vérifier`, `Validé`. Pour chaque ID, noter dépendances, fichiers, commit, tests, preuve et résultat.

## Phase 0 — Cadrage

- [ ] P0-01 Relire cahier, décisions et contraintes.
- [ ] P0-02 Figer produit et limite « tous les langages ».
- [ ] P0-03 Inventorier fonctions et tests actuels.
- [ ] P0-04 Transformer chaque capture de `images/` en exigences.
- [ ] P0-05 Valider logo, palette, icônes et thèmes.
- [ ] P0-06 Choisir langages V1 et outils requis.
- [ ] P0-07 Définir le premier lancement et les outils absents.
- [ ] P0-08 Choisir GitHub PKCE/callback local ou Device Flow.
- [ ] P0-09 Définir permissions, révocation, changement de compte et stockage des jetons.
- [ ] P0-10 Définir onboarding Gemini/Groq, relais, limites et confidentialité.
- [ ] P0-11 Relire et valider toutes les simulations de parcours.

## Phase 1 — Socle

- [ ] P1-01 Contrats `ICommand`, `IWorkspace`, `IDocument`, `ILanguageProvider`.
- [ ] P1-02 Événements applicatifs et annulation.
- [ ] P1-03 Séparer orchestration de `Form1` et services.
- [ ] P1-04 Logs structurés et gestion globale des exceptions.
- [ ] P1-05 Tests démarrage, fermeture, configuration et récupération.

## Phase 2 — Workbench

- [ ] P2-01 Activité, sidebar, éditeur, panneau inférieur et status bar.
- [ ] P2-02 Menus et palette depuis un registre commun.
- [ ] P2-03 Layout, redimensionnement, masquage et restauration.
- [ ] P2-04 Logo, icônes, typographie, espacements et états KEYCE.
- [ ] P2-05 Thèmes clair/sombre et contraste.
- [ ] P2-06 Tests résolutions, DPI et clavier seul.

## Phase 3 — Documents

- [ ] P3-01 Nouveau/ouvrir fichier, dossier, workspace, solution, projet.
- [ ] P3-02 Activation, aperçu, épinglage et groupes d’onglets.
- [x] P3-03 Fermeture X, clic milieu, Ctrl+W et menu contextuel.
- [x] P3-04 Confirmation pour fichiers modifiés.
- [ ] P3-05 Auto-save, encodages, fins de ligne, sauvegarde atomique.
- [ ] P3-06 Session, projets récents et session corrompue.
- [ ] P3-07 Synchronisation explorateur/onglets sans écrasement.

## Phase 4 — Éditeur

- [ ] P4-01 AvalonEdit derrière abstraction.
- [ ] P4-02 Coloration et mode de langage V1.
- [ ] P4-03 Indentation, paires, commentaires, pliage, sélection multiple.
- [ ] P4-04 Recherche/remplacement avec regex, casse et mot entier.
- [ ] P4-05 Complétion, snippets, symboles, définition, références, renommage.
- [ ] P4-06 Formatage par fournisseur et à l’enregistrement.
- [ ] P4-07 Gros fichiers, binaires, encodages invalides, lecture seule.

## Phase 5 — Diagnostics

- [x] P5-01 Modèle gravité/code/message/fichier/ligne/colonne.
- [ ] P5-02 Analyse/build vers soulignements et gouttière (soulignements réalisés ; gouttière à compléter).
- [ ] P5-03 Problems avec filtre et tri.
- [ ] P5-04 Hover et navigation précise.
- [ ] P5-05 Diagnostics obsolètes et annulation.
- [ ] P5-06 Tests erreurs, avertissements, externes et fichiers fermés.

## Phase 6 — Langages

- [ ] P6-01 Registre fournisseurs et détection outils.
- [ ] P6-02 Profils build/run/test/debug par workspace.
- [ ] P6-03 C# projets, solutions, SDK, build, run.
- [ ] P6-04 Python, JS/TS, PHP, Java, Go, Rust, C/C++ selon installation.
- [ ] P6-05 Preview HTML/CSS et Markdown.
- [ ] P6-06 Outil manquant, version et variables.
- [ ] P6-07 Tests succès, erreur, stdin, code retour, arrêt.

## Phase 7 — Terminal

- [ ] P7-01 Couche ConPTY compatible licence.
- [ ] P7-02 Profils PowerShell/cmd, sessions et dossier courant.
- [ ] P7-03 ANSI, couleurs, redimensionnement, historique, copier/coller.
- [ ] P7-04 Ctrl+C, programmes interactifs et arrêt forcé.
- [ ] P7-05 Tâches, variables, dépendances et sorties cliquables.
- [ ] P7-06 Tests dotnet, Python interactif, commande longue et erreur.

## Phase 8 — Git

- [ ] P8-01 Statut, diff, stage, commit, branche, historique.
- [ ] P8-02 Pull, push, fetch, stash, merge/rebase.
- [ ] P8-03 Vue Source Control et décorations.
- [ ] P8-04 OAuth GitHub facultatif, consentement et déconnexion.
- [ ] P8-05 Credential Manager/DPAPI et révocation.
- [ ] P8-06 Tests sans compte, avec compte, réseau absent, conflit.

## Phase 9 — Debug et tests

- [ ] P9-01 DAP, logs et annulation.
- [ ] P9-02 `vsdbg.exe`, configuration et message d’installation.
- [ ] P9-03 Breakpoints, pause, continuation, pas à pas, pile, variables.
- [ ] P9-04 Test runner avec résultats cliquables.
- [ ] P9-05 Configurations par langage/projet.
- [ ] P9-06 Adaptateur absent, crash et arrêt utilisateur.

## Phase 10 — Extensions

- [ ] P10-01 Manifeste, version, capacités, permissions.
- [ ] P10-02 Processus isolé et récupération après crash.
- [ ] P10-03 Contributions commandes, langages, thèmes, vues, tâches.
- [ ] P10-04 Installer, désactiver, mettre à jour, supprimer.
- [ ] P10-05 Tester extension mal formée, lente, interdite, incompatible.

## Phase 11 — IA

- [ ] P11-01 Interface `IAiProvider` et adaptateurs.
- [ ] P11-02 Deux clés Gemini et deux Groq : priorité, rotation, cooldown.
- [ ] P11-03 Modèles, timeout, annulation, hors ligne.
- [ ] P11-04 Secrets protégés et absents des logs/Git.
- [ ] P11-05 Chat contextualisé et aperçu diff.
- [ ] P11-06 Application transactionnelle, restauration, confinement.
- [ ] P11-07 Faux fournisseurs et tests de fuite.

## Phase 12 — Pédagogie et accessibilité

- [ ] P12-01 Apprentissage, indices, explications, progression.
- [ ] P12-02 Politique d’examen configurable.
- [ ] P12-03 Blocage vérifié terminal/IA/extensions/presse-papiers.
- [ ] P12-04 Clavier, contrastes, tailles, messages accessibles.
- [ ] P12-05 Tests de contournement et journal examen.

## Phase 13 — Livraison

- [ ] P13-01 Tests unitaires, intégration et UI sur machine propre.
- [ ] P13-02 Régression après chaque lot.
- [ ] P13-03 Performance démarrage/mémoire/gros fichier/indexation.
- [ ] P13-04 Sécurité et licences.
- [ ] P13-05 Documentation installation SDK et dépannage.
- [ ] P13-06 Publication Windows, version, changelog, retour arrière.
- [ ] P13-07 Recette complète avec au moins 95 % des critères.

## Fiche obligatoire

```text
ID / statut / dépendances :
Fichiers touchés / commit :
Test automatisé :
Test manuel :
Résultat attendu / obtenu :
Preuve (log ou capture) :
```
