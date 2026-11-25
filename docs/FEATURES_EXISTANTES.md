# Inventaire des Fonctionnalités Existantes — Tamam Workflow Studio

Ce document recense de manière exhaustive les fonctionnalités **actuellement implémentées** dans le projet `WorkflowK24` (*Tamam Workflow Studio*), leur niveau de maturité réel (vérifié dans le code) et leur équivalence fonctionnelle avec **Nintex K2**.

**Légende de maturité :**
- ✅ **Opérationnel** : Implémenté de bout en bout (UI + API + Persistance).
- 🟡 **Partiel / Prototype** : Fonctionnel en démo ou implémentation partielle nécessitant des compléments pour la production.
- ⚪ **Stub / UI uniquement** : Présent visuellement ou simulé, non encore branché à un vrai service métier.

---

## 1. Studio Visuel de Conception (Workflow Designer)

| Fonctionnalité | Équivalent Nintex K2 | Statut | Détails d'implémentation |
| :--- | :--- | :---: | :--- |
| **Canvas visuel Drag & Drop** | K2 Workflow Designer | ✅ | Basé sur `@xyflow/react` v12 : glisser-déposer depuis la palette, connexions dynamiques, zoom, pan, fit-view, minimap. |
| **6 Types de Nœuds BPM** | Events & Logic Steps | 🟡 | `Trigger` (Démarrage), `Approval` (Tâche humaine), `Decision` (Condition), `SendEmail` (Notification), `Stage` (Sous-processus), `End` (Fin). |
| **Sous-Workflows hiérarchiques (`StageNode`)** | Sub-Workflows / Phases | 🟡 | Création et édition visuelle d'un sous-graphe à l'intérieur d'un nœud `Stage`. Exécution backend présente mais à corriger si une tâche humaine met en pause le sous-workflow. |
| **Outcomes d'approbation dynamiques** | Task Outcomes | ✅ | Configuration d'issues multiples personnalisées (ex: *Approve*, *Reject*, *Request Revision*) avec poignées de sortie dédiées sur le canvas. |
| **Panneau d'inspection contextuel (`ConfigDrawer`)** | Properties Panel | ✅ | Formulaires dédiés par type de nœud (`TriggerConfig`, `ApprovalConfig`, `DecisionConfig`, `SendEmailConfig`, `StageConfig`, `EndEventConfig`). |
| **Templates métiers prêts à l'emploi** | Workflow Templates | ✅ | 3 modèles pré-configurés : *Employee Onboarding* (RH), *Executive Purchase Request* (Finance), *VPN & Server Admin Access* (IT Security) + Canvas vierge. |
| **Validation statique du graphe (`ValidationModal`)** | Design-time Validation | ✅ | Vérification structurelle du workflow avant publication/sauvegarde. |
| **Import / Export JSON (`JsonSchemaModal`)** | Package & Deploy (partiel) | ✅ | Export/import complet de la définition du graphe au format JSON + visualisation du schéma base de données. |
| **Simulateur interactif pas-à-pas (`SimulatorPanel`)** | Test / Debug Mode | ✅ | Exécution simulée sur le canvas avec surbrillance visuelle des nœuds actifs/réussis et journaux d'exécution en temps réel. |
| **Versionning des définitions** | Workflow Versioning | ✅ | Stockage versionné côté backend (`WorkflowDefinitions` + `WorkflowVersions`) avec suivi de la version active. |

---

## 2. Moteur d'Exécution & Gestion des Instances (Runtime Engine)

| Fonctionnalité | Équivalent Nintex K2 | Statut | Détails d'implémentation |
| :--- | :--- | :---: | :--- |
| **Démarrage d'instances via API** | K2 Workflow Runtime | ✅ | Endpoint `POST /api/instances/start` avec passage de variables de contexte JSON (`ContextVariablesJson`). |
| **Traversée de graphe côté serveur** | K2 Server Engine | 🟡 | `WorkflowExecutionEngine` gère le parcours séquentiel, les décisions simples (1re condition), la création de tâches et l'état terminal. |
| **Suivi des instances actives (`ActiveInstancesView`)** | K2 Management — Instances | 🟡 | Liste filtrable des instances (`RUNNING`, `PENDING`, `COMPLETED`, `FAILED`). Manque les actions d'administration (Suspend, Cancel, Retry, GoTo Activity). |
| **Détection des goulots d'étranglement** | Process Bottlenecks | ✅ | Endpoint `GET /api/instances/bottlenecks` identifiant les instances bloquées sur des tâches ou décisions. |
| **Journal d'audit chronologique (Timeline)** | ViewFlow / Audit Trail | ✅ | Table `WorkflowAuditLogs` + endpoint `GET /api/diagnostics/timeline/{instanceId}` retraçant chaque transition. |
| **Capture & diagnostic d'erreurs** | Error Profiles | ✅ | Table `WorkflowInstanceErrors` + endpoint `GET /api/diagnostics/failing/{instanceId}` (message, stack trace, snapshot des variables). |
| **Envoi d'emails automatisés** | Send Email Step | ⚪ | Nœud configuré dans le designer et appelé par le moteur, mais branché sur `LoggingEmailService` (log console uniquement, pas de SMTP réel). |

---

## 3. Centre de Tâches Humaines (Task Center / Worklist)

| Fonctionnalité | Équivalent Nintex K2 | Statut | Détails d'implémentation |
| :--- | :--- | :---: | :--- |
| **Boîte de réception des tâches (`TaskCenterView`)** | K2 Workspace / Worklist | ✅ | Affichage des tâches assignées par utilisateur ou groupe (`GET /api/tasks/assignee/{assigneeId}`). |
| **Traitement d'une tâche avec choix d'outcome** | Actioning a Task | ✅ | Endpoint `POST /api/tasks/{id}/complete` enregistrant l'issue choisie, le commentaire et faisant avancer automatiquement l'instance de workflow. |
| **Assignation Utilisateur ou Groupe** | Destination Users / Groups | 🟡 | Supporté dans le modèle de données (`AssigneeType.User` / `Group`) et l'UI ; pas encore de règles avancées (Round-Robin, Escalade, Out-of-Office, Slot/Quorum). |
| **Notifications in-app (cloche header)** | Task Notifications | 🟡 | Badge de compteur et menu déroulant des tâches en attente (pas de temps réel SignalR/WebSocket ni de notifications push/email). |

---

## 4. Modèles de Données Métier (SmartObjects)

| Fonctionnalité | Équivalent Nintex K2 | Statut | Détails d'implémentation |
| :--- | :--- | :---: | :--- |
| **Création & édition de schémas (`DataModelsView`)** | K2 SmartObject Designer | ✅ | Définition d'entités typées (`String`, `Number`, `Boolean`, `DateTime`, `GUID`), clé primaire, champs requis, catégories (`HR`, `Finance`, `IT`, `Core`, `Custom`). |
| **CRUD dynamique des données (Records)** | SmartBox Data Storage | ✅ | Stockage et édition des enregistrements via API (`/api/smartobjects/{id}/records`) en base SQL Server / PostgreSQL. |
| **Liaison SmartObjects ↔ Workflows** | SmartObject Step | ⚪ | Les SmartObjects existent en tant que référentiel de données, mais aucun nœud de workflow ne permet encore de lire/écrire un SmartObject pendant l'exécution. |

---

## 5. Analytique, Administration & Expérience Utilisateur (UX)

| Fonctionnalité | Équivalent Nintex K2 | Statut | Détails d'implémentation |
| :--- | :--- | :---: | :--- |
| **Tableau de bord Exécutif (`HomeDashboard`)** | K2 Workspace Home | ✅ | Vue synthétique des processus, tâches en attente et raccourcis vers le studio. |
| **Rapports & KPIs SLA (`AnalyticsView`)** | K2 Process Overview Reports | 🟡 | Endpoint `GET /api/analytics/kpis` calculant temps moyen de complétion, taux SLA, principal goulot et volume par département. |
| **Gestion des utilisateurs (`UsersView`)** | K2 Users & Roles | 🟡 | CRUD basique (`GET`/`POST`/`DELETE /api/users`) et rôles (`BPM Administrator`, `Process Manager`, `Workflow Developer`, `End User`). Pas encore de gestion de groupes dynamique ni d'AD/LDAP/Entra ID. |
| **Paramètres système (`SettingsView`)** | K2 Management Settings | 🟡 | Persistance clé-valeur (`GET`/`PUT /api/settings`) pour SMTP, SQL, rétention, télémétrie et MFA (paramètres stockés mais non encore consommés par les services runtime). |
| **Mode Démo vs Mode Connecté** | — *(Avantage Tamam)* | ✅ | Bascule dynamique via `ModeSelectorModal` : utilisation 100 % navigateur (Démo) ou connectée à l'API .NET 10. |
| **Multilingue EN / FR / AR + RTL natif** | — *(Avantage Tamam)* | 🟡 | Dictionnaire trilingue (`translations.ts`) et bascule LTR/RTL globale. Reste à traduire quelques libellés codés en dur (sidebar, certaines vues). |
| **4 Design Systems + Dark/Light Mode** | — *(Avantage Tamam)* | ✅ | Presets `Corporate`, `Microsoft`, `Apple`, `SAP` + mode sombre persistant (`useTheme`). |
| **Support bi-SGBD (SQL Server & PostgreSQL)** | SQL Server uniquement sur K2 | ✅ | Configuration par simple switch dans `appsettings.json` + scripts DDL et migrations EF Core. |
| **Documentation API OpenAPI / Scalar** | K2 Workflow REST API | ✅ | Documentation interactive disponible sur `/scalar/v1` et `/openapi/v1.json`. |
