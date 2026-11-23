# KEYCE CODE — Simulations de fonctionnement

Ces simulations ne sont pas encore du code. Elles décrivent ce que l’utilisateur doit voir et ce que le système doit faire. Elles serviront de scénarios de recette et permettront de détecter les oublis avant l’implémentation.

## Simulation 1 — Premier lancement sans compte

```text
Lancer KEYCE
  → écran de bienvenue KEYCE
  → choisir Clair ou Sombre
  → choisir « Ouvrir un dossier » ou « Commencer avec un fichier vide »
  → vérification des outils : .NET, Git, Python, Java, etc.
  → chaque outil est marqué Installé / Absent / Version à choisir
  → boutons « Configurer maintenant » et « Ignorer »
  → proposition « Se connecter à GitHub (facultatif) »
  → proposition « Configurer l’IA (facultatif) »
  → arrivée dans le workbench
```

Résultat attendu : l’utilisateur peut travailler immédiatement sans compte, sans clé IA et sans installer tous les langages.

## Simulation 2 — Ouvrir un projet C#

```text
Fichier → Ouvrir un projet ou une solution
  → choisir .sln, .slnx ou .csproj
  → KEYCE détecte les projets et leurs frameworks
  → afficher l’arborescence et le projet de démarrage
  → si plusieurs exécutables : demander lequel utiliser
  → si SDK absent : expliquer lequel manque et proposer les réglages
  → ouvrir les fichiers sans lancer automatiquement le programme
```

Résultat attendu : « ouvrir un fichier » ne doit jamais être confondu avec « ouvrir un projet ».

## Simulation 3 — Connexion GitHub réussie

Dans la première version Windows, KEYCE CODE délègue l’authentification à Git
Credential Manager, installé avec Git pour Windows. KEYCE CODE ne manipule donc
jamais le mot de passe ni le jeton.

```text
Cliquer sur le profil KEYCE → Connecter GitHub
  → KEYCE explique que le compte est facultatif
  → lancer l’interface officielle Git Credential Manager
  → l’utilisateur choisit son compte et termine l’authentification GitHub
  → GCM stocke le secret dans le coffre prévu par Git pour Windows
  → KEYCE relit uniquement la liste publique des comptes connectés
  → afficher « Connecté en tant que ... » et « Gérer l’accès »
```

Résultat attendu : aucun mot de passe ne transite par KEYCE et l’utilisateur voit exactement ce qu’il autorise. Git local reste disponible si l’utilisateur refuse.

## Simulation 4 — Évolution possible : connexion GitHub avec Device Flow

À utiliser seulement si le callback local n’est pas retenu :

```text
Cliquer « Connecter GitHub »
  → KEYCE affiche github.com/login/device et un code temporaire
  → bouton « Ouvrir dans le navigateur » et bouton « Annuler »
  → KEYCE attend sans interroger trop rapidement
  → autorisation acceptée : connexion réussie
  → code expiré : proposer « Générer un nouveau code »
  → refus : rester déconnecté sans message d’erreur alarmant
  → réseau coupé : pause/reprise et bouton annuler
```

Le polling doit respecter l’intervalle fourni par GitHub et traiter `authorization_pending`, `slow_down`, `expired_token`, `access_denied` et `device_flow_disabled`.

## Simulation 5 — Git sans compte

```text
Ouvrir un dossier contenant .git
  → Source Control affiche les changements
  → modifier un fichier
  → diff → stage → message → commit local
  → créer/changer de branche
  → tenter push sans authentification
  → KEYCE explique qu’une identité distante est requise
  → proposer connexion GitHub ou configuration d’un remote/token externe
```

Résultat attendu : les opérations locales ne sont jamais bloquées par l’absence de compte.

## Simulation 6 — GitHub déconnexion et changement de compte

```text
Profil → Compte GitHub → Déconnexion
  → demander confirmation
  → supprimer les jetons du coffre Windows
  → conserver les dépôts locaux et commits locaux
  → afficher « Connecter un autre compte »
```

Résultat attendu : la déconnexion ne supprime aucun fichier et ne détruit aucun dépôt.

## Simulation 7 — IA avec quatre clés et relais

```text
Paramètres → Intelligence artificielle
  → Gemini clé 1 : test réussi
  → Gemini clé 2 : non configurée
  → Groq clé 1 : limite atteinte
  → Groq clé 2 : test réussi
  → choisir l’ordre Gemini 1 → Gemini 2 → Groq 1 → Groq 2
  → demander une explication de code
  → Gemini 1 répond
  → Gemini 1 tombe en erreur : KEYCE passe à Gemini 2 puis Groq
  → afficher le fournisseur actif sans afficher la clé
```

Résultat attendu : une clé invalide ou limitée ne bloque pas l’assistant et l’utilisateur peut désactiver une clé.

## Simulation 8 — Erreur de code et diagnostic

```text
Écrire un code invalide
  → analyse en arrière-plan
  → soulignement rouge ondulé sous l’erreur
  → icône dans la gouttière et compteur Problems
  → survol : message, code, ligne et colonne
  → cliquer le diagnostic
  → curseur placé exactement au bon endroit
  → corriger le code
  → le diagnostic disparaît sans conserver une erreur obsolète
```

## Simulation 9 — Vrai terminal

```text
Terminal → Nouveau terminal PowerShell
  → prompt réel avec dossier du workspace
  → lancer un programme qui demande une saisie
  → écrire la réponse directement dans le terminal
  → Ctrl+C interrompt le programme
  → ouvrir un second terminal cmd
  → fermer un terminal sans fermer l’éditeur
```

## Simulation 10 — Échec et récupération

```text
Perte réseau pendant GitHub/IA
  → l’opération passe en erreur contrôlée
  → le document reste ouvert et intact
  → bouton Réessayer / Annuler / Ouvrir les détails
  → aucun secret n’apparaît dans les détails
  → retour au travail normal
```

## Manquements à vérifier avant codage

- Identité visuelle finale : logo, couleurs, icônes et nom exacts.
- Choix définitif GitHub : PKCE avec callback local ou Device Flow.
- Permissions GitHub minimales et stratégie de révocation.
- Modèles Gemini/Groq, ordre par défaut et limites utilisateur.
- Langages réellement promis en V1 et outils d’installation associés.
- Format des workspaces, tâches, profils de terminal et configurations de debug.
- Politique précise du mode examen et personne autorisée à l’activer.
- Compatibilité/licence de la couche ConPTY et des futures extensions.
