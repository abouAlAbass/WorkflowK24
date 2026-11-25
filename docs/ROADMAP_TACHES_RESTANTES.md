# Roadmap & Tâches Restantes — Vers un Produit Fini et Commercialisable

Ce plan d'action détaille l'ensemble des tâches techniques, fonctionnelles et d'industrialisation nécessaires pour transformer **Tamam Workflow Studio** de son état actuel (prototype avancé / POC) en un **produit commercialisable de niveau Enterprise** capable de concurrencer Nintex K2.

---

## Vue d'ensemble des Phases

| Phase | Objectif | Durée estimée *(équipe 2-3 devs)* | Jalon Commercial |
| :--- | :--- | :---: | :--- |
| **Phase 0 (P0)** | Sécurité, Fiabilité & Correction des bugs bloquants | **3 à 4 semaines** | **Beta Privée** (déployable chez un 1er client pilote) |
| **Phase 1 (P1)** | Socle Produit Commercial (Formulaires, Connecteurs, Moteur complet) | **8 à 10 semaines** | **V1.0 Commerciale (GA)** — Vendable en PME/ETI |
| **Phase 2 (P2)** | Parité Enterprise Nintex K2 (Multi-tenant, SSO, SLA, Gouvernance) | **8 à 12 semaines** | **V2.0 Enterprise** — Vendable Grands Comptes & SaaS |
| **Phase 3 (P3)** | Différenciation IA & Écosystème | **Continu (post-V2)** | **Leadership & Croissance** |

---

## Phase 0 (P0) — Bloqueurs Critiques de Sécurité & Stabilité ✅ *(100 % Terminé — Prêt pour Beta Privée / Pilote)*

### Sécurité & Authentification (Backend)
- [x] **Protéger tous les endpoints API** : Ajout de `.RequireAuthorization()` sur tous les groupes (`/api/definitions`, `/api/instances`, `/api/tasks`, `/api/smartobjects`, `/api/users`, `/api/analytics`, `/api/settings`, `/api/diagnostics`) et des politiques RBAC (`AdminOnly`, `DesignerOrAdmin`).
- [x] **Remplacer l'authentification mock (`AuthEndpoints.cs`)** : Vérification réelle en base `SystemUsers` avec hachage cryptographique **PBKDF2-SHA256** (100 000 itérations, comparaison en temps constant), contrôle du statut `Active`, `/api/auth/me` et `/api/auth/change-password`. *(Reste Refresh Tokens en P1).*
- [x] **Supprimer les secrets codés en dur** : Suppression de la clé JWT fallback en dur dans `Program.cs` et `JwtTokenService.cs` avec validation *fail-fast* au démarrage (`>= 32 octets`).
- [x] **Sécuriser les requêtes et les entrées** : Ajout du **Rate Limiting ASP.NET Core** (`AuthPolicy` anti-brute-force + limiteur global), **Security Headers Middleware** (`CSP`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, `HSTS`), et validation stricte des JSON de graphes dans `CreateWorkflowDefinitionValidator` (taille max 1 Mo, max 500 nœuds, IDs uniques, anti-`<script>`).

### Correction du Moteur d'Exécution & Infrastructure
- [x] **Corriger le bug des sous-workflows `Stage`** : Pile d'exécution hiérarchique (`ExecutionStackJson`) ajoutée sur `WorkflowInstance` + `ResumeAfterTaskAsync` dans `WorkflowExecutionEngine.cs`. Le workflow parent se met bien en pause lorsqu'un sous-workflow s'arrête sur un nœud `approval` et ne reprend qu'à la complétion du sous-workflow.
- [x] **Protéger contre les cycles infinis** : Limite `MaxExecutionSteps = 250` dans `WorkflowExecutionEngine` enregistrant proprement une erreur `MAX_STEPS_EXCEEDED` sans `StackOverflowException`.
- [x] **Implémenter l'évaluation multi-conditions** : Support complet `AND` / `OR` dans `EvaluateConditionRule` + `DecisionConfig.tsx`, résolution JSON insensible à la casse et chemins imbriqués (`user.department`), opérateurs `>=`, `<=`, `not_contains`, `starts_with`, `ends_with`, `is_empty`, `is_not_empty`, booléens et dates ISO.
- [x] **Brancher un vrai service Email (`SmtpEmailService`)** : Remplacement de `LoggingEmailService` par `SmtpEmailService` consommant dynamiquement `SystemSettings` (`smtp.server`, `smtp.port`, `smtp.security`, credentials, mode pickup/live) + interpolation dynamique `{{requester}}`, `{{instanceId}}`, `{{amount}}`, `{{approver.email}}` dans `WorkflowExecutionEngine`.
- [x] **Aligner les Migrations EF Core** : Suppression des DDL bruts dans `DatabaseInitializer.cs`, correction des types `.HasColumnType("text")` incompatibles SQL Server et génération de la migration EF Core `AddEnterpriseEntitiesAndP0Security`.

### Finitions Frontend critiques
- [x] **Ajouter un vrai routeur URL** (`useUrlRouter.ts`) : Synchronisation bidirectionnelle HTML5 History API (`/dashboard`, `/designer/:workflowKey`, `/instances/:id`, `/tasks/:id`, `/smartobjects`, `/analytics`, `/users`, `/settings`) avec support Back/Forward et chargement par lien profond.
- [x] **Connecter l'identité utilisateur dans `AppLayout.tsx`** : Lecture dynamique de l'utilisateur authentifié depuis la session JWT + menu profil interactif (Bascule Mode Démo/Connecté, Changement de mot de passe PBKDF2 en direct, Déconnexion).
- [x] **Aligner `HttpWorkflowRepository` avec l'API** : Implémentation de `DELETE /api/definitions/{key}` (backend + frontend), extraction récursive des vrais outcomes dynamiques d'approbation depuis le graphe (y compris dans les `Stage`) et calcul serveur réel de `progressPercentage`, `durationSeconds` et `department`.
- [x] **Finaliser la traduction EN / FR / AR** : Internationalisation complète des menus de navigation, en-têtes déroulants, notifications et profil dans `translations.ts` et `AppLayout.tsx` avec support RTL natif.

---

## Phase 1 (P1) — V1.0 Commerciale *(Produit vendable et autonome)*

### 1. Concepteur de Formulaires No-Code (Équivalent K2 SmartForms) — ✅ *100 % Terminé*
> *Sans formulaires, les clients doivent coder leur propre front pour démarrer un workflow ou valider une tâche. C'est le cœur de la proposition de valeur de Nintex K2.*
- [x] **Form Designer Drag & Drop (`SmartFormsView.tsx` & `SmartFormRenderer.tsx`)** : Champs texte, zone multiligne, nombre, devise (`$`), date, case à cocher, liste déroulante statique ou **liée dynamiquement à un SmartObject**, upload de pièces jointes, **tableaux répétables (`repeater`)**, en-têtes de sections, grille 50 %/100 %, et **visibilité conditionnelle dynamique** (`equals`, `not_equals`, `greater_than`, `less_than`, `contains`, `is_not_empty`).
- [x] **Génération automatique de formulaire depuis un SmartObject** (1 clic : *Generate SmartForm* depuis `DataModelsView.tsx`, `SmartFormsView.tsx` et endpoint `POST /api/smartforms/generate/{smartObjectId}`).
- [x] **Liaison Formulaire ↔ Nœuds & Centre de Tâches** : Liaison du formulaire de démarrage sur `TriggerNode` (`TriggerConfig.tsx` + synchronisation 1 clic vers les variables du workflow) et formulaire de revue/édition (`readonly` / `editable`) sur `ApprovalNode` (`ApprovalConfig.tsx` + rendu interactif dans `TaskCenterView.tsx`).
- [x] **Gestion des pièces jointes (`LocalBlobStorageService.cs` & `/api/attachments`)** : Stockage sécurisé avec blocage d'extensions exécutables, limite 10 Mo, détection de signature antivirus EICAR, empreinte d'intégrité **SHA-256** et modale de prévisualisation intégrée (PDF & Images).

### 2. Enrichissement du Moteur BPM (Parité fonctionnelle cœur)
- [ ] **Nœuds de contrôle avancés** :
  - `Parallel Split & Join` (Exécution parallèle de plusieurs approbations simultanées avec règles de quorum : *Tous*, *Majorité*, *Premier répondant*).
  - `Timer / Delay` (Attente d'une durée ou d'une date précise).
  - `Set Variable / Expression` (Calculs, concaténation, formules).
  - `SmartObject Action` (Lire, Créer, Mettre à jour ou Supprimer un enregistrement SmartObject directement dans le workflow).
- [ ] **Connecteur HTTP / REST / Webhook (Nœud d'intégration universel)** : Appeler n'importe quelle API externe (ERP, CRM, SIRH) avec authentification (API Key, Bearer, OAuth2), mapping JSONPath de la réponse vers les variables du workflow et politique de retry exponentiel.
- [ ] **Déclencheurs réels (Triggers)** :
  - Déclenchement par **Webhook entrant** sécurisé (`POST /api/webhooks/{key}`).
  - Déclenchement planifié (**CRON / Scheduler**).
- [ ] **Gestion avancée des Tâches Humaines** :
  - **Réassignation / Délégation** de tâche (`POST /api/tasks/{id}/delegate`).
  - **Escalade SLA & Rappels automatiques** (relance email avant échéance, escalade au N+1 après expiration).
  - **Gestion des absences (Out-of-Office)** : redirection automatique des tâches sur une période donnée.
- [ ] **Contrôle opérationnel des Instances** :
  - Endpoints & UI pour **Suspendre**, **Reprendre**, **Annuler**, **Relancer une étape en erreur (Retry)** et **Voir le graphe en temps réel (ViewFlow)** avec le chemin parcouru surligné.
- [ ] **Moteur asynchrone & résilient** : Activer la persistance durable Wolverine (Transactional Outbox sur PostgreSQL/SQL Server) + notifications temps réel **SignalR** vers le Frontend.

### 3. Refactoring Frontend & Expérience Produit
- [ ] **Découper `useWorkflowStore.ts` (2000 lignes)** en stores dédiés (`useCanvasStore`, `useAuthStore`, `useUiStore`) + introduire **TanStack Query** pour le cache, la pagination et la synchronisation serveur.
- [ ] **Activer la recherche globale (`⌘K` Command Palette)** sur les workflows, instances, tâches et SmartObjects.
- [ ] **Pagination & filtres serveur** sur toutes les tables (`Instances`, `Tâches`, `SmartObjects`, `Utilisateurs`).

### 4. Packaging & Déploiement Commercial
- [ ] **Docker & Docker Compose** prêts pour la production (API + Frontend Nginx + PostgreSQL/SQL Server).
- [ ] **Pipeline CI/CD** (GitHub Actions / GitLab CI) : lint, tests unitaires & intégration, scan de vulnérabilités, build d'images Docker.
- [ ] **Installation On-Premise simplifiée** : Documentation d'installation complète + script de déploiement Windows Server (IIS) et Linux (Docker/Kubernetes).

---

## Phase 2 (P2) — V2.0 Enterprise & SaaS *(Grands Comptes, Secteur Public, SaaS)*

### Architecture SaaS & Sécurité Enterprise
- [ ] **Multi-Tenancy complet** : Isolation par `TenantId` (EF Core Global Query Filters ou schémas/bases dédiés pour les grands comptes), gestion des abonnements et quotas par tenant.
- [ ] **SSO Enterprise & Synchronisation d'annuaire** :
  - **Microsoft Entra ID (Azure AD)**, **Active Directory (LDAP/Kerberos)**, **Okta**, **Keycloak** (OIDC / SAML 2.0).
  - Synchronisation automatique des utilisateurs, groupes et hiérarchie managériale (`Manager` pour approbation N+1 dynamique).
- [ ] **Audit de conformité (RGPD / ISO 27001 / SOC 2)** : Journaux d'audit immuables (qui a modifié quel workflow, qui a approuvé quoi, à quelle heure, depuis quelle IP), politiques de rétention et anonymisation.

### Connecteurs Natifs (Service Brokers) & Génération Documentaire
- [ ] **Connecteurs natifs clés** :
  - **Microsoft 365** (SharePoint, Teams Adaptive Cards pour approuver directement dans Teams, Outlook, Excel).
  - **Connecteur SQL natif** (Exécution de requêtes / procédures stockées sur bases externes SQL Server, PostgreSQL, Oracle).
  - **Signature Électronique** (DocuSign, Yousign, Adobe Sign).
  - **Génération de documents PDF / Word** à partir de templates (contrats, bons de commande, attestations RH).
- [ ] **Promotion multi-environnements réelle (`Dev → Staging → Production`)** : Packaging (`.tamam` / `.zip`) regroupant Workflows + SmartObjects + Formulaires avec gestion des variables d'environnement et déploiement en un clic (équivalent *K2 Package & Deploy*).

### Observabilité & Haute Disponibilité
- [ ] **OpenTelemetry** (Traces distribuées, métriques Prometheus, dashboards Grafana).
- [ ] **Déploiement Kubernetes (Helm Chart)** avec scaling horizontal des workers Wolverine.
- [ ] **Système de licences** : Vérification de licence signée (On-Premise) et facturation Stripe (SaaS).

---

## Phase 3 (P3) — Différenciation IA & Écosystème *(Horizon 6-12 mois)*

- [ ] **Copilot IA de génération de Workflows & Formulaires** : Décrire un processus en langage naturel (FR/EN/AR) → génération automatique du graphe React Flow, des SmartObjects et des formulaires associés.
- [ ] **Nœud IA / Extraction Documentaire (IDP)** : Extraction automatique de données depuis des factures PDF, CIN/passeports, bons de commande (OCR + LLM) directement dans le workflow.
- [ ] **Marketplace de Templates métiers** : Pack RH, Pack Achats/Finance, Pack ITSM, Pack Conformité/Banque installables en un clic.
- [ ] **Application Mobile / PWA** : Validation rapide des tâches d'approbation sur smartphone avec notifications push (iOS/Android).

---

## Checklist "Go-Live Commercial" (Critères d'acceptation V1.0)

Avant de signer le premier contrat client payant, **100 % des critères suivants** doivent être validés :
1. ✅ Tous les endpoints API sont protégés par authentification + RBAC ; aucun secret en dur.
2. ✅ Un client peut concevoir un **Formulaire + SmartObject + Workflow d'approbation multi-niveaux + Email réel + Appel API REST** sans écrire de code.
3. ✅ Les tâches d'approbation disposent de rappels automatiques, escalades SLA, délégation et liens profonds par email.
4. ✅ Le déploiement On-Premise (Docker/IIS) prend moins de 30 minutes et passe un test de charge de 100 instances simultanées sans erreur.
5. ✅ Les tests automatisés (Backend + Frontend + E2E Playwright) passent à 100 % dans la CI.
6. ✅ La documentation utilisateur, administrateur et API (Scalar) est à jour en français et en anglais.
