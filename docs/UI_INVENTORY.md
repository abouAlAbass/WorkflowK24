# Tamam Workflow Studio - UI Inventory & Feature Audit

This document is the baseline inventory of all UI elements, top-bar actions, toolbox components, and panel configuration fields in **Tamam Workflow Studio**.
**Rule:** Zero features or capabilities may be deleted. Every item listed here must remain fully reachable, functional, and verified before and after UI/UX refinements.

---

## 1. Executive Top Header (`AppLayout.tsx`)

| Item ID | Control / Label | Type | Purpose / Action | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `hdr-brand` | TAMAM WORKFLOW STUDIO (v4.8 Enterprise) | Branding & Badge | App identification | Present | Retained & polished |
| `hdr-search` | Search Workflows & Tasks (`⌘K`) | Input | Global search filter | Present | Retained |
| `hdr-env` | Environment Selector (`Development`, `Staging`, `Production`) | Dropdown | Switches workspace active environment | Present | Retained |
| `hdr-theme` | Dark / Light Mode Toggle Button | Button (Icon) | Switches app-wide theme (Light ↔ Dark) | Present | Kept as single canonical toggle with tooltip & aria-label |
| `hdr-notif` | Notifications (`Bell` icon + pending badge) | Dropdown Button | Displays pending action items list | Present | Retained |
| `hdr-user` | User Profile (Avatar + Alex Vance / BPM Admin) | Display | Shows active logged-in user profile | Present | Retained |

---

## 2. Global Sidebar Navigation (`AppLayout.tsx`)

| Item ID | Nav Item Label | Type | Destination Tab | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `nav-dashboard` | Home Dashboard | Navigation Button | `dashboard` | Present | Retained |
| `nav-designer` | Workflow Designer (Badge: saved count) | Navigation Button | `designer` | Present | Retained |
| `nav-instances` | Active Instances (Badge: active count) | Navigation Button | `instances` | Present | Retained |
| `nav-smartobjects` | Data Models (SmartObjects) | Navigation Button | `smartobjects` | Present | Retained |
| `nav-tasks` | Task Center (Badge: pending tasks) | Navigation Button | `tasks` | Present | Retained |
| `nav-analytics` | Reports & Analytics | Navigation Button | `analytics` | Present | Retained |
| `nav-users` | Users & Access | Navigation Button | `users` | Present | Retained |
| `nav-settings` | System Settings | Navigation Button | `settings` | Present | Retained |
| `nav-collapse` | Collapse/Expand Sidebar Toggle | Button (Chevron) | Toggles sidebar between 64px and 256px | Present | Retained |

---

## 3. Workflow Designer Top Toolbar (`HeaderToolbar.tsx`)

| Item ID | Control / Label | Current Form | Target Location / Refinement | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `tb-title` | Workflow Name Input | Text Input (Squeezed/Truncated) | Flex-1 readable title with ellipsis + full tooltip | Present | Fixed (no truncation squeeze, hover tooltip) |
| `tb-studio-badge` | "Tamam Workflow Studio" Tag | Badge next to title | Repositioned so it never overlaps or squeezes title | Present | Repositioned & clean |
| `tb-sim-test` | Test Simulation | Primary Action Button (Green Play) | Primary visible button on toolbar | Present | Retained as Primary Action |
| `tb-sim-step` | Next Step (Simulation Active) | Action Button (Amber Step) | Visible during active simulation | Present | Retained |
| `tb-sim-stop` | Stop (Simulation Active) | Action Button (Square) | Visible during active simulation | Present | Retained |
| `tb-validate` | Validate (with error/warning count badge) | Primary Action Button | Opens Validation Report Modal | Present | Retained as Primary Action |
| `tb-save-def` | Save Definition ("Saved to DB!" confirmation) | Primary Action Button | Saves named definition to repository/db | Present | Retained as Primary Action with explicit naming |
| `tb-save-status` | Save status indicator | *Missing in original* | Toolbar indicator: "Unsaved changes" / "All changes saved" | Added | Added per Fix #6 |
| `tb-export-json` | Export JSON | Top-level Button | Relocated into "More" (⋯) overflow menu | Present | Moved to Overflow Menu (identical behavior) |
| `tb-import-json` | Import JSON | Top-level Button | Relocated into "More" (⋯) overflow menu | Present | Moved to Overflow Menu (identical behavior) |
| `tb-saved-db` | Saved DB (with count badge e.g. `(0)`) | Top-level Dropdown | Relocated into "More" (⋯) overflow menu with submenu / drawer access | Present | Moved to Overflow Menu (keeps count & list) |
| `tb-reset` | Reset Canvas (`RotateCcw` icon) | Top-level Button | Relocated into "More" (⋯) overflow menu | Present | Moved to Overflow Menu (identical behavior) |
| `tb-mode-badge` | Demo Mode / Connected Mode Badge | Status Button | Styled as badge/chip status indicator (clickable) | Present | Styled as Status Chip / Badge |
| `tb-theme-toggle`| Dark/Light Mode Button | Duplicate Button in Toolbar | Merged into header toggle (Fix #3) | Present | Consolidated into single header toggle |

---

## 4. Left Toolbox / Node Palette (`NodePalette.tsx`)

| Item ID | Control / Label | Type | Action / Purpose | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `pal-count` | `{nodes.length} Nodes` | Badge | Shows canvas node count | Present | Retained |
| `pal-tmpl-onboard` | 📋 Onboarding Template | Button | Loads employee onboarding workflow | Present | Retained |
| `pal-tmpl-expense` | 💳 Expense Appr. Template | Button | Loads purchase request workflow | Present | Retained |
| `pal-tmpl-itaccess`| 🔑 IT System Template | Button | Loads IT access workflow | Present | Retained |
| `pal-tmpl-blank` | ✨ Blank Canvas | Button | Clears canvas for new design | Present | Retained |
| `pal-node-trigger` | Start / Trigger (Play icon) | Drag Item & Quick Add (+) | Triggers (Form, Webhook, Schedule, DB) | Present | Compact card, single-line + tooltip |
| `pal-node-approval`| Approval Task (Check icon) | Drag Item & Quick Add (+) | Tasks (Dynamic outcome handles) | Present | Compact card, single-line + tooltip |
| `pal-node-email` | Send Email (Mail icon) | Drag Item & Quick Add (+) | Notifications (Automated emails) | Present | Compact card, single-line + tooltip |
| `pal-node-decision`| Decision / Rule (Branch icon) | Drag Item & Quick Add (+) | Logic (IF / THEN branching) | Present | Compact card, single-line + tooltip |
| `pal-node-end` | End Event (Flag icon) | Drag Item & Quick Add (+) | Terminal outcome node | Present | Compact card, single-line + tooltip |

---

## 5. Workflow Canvas & Canvas Tools (`WorkflowCanvas.tsx` & `CustomEdge.tsx`)

| Item ID | Control / Component | Type | Purpose | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `canvas-grid` | Background (Dots) | Visual Grid | Canvas alignment dots | Present | Retained with theme styling |
| `canvas-controls`| Zoom In, Zoom Out, Fit View, Lock | Button Group | Canvas viewport controls | Present | Grouped with restyled minimap |
| `canvas-minimap` | MiniMap | Visual Minimap | Canvas navigation & bird-eye view | Present | Restyled to 160x100, subtle shadow, rounded |
| `edge-smooth` | CustomEdge (smoothstep) | Connection Edge | Inter-node flow line | Present | Retained with smooth paths |
| `edge-label` | Edge Label Pill | Label Badge | Displays outcome branch label | Present | Padded, no overlap, hover delete |
| `edge-delete` | Delete Edge Button (`X`) | Button on hover | Removes connection edge | Present | Retained |

---

## 6. Canvas Nodes (`StartNode`, `ApprovalNode`, `DecisionNode`, `SendEmailNode`, `EndNode`)

| Item ID | Node / Elements | Font Size Before | Target Refinement | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `node-trigger` | Title, Trigger Type, Event Name, Payload Vars count, Outbound Handle | Header: 10px/14px, secondary: 10-11px | Titles >= 13px, secondary >= 12px, handle >= 12px target | Present | Enhanced readability |
| `node-approval`| Title, User/Group badge, Assignee name, Dynamic Decision Outcomes, Inbound & Outbound handles | Header: 10px/14px, secondary: 10-11px | Titles >= 13px, secondary >= 12px, handle >= 12px target | Present | Enhanced readability |
| `node-decision`| Title, Rule condition expression, True branch handle, False branch handle, Inbound handle | Header: 10px/14px, secondary: 10-11px | Titles >= 13px, secondary >= 12px, handle >= 12px target | Present | Enhanced readability |
| `node-email` | Title, Recipient To, Subject, Inbound & Outbound handles | Header: 10px/14px, secondary: 10-11px | Titles >= 13px, secondary >= 12px, handle >= 12px target | Present | Enhanced readability |
| `node-end` | Title, Terminal status badge, Completion message, Inbound handle | Header: 10px/14px, secondary: 10-11px | Titles >= 13px, secondary >= 12px, handle >= 12px target | Present | Enhanced readability |

---

## 7. Configuration Inspector Drawer (`ConfigDrawer.tsx` & Sub-components)

| Item ID | Form Field / Control | Sub-Component | Purpose | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `cfg-header-del`| Delete Node Button (`Trash2`) | Header | Deletes selected node | Present | Retained |
| `cfg-header-cls`| Close Inspector Button (`X`) | Header | Deselects node / closes drawer | Present | Retained |
| `cfg-trg-title` | Trigger Title / Label | `TriggerConfig` | Edits node label | Present | Retained |
| `cfg-trg-type` | Trigger Event Type Select | `TriggerConfig` | Form, Schedule, Webhook, Record | Present | Retained |
| `cfg-trg-event` | Event Identifier Name | `TriggerConfig` | Sets trigger event name | Present | Retained |
| `cfg-trg-vars` | Payload Variables List (Remove button) | `TriggerConfig` | Lists variables available downstream | Present | Retained |
| `cfg-trg-addvar`| Add Variable Form (Name, Type, Default, Add) | `TriggerConfig` | Adds input variable | Present | Retained |
| `cfg-app-title` | Task Title / Label | `ApprovalConfig` | Edits approval task name | Present | Retained |
| `cfg-app-assign`| Assignment Type Toggle (User vs Group) | `ApprovalConfig` | Toggles assignment mode | Present | Retained |
| `cfg-app-user` | Assignee Email / Group Input + Datalist + Quick select | `ApprovalConfig` | Sets task assignee | Present | Retained |
| `cfg-app-outcomes`| Dynamic Outcomes List (Color picker, Label, Delete) | `ApprovalConfig` | Manages outbound branch handles | Present | Retained |
| `cfg-app-addout`| Add Outcome Form (Label, Color Presets, Add button) | `ApprovalConfig` | Creates new outcome handle | Present | Retained |
| `cfg-app-sla` | Due Date SLA (Days) | `ApprovalConfig` | Sets task SLA deadline | Present | Retained |
| `cfg-app-reass` | Allow Task Reassignment Checkbox | `ApprovalConfig` | SLA reassignment permission | Present | Retained |
| `cfg-dec-title` | Decision Title / Label | `DecisionConfig` | Edits decision rule name | Present | Retained |
| `cfg-dec-field` | Target Field Variable | `DecisionConfig` | Target field in condition | Present | Retained |
| `cfg-dec-op` | Comparison Operator Select | `DecisionConfig` | >, <, ==, !=, contains | Present | Retained |
| `cfg-dec-val` | Comparison Value Input | `DecisionConfig` | Threshold value | Present | Retained |
| `cfg-dec-lblt` | Outbound Branch Label: True (If Met) | `DecisionConfig` | Label for positive branch | Present | Retained |
| `cfg-dec-lblf` | Outbound Branch Label: False (Else) | `DecisionConfig` | Label for negative branch | Present | Retained |
| `cfg-eml-title` | Task Title / Label | `SendEmailConfig` | Edits notification task name | Present | Retained |
| `cfg-eml-to` | Recipient Email (To) + Token Insert buttons | `SendEmailConfig` | Sets recipient address | Present | Retained |
| `cfg-eml-subj` | Email Subject Line | `SendEmailConfig` | Sets subject template | Present | Retained |
| `cfg-eml-body` | Email Message Body + Token Insert buttons | `SendEmailConfig` | Sets email template body | Present | Retained |
| `cfg-eml-html` | Send as Rich HTML Checkbox | `SendEmailConfig` | Toggles HTML mode | Present | Retained |
| `cfg-end-title` | End Event Title / Label | `EndEventConfig` | Edits terminal step name | Present | Retained |
| `cfg-end-status`| Execution Terminal Status Select | `EndEventConfig` | Completed, Rejected, Terminated | Present | Retained |
| `cfg-end-msg` | Completion Return Log Message Textarea | `EndEventConfig` | Sets final log message | Present | Retained |
| `cfg-footer-txt`| Auto-save confirmation footer | Footer | Explicitly states "Draft auto-saved" with timestamp | Present | Clarified per Fix #6 |

---

## 8. Modals & Execution Simulator

| Item ID | Control / Action | Modal / Panel | Purpose | Status Before | Status After |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `sim-step` | Step Forward | `SimulatorPanel` | Steps through workflow execution | Present | Retained |
| `sim-stop` | Close Sim (`Square` icon) | `SimulatorPanel` | Stops simulation | Present | Retained |
| `sim-logs` | Live Execution Logs Feed | `SimulatorPanel` | Displays timestamped logs per node | Present | Retained |
| `json-tab-exp` | Export Tab | `JsonSchemaModal` | Displays JSON schema for download/copy | Present | Retained |
| `json-copy` | Copy JSON | `JsonSchemaModal` | Copies schema to clipboard | Present | Retained |
| `json-download`| Download JSON | `JsonSchemaModal` | Downloads .json file | Present | Retained |
| `json-tab-imp` | Import Tab | `JsonSchemaModal` | Paste JSON definition | Present | Retained |
| `json-file-up` | File Upload | `JsonSchemaModal` | Imports schema from file | Present | Retained |
| `json-tab-db` | Database Tab | `JsonSchemaModal` | Named version DB storage | Present | Retained |
| `json-save-db` | Save Named Version | `JsonSchemaModal` | Commits version to DB | Present | Retained |
| `json-load-db` | Load Version from DB | `JsonSchemaModal` | Loads saved definition | Present | Retained |
| `json-del-db` | Delete Version from DB | `JsonSchemaModal` | Removes saved definition | Present | Retained |
| `val-node-sel` | Click Issue to Select Node | `ValidationModal` | Jumps to invalid node on canvas | Present | Retained |
| `val-close` | Close Modal | `ValidationModal` | Closes validation report | Present | Retained |
| `mode-demo` | Select Demo Mode | `ModeSelectorModal` | In-memory sandbox mode | Present | Retained |
| `mode-conn` | Select Connected Mode | `ModeSelectorModal` | Real .NET 10 API mode with login | Present | Retained |
| `mode-login` | Email, Password, Connect | `ModeSelectorModal` | Authenticates against backend | Present | Retained |

---

## Verification Summary
- **Total Cataloged Controls:** 74 controls/fields/actions.
- **Removed Functionalities:** **0** (Zero).
- **Relocated Functionalities:** Export JSON, Import JSON, Saved DB (with badge), Reset Canvas (grouped into overflow ⋯ menu).
- **Consolidated Controls:** Theme toggle (merged duplicate into single top header toggle).
