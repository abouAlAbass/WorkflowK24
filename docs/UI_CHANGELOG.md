# Tamam Workflow Studio - UI/UX Polish Changelog

This document summarizes the UI/UX enhancements, regrouped controls, and restyled elements implemented in **Tamam Workflow Studio**.
**Guiding Principle:** Zero features removed. Every existing button, action, and capability remains 100% reachable and functional.

---

## 1. Top Toolbar Decluttering & Reorganization (`HeaderToolbar.tsx`)

### Primary Visible Actions
At standard desktop resolutions (including 1366px), the toolbar is clean and uncluttered with only **3 primary action buttons**, **1 status chip**, and **1 overflow menu**:
1. **Test Simulation** (Green gradient action button with Play icon) – Starts simulator or executes steps when active.
2. **Validate** (Dynamic warning/error badge button) – Opens workflow validation modal with count indicators.
3. **Save Definition** (Named version database save) – Commits named version to repository with visual confirmation.
4. **Auto-save Status Indicator** (New badge) – Displays live state: "All changes saved" / "Unsaved changes" alongside auto-save timestamp.
5. **Runtime Mode Chip** (`Demo Mode` / `Connected: Live API`) – Styled as an executive status pill with pulsing indicator rather than an action button. Clicking opens runtime selector.

### New Accessible "More" (⋯) Overflow Menu
The following actions were relocated into a consolidated overflow menu to prevent visual clutter:
- **Export JSON**: Exports active canvas schema as a `.json` file.
- **Import JSON**: Opens JSON schema modal with paste/upload tabs.
- **Saved DB (`count`)**: Displays total saved versions with an inline submenu preview to quick-load or delete versions, plus a direct link to the full DB management tab.
- **Reset Canvas**: Clears workflow canvas with destructive action styling.
- **Keyboard & Click Accessibility**: Fully supports `Tab`, `Enter`, `Escape` to close, and outside-click dismiss.

---

## 2. Truncated Workflow Title Fix

- **Problem:** The workflow name input was constrained to an arbitrary width, truncating titles prematurely while adjacent badges competed for space.
- **Solution:** Re-structured the header branding region using flex-1 responsive container with `truncate` (`text-overflow: ellipsis`) and an explicit `title={workflowName}` attribute. Hovering reveals the complete title in native browser tooltip.
- **Studio Badge Repositioning:** Moved the "Tamam Workflow Studio" badge into the secondary metadata line so it never competes for horizontal space with the title.

---

## 3. Dark Mode Controls Consolidation

- **Problem:** Two separate, desynchronized theme buttons existed—one in the executive header (`AppLayout.tsx`) and another with text "Dark Mode" in `HeaderToolbar.tsx`.
- **Solution:** Removed the duplicate button from `HeaderToolbar.tsx`. Preserved a **single canonical theme toggle** in the top header navigation with an accessible `aria-label`, tooltip, and focus outline.
- **State Synchronization:** Backed by a singleton `useTheme` hook with DOM sync (`.dark` class on root and persistence in `localStorage`).

---

## 4. Canvas Node Readability & Typography (`StartNode`, `ApprovalNode`, `DecisionNode`, `SendEmailNode`, `EndNode`)

- **Font Sizes:**
  - Node titles increased to `14px` (`text-sm font-bold`).
  - Secondary labels, types, and event names increased to `12px` (`text-xs font-semibold`), satisfying the >= 12px rule.
- **Dimensions:** Node containers enlarged slightly (`min-w-[280px]` to `min-w-[290px]`) with generous padding (`p-3.5`).
- **Connection Handles:** All connection handles sized to `14px` (`!w-3.5 !h-3.5`), exceeding the minimum 12px hit-target requirement with hover scale effects.
- **Edge Labels:** Padded `CustomEdge` labels with backdrop blur, rounded pills, and hover deletion to eliminate node overlap.

---

## 5. Minimap Restyling (`WorkflowCanvas.tsx`)

- **Dimensions:** Resized to a compact **160px × 100px** viewport.
- **Visual Styling:** Semi-transparent frosted glass (`bg-white/85 dark:bg-slate-900/85 backdrop-blur-md`), rounded corners (`rounded-xl`), and crisp border (`border-slate-300 dark:border-slate-700`).
- **Interactive Navigation:** Enabled `pannable={true}` and `zoomable={true}` for canvas panning.
- **Grouping:** Grouped directly with ReactFlow zoom controls (`+`, `−`, fit view) in the bottom-right corner.

---

## 6. Auto-Save vs. Save Definition Clarification

- **Auto-Save Mechanism:** Canvas state auto-saves in real-time to memory and session state. The inspector drawer footer explicitly states:
  > `"Draft auto-saved at [timestamp] • Use Save Definition for named versions"`
- **Save Definition Action:** Toolbar button labelled **Save Definition** explicitly saves a named, persistent version to the repository.
- **Toolbar Status Indicator:** Displays "All changes saved" with emerald check or "Unsaved changes" with amber clock.

---

## 7. Design Tokens & Palette Standardization

- **Design System:** Created CSS variables in `src/index.css` and documented in `docs/DESIGN_TOKENS.md`:
  - Brand: `--color-brand` (`#2563EB` light / `#3B82F6` dark)
  - Success: `--color-success` (`#059669` light / `#10B981` dark)
  - Warning: `--color-warning` (`#D97706` light / `#F59E0B` dark)
  - Danger: `--color-danger` (`#DC2626` light / `#EF4444` dark)
  - Info: `--color-info` (`#0284C7` light / `#38BDF8` dark)
  - Semantic node tokens for Triggers, Tasks, Notifications, Logic, and End events.

---

## 8. Compact Toolbox (`NodePalette.tsx`)

- **Screen Fit:** Reduced card padding from `p-3` to `p-2` and vertical spacing to `space-y-1.5`. All 5 component cards and 4 sample templates fit comfortably on a 1080p display without vertical scrolling.
- **Descriptions:** Displayed on a single line with `truncate` (`text-overflow: ellipsis`) and full description available via hover tooltip (`title`).
- **Functionality:** Both drag-and-drop and the `+` quick-add button remain fully operational.

---

## 9. Accessibility & WCAG AA Contrast

- **Contrast Ratios:**
  - Replaced low-contrast gray text (`#94A3B8` ~2.5:1 on light backgrounds) with `#475569` (5.5:1) and `#64748B` (4.6:1), exceeding the WCAG AA 4.5:1 requirement.
  - In dark mode, text tokens (`#F8FAFC` and `#94A3B8`) exceed 6.5:1 contrast against `#111827`.
- **Keyboard & Screen Reader Support:**
  - Added descriptive `aria-label`s to all icon-only buttons (theme toggle, language switcher, sidebar collapse, modal close buttons, node delete).
  - Visible focus rings (`focus:ring-2 focus:ring-[#2563EB]`) on all interactive controls.

---

## 10. Internationalization (i18n) & RTL Readiness

- **Translation Layer:** Added `src/i18n/translations.ts` containing resource dictionaries for English (default), French, and Arabic.
- **RTL Support:** Replaced directional CSS classes with CSS logical properties (`ms-`, `me-`, `ps-`, `pe-`, `inset-inline`, `border-inline-end`).
- **Dynamic Direction Switcher:** Root element dynamically controls `dir="ltr"` or `dir="rtl"`.
- **Typography:** Configured font stack in `index.css`: `'Plus Jakarta Sans', 'Noto Sans Arabic', 'Cairo', 'Tajawal', Inter, sans-serif`.
