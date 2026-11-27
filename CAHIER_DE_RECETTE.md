# CAHIER DE RECETTE — TAMAM WORKFLOW STUDIO

---

## 1. Page de garde

| Champ | Valeur |
| :--- | :--- |
| **Nom du produit** | **TAMAM Workflow Studio** *(Alternative Enterprise BPM à Nintex K2)* |
| **Version du produit** | `v4.8 Enterprise` (UI) — Socle technique `.NET 10` / `React 19` / `Vite 8` |
| **Version du document** | `1.0` |
| **Date de rédaction** | `25/11/2026` |
| **Auteur** | Ingénieur QA Senior / Responsable de Recette Logicielle |
| **Statut du document** | **Prêt pour exécution de recette** *(Vierge — en attente de campagne)* |
| **Destinataires** | Équipe QA, Product Owner, Équipe Développement, Direction Technique |

---

## 2. Sommaire

1. [Page de garde](#1-page-de-garde)
2. [Sommaire](#2-sommaire)
3. [Présentation du périmètre](#3-présentation-du-périmètre)
4. [Environnement de test](#4-environnement-de-test)
5. [Conventions](#5-conventions)
6. [Scénarios de test par module](#6-scénarios-de-test-par-module)
   - [6.1 Authentification, sessions et gestion des comptes (`AUTH`)](#61-authentification-sessions-et-gestion-des-comptes-auth)
   - [6.2 Tableau de bord exécutif et navigation (`DASH`)](#62-tableau-de-bord-exécutif-et-navigation-dash)
   - [6.3 Studio de conception de workflows — Workflow Designer (`WFD`)](#63-studio-de-conception-de-workflows--workflow-designer-wfd)
   - [6.4 Concepteur de formulaires intelligents — SmartForms & Pièces jointes (`FRM`)](#64-concepteur-de-formulaires-intelligents--smartforms--pièces-jointes-frm)
   - [6.5 Moteur d'exécution, instances actives et ViewFlow (`INST`)](#65-moteur-dexécution-instances-actives-et-viewflow-inst)
   - [6.6 Centre de tâches humaines — Task Center (`TASK`)](#66-centre-de-tâches-humaines--task-center-task)
   - [6.7 Modèles de données métier — SmartObjects (`SMO`)](#67-modèles-de-données-métier--smartobjects-smo)
   - [6.8 Analytique et rapports SLA (`ANA`)](#68-analytique-et-rapports-sla-ana)
   - [6.9 Administration — Utilisateurs, rôles et paramètres système (`ADM`)](#69-administration--utilisateurs-rôles-et-paramètres-système-adm)
   - [6.10 Gestion des erreurs et cas limites (`ERR`)](#610-gestion-des-erreurs-et-cas-limites-err)
   - [6.11 Sécurité — Accès, autorisations, injections et sessions (`SEC`)](#611-sécurité--accès-autorisations-injections-et-sessions-sec)
   - [6.12 Performance et temps de réponse (`PERF`)](#612-performance-et-temps-de-réponse-perf)
   - [6.13 Compatibilité — Navigateurs, responsive, thèmes et i18n (`COMP`)](#613-compatibilité--navigateurs-responsive-thèmes-et-i18n-comp)
   - [6.14 Accessibilité (`A11Y`)](#614-accessibilité-a11y)
   - [6.15 Tests non fonctionnels — Emails, health check, OpenAPI et persistance (`NFR`)](#615-tests-non-fonctionnels--emails-health-check-openapi-et-persistance-nfr)
7. [Matrice de traçabilité](#7-matrice-de-traçabilité)
8. [Synthèse de recette](#8-synthèse-de-recette)
9. [Anomalies constatées](#9-anomalies-constatées)
10. [Hypothèses, zones ambiguës et points d'attention](#10-hypothèses-zones-ambiguës-et-points-dattention)
11. [Validation finale](#11-validation-finale)

---

## 3. Présentation du périmètre

### 3.1 Fonctionnalités couvertes (Incluses)

Le présent cahier de recette couvre l'intégralité des fonctionnalités implémentées dans le code source de **TAMAM Workflow Studio** (frontend React 19 + backend ASP.NET Core .NET 10 / Wolverine CQRS / EF Core 10) :

- **Double mode d'exécution** : *Demo Mode* (100 % navigateur via `InMemoryWorkflowRepository` et `localStorage`) et *Connected Mode* (API REST .NET 10 via `HttpWorkflowRepository` + JWT Bearer).
- **Authentification et gestion des comptes** : connexion JWT HS256, vérification du statut actif, profil `/api/auth/me`, changement de mot de passe PBKDF2-SHA256, déconnexion, limitation de débit (*rate limiting*).
- **Tableau de bord exécutif et navigation** : KPIs, approbations rapides avec confirmation, lancements rapides, journal d'audit, routage HTML5 History API (`/dashboard`, `/designer/:workflowKey`, `/smartforms`, `/instances`, `/smartobjects`, `/tasks`, `/analytics`, `/users`, `/settings`), notifications in-app.
- **Workflow Designer (Studio visuel)** : canvas `@xyflow/react`, 6 types de nœuds (`Trigger`, `Approval`, `Stage`, `SendEmail`, `Decision`, `End`), sous-processus hiérarchiques (`Stage` avec fil d'Ariane), panneaux de configuration dédiés, 4 modèles prêts à l'emploi, validation statique, sauvegarde versionnée, import/export JSON, simulateur pas-à-pas.
- **SmartForms & Pièces jointes** : concepteur visuel (10 types de contrôles), génération depuis SmartObject, visibilité conditionnelle, versionning immuable (`Draft` / `Published` / `Archived`), concurrence optimiste, soumission atomique transactionnelle avec `Idempotency-Key`, validation serveur zéro-trust, upload sécurisé (magic bytes, EICAR, anti-IDOR, prévisualisation/téléchargement).
- **Moteur d'exécution, Instances actives & ViewFlow** : démarrage d'instances, traversée de graphe (décisions multi-conditions AND/OR, sous-graphes Stage, protection anti-boucle 250 étapes), filtrage, détection de goulots (`/bottlenecks`), ViewFlow avec masquage des données sensibles, timeline paginée (`/events`), diagnostics d'erreurs, commandes opérationnelles (`Suspend`, `Resume`, `Cancel`, `Retry`) avec idempotence `commandId`.
- **Centre de tâches (Task Center)** : boîte de réception filtrée par rôle/assignation (`User`/`Group`), complétion avec outcomes dynamiques et commentaires, intégration SmartForm en mode `readonly` ou `editable` (`formPatch`) épinglé à la version soumise.
- **SmartObjects (Modèles de données)** : CRUD des schémas typés (`String`, `Number`, `Boolean`, `DateTime`, `GUID`), protection de la clé primaire, CRUD et recherche d'enregistrements, génération de SmartForm.
- **Analytique & SLA** : calcul et affichage des KPIs (`/api/analytics/kpis`) et volume par département.
- **Administration** : CRUD utilisateurs (`AdminOnly`), filtrage, configuration des paramètres système SMTP et SQL (`/api/settings`).
- **Transverse** : sécurité applicative, gestion des erreurs et quotas, performance, compatibilité navigateurs/responsive, 4 thèmes Design System + mode sombre/clair, i18n EN/FR/AR + RTL, accessibilité, health check, OpenAPI/Scalar, service email et persistance bi-SGBD (SQL Server / PostgreSQL).

### 3.2 Fonctionnalités exclues (Hors périmètre)

Les éléments suivants ne sont **pas implémentés** dans la version actuelle du code et sont donc exclus des tests fonctionnels nominaux :

- Authentification fédérée externe (OAuth2, OIDC, SAML, Active Directory / LDAP / Microsoft Entra ID) et MFA actif (le flag `mfaEnforced` est persisté en base mais non appliqué au login).
- Multi-tenancy (cloisonnement par locataire).
- Notifications temps réel (WebSockets / SignalR) et notifications push.
- Services d'arrière-plan (`BackgroundService` / `IHostedService`) : escalade SLA automatique, rappels programmés, déclencheurs planifiés (`schedule`) automatiques et webhooks sortants.
- Règles avancées de délégation de tâches (Claim/Release, Out-of-Office, Round-Robin, Quorum/Slots) dans le Task Center.
- Nœud de workflow dédié à la lecture/écriture d'un SmartObject en cours d'exécution.
- Annuler / Rétablir (*Undo / Redo*) dans le Workflow Designer.
- Recherche globale interactive (`⌘K` dans le header — champ visuel sans moteur de recherche câblé) et bouton `Connector Config` dans `DataModelsView` (non câblé).
- Optimisation SEO publique (application métier SPA authentifiée).

---

## 4. Environnement de test

### 4.1 Matériel, OS et navigateurs cibles

| Catégorie | Spécification cible |
| :--- | :--- |
| **Systèmes d'exploitation** | Windows 11 (23H2+), macOS 14+ (Sonoma), Ubuntu 22.04+ LTS |
| **Navigateurs Desktop** | Google Chrome (dernière version stable), Microsoft Edge (Chromium), Mozilla Firefox (dernière stable), Safari 17+ |
| **Résolutions / Appareils** | Desktop `1920×1080` & `1440×900` (`xl`), Laptop `1280×720` (`lg`), Tablette `768×1024` (`md`), Mobile `390×844` (`sm`) |
| **Runtime Backend** | `.NET 10 SDK` (`net10.0`), ASP.NET Core Kestrel |
| **Runtime Frontend** | `Node.js 20+ LTS`, `Vite 8.2`, `React 19.2` |
| **Bases de données** | SQL Server LocalDB / SQL Server 2022 (`Provider: SqlServer`) et PostgreSQL 16+ (`Provider: PostgreSql`) |
| **Outils QA complémentaires** | DevTools navigateur (Réseau / Console / Application), Postman ou `curl` (tests API/sécurité/concurrence), lecteur d'écran (NVDA ou VoiceOver) |

### 4.2 URLs de l'environnement de recette

| Service | URL par défaut | Remarque |
| :--- | :--- | :--- |
| **Frontend (SPA Vite)** | `http://localhost:5173` | Configurable via Vite |
| **API Backend (.NET 10)** | `http://localhost:5000/api` | Définie dans `.env` (`VITE_API_BASE_URL`) |
| **Health Check** | `http://localhost:5000/health` | Accès anonyme |
| **Documentation Scalar UI** | `http://localhost:5000/scalar/v1` | Disponible uniquement en environnement `Development` |
| **Spécification OpenAPI** | `http://localhost:5000/openapi/v1.json` | Disponible uniquement en environnement `Development` |

### 4.3 Comptes de test (Jeu initial `DatabaseInitializer.cs` & Mode Démo)

> **Mot de passe par défaut** de tous les comptes pré-initialisés en base (`Connected Mode`) : **`Password123!`**

| ID | Nom complet | Email | Rôle | Groupe | Statut | Usage principal en recette |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `usr-1` | Adel Sebti | `a.sebti@enterprise.internal` | `Administrator` | `IT & Security` | `Active` | Administration complète, démasquage ViewFlow, `retry_from_node` |
| `usr-2` | Karim Khmissi | `k.khmissi@enterprise.internal` | `Workflow Designer` | `Operations` | `Active` | Conception workflows/SmartForms, tests refus `AdminOnly` (`403`) |
| `usr-3` | Mohamed Fetni | `m.fetni@enterprise.internal` | `Process Manager` | `Finance` | `Active` | Gestion instances, approbations, tests `DesignerOrAdmin` |
| `usr-4` | Yacine Benali | `y.benali@enterprise.internal` | `End User` | `Legal` | `Inactive` | Test blocage connexion compte inactif (`403`) |
| `usr-5` | Adel Sebti | `adel.sebti@corp.internal` | `BPM Administrator` | `IT & Security` | `Active` | Bouton pré-rempli `Adel (Admin)` dans `ModeSelectorModal` |
| `usr-6` | Karim Khmissi | `karim.khmissi@corp.internal` | `Process Manager` | `Finance Managers` | `Active` | Bouton pré-rempli `Karim (Finance)` ; tâches de groupe Finance |
| *(À créer)* | Utilisateur Standard Actif | `enduser.test@corp.internal` | `End User` | `General` | `Active` | À créer via `ADM-002` pour tester les restrictions `403` (`DesignerOrAdmin`, opérateurs, anti-IDOR) |
| *(Démo)* | Mohamed Fetni | `mohamed.fetni@enterprise.com` | `BPM Administrator` | — | `Active` | Utilisateur local par défaut en **Demo Mode** |

### 4.4 Jeux de données et prérequis de démarrage

1. **Démarrage Backend** : depuis `backend/src/WorkflowEngine.Api`, vérifier `appsettings.Development.json` (`Jwt:SecretKey` ≥ 32 octets, `Database:Provider` = `SqlServer` ou `PostgreSql`), puis exécuter `dotnet run`. Les migrations EF Core et le seeding s'exécutent automatiquement au démarrage.
2. **Démarrage Frontend** : à la racine, vérifier `.env` (`VITE_API_BASE_URL=http://localhost:5000/api`), exécuter `npm install && npm run dev`.
3. **Fichiers de test à préparer** :
   - `devis_valide.pdf` : fichier PDF valide (< 1 Mo, en-tête `%PDF-`).
   - `capture_valide.png` : image PNG valide (< 1 Mo, magic bytes `89 50 4E 47 0D 0A 1A 0A`).
   - `fichier_lourd.pdf` : fichier PDF valide de taille > 10 Mo (ex. 10,5 Mo).
   - `faux_pdf.pdf` : fichier texte renommé en `.pdf` (sans signature `%PDF-`).
   - `script_malveillant.exe` : fichier avec extension interdite.
   - `eicar_test.pdf` : fichier commençant par `%PDF-` et contenant la chaîne standard de test antivirus EICAR (`X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*`).
   - `workflow_valide.json` et `workflow_invalide.json` : fichiers JSON d'import pour le Workflow Designer.

---

## 5. Conventions

### 5.1 Légende des statuts d'exécution

| Symbole | Statut | Signification |
| :---: | :--- | :--- |
| ✅ | **Validé** | Le résultat obtenu est strictement conforme au résultat attendu. |
| ❌ | **Échec** | Un écart bloquant ou fonctionnel a été constaté (anomalie à déclarer en section 9). |
| ⏳ | **À tester** | Cas de test non encore exécuté. |
| ⚠️ | **Bloqué** | Exécution impossible en raison d'un prérequis manquant ou d'une anomalie amont. |

### 5.2 Niveaux de priorité

| Priorité | Définition |
| :--- | :--- |
| **Critique** | Fonctionnalité cœur de métier, sécurité, intégrité des données ou blocage complet de l'application. Tout échec est bloquant pour la mise en production. |
| **Majeure** | Fonctionnalité importante dont le dysfonctionnement dégrade significativement l'usage mais dispose éventuellement d'un contournement. |
| **Mineure** | Anomalie cosmétique, confort UX, cas limite secondaire ou texte d'interface. |

### 5.3 Nomenclature des identifiants

Chaque cas de test est identifié de manière unique selon le format **`[MODULE]-[NNN]`** :

- `AUTH` : Authentification, sessions et comptes
- `DASH` : Tableau de bord exécutif et navigation
- `WFD` : Workflow Designer (Studio visuel)
- `FRM` : SmartForms & Pièces jointes
- `INST` : Moteur d'exécution, Instances actives & ViewFlow
- `TASK` : Centre de tâches (Task Center)
- `SMO` : Modèles de données (SmartObjects)
- `ANA` : Analytique et rapports SLA
- `ADM` : Administration (Utilisateurs & Paramètres système)
- `ERR` : Gestion des erreurs et cas limites
- `SEC` : Sécurité
- `PERF` : Performance et temps de réponse
- `COMP` : Compatibilité (navigateurs, responsive, thèmes, i18n)
- `A11Y` : Accessibilité
- `NFR` : Tests non fonctionnels

---

## 6. Scénarios de test par module

### 6.1 Authentification, sessions et gestion des comptes (`AUTH`)

#### AUTH-001 – Ouverture automatique du sélecteur de mode au premier accès et démarrage en Demo Mode
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : `localStorage` du navigateur totalement vidé sur `http://localhost:5173`.
- **Données de test** : Aucune (mode local).
- **Étapes** :
  1. Ouvrir `http://localhost:5173` dans le navigateur.
  2. Vérifier l'affichage de la modale intitulée `Select Runtime Mode`.
  3. Sélectionner la carte `Demo Mode (In-Memory)` puis cliquer sur `Start Demo Session`.
  4. Ouvrir le menu profil utilisateur en haut à droite.
- **Résultat attendu** : La modale se ferme, l'utilisateur arrive sur `/dashboard`, la clé `workflow_engine_active_mode` vaut `"demo"` dans `localStorage`, le menu profil affiche `Mohamed Fetni` (`BPM Administrator`) avec le badge `Local Demo Session`, et le bouton `Sign Out` n'est pas affiché. Aucune requête réseau vers `/api` n'est émise.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-002 – Connexion nominale en Connected Mode avec compte administrateur
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : API Backend démarrée sur `http://localhost:5000` ; `localStorage` vidé.
- **Données de test** : Email `adel.sebti@corp.internal`, mot de passe `Password123!`.
- **Étapes** :
  1. Ouvrir `http://localhost:5173`.
  2. Dans `Select Runtime Mode`, sélectionner `Connected Mode (Live API)`.
  3. Cliquer sur le bouton rapide `Adel (Admin)` ou saisir les identifiants.
  4. Cliquer sur `Connect & Enter`.
- **Résultat attendu** : L'API répond `HTTP 200` sur `POST /api/auth/login` avec un JWT valide (durée 7 jours) et le profil utilisateur. La clé `workflow_engine_auth_session` est enregistrée dans `localStorage`. Le menu profil affiche `Adel Sebti` (`BPM Administrator`), le badge `Connected API Session` et le bouton `Sign Out`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-003 – Connexion refusée avec mot de passe erroné ou email inexistant
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Négatif
- **Prérequis** : API Backend démarrée ; modale `Select Runtime Mode` ouverte sur `Connected Mode (Live API)`.
- **Données de test** : Cas A : `adel.sebti@corp.internal` / `MauvaisMdp99!` ; Cas B : `inconnu@corp.internal` / `Password123!`.
- **Étapes** :
  1. Saisir les identifiants du Cas A et cliquer sur `Connect & Enter`.
  2. Observer la réponse réseau et l'interface.
  3. Répéter avec le Cas B.
- **Résultat attendu** : Dans les deux cas, l'API répond `HTTP 401 Unauthorized` sans révéler si l'email existe ou non. Un bandeau d'erreur rouge s'affiche dans la modale. Aucune session n'est enregistrée dans `localStorage` et la modale reste ouverte.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-004 – Connexion refusée avec champs email ou mot de passe vides
- **Module** : Authentification (`AUTH`)
- **Priorité** : Majeure
- **Type** : Négatif
- **Prérequis** : API Backend démarrée.
- **Données de test** : `{ "email": "", "password": "" }` et `{ "email": "adel.sebti@corp.internal", "password": "" }`.
- **Étapes** :
  1. Envoyer `POST /api/auth/login` avec un champ vide (via UI ou client HTTP).
  2. Vérifier le code HTTP et le corps de la réponse.
- **Résultat attendu** : L'API répond `HTTP 400 Bad Request` indiquant que l'email et le mot de passe sont obligatoires. Aucun jeton JWT n'est généré.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-005 – Connexion bloquée pour un utilisateur au statut `Inactive`
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : API Backend démarrée avec les données initiales (`usr-4` a le statut `Inactive`).
- **Données de test** : Email `y.benali@enterprise.internal`, mot de passe `Password123!`.
- **Étapes** :
  1. Dans `Connected Mode (Live API)`, saisir `y.benali@enterprise.internal` et `Password123!`.
  2. Cliquer sur `Connect & Enter`.
- **Résultat attendu** : L'API répond `HTTP 403 Forbidden`. L'utilisateur n'est pas authentifié et un message d'erreur s'affiche dans la modale.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-006 – Bascule à chaud de Demo Mode vers Connected Mode
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur actuellement connecté en **Demo Mode** (`workflow_engine_active_mode = "demo"`). API Backend démarrée.
- **Données de test** : `adel.sebti@corp.internal` / `Password123!`.
- **Étapes** :
  1. Ouvrir le menu profil utilisateur et cliquer sur `Switch Runtime Mode / Login`.
  2. Sélectionner `Connected Mode (Live API)`, saisir les identifiants et cliquer sur `Connect & Enter`.
  3. Inspecter l'onglet Réseau des DevTools et le menu profil.
- **Résultat attendu** : L'appel `POST /api/auth/login` doit être réellement envoyé à l'API .NET 10 (`HTTP 200`), un vrai JWT signé doit être stocké dans `workflow_engine_auth_session`, et le mode doit basculer vers `Connected API Session` avec rechargement des données depuis la base. *(Point d'attention QA : vérifier l'ordre d'appel `setMode('connected')` vs `login()` dans `ModeSelectorModal.tsx` — voir section 10).*
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-007 – Consultation du profil authentifié (`GET /api/auth/me`)
- **Module** : Authentification (`AUTH`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur connecté en Connected Mode (`adel.sebti@corp.internal`).
- **Données de test** : Jeton Bearer JWT valide.
- **Étapes** :
  1. Appeler `GET /api/auth/me` avec l'en-tête `Authorization: Bearer <token>`.
- **Résultat attendu** : Réponse `HTTP 200` contenant `id`, `name` (`Adel Sebti`), `email` (`adel.sebti@corp.internal`), `role` (`BPM Administrator`), `group` (`IT & Security`), sans jamais exposer le hash du mot de passe.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-008 – Changement de mot de passe nominal et vérifications de robustesse
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté en Connected Mode avec `karim.khmissi@corp.internal`.
- **Données de test** : Mot de passe actuel `Password123!`, nouveau mot de passe court `1234567`, nouveau mot de passe valide `NewSecurePass456!`.
- **Étapes** :
  1. Ouvrir le menu profil > cliquer sur `Change Password`.
  2. Saisir `Password123!` et `1234567` (7 caractères), puis soumettre.
  3. Saisir un mauvais mot de passe actuel (`WrongPass!`) et `NewSecurePass456!`, puis soumettre.
  4. Saisir `Password123!` et `NewSecurePass456!`, puis cliquer sur `Update Password`.
  5. Se déconnecter et tenter de se reconnecter avec `Password123!`, puis avec `NewSecurePass456!`.
  6. Rétablir `Password123!` pour les tests suivants.
- **Résultat attendu** :
  - Étape 2 : blocage HTML5 (`minLength={8}`) et rejet API `HTTP 400` si appelé directement.
  - Étape 3 : rejet API `HTTP 400` avec message d'erreur affiché dans le formulaire.
  - Étape 4 : `HTTP 200` sur `POST /api/auth/change-password`, affichage du message `Password updated!` puis fermeture automatique après 1,5 s.
  - Étape 5 : l'ancien mot de passe échoue (`401`), le nouveau réussit (`200`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### AUTH-009 – Déconnexion utilisateur (`Sign Out`)
- **Module** : Authentification (`AUTH`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté en Connected Mode.
- **Données de test** : Session active dans `localStorage`.
- **Étapes** :
  1. Ouvrir le menu profil en haut à droite.
  2. Cliquer sur `Sign Out`.
  3. Inspecter `localStorage` et l'interface.
- **Résultat attendu** : La clé `workflow_engine_auth_session` est supprimée de `localStorage`, le mode bascule sur `demo`, la modale `Select Runtime Mode` s'ouvre, et aucune requête authentifiée résiduelle ne conserve l'ancien JWT.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.2 Tableau de bord exécutif et navigation (`DASH`)

#### DASH-001 – Affichage des indicateurs KPI du Tableau de bord
- **Module** : Tableau de bord (`DASH`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur connecté (Mode Démo ou Connecté) sur `/dashboard`.
- **Données de test** : Instances et tâches en cours.
- **Étapes** :
  1. Accéder à l'onglet `Home Dashboard` (`/dashboard`).
  2. Vérifier les 4 cartes KPI : `Pending Approvals`, `Active Workflow Instances`, `Completed Today`, `System Health`.
  3. Comparer le compteur `Pending Approvals` au nombre de tâches affichées dans le tableau `My Pending Approvals` et au badge de la cloche de notifications.
- **Résultat attendu** : Les 4 cartes KPI s'affichent sans valeur `NaN` ni `undefined`. Le compteur `Pending Approvals` correspond exactement au nombre de tâches en attente de l'utilisateur et au badge numérique de la cloche. Le bouton `Open Workflow Canvas` redirige vers `/designer`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### DASH-002 – Filtrage et décision rapide (`Approve` / `Reject`) depuis le Tableau de bord
- **Module** : Tableau de bord (`DASH`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Au moins 2 tâches en attente dont 1 de priorité `High`.
- **Données de test** : Commentaire de décision : `Validé en comité de direction`.
- **Étapes** :
  1. Cliquer sur le filtre `High Priority`, vérifier la liste, puis revenir sur `All`.
  2. Cliquer sur `Approve` sur une tâche en attente.
  3. Vérifier l'ouverture de la modale `Confirm Decision: Approval`, saisir le commentaire et cliquer sur `Confirm Approval`.
  4. Répéter l'opération avec `Reject` puis cliquer sur `Cancel` dans la modale.
- **Résultat attendu** : Le filtre `High Priority` n'affiche que les tâches `High`. La confirmation d'approbation fait disparaître la tâche de la liste, décrémente `Pending Approvals` et le badge de la cloche, et ajoute une entrée dans `Recent Execution Audit Feed`. L'annulation (`Cancel`) laisse la tâche intacte. Si toutes les tâches sont traitées, l'état vide `All tasks completed` s'affiche.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### DASH-003 – Lancement rapide d'un processus (`Quick Launch Triggers`)
- **Module** : Tableau de bord (`DASH`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur sur `/dashboard`.
- **Données de test** : Cartes `Employee Onboarding`, `Purchase Request`, `IT Access Request`.
- **Étapes** :
  1. Cliquer sur la carte `Purchase Request` dans la section `Quick Launch Triggers`.
  2. Vérifier le compteur `Active Workflow Instances` et naviguer vers `/instances`.
- **Résultat attendu** : Une nouvelle instance de workflow est créée sans erreur, le compteur d'instances actives est incrémenté, et l'instance apparaît dans l'onglet `Active Instances`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### DASH-004 – Routage URL, lien profond (`/designer/:workflowKey`) et historique navigateur
- **Module** : Tableau de bord & Navigation (`DASH`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Une définition sauvegardée portant la clé `expense` (ou une clé existante en base).
- **Données de test** : URLs `/dashboard`, `/designer`, `/designer/expense`, `/smartforms`, `/instances`, `/smartobjects`, `/tasks`, `/analytics`, `/users`, `/settings`, `/route-inexistante`.
- **Étapes** :
  1. Cliquer successivement sur chacun des 9 items de la barre latérale et vérifier la mise à jour de l'URL navigateur.
  2. Saisir directement `http://localhost:5173/designer/expense` dans la barre d'adresse.
  3. Cliquer sur le bouton `Précédent` du navigateur.
  4. Saisir `http://localhost:5173/route-inexistante`.
- **Résultat attendu** : Chaque onglet synchronise son URL exacte. L'accès direct à `/designer/expense` charge automatiquement le graphe correspondant dans le studio. Le bouton `Précédent` restaure la vue précédente via `popstate`. Toute route inconnue redirige par défaut vers `/dashboard`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### DASH-005 – Menu cloche de notifications et sélecteur d'environnement
- **Module** : Tableau de bord & Navigation (`DASH`)
- **Priorité** : Mineure
- **Type** : UI
- **Prérequis** : Au moins 1 tâche en attente.
- **Données de test** : Environnements `Development`, `Staging`, `Production`.
- **Étapes** :
  1. Cliquer sur l'icône cloche dans la barre supérieure.
  2. Cliquer sur une notification de tâche listée.
  3. Changer le sélecteur d'environnement de `Development` vers `Staging` puis `Production`.
- **Résultat attendu** : La cloche liste les tâches en attente avec `Requested by {requester} • Due {dueDate}` (ou `No pending notifications` si vide). Le clic sur une notification ferme le menu et navigue vers `/tasks`. Le badge d'environnement change de couleur (bleu → ambre → émeraude).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.3 Studio de conception de workflows — Workflow Designer (`WFD`)

#### WFD-001 – Chargement des 4 modèles de workflow depuis la palette
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur sur `/designer`.
- **Données de test** : Modèles `📋 Onboarding`, `💳 Expense Appr.`, `🔑 IT System`, `✨ Blank Canvas`.
- **Étapes** :
  1. Cliquer successivement sur chacun des 4 boutons de modèles dans la palette gauche.
  2. Vérifier les nœuds, arêtes et le compteur `{N} Nodes` après chaque chargement.
- **Résultat attendu** : Les 3 modèles métiers chargent un graphe complet et connecté avec le titre associé ; `✨ Blank Canvas` vide le canvas (0 nœud) et met à jour le compteur à `0 Nodes`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-002 – Ajout des 6 types de nœuds (Drag & Drop + Quick Add), connexion et suppression d'arête
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Canvas vierge sur `/designer`.
- **Données de test** : Les 6 types : `Start / Trigger`, `Approval Task`, `Workflow Stage`, `Send Email`, `Decision / Rule`, `End Event`.
- **Étapes** :
  1. Glisser-déposer un nœud `Start / Trigger` et un nœud `End Event` sur le canvas.
  2. Utiliser le bouton `+` (Quick Add) au survol de la palette pour ajouter `Approval Task`, `Workflow Stage`, `Send Email` et `Decision / Rule`.
  3. Relier la poignée de sortie du `Trigger` à l'entrée de `Approval Task`, puis la sortie d'un outcome vers `End Event`.
  4. Survoler une arête et cliquer sur le bouton de suppression (`X`).
  5. Sélectionner un nœud et cliquer sur `Delete this node` dans le `ConfigDrawer`.
- **Résultat attendu** : Chaque nœud apparaît avec son icône et sa couleur dédiée (émeraude, bleu, indigo, ambre, violet, rouge), le compteur `{N} Nodes` s'incrémente, la MiniMap reflète les nœuds, les arêtes animées se créent avec leur libellé, la suppression d'arête et la suppression de nœud nettoient proprement le graphe.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-003 – Configuration détaillée de chaque type de nœud dans `ConfigDrawer`
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Graphe contenant les 6 types de nœuds.
- **Données de test** :
  - `Trigger` : type `form`, SmartForm lié `Executive Purchase Requisition`, clic sur `Sync Form Fields → Payload Variables`.
  - `Approval` : bascule `Specific User` / `Group / Role`, assigné `Finance Managers`, ajout d'un outcome `Request Revision`, SLA = `5` jours (tester limites `1` et `30`), case `Allow Task Reassignment`, SmartForm lié en mode `editable`.
  - `SendEmail` : destinataire `{RequesterEmail}`, insertion du token `{EmployeeName}` dans le corps, activation `HTML Format`.
  - `Decision` : ajout de 2 règles (`Amount greater_than 5000` `AND` `Department equals Finance`), test d'un opérateur sans valeur (`is_empty`), libellés True/False.
  - `End` : statut terminal `rejected`, message de retour.
- **Étapes** :
  1. Cliquer sur chaque nœud pour ouvrir le `ConfigDrawer` et appliquer les données ci-dessus.
  2. Sur `Approval`, tenter de supprimer le dernier outcome restant.
  3. Vérifier la mise à jour visuelle immédiate sur les nœuds du canvas (poignées d'outcomes, libellés).
- **Résultat attendu** : Toutes les propriétés sont persistées dans l'état du graphe. La synchronisation `Sync Form Fields → Payload Variables` injecte les champs du SmartForm dans les variables du Trigger. Le bouton de suppression d'outcome est désactivé lorsqu'il ne reste qu'un seul outcome. Pour `is_empty` / `is_not_empty`, le champ de valeur est masqué. Le pied de panneau indique `Draft auto-saved at {heure}`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-004 – Navigation hiérarchique dans un sous-processus `Workflow Stage` (Drill-Down)
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Un graphe contenant un nœud `Workflow Stage`.
- **Données de test** : Nom du Stage : `Validation Financière`, 2 sous-nœuds internes (`Approval` + `SendEmail`).
- **Étapes** :
  1. Double-cliquer sur le nœud `Workflow Stage` (ou cliquer sur `Open & Design Stage Canvas` dans `ConfigDrawer`).
  2. Vérifier l'apparition de la bannière `Stage Scope Active` et du fil d'Ariane `Main Workflow > Validation Financière`.
  3. Ajouter 2 sous-nœuds et 1 transition dans le canvas du Stage.
  4. Ajouter un second `Workflow Stage` imbriqué et entrer dedans pour vérifier le fil d'Ariane à 3 niveaux.
  5. Cliquer sur `Back to Main Workflow` puis inspecter les statistiques du Stage dans `ConfigDrawer`.
- **Résultat attendu** : Le canvas bascule proprement dans le contexte isolé du Stage avec compteur `{N} Sub-Nodes • {M} Transitions`. Le retour au workflow principal conserve intact le sous-graphe, et `StageConfig` affiche le nombre exact de sous-étapes et de transitions.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-005 – Validation statique du graphe (`ValidationModal`) — Cas nominal et cas d'erreurs/avertissements
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur sur `/designer`.
- **Données de test** :
  - Graphe invalide : sans `Trigger`, sans `End`, un `Approval` sans assigné, un `SendEmail` sans destinataire, un nœud isolé (sans connexion entrante ni sortante).
  - Graphe valide : modèle `Onboarding` complet.
- **Étapes** :
  1. Construire le graphe invalide et cliquer sur `Validate` dans la barre d'outils.
  2. Vérifier le badge rouge sur `Validate` et la liste des erreurs/avertissements dans `ValidationModal`.
  3. Cliquer sur une erreur dans la modale.
  4. Charger le modèle `Onboarding` et cliquer sur `Validate`.
- **Résultat attendu** :
  - Pour le graphe invalide, la modale rapporte exactement : absence de Trigger, absence de End, Approval sans assigné, SendEmail sans Recipient (erreurs), ainsi que nœud inatteignable et cul-de-sac (avertissements).
  - Le clic sur une anomalie ferme la modale et sélectionne le nœud concerné sur le canvas.
  - Pour le modèle valide, le badge est vert (`0` anomalie) et la modale affiche `All Checks Passed!`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-006 – Sauvegarde, versionning et suppression d'une définition en base (`POST / GET / DELETE /api/definitions`)
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté en Connected Mode avec un compte `DesignerOrAdmin` (`adel.sebti@corp.internal`).
- **Données de test** : Titre `Validation Achats QA`, clé générée/spécifiée `validation_achats_qa`.
- **Étapes** :
  1. Modifier le titre dans le champ `Workflow Title` et construire un graphe valide.
  2. Cliquer sur `Save Definition` et observer le retour (`Saved to DB!`).
  3. Modifier un nœud et cliquer une seconde fois sur `Save Definition`.
  4. Ouvrir le menu `⋯` > `Saved DB`, vérifier la présence de la définition, la charger sur le canvas, puis la supprimer.
- **Résultat attendu** : `POST /api/definitions` retourne `HTTP 201` (en-tête `Location: /api/definitions/{key}`). La seconde sauvegarde crée une nouvelle version dans `WorkflowVersions` (version incrémentée). Le chargement restaure exactement le graphe sauvegardé. La suppression appelle `DELETE /api/definitions/{key}` (`HTTP 204`) et retire l'entrée de la liste (puis `GET /api/definitions/{key}` renvoie `404`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-007 – Import / Export JSON (`JsonSchemaModal`) et gestion des JSON corrompus
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Majeure
- **Type** : Négatif
- **Prérequis** : Utilisateur sur `/designer`.
- **Données de test** :
  - JSON valide exporté depuis le modèle `Onboarding`.
  - JSON syntaxiquement cassé : `{ "nodes": [`.
  - JSON valide sans tableau `nodes` : `{ "title": "Test" }`.
- **Étapes** :
  1. Ouvrir `⋯` > `Export JSON`, tester `Copy JSON` et `Download .json File`.
  2. Réinitialiser le canvas via `⋯` > `Reset Canvas`.
  3. Ouvrir `⋯` > `Import JSON`, coller le JSON syntaxiquement cassé et tenter l'import.
  4. Coller `{ "title": "Test" }` et tenter l'import.
  5. Importer le fichier `.json` valide téléchargé à l'étape 1.
- **Résultat attendu** : L'export génère un fichier `.json` complet. Le JSON cassé affiche `Invalid JSON syntax.`. Le JSON sans tableau `nodes` affiche `Invalid JSON: Missing "nodes" array.` ou `Invalid schema: Missing "nodes" array.`. L'import valide reconstruit intégralement les nœuds et arêtes sur le canvas.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### WFD-008 – Simulateur pas-à-pas (`SimulatorPanel`)
- **Module** : Workflow Designer (`WFD`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Modèle `Onboarding` chargé sur `/designer`.
- **Données de test** : Graphe `Onboarding`.
- **Étapes** :
  1. Cliquer sur `Test Simulation` (`Play`) dans la barre d'outils.
  2. Cliquer répétitivement sur `Step Forward` (ou `Next Step` dans la barre) jusqu'à atteindre le nœud `End`.
  3. Cliquer sur `Close Sim` (ou `Stop`).
- **Résultat attendu** : La console flottante `Execution Simulation Console` s'ouvre, le nœud actif est mis en surbrillance visuelle sur le canvas à chaque pas, les journaux d'exécution se remplissent chronologiquement, le badge `Finished Execution` apparaît au nœud terminal, et `Close Sim` ferme proprement la simulation.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.4 Concepteur de formulaires intelligents — SmartForms & Pièces jointes (`FRM`)

#### FRM-001 – Création d'un SmartForm vierge et ajout des 10 types de contrôles
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté en Connected Mode avec un rôle `DesignerOrAdmin`, sur `/smartforms`.
- **Données de test** : Titre `Demande de Matériel IT`, catégorie `IT`, les 10 contrôles (`Section Header`, `Text Input`, `Multi-line Text`, `Number`, `Currency ($)`, `Date Picker`, `Boolean Checkbox`, `Dropdown / Lookup`, `Repeatable Table`, `File / PDF Upload`).
- **Étapes** :
  1. Cliquer sur `New`, saisir `Demande de Matériel IT`, choisir la catégorie `IT` et valider.
  2. Ajouter chacun des 10 types de contrôles sur le canvas.
  3. Configurer les propriétés d'un champ dans l'inspecteur droit : largeur `Half (50%)` / `Full (100%)`, cases `Required` et `Read-Only`, texte d'aide, valeur par défaut.
  4. Réordonner 2 champs (glisser-déposer ou boutons Haut/Bas) puis supprimer un champ.
  5. Cliquer sur `Save Form`.
- **Résultat attendu** : Le formulaire est créé (`POST /api/smartforms` → `HTTP 201`) en statut `v1 • Draft`. Les espaces dans le nom de variable sont remplacés par `_`. La mise en page 2 colonnes respecte les largeurs `50 %` / `100 %`. La sauvegarde persiste le schéma sans erreur.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-002 – Génération automatique d'un SmartForm depuis un SmartObject (`POST /api/smartforms/generate/{smartObjectId}`)
- **Module** : SmartForms (`FRM`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : SmartObject `Employee SmartObject` existant.
- **Données de test** : Source `Employee SmartObject`, workflow optionnel `onboarding`.
- **Étapes** :
  1. Sur `/smartforms`, cliquer sur `Generate from SmartObject`.
  2. Sélectionner `Employee SmartObject` et le workflow `onboarding`, puis confirmer.
- **Résultat attendu** : L'API répond `HTTP 201`. Un nouveau SmartForm est créé avec les champs typés mappés automatiquement depuis le schéma du SmartObject, lié à ce SmartObject et au workflow `onboarding`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-003 – Liste déroulante (`select`) statique et dynamique (SmartObject Lookup)
- **Module** : SmartForms (`FRM`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : SmartForm en cours d'édition avec un champ `Dropdown / Lookup` ; SmartObject `Employee SmartObject` contenant des enregistrements.
- **Données de test** : Options statiques `Basse, Normale, Haute` ; puis Lookup dynamique vers `Employee SmartObject`.
- **Étapes** :
  1. Configurer le champ `select` en mode statique avec `Basse, Normale, Haute`, puis basculer en `Live Preview & Run` et vérifier les options proposées.
  2. Revenir en `Designer`, basculer la source sur `SmartObject Lookup` (sélectionner le SmartObject, le champ Value et le champ Display), puis vérifier en `Live Preview & Run`.
- **Résultat attendu** : En mode statique, seules les 3 options configurées apparaissent. En mode Lookup dynamique, la liste se peuple avec les valeurs réelles issues des enregistrements du SmartObject cible.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-004 – Règles de visibilité conditionnelle et validation côté client & serveur zéro-trust
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Formulaire pré-initialisé `Executive Purchase Requisition` (`form-purchase-req`) publié (`Published`).
- **Données de test** :
  - Cas A : `Amount = 2000` (champ conditionnel `ExecutiveJustification` masqué, même s'il est requis).
  - Cas B : `Amount = 6000` (`ExecutiveJustification` devient visible et requis).
  - Cas C (API directe) : envoi d'un champ fantôme `{"ChampNonDeclare": "hack"}` et modification d'un champ `readOnly`.
- **Étapes** :
  1. Ouvrir `Executive Purchase Requisition` en mode `Live Preview & Run`.
  2. Tenter de soumettre le formulaire vide.
  3. Saisir les champs requis avec `Amount = 2000` et vérifier que `ExecutiveJustification` est masqué.
  4. Passer `Amount` à `6000` : vérifier l'apparition immédiate de `ExecutiveJustification`, tenter de soumettre sans le remplir, puis le remplir.
  5. Via client HTTP, envoyer `POST /api/smartforms/form-purchase-req/submit` avec un champ fantôme et une modification de champ `readOnly`.
- **Résultat attendu** :
  - Étape 2 : erreurs inline `{label} is required` avec `aria-invalid="true"` et `role="alert"`.
  - Étape 3 : le champ masqué n'est ni affiché ni bloquant et est retiré du payload soumis.
  - Étape 4 : le champ devient obligatoire dès `Amount > 5000`.
  - Étape 5 : les champs fantômes sont ignorés/supprimés par `SmartFormValidationEngine` ; toute tentative de modifier un champ `readOnly` retourne `HTTP 400` avec le code `READ_ONLY_FIELD_MODIFIED`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-005 – Versionning immuable (`Draft` → `Published` → `Archived`) et consultation historique en lecture seule
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : SmartForm nouvellement créé (`v1 • Draft`) en Connected Mode.
- **Données de test** : Formulaire `Demande de Matériel IT`.
- **Étapes** :
  1. Cliquer sur `Publish Version` (`POST /api/smartforms/{id}/publish`).
  2. Tenter de publier à nouveau sans modification (`POST /api/smartforms/{id}/publish`).
  3. Modifier un champ et cliquer sur `Save Form` (`PUT /api/smartforms/{id}`).
  4. Publier cette nouvelle version (`v2 • Published`).
  5. Sélectionner `v1` dans le sélecteur `#smartform-version-history`.
- **Résultat attendu** :
  - Étape 1 : `HTTP 200`, badge passe à `v1 • Published`.
  - Étape 2 : `HTTP 409` avec le code `NO_DRAFT_TO_PUBLISH`.
  - Étape 3 : `v1` reste intacte ; une nouvelle version `v2 • Draft` est créée.
  - Étape 4 : `v2` devient `Published` et `v1` passe à `Archived`.
  - Étape 5 : une bannière ambre affiche `Viewing immutable snapshot v1 (Archived) in read-only mode.` avec le bouton `Return to Latest`, et les actions d'édition/sauvegarde sont désactivées. Toute tentative API de modifier une version publiée/archivée retourne `HTTP 409` (`FORM_VERSION_IMMUTABLE`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-006 – Concurrence optimiste sur la sauvegarde d'un SmartForm (`409 CONCURRENT_MODIFICATION`)
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Limite
- **Prérequis** : Même SmartForm ouvert simultanément dans deux onglets (Onglet A et Onglet B) en Connected Mode.
- **Données de test** : Modification du titre dans Onglet A (`Titre A`), puis dans Onglet B (`Titre B`).
- **Étapes** :
  1. Dans Onglet A, modifier le formulaire et cliquer sur `Save Form`.
  2. Sans recharger Onglet B, modifier le formulaire dans Onglet B et cliquer sur `Save Form`.
- **Résultat attendu** : Onglet A réussit (`HTTP 200`, `Version` incrémentée). Onglet B reçoit `HTTP 409` (`CONCURRENT_MODIFICATION`) car son `expectedVersion` est obsolète, et l'interface affiche le message explicite `CONCURRENT_MODIFICATION: this form was modified by another user. Reload it and re-apply your changes.`. Aucune perte silencieuse des modifications d'Onglet A.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-007 – Soumission atomique transactionnelle d'un SmartForm publié + démarrage workflow lié
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : `form-purchase-req` publié, lié au SmartObject `so-expense` et au workflow actif `expense`.
- **Données de test** : Données valides avec 2 lignes dans le répéteur `LineItems` (`Add Line Item`) et une pièce jointe `devis_valide.pdf`.
- **Étapes** :
  1. Remplir `Executive Purchase Requisition` dans `Live Preview & Run`, ajouter 2 lignes au répéteur et téléverser `devis_valide.pdf`.
  2. Soumettre le formulaire.
  3. Vérifier la table `SmartFormSubmissions`, les enregistrements de `so-expense`, les instances dans `/instances` et l'attachement lié.
- **Résultat attendu** : `POST /api/smartforms/form-purchase-req/submit` renvoie `HTTP 201`. Dans une unique transaction atomique : la soumission est persistée et épinglée au `SmartFormVersionId` publié, un enregistrement est inséré dans `so-expense`, une instance du workflow `expense` est démarrée avec les variables du formulaire, et la pièce jointe `att-*` est rattachée à l'instance.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-008 – Rejet de soumission sur un SmartForm non publié (`422 FORM_NOT_PUBLISHED`)
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Négatif
- **Prérequis** : SmartForm en statut `Draft` uniquement (jamais publié).
- **Données de test** : Payload JSON valide.
- **Étapes** :
  1. Envoyer `POST /api/smartforms/{draftFormId}/submit` avec un payload valide.
- **Résultat attendu** : L'API refuse la soumission avec `HTTP 422 Unprocessable Entity` et le code `FORM_NOT_PUBLISHED`. Aucune soumission, aucun enregistrement SmartObject et aucune instance de workflow ne sont créés.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-009 – Idempotence de soumission via `Idempotency-Key` (`201` / `200` / `409`)
- **Module** : SmartForms (`FRM`)
- **Priorité** : Critique
- **Type** : Limite
- **Prérequis** : SmartForm `form-purchase-req` publié ; deux comptes actifs (`adel.sebti@corp.internal` et `karim.khmissi@corp.internal`).
- **Données de test** : En-tête `Idempotency-Key: 11111111-2222-3333-4444-555555555555` ; clé trop longue (> 128 caractères).
- **Étapes** :
  1. Avec le compte Adel, envoyer `POST /api/smartforms/form-purchase-req/submit` avec la clé UUID.
  2. Ré-envoyer exactement la même requête avec la même clé UUID et le même compte Adel.
  3. Envoyer la même requête avec la même clé UUID mais le jeton JWT de Karim.
  4. Envoyer une requête avec une `Idempotency-Key` de 129 caractères.
- **Résultat attendu** :
  - Étape 1 : `HTTP 201` (nouvelle soumission + 1 instance créée).
  - Étape 2 : `HTTP 200` avec `idempotentReplay: true` ; aucune seconde soumission ni seconde instance créée.
  - Étape 3 : `HTTP 409 Conflict` avec code `IDEMPOTENCY_KEY_CONFLICT`.
  - Étape 4 : `HTTP 400 Bad Request` avec code `INVALID_IDEMPOTENCY_KEY`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### FRM-010 – Upload, prévisualisation, téléchargement et suppression de pièces jointes (`POST/GET/DELETE /api/attachments`)
- **Module** : SmartForms & Pièces jointes (`FRM`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Utilisateur authentifié sur un SmartForm contenant un champ `File / PDF Upload`.
- **Données de test** : `devis_valide.pdf` et `capture_valide.png`.
- **Étapes** :
  1. Téléverser `devis_valide.pdf` puis `capture_valide.png` dans le champ fichier.
  2. Cliquer sur l'icône de prévisualisation pour chacun des deux fichiers.
  3. Cliquer sur le bouton de téléchargement.
  4. Supprimer la pièce jointe avant soumission (`DELETE /api/attachments/{id}`).
- **Résultat attendu** : L'upload appelle `POST /api/attachments/upload` (`HTTP 201`), calcule le hash SHA-256 et stocke le fichier. La prévisualisation ouvre une modale affichant le PDF (iframe) ou l'image PNG. Le téléchargement retourne `HTTP 200` avec `X-Content-Type-Options: nosniff` et `Content-Disposition: attachment`. La suppression retourne `HTTP 204`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.5 Moteur d'exécution, instances actives et ViewFlow (`INST`)

#### INST-001 – Démarrage d'une instance via API (`POST /api/instances/start`) et traversée séquentielle
- **Module** : Moteur d'exécution & Instances (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Définition active contenant `Trigger` → `SendEmail` → `Approval` → `End`.
- **Données de test** : `{ "workflowKey": "expense", "initiatorId": "adel.sebti@corp.internal", "triggerVariables": { "Amount": 1200, "Department": "Finance", "EmployeeName": "Karim" } }`.
- **Étapes** :
  1. Envoyer `POST /api/instances/start` avec le jeu de données.
  2. Appeler `GET /api/instances/{instanceId}` et `GET /api/tasks/instance/{instanceId}`.
- **Résultat attendu** : `HTTP 200` avec `instanceId`. Le moteur traverse `Trigger` et `SendEmail`, crée automatiquement une `WorkflowTask` au statut `Pending` sur le nœud `Approval`, et positionne l'instance en état d'attente (`Waiting` / `Running`) sur l'ID du nœud d'approbation.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-002 – Évaluation des nœuds `Decision` (règles multiples `AND` / `OR` et opérateurs)
- **Module** : Moteur d'exécution & Instances (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Workflow publié contenant un nœud `Decision` avec règles combinées (`Amount greater_than 5000` `AND` `Department equals Finance`, plus un second scénario `OR` avec `contains`, `starts_with`, `is_empty` et accès pointé `Employee.Level`).
- **Données de test** :
  - Jeu 1 (Vrai) : `Amount = 8000`, `Department = "Finance"`.
  - Jeu 2 (Faux) : `Amount = 8000`, `Department = "HR"`.
- **Étapes** :
  1. Démarrer une instance avec le Jeu 1 et inspecter la branche empruntée dans ViewFlow (`GET /api/instances/{id}/flow`).
  2. Démarrer une instance avec le Jeu 2 et inspecter la branche empruntée.
- **Résultat attendu** : Le Jeu 1 suit exclusivement la branche `True` ; le Jeu 2 suit exclusivement la branche `False`. Un événement `DecisionEvaluated` est tracé dans `WorkflowAuditLogs`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-003 – Exécution d'un sous-workflow `Stage` avec pause sur tâche humaine et reprise
- **Module** : Moteur d'exécution & Instances (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Workflow contenant `Trigger` → `Stage` (contenant un `Approval` interne) → `End`.
- **Données de test** : Variables de démarrage valides.
- **Étapes** :
  1. Démarrer une instance du workflow contenant le `Stage`.
  2. Vérifier que la tâche humaine interne au `Stage` est créée et que l'instance est en pause (pile `ExecutionStackJson` alimentée).
  3. Compléter la tâche humaine interne (`POST /api/tasks/{id}/complete`).
  4. Vérifier l'état final de l'instance.
- **Résultat attendu** : L'instance entre dans le `Stage`, se met en pause sur la tâche interne, puis à la complétion dépile `ExecutionStackJson`, ressort du `Stage`, atteint le nœud `End` du graphe parent et passe au statut `Completed`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-004 – Liste, recherche et filtrage des instances actives (`ActiveInstancesView`)
- **Module** : Instances actives (`INST`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Plusieurs instances existantes avec des statuts variés (`RUNNING`, `PENDING`/`WAITING`, `COMPLETED`, `FAILED`).
- **Données de test** : Mot-clé de recherche (ID d'instance ou nom du demandeur).
- **Étapes** :
  1. Naviguer vers `/instances`.
  2. Cliquer successivement sur les filtres `ALL`, `RUNNING`, `PENDING`, `COMPLETED`, `FAILED`.
  3. Saisir un ID d'instance ou un nom dans `Filter instances by name, ID, or requester...`.
  4. Cliquer sur `Inspect Graph` sur une ligne.
- **Résultat attendu** : Chaque filtre n'affiche que les instances du statut correspondant (et `GET /api/instances?status=...` filtre correctement). La recherche texte filtre dynamiquement par nom, ID ou demandeur. `Inspect Graph` ouvre le modèle dans `/designer`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-005 – Consultation du panneau ViewFlow, timeline paginée et diagnostics d'erreur
- **Module** : Instances actives & ViewFlow (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Une instance active et une instance en échec (`Faulted` / `Failed`).
- **Données de test** : `GET /api/instances/{id}/flow`, `GET /api/instances/{id}/events?afterSeq=0&limit=10`, `GET /api/diagnostics/failing/{id}`, `GET /api/diagnostics/timeline/{id}`, `GET /api/instances/bottlenecks`.
- **Étapes** :
  1. Cliquer sur `ViewFlow` sur une instance dans `/instances`.
  2. Vérifier les 3 sections : `Executed Nodes`, `Context Variables` et `Event Timeline`.
  3. Cliquer sur `Refresh ViewFlow`, puis sur `Close ViewFlow`.
  4. Appeler les endpoints `/events` (avec `afterSeq` et `limit`), `/diagnostics/failing/{id}`, `/diagnostics/timeline/{id}` et `/instances/bottlenecks`.
- **Résultat attendu** : Le panneau ViewFlow affiche le statut exact, la version, les nœuds exécutés (statut, tentatives, durée, erreur éventuelle), les variables de contexte et la chronologie ordonnée (`#seq`, `type`, `Actor`, `Node`, horodatage). `/diagnostics/failing/{id}` retourne le message d'erreur, la stack trace et le snapshot des variables. `/instances/bottlenecks` liste les nœuds bloquants.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-006 – Commandes opérationnelles `Suspend` et `Resume` sur une instance active
- **Module** : Instances actives & ViewFlow (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté en tant qu'opérateur (`BPM Administrator` ou `Process Manager`) ; instance au statut `Waiting` ou `Running`.
- **Données de test** : `commandId` UUID généré automatiquement par l'UI.
- **Étapes** :
  1. Ouvrir `ViewFlow` sur l'instance active et cliquer sur `Suspend`.
  2. Vérifier le badge de statut et le bandeau `Suspended from {ancien_statut}`.
  3. Cliquer sur `Resume`.
- **Résultat attendu** : `Suspend` appelle `POST /api/instances/{id}/commands/suspend` (`HTTP 200`), passe l'instance à `Suspended` en mémorisant `SuspendedFromStatus`, et affiche le message de succès (`role="status"`). Le bouton `Suspend` disparaît au profit de `Resume`. `Resume` restaure exactement l'état précédent (`Waiting` ou `Running`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-007 – Commande opérationnelle `Cancel` avec validation de raison (≥ 10 caractères) et révocation des tâches
- **Module** : Instances actives & ViewFlow (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Instance en statut `Waiting` possédant au moins 1 tâche `Pending`.
- **Données de test** : Raison trop courte `"Annulé"` (6 car.) ; raison valide `"Demande annulée par le service achats suite à doublon"`.
- **Étapes** :
  1. Ouvrir `ViewFlow` et cliquer sur `Cancel`.
  2. Saisir `"Annulé"` dans `Cancellation Reason` et observer l'état du bouton `Confirm Cancel` et le message de validation.
  3. Saisir `"   12345   "` (espaces autour de 5 caractères).
  4. Saisir la raison valide (≥ 10 caractères hors espaces) et cliquer sur `Confirm Cancel`.
  5. Vérifier le statut de la tâche associée via `GET /api/tasks/instance/{id}`.
- **Résultat attendu** : Tant que la raison trimmée fait < 10 caractères, `Confirm Cancel` est désactivé avec le message `Cancellation reason must be at least 10 characters ({len}/10).` (et l'API renvoie `HTTP 400` en appel direct). Une fois validée, l'instance passe à `Cancelled`, toutes ses tâches `Pending` sont révoquées en `Cancelled` et ses `NodeExecutions` actives passent à `Cancelled`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-008 – Commande opérationnelle `Retry` (`retry_failed_node` et `retry_from_node`)
- **Module** : Instances actives & ViewFlow (`INST`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Instance en statut `Faulted` (échec sur un nœud).
- **Données de test** : Mode `retry_failed_node` (via UI bouton `Retry`) ; mode `retry_from_node` avec `fromNodeId` (via API avec compte `Administrator`).
- **Étapes** :
  1. Ouvrir `ViewFlow` sur l'instance `Faulted` et vérifier la présence du bouton `Retry`.
  2. Cliquer sur `Retry` (`POST /api/instances/{id}/commands/retry`, mode `retry_failed_node`).
  3. Sur une autre instance `Faulted`, appeler `POST /api/instances/{id}/commands/retry` avec `mode: "retry_from_node"` et `fromNodeId` valide.
- **Résultat attendu** : L'instance quitte l'état `Faulted`, repasse à `Running` et ré-exécute le nœud ciblé. Le compteur de tentatives (`attempts`) dans `Executed Nodes` s'incrémente.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-009 – Machine à états stricte : rejet des transitions interdites (`409 INVALID_TRANSITION`)
- **Module** : Instances actives (`INST`)
- **Priorité** : Critique
- **Type** : Négatif
- **Prérequis** : Une instance `Completed`, une instance `Cancelled` et une instance `Running`.
- **Données de test** : Appels directs API avec un `commandId` UUID valide.
- **Étapes** :
  1. Envoyer `POST /api/instances/{completedId}/commands/suspend`.
  2. Envoyer `POST /api/instances/{cancelledId}/commands/resume`.
  3. Envoyer `POST /api/instances/{runningId}/commands/resume`.
  4. Envoyer `POST /api/instances/{runningId}/commands/retry`.
- **Résultat attendu** : Les 4 appels sont rejetés avec `HTTP 409 Conflict` et le code `INVALID_TRANSITION`. Aucune mutation d'état ni d'événement parasite n'est persistée en base.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### INST-010 – Idempotence des commandes opérationnelles par `commandId`
- **Module** : Instances actives (`INST`)
- **Priorité** : Majeure
- **Type** : Limite
- **Prérequis** : Instance au statut `Waiting`.
- **Données de test** : `commandId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"`.
- **Étapes** :
  1. Envoyer `POST /api/instances/{id}/commands/suspend` avec ce `commandId`.
  2. Ré-envoyer exactement la même requête avec le même `commandId`.
- **Résultat attendu** : Les deux appels retournent `HTTP 200` sans erreur `409 INVALID_TRANSITION` ; le second est reconnu via `CommandLogs` comme rejeu idempotent et ne génère pas d'événement dupliqué dans `InstanceEvents`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.6 Centre de tâches humaines — Task Center (`TASK`)

#### TASK-001 – Visibilité cloisonnée des tâches par utilisateur, groupe et administrateur (`GET /api/tasks`)
- **Module** : Centre de tâches (`TASK`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : 1 tâche assignée à `karim.khmissi@corp.internal` (`User`), 1 tâche assignée au groupe `Finance Managers` (`Group`), 1 tâche assignée à `adel.sebti@corp.internal` (`User`).
- **Données de test** : Comptes `karim.khmissi@corp.internal` (`Process Manager`, groupe `Finance Managers`) et `adel.sebti@corp.internal` (`BPM Administrator`).
- **Étapes** :
  1. Se connecter avec Karim et ouvrir `/tasks` (vérifier `GET /api/tasks`).
  2. Se connecter avec Adel (`BPM Administrator`) et ouvrir `/tasks`.
- **Résultat attendu** : Karim voit uniquement sa tâche personnelle et celle de son groupe `Finance Managers` (pas la tâche personnelle d'Adel). Adel (administrateur) voit l'ensemble des tâches. Si aucune tâche n'est en attente, l'état vide `Task Inbox Clear!` s'affiche.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### TASK-002 – Complétion nominale d'une tâche avec choix d'outcome dynamique et commentaire
- **Module** : Centre de tâches (`TASK`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Tâche en attente avec outcomes configurés (`Approve`, `Reject`, `Request Revision`).
- **Données de test** : Outcome `Approve`, commentaire `"Budget vérifié et approuvé"`.
- **Étapes** :
  1. Dans `/tasks`, sélectionner la tâche dans la colonne gauche `Pending Inbox`.
  2. Vérifier l'affichage des boutons d'outcomes dynamiques et des métadonnées (`Requester`, `Priority & Amount`, `Requisition Description & Notes`).
  3. Cliquer sur `Approve`, saisir le commentaire dans la modale `Log Task Decision: Approve`, puis cliquer sur `Confirm Approve`.
- **Résultat attendu** : `POST /api/tasks/{id}/complete` renvoie `HTTP 200`. La tâche passe au statut `Approved` (ou `Rejected` si l'outcome vaut `"reject"` insensible à la casse), disparaît de la boîte de réception, et le moteur fait avancer l'instance sur la branche correspondant à l'outcome choisi.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### TASK-003 – Formulaire SmartForm lié en mode `Read-Only Review` épinglé à la version soumise
- **Module** : Centre de tâches (`TASK`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Instance démarrée depuis `form-purchase-req` v1, parvenue sur un nœud `Approval` configuré avec `formMode = readonly`. Entre-temps, `form-purchase-req` a été modifié et republié en v2.
- **Données de test** : Tentative API d'envoi d'un `formPatch` sur cette tâche `readonly`.
- **Étapes** :
  1. Ouvrir la tâche dans `/tasks`.
  2. Vérifier le badge de version du formulaire affiché et l'impossibilité d'éditer les champs.
  3. Via client HTTP, envoyer `POST /api/tasks/{id}/complete` en incluant un `formPatch` non vide.
- **Résultat attendu** : L'UI affiche le formulaire exact en `v1` (version immuable épinglée au moment de la soumission, sans les modifications de la v2) avec tous les champs en lecture seule. L'appel API avec `formPatch` sur une tâche `readonly` est rejeté avec `HTTP 400` (`FORM_READ_ONLY`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### TASK-004 – Formulaire SmartForm lié en mode `Editable` avec transmission de `formPatch`
- **Module** : Centre de tâches (`TASK`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Nœud `Approval` configuré avec un SmartForm en mode `editable`.
- **Données de test** : Modification d'un champ éditable (ex. `CostCenter = "CC-900"`).
- **Étapes** :
  1. Ouvrir la tâche dans `/tasks`.
  2. Modifier la valeur du champ éditable dans le formulaire embarqué.
  3. Cliquer sur `Approve` et confirmer.
  4. Inspecter les `Context Variables` de l'instance dans ViewFlow.
- **Résultat attendu** : L'appel `POST /api/tasks/{id}/complete` transmet le `formPatch` contenant uniquement les champs visibles modifiés, le serveur valide le patch via `SmartFormValidationEngine` et met à jour les variables de contexte de l'instance avec la nouvelle valeur (`CC-900`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### TASK-005 – Blocage de la complétion d'une tâche lorsque l'instance parente est `Suspended` (`409 INSTANCE_SUSPENDED`)
- **Module** : Centre de tâches (`TASK`)
- **Priorité** : Critique
- **Type** : Négatif
- **Prérequis** : Instance avec tâche `Pending`, suspendue via `INST-006`.
- **Données de test** : Outcome `Approve`.
- **Étapes** :
  1. Tenter de compléter la tâche via `/tasks` ou `POST /api/tasks/{id}/complete`.
- **Résultat attendu** : L'API rejette la complétion avec `HTTP 409 Conflict` et le code `INSTANCE_SUSPENDED`. La tâche reste `Pending` et l'instance reste `Suspended`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### TASK-006 – Double complétion simultanée / concurrence optimiste sur une tâche (`409`)
- **Module** : Centre de tâches (`TASK`)
- **Priorité** : Critique
- **Type** : Limite
- **Prérequis** : Tâche de groupe `Pending` ouverte simultanément par deux utilisateurs autorisés (ou deux requêtes concurrentes avec le même `expectedVersion`).
- **Données de test** : Requête 1 : `Approve` (`expectedVersion: 1`) ; Requête 2 : `Reject` (`expectedVersion: 1`).
- **Étapes** :
  1. Exécuter la Requête 1 et vérifier le succès.
  2. Exécuter la Requête 2 avec l'ancien `expectedVersion` (ou sur la tâche déjà complétée).
- **Résultat attendu** : Une seule complétion réussit (`HTTP 200`). La seconde est rejetée avec `HTTP 409 Conflict` (`CONCURRENT_MODIFICATION` ou `TASK_ALREADY_COMPLETED`). L'instance n'avance qu'une seule fois.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.7 Modèles de données métier — SmartObjects (`SMO`)

#### SMO-001 – Création, modification et suppression d'un schéma SmartObject (`POST/PUT/DELETE /api/smartobjects`)
- **Module** : SmartObjects (`SMO`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté avec un rôle `DesignerOrAdmin`, sur `/smartobjects`.
- **Données de test** : Nom `ContractEntity`, nom d'affichage `Contrat Fournisseur`, catégorie `Custom`, description `Suivi des contrats`.
- **Étapes** :
  1. Cliquer sur `New SmartObject Entity`, saisir les données et valider.
  2. Cliquer sur `Edit`, modifier la description et enregistrer.
  3. Tenter de créer un SmartObject avec un nom vide ou un nom déjà existant.
  4. Supprimer le SmartObject créé via `Delete` et confirmer dans la modale.
- **Résultat attendu** : Création (`HTTP 201`), édition (`HTTP 200`) et suppression (`HTTP 204`) fonctionnent et mettent à jour le répertoire gauche. Un nom vide est rejeté (`HTTP 400`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SMO-002 – Ajout de champs typés et protection contre la suppression de la clé primaire
- **Module** : SmartObjects (`SMO`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : SmartObject sélectionné dans l'onglet `Schema & Fields`.
- **Données de test** : Champs de types `String`, `Number`, `Boolean`, `DateTime`, `GUID` (avec options `Primary Key` / `Required`).
- **Étapes** :
  1. Cliquer sur `Add Field` et ajouter un champ de chacun des 5 types.
  2. Repérer le champ marqué `Primary Key` et vérifier l'état de son bouton de suppression.
  3. Supprimer un champ non-clé primaire.
- **Résultat attendu** : Les 5 types sont affichés dans le tableau du schéma avec leurs indicateurs `Primary Key` et `Required`. Le bouton de suppression du champ clé primaire est désactivé (`Primary key field cannot be deleted`). La suppression d'un champ ordinaire fonctionne.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SMO-003 – CRUD complet et recherche des enregistrements (`Data Items / Records`)
- **Module** : SmartObjects (`SMO`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : SmartObject `Employee SmartObject` sélectionné, onglet `Data Items (Records)`.
- **Données de test** : Nouvel enregistrement avec GUID auto-généré (bouton générateur), nom `Sami Mansour`, champs booléen/date/nombre renseignés.
- **Étapes** :
  1. Cliquer sur `Add Record`, utiliser la génération automatique de GUID, remplir les champs et enregistrer (`POST /api/smartobjects/{id}/records`).
  2. Saisir `Sami Mansour` dans `Search data items...` et vérifier le compteur `Showing X of Y records`.
  3. Cliquer sur `Edit` sur la ligne : vérifier que le champ clé primaire est désactivé en édition, modifier un champ et enregistrer (`PUT`).
  4. Cliquer sur `Delete` et confirmer (`DELETE`).
- **Résultat attendu** : Création (`HTTP 201`), filtrage en temps réel, mise à jour (`HTTP 200` avec PK protégée) et suppression (`HTTP 204`) fonctionnent et sont persistés en base de données. Le bouton `Refresh` recharge fidèlement les données serveur.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SMO-004 – Génération d'un SmartForm directement depuis `DataModelsView`
- **Module** : SmartObjects (`SMO`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : SmartObject `Purchase Request SmartObject` sélectionné sur `/smartobjects`.
- **Données de test** : Aucune.
- **Étapes** :
  1. Cliquer sur le bouton `Generate SmartForm` dans l'en-tête de `DataModelsView`.
- **Résultat attendu** : L'application génère le SmartForm correspondant et redirige automatiquement vers `/smartforms` avec le nouveau formulaire ouvert et prêt à être configuré.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SMO-005 – Comportement Upsert API sur les enregistrements SmartObject
- **Module** : SmartObjects (`SMO`)
- **Priorité** : Mineure
- **Type** : Limite
- **Prérequis** : SmartObject existant en Connected Mode.
- **Données de test** : Clé primaire `EMP-UPSERT-01`.
- **Étapes** :
  1. Appeler `POST /api/smartobjects/{id}/records` deux fois avec la même clé primaire `EMP-UPSERT-01` et des valeurs mises à jour.
  2. Appeler `PUT /api/smartobjects/{id}/records/EMP-UPSERT-02` sur une clé inexistante.
  3. Appeler `DELETE /api/smartobjects/{id}/records/CLE-INEXISTANTE`.
- **Résultat attendu** : Le second `POST` met à jour l'enregistrement existant sans créer de doublon (contrainte unique `(SmartObjectId, RecordKey)` respectée). Le `PUT` sur clé absente crée l'enregistrement (`HTTP 200`). Le `DELETE` sur clé inexistante retourne `HTTP 404`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.8 Analytique et rapports SLA (`ANA`)

#### ANA-001 – Affichage des KPIs SLA et de la répartition par département (`GET /api/analytics/kpis`)
- **Module** : Analytique (`ANA`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Connecté en Connected Mode avec des instances actives et complétées en base.
- **Données de test** : Aucune.
- **Étapes** :
  1. Naviguer vers `/analytics`.
  2. Vérifier l'appel réseau `GET /api/analytics/kpis` (`HTTP 200`).
  3. Vérifier l'affichage des 3 cartes KPI (`Avg Completion Time`, `SLA Compliance Rate`, `Top Bottleneck Node`) et du graphique horizontal `Workflow Volume Breakdown by Department`.
- **Résultat attendu** : Les valeurs affichées correspondent exactement à la réponse JSON de `/api/analytics/kpis`. Les barres de progression par département affichent des pourcentages cohérents (0 à 100 %) et le nombre exact d'instances. Aucune erreur console.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ANA-002 – Évolution des KPIs analytiques après exécution et complétion d'une instance
- **Module** : Analytique (`ANA`)
- **Priorité** : Mineure
- **Type** : Fonctionnel
- **Prérequis** : Relevé initial des compteurs sur `/analytics`.
- **Données de test** : Démarrage et complétion de bout en bout d'une nouvelle instance Finance.
- **Étapes** :
  1. Démarrer puis mener à terme une nouvelle instance d'un workflow.
  2. Recharger `/analytics` et comparer les indicateurs.
- **Résultat attendu** : Le volume du département concerné s'incrémente et le temps moyen de complétion / goulot d'étranglement se recalcule sans erreur.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.9 Administration — Utilisateurs, rôles et paramètres système (`ADM`)

#### ADM-001 – Consultation, recherche et filtrage par rôle dans `UsersView`
- **Module** : Administration (`ADM`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Connecté sur `/users` avec les 6 comptes pré-initialisés.
- **Données de test** : Recherche `"Karim"`, filtre rôle `Process Manager`, recherche sans résultat `"zzz-inexistant"`.
- **Étapes** :
  1. Ouvrir `/users` et vérifier les colonnes (`User`, `BPM System Role`, `Permission Group`, `Status`, `Actions`).
  2. Filtrer par rôle `Process Manager`.
  3. Saisir `"Karim"` dans `Search by name, email, or department...`.
  4. Saisir `"zzz-inexistant"`.
- **Résultat attendu** : Les filtres combinés réduisent exactement la liste aux comptes correspondants. Pour `"zzz-inexistant"`, le message d'état vide `No users matching the search query or filter.` s'affiche.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ADM-002 – Création d'un utilisateur BPM (`POST /api/users`) — Cas nominal et validations
- **Module** : Administration (`ADM`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Connecté en tant qu'`Administrator` ou `BPM Administrator` sur `/users`.
- **Données de test** :
  - Invalide : nom ou email vide ; email invalide ; email déjà existant (`adel.sebti@corp.internal`).
  - Valide : Nom `Utilisateur Standard Actif`, Email `enduser.test@corp.internal`, Rôle `End User`, Groupe `General`, Statut `Active`, Avatar sélectionné.
- **Étapes** :
  1. Cliquer sur `Add User or Group`.
  2. Tenter de soumettre avec le nom ou l'email vide.
  3. Remplir les données valides et soumettre.
  4. Tenter de recréer un utilisateur avec l'email `enduser.test@corp.internal` (doublon).
- **Résultat attendu** :
  - Étape 2 : l'UI bloque et affiche `Full name and email address are required.` (et l'API renvoie `HTTP 400` sur champs invalides ou mot de passe < 8 car.).
  - Étape 3 : `POST /api/users` renvoie `HTTP 201`, l'utilisateur apparaît dans le tableau et peut se connecter via `/api/auth/login`.
  - Étape 4 : la création en doublon est rejetée (contrainte d'unicité `Email`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ADM-003 – Suppression d'un utilisateur (`DELETE /api/users/{id}`)
- **Module** : Administration (`ADM`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Un utilisateur de test jetable créé (`temp.qa@corp.internal`).
- **Données de test** : ID de l'utilisateur jetable et un ID inexistant (`usr-99999`).
- **Étapes** :
  1. Cliquer sur l'icône de suppression de `temp.qa@corp.internal` dans `/users`.
  2. Annuler la boîte `window.confirm` et vérifier que la ligne reste présente.
  3. Cliquer à nouveau et confirmer la suppression.
  4. Envoyer `DELETE /api/users/usr-99999` via API.
- **Résultat attendu** : L'annulation conserve l'utilisateur. La confirmation appelle `DELETE /api/users/{id}` (`HTTP 204`) et retire la ligne du tableau. La suppression d'un ID inexistant retourne `HTTP 404`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ADM-004 – Lecture et mise à jour des paramètres système SMTP et SQL (`GET/PUT /api/settings`)
- **Module** : Administration (`ADM`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Connecté en tant que `BPM Administrator` sur `/settings`.
- **Données de test** : `SMTP Host = smtp.recette.internal`, `Port = 587`, `SSL / TLS = STARTTLS`, `Max Pool Size = 100`, `Timeout (Sec) = 30`.
- **Étapes** :
  1. Ouvrir `/settings` et vérifier le chargement des valeurs persistées (`GET /api/settings`).
  2. Modifier les champs SMTP et SQL avec les données de test, puis cliquer sur `Save System Settings`.
  3. Recharger la page (`F5`) et vérifier les valeurs affichées.
- **Résultat attendu** : `PUT /api/settings` retourne `HTTP 200`, le bouton affiche `Saved Successfully` pendant 3 secondes, et les valeurs modifiées sont toujours présentes après rechargement (persistées dans `SystemSettings`). Le champ `SQL Connection String` reste masqué (`type="password"`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ADM-005 – Validation des valeurs limites sur les paramètres système (`PUT /api/settings`)
- **Module** : Administration (`ADM`)
- **Priorité** : Majeure
- **Type** : Limite
- **Prérequis** : Compte `AdminOnly` authentifié.
- **Données de test** :
  - Cas A : `smtpPort: "abc"` (non numérique).
  - Cas B : `sqlMaxPoolSize: 0` et `sqlMaxPoolSize: 1001` (hors plage `1..1000`).
  - Cas C : `sqlMaxPoolSize: 1` et `sqlMaxPoolSize: 1000` (bornes exactes).
- **Étapes** :
  1. Envoyer `PUT /api/settings` pour les Cas A et B.
  2. Envoyer `PUT /api/settings` pour le Cas C.
- **Résultat attendu** : Cas A et B sont rejetés avec `HTTP 400 Bad Request` (`ValidationProblem`). Cas C (bornes `1` et `1000`) est accepté avec `HTTP 200`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.10 Gestion des erreurs et cas limites (`ERR`)

#### ERR-001 – Protection anti-boucle infinie dans le moteur d'exécution (`MaxExecutionSteps = 250`)
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Critique
- **Type** : Limite
- **Prérequis** : Définition de workflow contenant un cycle sans nœud bloquant (ex. `Decision` qui boucle sur lui-même ou sur `SendEmail`).
- **Données de test** : Variables provoquant la boucle infinie.
- **Étapes** :
  1. Publier le workflow cyclique et démarrer une instance via `POST /api/instances/start`.
  2. Consulter `GET /api/instances/{id}` et `GET /api/diagnostics/failing/{id}`.
- **Résultat attendu** : Le moteur interrompt proprement l'exécution à 250 étapes sans bloquer le serveur ni saturer la RAM/CPU. L'instance passe en statut `Faulted` avec le code d'erreur `MAX_STEPS_EXCEEDED` enregistré dans `WorkflowInstanceErrors`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ERR-002 – Quotas de définition de workflow (`> 500 nœuds`, `> 1 Mo JSON`, IDs dupliqués, clé invalide)
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Majeure
- **Type** : Limite
- **Prérequis** : Compte `DesignerOrAdmin` authentifié.
- **Données de test** :
  - Cas A : `workflowKey = "clé avec espaces!"` (hors regex `^[a-zA-Z0-9_-]+$`) ou > 100 caractères.
  - Cas B : `graphJson` contenant 501 nœuds.
  - Cas C : `graphJson` contenant deux nœuds avec le même `id`.
  - Cas D : `graphJson` > 1 000 000 caractères.
- **Étapes** :
  1. Envoyer `POST /api/definitions` pour chacun des 4 cas.
- **Résultat attendu** : Les 4 requêtes sont rejetées par `CreateWorkflowDefinitionValidator` avec `HTTP 400 Bad Request` (`ValidationProblem`) et message explicite. Rien n'est persisté en base.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ERR-003 – Quotas SmartForm (`> 200 champs`, `> 512 Ko FieldsJson`, `> 256 Ko payload`)
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Majeure
- **Type** : Limite
- **Prérequis** : Compte `DesignerOrAdmin` authentifié.
- **Données de test** :
  - Cas A : `POST /api/smartforms` avec 201 champs.
  - Cas B : `POST /api/smartforms` avec `FieldsJson` > 512 Ko.
  - Cas C : `POST /api/smartforms/{id}/submit` avec un corps JSON > 256 Ko.
- **Étapes** :
  1. Envoyer `POST /api/smartforms` avec le Cas A (201 champs).
  2. Envoyer `POST /api/smartforms` avec le Cas B (`FieldsJson` > 512 Ko).
  3. Envoyer `POST /api/smartforms/{id}/submit` avec le Cas C (corps JSON > 256 Ko).
- **Résultat attendu** : Cas A et B retournent `HTTP 400 Bad Request`. Cas C retourne `HTTP 400` avec le code `PAYLOAD_TOO_LARGE`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ERR-004 – Rejet des fichiers dépassant la limite de 10 Mo (`FILE_TOO_LARGE`)
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Majeure
- **Type** : Limite
- **Prérequis** : SmartForm avec champ `File / PDF Upload` (en Mode Démo et en Mode Connecté).
- **Données de test** : `fichier_lourd.pdf` (10,5 Mo).
- **Étapes** :
  1. Tenter de téléverser `fichier_lourd.pdf` depuis l'UI en Mode Démo.
  2. Tenter de téléverser `fichier_lourd.pdf` en Mode Connecté (`POST /api/attachments/upload`).
- **Résultat attendu** : En Mode Démo, l'UI bloque avec `File exceeds maximum allowed size of 10 MB.`. En Mode Connecté, l'API rejette avec `HTTP 400` (`FILE_TOO_LARGE`) et aucun fichier n'est écrit sur disque.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ERR-005 – Rollback transactionnel complet en cas d'échec du démarrage du workflow lié (`422 WORKFLOW_START_FAILED`)
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Critique
- **Type** : Négatif
- **Prérequis** : SmartForm publié lié au SmartObject `so-expense` et à une clé de workflow inexistante ou sans version active (`workflow_inexistant_qa`).
- **Données de test** : Payload valide avec `startLinkedWorkflow: true`.
- **Étapes** :
  1. Relever le nombre actuel de soumissions (`SmartFormSubmissions`) et d'enregistrements dans `so-expense`.
  2. Appeler `POST /api/smartforms/{id}/submit`.
  3. Re-vérifier les deux compteurs en base.
- **Résultat attendu** : L'API répond `HTTP 422 Unprocessable Entity` (`WORKFLOW_START_FAILED`). Grâce au rollback de `ExecuteInTransactionAsync`, aucune soumission orpheline et aucun enregistrement `so-expense` n'ont été conservés en base.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ERR-006 – Perte de connexion API / Backend arrêté en Connected Mode
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Majeure
- **Type** : Négatif
- **Prérequis** : Utilisateur en Connected Mode ; couper temporairement le processus Backend (`dotnet run`).
- **Données de test** : Tentative de login, de sauvegarde de workflow ou de rafraîchissement de ViewFlow.
- **Étapes** :
  1. Tenter une action appelant l'API (ex. `Connect & Enter` ou `Refresh ViewFlow`).
  2. Observer le comportement de l'interface.
- **Résultat attendu** : L'application ne plante pas (pas d'écran blanc / crash React), affiche un message d'erreur explicite (`Failed to authenticate or connect to API.` ou bandeau d'alerte `role="alert"`), et l'utilisateur peut basculer vers `Demo Mode` via le menu profil.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### ERR-007 – Validation *fail-fast* au démarrage backend (Provider SGBD invalide / Clé JWT < 32 octets)
- **Module** : Gestion des erreurs (`ERR`)
- **Priorité** : Majeure
- **Type** : Négatif
- **Prérequis** : Accès temporaire à la configuration d'environnement de démarrage backend.
- **Données de test** :
  - Cas A : `Database__Provider=Oracle` (non supporté).
  - Cas B : `Jwt__SecretKey=cle_trop_courte` (< 32 octets).
- **Étapes** :
  1. Lancer `dotnet run` avec le Cas A, observer les logs et le code de sortie.
  2. Lancer `dotnet run` avec le Cas B, observer les logs et le code de sortie.
- **Résultat attendu** : Dans les deux cas, l'application refuse de démarrer immédiatement (*fail-fast*) avec une exception descriptive (`InvalidOperationException`) indiquant la configuration manquante ou invalide.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.11 Sécurité — Accès, autorisations, injections et sessions (`SEC`)

#### SEC-001 – Accès refusé sans JWT, avec JWT altéré ou JWT expiré (`401 Unauthorized`)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : API Backend démarrée.
- **Données de test** : Aucun en-tête `Authorization` ; jeton avec signature modifiée ; jeton expiré.
- **Étapes** :
  1. Appeler `GET /api/definitions`, `GET /api/instances`, `GET /api/tasks`, `GET /api/smartforms`, `GET /api/smartobjects`, `GET /api/users`, `GET /api/settings`, `GET /api/analytics/kpis` sans jeton.
  2. Répéter avec un jeton JWT dont le dernier caractère de signature est altéré.
- **Résultat attendu** : Tous les endpoints `/api/*` protégés retournent `HTTP 401 Unauthorized`. Seuls `GET /health` et `POST /api/auth/login` restent accessibles anonymement.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-002 – Contrôle d'accès RBAC : restriction `AdminOnly` (`403 Forbidden`)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Connecté avec `k.khmissi@enterprise.internal` (`Workflow Designer`) et `enduser.test@corp.internal` (`End User`).
- **Données de test** : Endpoints réservés `AdminOnly` : `POST /api/users`, `DELETE /api/users/usr-1`, `GET /api/settings`, `PUT /api/settings`.
- **Étapes** :
  1. Appeler chacun de ces 4 endpoints avec le JWT du `Workflow Designer`, puis avec celui de l'`End User`.
- **Résultat attendu** : Tous les appels retournent `HTTP 403 Forbidden`. Aucune lecture de paramètres sensibles ni mutation d'utilisateur n'est effectuée.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-003 – Contrôle d'accès RBAC : restriction `DesignerOrAdmin` et commandes d'instance (`403 Forbidden`)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Connecté avec `enduser.test@corp.internal` (`End User`) et `k.khmissi@enterprise.internal` (`Workflow Designer`).
- **Données de test** :
  - Pour `End User` : `POST/DELETE /api/definitions`, `POST/PUT/DELETE /api/smartforms`, `POST /api/smartforms/{id}/publish`, `POST/PUT/DELETE /api/smartobjects`, `POST /api/instances/{id}/commands/suspend|resume|cancel|retry`.
  - Pour `Workflow Designer` : `POST /api/instances/{faultedId}/commands/retry` avec `mode: "retry_from_node"`.
- **Étapes** :
  1. Exécuter les requêtes listées avec le JWT de l'`End User`.
  2. Exécuter `retry_from_node` avec le JWT du `Workflow Designer`.
- **Résultat attendu** : Tous les appels retournent `HTTP 403 Forbidden`. `retry_from_node` est strictement réservé aux rôles administrateurs (`Admin`, `Administrator`, `BPM Administrator`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-004 – Interdiction de compléter la tâche assignée à un autre utilisateur (`403 FORBIDDEN`)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Tâche `Pending` assignée en `AssigneeType.User` à `karim.khmissi@corp.internal`.
- **Données de test** : JWT de `enduser.test@corp.internal` (non administrateur, non assigné).
- **Étapes** :
  1. Envoyer `POST /api/tasks/{taskId}/complete` avec le JWT de `enduser.test@corp.internal`.
- **Résultat attendu** : Réponse `HTTP 403 Forbidden` (`FORBIDDEN`). La tâche reste `Pending`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-005 – Protection Anti-IDOR sur le téléchargement et la suppression de pièces jointes (`403 FORBIDDEN`)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Pièce jointe `att-xyz` téléversée par `karim.khmissi@corp.internal` et liée à une instance dont `enduser.test@corp.internal` n'est ni l'initiateur ni un assigné de tâche.
- **Données de test** : JWT de `enduser.test@corp.internal` (tiers non autorisé) et JWT d'`adel.sebti@corp.internal` (Admin).
- **Étapes** :
  1. Avec le JWT de `enduser.test@corp.internal`, appeler `GET /api/attachments/att-xyz`.
  2. Avec le même JWT, appeler `DELETE /api/attachments/att-xyz`.
  3. Avec le JWT de l'Admin, appeler `GET /api/attachments/att-xyz`.
- **Résultat attendu** : Les étapes 1 et 2 retournent `HTTP 403 Forbidden` (`FORBIDDEN`, et dans l'UI : `Access denied (403): You are not authorized to download this attachment.`). L'étape 3 (Admin) retourne `HTTP 200`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-006 – Masquage des variables sensibles (`***`) dans ViewFlow et Events selon le rôle
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Instance démarrée avec les variables `salary = 95000`, `iban = "FR7630006000011234567890189"`, `apiToken = "tok_live_123"`, `Department = "Finance"`.
- **Données de test** : JWT de `enduser.test@corp.internal` (sans permission `instance.view_sensitive`) vs JWT de `a.sebti@enterprise.internal` (`Administrator`).
- **Étapes** :
  1. Appeler `GET /api/instances/{id}/flow` et `GET /api/instances/{id}/events` avec le JWT de l'`End User`.
  2. Répéter avec le JWT de l'`Administrator`.
- **Résultat attendu** : Pour l'`End User`, les valeurs des clés contenant `password`, `secret`, `token`, `iban`, `salary` (ou marquées `isSensitive: true`) sont systématiquement masquées en `"***"`, tandis que `Department` reste `"Finance"`. Pour l'`Administrator`, les valeurs réelles sont visibles.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-007 – Validation anti-malware (EICAR), magic bytes et spoofing d'extension à l'upload
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Utilisateur authentifié ; fichiers `eicar_test.pdf`, `faux_pdf.pdf`, `script_malveillant.exe`.
- **Données de test** :
  - `eicar_test.pdf` (signature EICAR).
  - `faux_pdf.pdf` (extension `.pdf`, contenu texte brut sans `%PDF-`).
  - `script_malveillant.exe` (extension interdite).
  - Nom malveillant `../../Windows/win.ini.pdf` (path traversal).
- **Étapes** :
  1. Envoyer `POST /api/attachments/upload` successivement avec `eicar_test.pdf`, `faux_pdf.pdf` et `script_malveillant.exe`.
  2. Envoyer un PDF valide nommé `../../evil\r\n.pdf`.
- **Résultat attendu** :
  - `eicar_test.pdf` → `HTTP 400` (`MALWARE_DETECTED`).
  - `faux_pdf.pdf` et `script_malveillant.exe` → `HTTP 400` (`INVALID_FILE_TYPE`).
  - Pour le nom avec path traversal / CRLF : le nom de fichier est assaini (aucun chemin parent ni saut de ligne), le fichier est stocké uniquement dans `BlobStorage:RootPath`, et l'en-tête `Content-Disposition` est nettoyé.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-008 – Protection contre l'injection XSS (`<script>`) dans les noms de workflows et champs texte
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : Connecté en tant que `DesignerOrAdmin`.
- **Données de test** : Nom de workflow `<script>alert('XSS')</script>` ; champ SmartForm texte `<img src=x onerror=alert(1)>`.
- **Étapes** :
  1. Envoyer `POST /api/definitions` avec `name = "<script>alert('XSS')</script>"`.
  2. Soumettre `<img src=x onerror=alert(1)>` dans un champ texte d'un SmartForm, puis consulter la valeur dans ViewFlow et SmartObjects.
- **Résultat attendu** : L'étape 1 est rejetée par `CreateWorkflowDefinitionValidator` (`HTTP 400`). À l'étape 2, React échappe automatiquement la chaîne : le texte brut s'affiche sans exécution de JavaScript (aucune boîte `alert`).
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-009 – Vérification des en-têtes HTTP de sécurité (CSP, X-Frame-Options, nosniff, Referrer-Policy)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Majeure
- **Type** : Sécurité
- **Prérequis** : API Backend démarrée.
- **Données de test** : Requête `GET /api/definitions` (authentifiée) et `GET /health`.
- **Étapes** :
  1. Inspecter tous les en-têtes de réponse HTTP retournés par l'API.
- **Résultat attendu** : Les en-têtes suivants sont présents et exacts :
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
  - `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'; base-uri 'self'`
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-010 – Limitation de débit (*Rate Limiting*) sur `/api/auth/login` et limite globale (`429`)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Critique
- **Type** : Sécurité
- **Prérequis** : API Backend démarrée avec configuration par défaut (`AuthPolicy = 30 req/min`, globale `300 req/min`).
- **Données de test** : 35 requêtes consécutives `POST /api/auth/login` depuis la même IP en moins de 60 secondes.
- **Étapes** :
  1. Exécuter une boucle de 35 requêtes `POST /api/auth/login` en quelques secondes.
  2. Attendre l'expiration de la fenêtre (1 minute) et réessayer 1 requête valide.
- **Résultat attendu** : Les 30 premières requêtes sont traitées (`401`/`200`), puis à partir de la 31e requête l'API retourne `HTTP 429 Too Many Requests`. Après expiration de la fenêtre d'une minute, l'accès est rétabli.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### SEC-011 – Politique CORS stricte (origines autorisées vs origine tierce)
- **Module** : Sécurité (`SEC`)
- **Priorité** : Majeure
- **Type** : Sécurité
- **Prérequis** : API Backend démarrée.
- **Données de test** : En-tête `Origin: http://localhost:5173` vs `Origin: https://evil.example.com`.
- **Étapes** :
  1. Envoyer une requête `OPTIONS /api/definitions` (preflight) avec `Origin: http://localhost:5173`.
  2. Envoyer la même requête avec `Origin: https://evil.example.com`.
- **Résultat attendu** : Pour `http://localhost:5173`, `Access-Control-Allow-Origin: http://localhost:5173` et `Access-Control-Allow-Credentials: true` sont présents. Pour `https://evil.example.com`, aucun en-tête `Access-Control-Allow-Origin` n'est renvoyé.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.12 Performance et temps de réponse (`PERF`)

#### PERF-001 – Temps de chargement initial de la SPA et navigation inter-vues
- **Module** : Performance (`PERF`)
- **Priorité** : Majeure
- **Type** : Performance
- **Prérequis** : Build de production (`npm run build && npm run preview`) ou serveur Vite ; onglet Performance/Network DevTools ouvert.
- **Données de test** : Accès à `/dashboard` puis navigation vers `/designer`, `/smartforms`, `/instances`, `/tasks`.
- **Étapes** :
  1. Mesurer le temps de chargement complet (*First Contentful Paint* / *Load*) à froid sur `/dashboard`.
  2. Mesurer le temps de bascule entre les 9 onglets de la barre latérale.
- **Résultat attendu** : Le chargement initial s'effectue en < 2 secondes sur réseau local ; la bascule d'onglet est quasi instantanée (< 300 ms) sans gel du thread principal.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### PERF-002 – Temps de réponse des endpoints API critiques sous charge nominale
- **Module** : Performance (`PERF`)
- **Priorité** : Majeure
- **Type** : Performance
- **Prérequis** : Backend connecté à SQL Server ou PostgreSQL avec au moins 50 instances et 50 tâches en base.
- **Données de test** : 20 appels séquentiels sur `GET /api/instances`, `GET /api/tasks`, `GET /api/analytics/kpis`, `POST /api/instances/start`.
- **Étapes** :
  1. Mesurer les temps de réponse P95 de chacun des 4 endpoints.
- **Résultat attendu** : Temps de réponse P95 < 500 ms pour les lectures (`GET`) et < 1 000 ms pour le démarrage d'instance (`POST /api/instances/start`), sans erreur `500` ni fuite mémoire.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### PERF-003 – Fluidité du Workflow Designer avec un graphe dense (50+ nœuds)
- **Module** : Performance (`PERF`)
- **Priorité** : Mineure
- **Type** : Performance
- **Prérequis** : Fichier JSON contenant 50 nœuds connectés importé dans `/designer`.
- **Données de test** : Graphe de 50 nœuds et 60 arêtes.
- **Étapes** :
  1. Importer le graphe dans le Workflow Designer.
  2. Effectuer des opérations de zoom, pan, déplacement d'un nœud, ouverture du `ConfigDrawer` et `Validate`.
- **Résultat attendu** : Le canvas reste fluide (déplacement et zoom réactifs sans blocage > 100 ms), la MiniMap se met à jour en temps réel et la validation statique s'exécute en < 300 ms.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### PERF-004 – Pagination et bornage des événements ViewFlow (`GET /api/instances/{id}/events`)
- **Module** : Performance (`PERF`)
- **Priorité** : Majeure
- **Type** : Performance
- **Prérequis** : Instance disposant d'un historique de plusieurs événements dans `InstanceEvents`.
- **Données de test** : `afterSeq=0&limit=5`, `afterSeq=5&limit=5`, `afterSeq=-10&limit=9999`.
- **Étapes** :
  1. Appeler `/api/instances/{id}/events?afterSeq=0&limit=5`, puis la page suivante avec `afterSeq=5&limit=5`.
  2. Appeler `/api/instances/{id}/events?afterSeq=-10&limit=9999`.
- **Résultat attendu** : La pagination retourne des tranches ordonnées sans doublon ni omission. Les valeurs hors bornes sont automatiquement bornées par le backend (`afterSeq` ≥ 0, `limit` borné dans `[1..200]`) sans erreur `500`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.13 Compatibilité — Navigateurs, responsive, thèmes et i18n (`COMP`)

#### COMP-001 – Compatibilité multi-navigateurs (Chrome, Edge, Firefox, Safari)
- **Module** : Compatibilité (`COMP`)
- **Priorité** : Majeure
- **Type** : UI
- **Prérequis** : Application accessible depuis les 4 navigateurs cibles.
- **Données de test** : Parcours bout-en-bout : login → création nœud Drag & Drop dans `/designer` → aperçu SmartForm → consultation ViewFlow.
- **Étapes** :
  1. Exécuter le parcours sur Chrome, Edge, Firefox et Safari.
  2. Vérifier la console JavaScript et le rendu CSS (polices, ombres, SVG, arêtes `@xyflow/react`).
- **Résultat attendu** : Aucun défaut bloquant de rendu, aucun décalage des poignées de connexion sur le canvas, aucune erreur console spécifique à un navigateur.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### COMP-002 – Responsive Design sur résolutions Desktop, Tablette et Mobile (`xl`, `lg`, `md`, `sm`)
- **Module** : Compatibilité (`COMP`)
- **Priorité** : Majeure
- **Type** : UI
- **Prérequis** : Outil de simulation d'appareils DevTools.
- **Données de test** : Largeurs `1920px` (`xl`), `1024px` (`lg`), `768px` (`md`), `390px` (`sm`).
- **Étapes** :
  1. Tester chaque résolution sur `/dashboard`, `/smartforms`, `/instances` et `/tasks`.
  2. Tester le bouton de repli/dépli de la barre latérale (`Collapse Sidebar` / `Expand Sidebar`, `w-64` ↔ `w-16`).
- **Résultat attendu** : Les grilles s'adaptent proprement (4 cols → 1–2 cols ; SmartForm 2 cols `md:grid-cols-2` → 1 col sur mobile ; barre de recherche masquée sous `md` ; nom profil masqué sous `lg` ; pilule auto-save masquée sous `xl`). Aucun débordement horizontal non désiré sur les vues principales.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### COMP-003 – 4 Design Systems (`Corporate`, `Microsoft`, `Apple`, `SAP`) et bascule Mode Sombre / Clair
- **Module** : Compatibilité (`COMP`)
- **Priorité** : Mineure
- **Type** : UI
- **Prérequis** : Utilisateur connecté.
- **Données de test** : Presets `Corporate Enterprise`, `Microsoft Fluent`, `Apple Human Interface`, `SAP Horizon / Fiori` + modes `Light` / `Dark`.
- **Étapes** :
  1. Ouvrir le sélecteur de thème (`ThemeSelector`) et activer successivement les 4 Design Systems.
  2. Basculer entre Mode Clair et Mode Sombre via le bouton du header puis recharger la page (`F5`).
- **Résultat attendu** : Les couleurs primaires, rayons de bordure, polices et la bordure supérieure orange 3 px de `SAP Horizon` s'appliquent immédiatement. La classe `.dark` est ajoutée/retirée sur `<html>`. Les clés `tamam_design_theme` et `tamam_theme` persistent après rechargement.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### COMP-004 – Internationalisation (`EN` / `FR` / `AR`) et bascule de direction globale `LTR` / `RTL`
- **Module** : Compatibilité (`COMP`)
- **Priorité** : Majeure
- **Type** : UI
- **Prérequis** : Utilisateur connecté.
- **Données de test** : Langues `English (LTR)`, `Français (LTR)`, `العربية (RTL)` + bouton manuel `Switch layout direction (LTR / RTL)`.
- **Étapes** :
  1. Sélectionner `Français (LTR)` dans le menu langue et vérifier la barre supérieure, la barre latérale (ex. `Tableau de bord`, `Studio de Workflow`, `Centre de tâches`) et la palette du Designer.
  2. Sélectionner `العربية (RTL)` et vérifier les attributs `dir="rtl"` et `lang="ar"` sur `<html>`, l'inversion de l'icône de repli de sidebar (`rtl:rotate-180`) et l'alignement droit-à-gauche.
  3. Tester la bascule manuelle `LTR / RTL` puis recharger la page (`F5`).
- **Résultat attendu** : Les attributs `lang` et `dir` de `<html>` se mettent à jour dynamiquement, la disposition RTL s'applique sans chevauchement dans le shell applicatif, et les clés `tamam_language` / `tamam_direction` persistent après rechargement. *(Note : voir section 10 concernant les libellés internes encore codés en anglais).*
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.14 Accessibilité (`A11Y`)

#### A11Y-001 – Navigation clavier, anneaux de focus visibles et fermeture par touche `Escape`
- **Module** : Accessibilité (`A11Y`)
- **Priorité** : Majeure
- **Type** : UI
- **Prérequis** : Utilisateur sur `/designer` et `/smartforms`, navigation au clavier uniquement (`Tab`, `Shift+Tab`, `Enter`, `Space`, `Escape`).
- **Données de test** : Menu débordement `⋯` de `HeaderToolbar`, champs de `SmartFormRenderer`.
- **Étapes** :
  1. Parcourir le header et la barre latérale à l'aide de `Tab`.
  2. Ouvrir le menu `⋯` (`More actions`) au clavier, appuyer sur `Escape` et vérifier où retourne le focus.
  3. Parcourir un SmartForm au clavier et soumettre via `Enter`.
- **Résultat attendu** : Tous les éléments interactifs affichent un anneau de focus visible (`focus:ring-2`). La touche `Escape` ferme le menu `⋯` et restitue le focus au bouton déclencheur. Aucun piège clavier bloquant.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### A11Y-002 – Sémantique HTML, landmarks et attributs ARIA sur les formulaires et alertes
- **Module** : Accessibilité (`A11Y`)
- **Priorité** : Majeure
- **Type** : UI
- **Prérequis** : Inspecteur DOM / lecteur d'écran (NVDA ou VoiceOver).
- **Données de test** : Soumission d'un SmartForm avec champ requis vide ; annulation d'instance dans ViewFlow avec raison < 10 car.
- **Étapes** :
  1. Vérifier la présence des landmarks `<header>`, `<aside>`, `<main>`, `<nav aria-label="Stage Breadcrumbs">`, `<section aria-label="ViewFlow Inspector for ...">`.
  2. Déclencher une erreur de validation sur un SmartForm et inspecter les attributs ARIA du champ en erreur et de son message.
  3. Déclencher un bandeau d'erreur et un bandeau de succès dans ViewFlow.
- **Résultat attendu** : Les champs invalides portent `aria-invalid="true"`, `aria-required="true"` et `aria-describedby` pointant vers le message d'erreur en `role="alert"`. Les retours de commandes ViewFlow utilisent `role="alert"` (erreurs) et `role="status"` (succès). Les boutons icônes disposent d'un `aria-label` explicite.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### A11Y-003 – Contraste des couleurs en Mode Clair et Mode Sombre sur les 4 Design Systems
- **Module** : Accessibilité (`A11Y`)
- **Priorité** : Mineure
- **Type** : UI
- **Prérequis** : Outil d'audit contraste (Lighthouse / axe DevTools).
- **Données de test** : Pages `/dashboard`, `/tasks`, `/instances` en Mode Clair et Mode Sombre.
- **Étapes** :
  1. Lancer un audit axe/Lighthouse sur les 3 pages en Mode Clair, puis en Mode Sombre.
- **Résultat attendu** : Aucun texte principal ni libellé de bouton n'est illisible ; ratio de contraste cible ≥ 4,5:1 pour le texte courant (WCAG 2.1 AA) et ≥ 3:1 pour les textes larges / composants d'interface.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

### 6.15 Tests non fonctionnels — Emails, health check, OpenAPI et persistance (`NFR`)

#### NFR-001 – Endpoint de santé (`GET /health`)
- **Module** : Tests non fonctionnels (`NFR`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : API Backend démarrée.
- **Données de test** : Requête anonyme `GET http://localhost:5000/health`.
- **Étapes** :
  1. Appeler `GET http://localhost:5000/health` sans jeton d'authentification.
- **Résultat attendu** : Réponse `HTTP 200 OK` contenant un JSON avec `status: "Healthy"`, `timestamp` valide et `engine`.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### NFR-002 – Documentation OpenAPI / Scalar disponible en `Development` et masquée hors `Development`
- **Module** : Tests non fonctionnels (`NFR`)
- **Priorité** : Mineure
- **Type** : Sécurité
- **Prérequis** : Backend lancé d'abord avec `ASPNETCORE_ENVIRONMENT=Development`, puis avec `ASPNETCORE_ENVIRONMENT=Production`.
- **Données de test** : URLs `/openapi/v1.json` et `/scalar/v1`.
- **Étapes** :
  1. En `Development`, ouvrir `/openapi/v1.json` et `/scalar/v1`.
  2. Redémarrer en `Production` et appeler les mêmes URLs.
- **Résultat attendu** : En `Development`, le schéma OpenAPI JSON (`HTTP 200`) et l'interface interactive Scalar se chargent correctement. En `Production`, ces routes ne sont plus exposées (`HTTP 404`) et l'en-tête `Strict-Transport-Security` (HSTS) est activé.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### NFR-003 – Envoi d'emails (`SmtpEmailService`) : interpolation de variables, mode dry-run / pickup / SMTP réel
- **Module** : Tests non fonctionnels (`NFR`)
- **Priorité** : Majeure
- **Type** : Fonctionnel
- **Prérequis** : Workflow contenant un nœud `SendEmail` avec tokens `{{EmployeeName}}`, `{{instanceId}}`, `{{workflowKey}}`, `{{initiatorId}}`.
- **Données de test** :
  - Mode A (défaut) : `smtp.enabled = false` (dry-run log).
  - Mode B : `Email:PickupDirectory` configuré vers un dossier local (ou serveur SMTP de test Mailpit/Papercut avec `smtp.enabled = true`).
- **Étapes** :
  1. Démarrer une instance en Mode A et consulter les logs console du backend.
  2. Configurer le Mode B, démarrer une seconde instance et inspecter le fichier `.eml` généré (ou la boîte Mailpit).
- **Résultat attendu** : Les variables `{{...}}` sont interpolées avec les valeurs réelles de l'instance. En Mode A, le log trace l'envoi simulé sans exception. En Mode B, l'email est effectivement déposé en `.eml` ou reçu par le serveur SMTP dans la limite du timeout de 15 s.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

#### NFR-004 – Persistance bi-SGBD (SQL Server & PostgreSQL) et persistance `localStorage` (Demo Mode)
- **Module** : Tests non fonctionnels (`NFR`)
- **Priorité** : Critique
- **Type** : Fonctionnel
- **Prérequis** : Accès à une instance SQL Server et à une instance PostgreSQL.
- **Données de test** : Création d'une définition, d'un SmartForm et d'une instance.
- **Étapes** :
  1. Configurer `Database:Provider = SqlServer`, démarrer l'API, vérifier la création automatique des 17 tables via `MigrateAsync()` et le seeding, puis créer/relire une définition après redémarrage.
  2. Configurer `Database:Provider = PostgreSql`, démarrer l'API, vérifier la création via `EnsureCreatedAsync()` et le seeding, puis créer/relire une définition après redémarrage.
  3. En **Demo Mode**, sauvegarder un workflow et un SmartForm, recharger la page (`F5`) et vérifier leur conservation via `localStorage` (`tamam_saved_workflows`, `tamam_smartforms_v1`).
- **Résultat attendu** : Les deux SGBD initialisent les 17 tables sans erreur, appliquent les contraintes d'unicité et de concurrence, et persistent durablement les données. Le Mode Démo conserve ses modifications locales après rechargement.
- **Résultat obtenu** : _(à remplir)_
- **Statut** : ⏳ À tester
- **Commentaires** : _(à remplir)_

---

## 7. Matrice de traçabilité

| Module | Fonctionnalité / Exigence métier | Cas de test associés | Nb |
| :--- | :--- | :--- | :---: |
| **AUTH** | Sélecteur de mode (Démo vs Connecté) et bascule à chaud | `AUTH-001`, `AUTH-002`, `AUTH-006` | 3 |
| **AUTH** | Connexion JWT, contrôle des erreurs, compte inactif (`403`) | `AUTH-002`, `AUTH-003`, `AUTH-004`, `AUTH-005` | 4 |
| **AUTH** | Profil `/api/auth/me`, changement de mot de passe, déconnexion | `AUTH-007`, `AUTH-008`, `AUTH-009` | 3 |
| **DASH** | KPIs exécutifs, approbations rapides, lancements rapides | `DASH-001`, `DASH-002`, `DASH-003` | 3 |
| **DASH** | Routage HTML5, deep-link `/designer/:key`, notifications & env | `DASH-004`, `DASH-005` | 2 |
| **WFD** | Modèles, 6 nœuds BPM, connexions, inspecteur `ConfigDrawer` | `WFD-001`, `WFD-002`, `WFD-003` | 3 |
| **WFD** | Sous-processus hiérarchiques `Stage` (drill-down & fil d'Ariane) | `WFD-004` | 1 |
| **WFD** | Validation statique, sauvegarde versionnée, Import/Export JSON, Simulateur | `WFD-005`, `WFD-006`, `WFD-007`, `WFD-008` | 4 |
| **FRM** | Concepteur (10 contrôles), génération depuis SmartObject, Lookup dynamique | `FRM-001`, `FRM-002`, `FRM-003` | 3 |
| **FRM** | Visibilité conditionnelle, validation zéro-trust, versionning immuable, concurrence | `FRM-004`, `FRM-005`, `FRM-006` | 3 |
| **FRM** | Soumission atomique, rejet non publié (`422`), idempotence, pièces jointes | `FRM-007`, `FRM-008`, `FRM-009`, `FRM-010` | 4 |
| **INST** | Exécution moteur (séquentiel, Décisions AND/OR, sous-graphes `Stage`) | `INST-001`, `INST-002`, `INST-003` | 3 |
| **INST** | Liste/filtres, ViewFlow, timeline paginée, diagnostics & bottlenecks | `INST-004`, `INST-005` | 2 |
| **INST** | Commandes opérationnelles (`Suspend`, `Resume`, `Cancel`, `Retry`), machine à états, idempotence | `INST-006`, `INST-007`, `INST-008`, `INST-009`, `INST-010` | 5 |
| **TASK** | Visibilité cloisonnée, complétion outcomes dynamiques, SmartForm `readonly`/`editable` | `TASK-001`, `TASK-002`, `TASK-003`, `TASK-004` | 4 |
| **TASK** | Blocage sur instance suspendue et concurrence optimiste sur tâches | `TASK-005`, `TASK-006` | 2 |
| **SMO** | CRUD schémas, champs typés & protection PK, CRUD/Upsert records, génération SmartForm | `SMO-001`, `SMO-002`, `SMO-003`, `SMO-004`, `SMO-005` | 5 |
| **ANA** | KPIs SLA, principal goulot et répartition par département | `ANA-001`, `ANA-002` | 2 |
| **ADM** | CRUD & filtrage utilisateurs, paramètres système SMTP/SQL & bornes | `ADM-001`, `ADM-002`, `ADM-003`, `ADM-004`, `ADM-005` | 5 |
| **ERR** | Anti-boucle 250 étapes, quotas, taille fichier, rollback tx, perte API, fail-fast | `ERR-001` à `ERR-007` | 7 |
| **SEC** | JWT `401`, RBAC `403`, Anti-IDOR, masquage `***`, EICAR/magic bytes, XSS, headers, rate limit, CORS | `SEC-001` à `SEC-011` | 11 |
| **PERF** | Chargement SPA, temps API P95, canvas 50+ nœuds, pagination ViewFlow | `PERF-001` à `PERF-004` | 4 |
| **COMP** | Multi-navigateurs, responsive, 4 Design Systems + Dark Mode, EN/FR/AR + RTL | `COMP-001` à `COMP-004` | 4 |
| **A11Y** | Clavier/focus/`Escape`, landmarks/ARIA, contrastes WCAG | `A11Y-001` à `A11Y-003` | 3 |
| **NFR** | `/health`, OpenAPI/Scalar, `SmtpEmailService`, bi-SGBD & `localStorage` | `NFR-001` à `NFR-004` | 4 |

---

## 8. Synthèse de recette

*(Tableau vierge à compléter à l'issue de la campagne d'exécution)*

| Module | Préfixe | Total | ✅ Validés | ❌ Échecs | ⚠️ Bloqués | ⏳ À tester | % Exécuté | % Réussite |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| Authentification & Comptes | `AUTH` | 9 | 0 | 0 | 0 | 9 | 0 % | 0 % |
| Tableau de bord & Navigation | `DASH` | 5 | 0 | 0 | 0 | 5 | 0 % | 0 % |
| Workflow Designer | `WFD` | 8 | 0 | 0 | 0 | 8 | 0 % | 0 % |
| SmartForms & Pièces jointes | `FRM` | 10 | 0 | 0 | 0 | 10 | 0 % | 0 % |
| Moteur, Instances & ViewFlow | `INST` | 10 | 0 | 0 | 0 | 10 | 0 % | 0 % |
| Centre de tâches | `TASK` | 6 | 0 | 0 | 0 | 6 | 0 % | 0 % |
| SmartObjects | `SMO` | 5 | 0 | 0 | 0 | 5 | 0 % | 0 % |
| Analytique & SLA | `ANA` | 2 | 0 | 0 | 0 | 2 | 0 % | 0 % |
| Administration | `ADM` | 5 | 0 | 0 | 0 | 5 | 0 % | 0 % |
| Gestion des erreurs & Limites | `ERR` | 7 | 0 | 0 | 0 | 7 | 0 % | 0 % |
| Sécurité | `SEC` | 11 | 0 | 0 | 0 | 11 | 0 % | 0 % |
| Performance | `PERF` | 4 | 0 | 0 | 0 | 4 | 0 % | 0 % |
| Compatibilité, Thèmes & i18n | `COMP` | 4 | 0 | 0 | 0 | 4 | 0 % | 0 % |
| Accessibilité | `A11Y` | 3 | 0 | 0 | 0 | 3 | 0 % | 0 % |
| Tests non fonctionnels | `NFR` | 4 | 0 | 0 | 0 | 4 | 0 % | 0 % |
| **TOTAL** | — | **93** | **0** | **0** | **0** | **93** | **0 %** | **0 %** |

---

## 9. Anomalies constatées

*(Tableau vierge à renseigner durant la campagne de recette)*

| ID Anomalie | Cas de test lié | Description de l'anomalie | Gravité *(Bloquante / Majeure / Mineure)* | Statut *(Ouverte / En cours / Corrigée / Fermée)* | Correctif / Référence PR |
| :--- | :---: | :--- | :---: | :---: | :--- |
| `ANO-001` | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ |
| `ANO-002` | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ |
| `ANO-003` | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ | _(à remplir)_ |

---

## 10. Hypothèses, zones ambiguës et points d'attention

L'audit exhaustif du code source a mis en évidence les éléments suivants à surveiller prioritairement lors de la recette :

### 10.1 Points d'attention techniques détectés dans le code
1. **Bascule `Demo Mode` → `Connected Mode` (`ModeSelectorModal.tsx`)** : dans `handleApply`, `repositoryManager.getRepository().login(...)` est appelé *avant* `repositoryManager.setMode('connected')`. Lorsque l'utilisateur passe de Démo à Connecté, `getRepository()` peut encore pointer sur `InMemoryWorkflowRepository` au moment du login (`AUTH-006`).
2. **Visibilité UI indépendante du rôle** : les 9 onglets de la barre latérale sont affichés pour tous les rôles côté frontend ; le cloisonnement RBAC (`403 Forbidden`) est appliqué côté backend uniquement (`SEC-002`, `SEC-003`).
3. **Couverture i18n partielle** : `src/i18n/translations.ts` traduit le shell (header, sidebar, profil, barre d'outils et palette du Designer), mais les vues métiers internes (`HomeDashboard`, `SmartFormsView`, `ActiveInstancesView`, `DataModelsView`, `TaskCenterView`, `AnalyticsView`, `UsersView`, `SettingsView`, modales) contiennent des libellés codés en dur en anglais (`COMP-004`). De même, `HomeDashboard` affiche `Welcome back, Mohamed Fetni 👋` en dur quel que soit l'utilisateur connecté.
4. **Contrôles UI non câblés / statiques** :
   - La barre de recherche globale du header (`⌘K`) conserve un état local `searchQuery` sans panneau de résultats ni raccourci clavier branché.
   - Le bouton `Connector Config` dans `DataModelsView` n'a pas de gestionnaire de clic.
   - Dans `SettingsView`, les champs `retentionDays`, `telemetryEnabled` et `mfaEnforced` existent dans le modèle API et l'état frontend mais ne sont pas rendus dans le formulaire UI.
5. **Gestion d'erreurs HTTP spécifiques côté backend** :
   - La création d'un utilisateur avec un email déjà existant (`POST /api/users`) ou le démarrage d'une instance sur une clé de workflow sans version active (`POST /api/instances/start`) lève une `InvalidOperationException` (interceptée par `UseExceptionHandler` en `500` plutôt qu'un `409` ou `404` dédié — à vérifier lors de `ADM-002` et `INST-001`).
6. **Incohérence de syntaxe de tokens email UI vs Backend** : `SendEmailConfig.tsx` propose des tokens à simples accolades (`{EmployeeName}`), tandis que `WorkflowExecutionEngine.cs` interpole des doubles accolades (`{{var}}`) (`WFD-003` / `NFR-003`).

### 10.2 Hypothèses retenues
- **Port Backend** : aucun `launchSettings.json` n'étant présent dans le dépôt, le port par défaut retenu est `http://localhost:5000` conformément à `.env.example` (`VITE_API_BASE_URL=http://localhost:5000/api`) et au `README.md`.
- **Base de données** : la recette nominale est supposée exécutée sur **SQL Server** (provider par défaut dans `appsettings.json`), avec un test de portabilité dédié sur **PostgreSQL** (`NFR-004`).
- **Compte `End User` actif** : le seul compte `End User` pré-initialisé (`usr-4`, `y.benali@enterprise.internal`) ayant le statut `Inactive`, la création d'un compte `End User` actif (`enduser.test@corp.internal` dans `ADM-002`) est supposée exécutée en amont des tests RBAC (`SEC-002` à `SEC-006`).
- **Seuils de performance** : aucun SLA de latence n'étant formalisé dans la documentation du projet, des seuils standards d'application métier interne (< 2 s au chargement initial, P95 < 500 ms en lecture API, < 1 000 ms en écriture/démarrage) ont été retenus pour `PERF-001` et `PERF-002`.

---

## 11. Validation finale

| Rôle | Nom et prénom | Date | Signature |
| :--- | :--- | :--- | :--- |
| **Responsable de Recette (QA)** | ______________________________ | ____ / ____ / ________ | ________________________ |
| **Responsable Produit (PO)** | ______________________________ | ____ / ____ / ________ | ________________________ |
| **Responsable Technique (Tech Lead)** | ______________________________ | ____ / ____ / ________ | ________________________ |

### Décision de mise en livraison

- [ ] **Accepté** *(aucune anomalie bloquante ni majeure ouverte)*
- [ ] **Accepté avec réserves** *(liste des réserves et échéances de correction ci-dessous)*
- [ ] **Refusé** *(anomalie(s) bloquante(s) empêchant la mise en production)*

**Commentaires / Réserves éventuelles :**

> _(à remplir lors de la clôture de recette)_
