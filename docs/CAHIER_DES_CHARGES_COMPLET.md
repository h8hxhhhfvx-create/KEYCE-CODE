# KEYCE CODE — Cahier des charges produit

Version 1.0 — document de référence avant implémentation

## 1. Vision et périmètre

KEYCE CODE est un environnement de développement Windows destiné à devenir l’éditeur quotidien de l’équipe. Il reprend les principes de VS Code et de Visual Studio, puis ajoute une expérience KEYCE : apprentissage guidé, mode examen, aide IA contrôlée et exécution simplifiée.

Le produit cible Windows 10/11, .NET, Windows Forms comme hôte et WPF pour les vues adaptées. Il doit éditer fichiers, dossiers, workspaces, projets et solutions, avec une plateforme extensible pour les langages.

« Tous les langages » signifie : ouverture et texte pour tous, puis coloration, intelligence, exécution, tests, preview et débogage selon les fournisseurs et outils disponibles. KEYCE CODE ne fournit pas tous les compilateurs : il détecte ce qui est installé, indique ce qui manque et permet de le configurer.

## 2. Fonctionnalités attendues

- Workbench : menus, barre d’activité, sidebar, éditeur central, panneau inférieur, status bar, palette, recherche rapide, navigation et layouts redimensionnables.
- Documents : nouveau/ouverture fichier, dossier, workspace, solution et projet ; onglets multiples, aperçu, épinglage, indicateur non enregistré, groupes et restauration de session.
- Fermeture sûre : X, clic milieu, Ctrl+W, fermer les autres/à droite/enregistrés/tout et confirmation avant perte.
- Éditeur : AvalonEdit, lignes, minimap, pliage, indentation, paires, commentaires, sélection multiple, recherche/remplacement, snippets, complétion, symboles, définition, références, renommage et formatage.
- Diagnostics : soulignement ondulé, marqueur de gouttière, hover, Problems avec filtre/tri et navigation ligne/colonne.
- Langages V1 : C#, Python, JavaScript/TypeScript, HTML/CSS, PHP, Java, C/C++, Go, Rust, Ruby, Lua, PowerShell, Bash, SQL, JSON, YAML, XML et Markdown.
- Exécution : fournisseurs par langage, détection des SDK, build/run/test/debug, tâches, variables et messages d’outil manquant. HTML/CSS et Markdown disposent d’une preview.
- Terminal : vrai terminal ConPTY, PowerShell/cmd, stdin interactif, ANSI, historique, Ctrl+C, programmes interactifs, sessions multiples et redimensionnement.
- Git : statut, diff, stage, commit, branches, stash, merge/rebase, pull/push et historique ; GitHub facultatif par navigateur/device OAuth, secrets dans Credential Manager/DPAPI.
- Debug : DAP, `vsdbg.exe` pour C#, points d’arrêt, pause, continuation, pas à pas, pile, variables et exceptions. Run reste fonctionnel sans adaptateur.
- IA : deux clés Gemini, deux clés Groq, Ollama optionnel, rotation/cooldown/timeout, secrets protégés, aperçu diff, confirmation, restauration et confinement au workspace.
- Pédagogie : mode apprentissage, indices, explications, progression et mode examen configurable.

## 3. Identité visuelle

Les captures de `images/` sont des références de structure et de comportement : activité verticale, menus en haut, explorateur gauche, onglets au centre, panneau inférieur et status bar. KEYCE CODE reprend cette logique mais possède son propre logo, ses icônes et sa palette.

Le design system doit définir couleurs clair/sombre, états hover/focus/disabled/error, typographie, tailles, espacements, contrastes, densité, DPI et redimensionnement. Aucun écran n’est terminé sans test en petite et grande résolution.

## 4. Premier démarrage et connexions

Au premier lancement, KEYCE CODE affiche un assistant court : choisir le thème, confirmer le dossier de travail, vérifier les outils disponibles, puis proposer sans imposer GitHub et les fournisseurs IA. L’utilisateur peut passer chaque étape et la retrouver dans Paramètres.

Git local est disponible sans connexion. Le bouton « Se connecter à GitHub » ouvre le navigateur vers le flux officiel, avec les permissions demandées clairement affichées. Pour une application desktop, le flux retenu devra utiliser Authorization Code + PKCE avec callback local contrôlé, ou Device Flow si le callback local n’est pas retenu. Le Device Flow exige l’affichage d’un code, un polling respectant l’intervalle et la gestion de l’expiration, de l’annulation et du ralentissement.

Après autorisation, KEYCE récupère l’identité minimale, vérifie le compte, stocke les jetons dans le coffre Windows, puis affiche le compte connecté et les dépôts accessibles. Aucun jeton ne va dans les paramètres JSON, les logs, les captures ou Git. L’utilisateur peut choisir les dépôts/organisations autorisés, se déconnecter, révoquer la session et reconnecter un autre compte. Une erreur réseau ne doit pas rendre l’éditeur inutilisable.

Les clés Gemini et Groq suivent un parcours indépendant : l’utilisateur saisit chaque clé dans Paramètres, KEYCE teste la connexion sans afficher la valeur, indique l’ordre des quatre clés, permet de désactiver une clé et explique quel fournisseur a répondu. Une clé invalide est isolée sans effacer les autres.

## 5. Architecture cible

`Workbench`, `CommandSystem`, `DocumentSystem`, `LanguagePlatform`, `ExecutionPlatform`, `DiagnosticsPlatform`, `SourceControl`, `DebugPlatform`, `ExtensionPlatform`, `AiPlatform`, `Settings` et `Security` sont des modules séparés. Les services communiquent par interfaces et événements typés ; l’interface ne lance pas directement les processus et ne manipule pas les secrets.

## 6. Sécurité et définition de terminé

Valider les chemins, confiner les actions IA, ne jamais journaliser les secrets, annuler les processus, libérer les ressources et écrire atomiquement. Tester permissions, fichiers verrouillés, encodages, gros fichiers et interruptions.

Une fonctionnalité est terminée si son code est intégré, ses tests passent, son parcours manuel est validé, ses erreurs sont traitées, sa documentation est à jour et aucune régression n’est connue.

## 7. Références

- [Organisation du code source VS Code](https://github.com/microsoft/vscode/wiki/source-code-organization)
- [Dépôt officiel VS Code](https://github.com/microsoft/vscode)
- [Contrôle de code source VS Code](https://code.visualstudio.com/docs/sourcecontrol/overview)
- [Éditeur et terminal intégré VS Code](https://code.visualstudio.com/docs/editing/getting-started/overview)
