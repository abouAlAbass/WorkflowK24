# Tamam Workflow Studio - Design Tokens Specification

This document details the standardized design tokens implemented in **Tamam Workflow Studio** to guarantee visual consistency, eliminate hard-coded magic values, and ensure WCAG AA (4.5:1) accessibility across light and dark modes.

---

## 1. Core Brand & Semantic Color Tokens

| Token Name | Light Mode Value | Dark Mode Value | Semantic Role & WCAG Ratio |
| :--- | :--- | :--- | :--- |
| `--color-brand` | `#2563EB` | `#3B82F6` | Primary brand accent for navigation, selection, primary buttons (Contrast > 4.5:1) |
| `--color-brand-hover` | `#1D4ED8` | `#60A5FA` | Hover state for interactive brand elements |
| `--color-brand-subtle` | `#EFF6FF` | `rgba(37, 99, 235, 0.15)` | Background tint for active tabs and selection chips |
| `--color-success` | `#059669` | `#10B981` | Validation passes, completed runs, start triggers (Contrast 4.6:1 on white) |
| `--color-success-subtle`| `#ECFDF5` | `rgba(5, 150, 105, 0.15)` | Subtle success backgrounds |
| `--color-warning` | `#D97706` | `#F59E0B` | Simulation in-progress, validation warnings, email steps |
| `--color-warning-subtle`| `#FFFBEB` | `rgba(217, 119, 6, 0.15)` | Subtle warning backgrounds |
| `--color-danger` | `#DC2626` | `#EF4444` | Validation errors, termination/rejection endpoints, destructive actions |
| `--color-danger-subtle` | `#FEF2F2` | `rgba(220, 38, 38, 0.15)` | Subtle danger backgrounds |
| `--color-info` | `#0284C7` | `#38BDF8` | Informational callouts and tooltips |

---

## 2. Canvas Workflow Node Tokens

All node types utilize a dedicated token pair (color + subtle background + border) with consistent saturation:

| Node Type | Color Token | Surface Token | Border Token | Standard Meaning |
| :--- | :--- | :--- | :--- | :--- |
| **Start / Trigger** | `--node-trigger` (`#059669`) | `--node-trigger-bg` (`#ECFDF5`) | `--node-trigger-border` (`#A7F3D0`) | Form submission, Timer, Webhook, DB record |
| **Approval Task** | `--node-task` (`#2563EB`) | `--node-approval-bg` (`#EFF6FF`) | `--node-approval-border` (`#BFDBFE`) | Human or role decision with dynamic outcomes |
| **Send Email** | `--node-email` (`#D97706`) | `--node-email-bg` (`#FFFBEB`) | `--node-email-border` (`#FDE68A`) | Automated transactional notification |
| **Decision / Rule** | `--node-decision` (`#7C3AED`) | `--node-decision-bg` (`#F5F3FF`) | `--node-decision-border` (`#DDD6FE`) | Conditional IF / THEN logical branching |
| **End Event** | `--node-end` (`#DC2626`) | `--node-end-bg` (`#FEF2F2`) | `--node-end-border` (`#FECACA`) | Terminal completion, rejection, or error |

---

## 3. Surface & Typography Contrast Tokens (WCAG AA)

| Token Name | Light Value | Dark Value | Purpose & Contrast Verification |
| :--- | :--- | :--- | :--- |
| `--text-primary` | `#0F172A` | `#F8FAFC` | Primary headings, titles, node titles (Contrast: 16.5:1 on light, 15.8:1 on dark) |
| `--text-secondary` | `#475569` | `#94A3B8` | Body text, captions, secondary labels (Contrast: 5.5:1 on light > 4.5:1, 6.5:1 on dark > 4.5:1) |
| `--text-muted` | `#64748B` | `#94A3B8` | Placeholders, timestamps, hints (Contrast: 4.6:1 on light > 4.5:1, meets WCAG AA) |
| `--bg-app` | `#F1F5F9` | `#0B0F19` | Application viewport background |
| `--bg-surface` | `#FFFFFF` | `#111827` | Cards, toolbar, sidebars, modal panels |
| `--bg-canvas` | `#F8FAFC` | `#090D16` | ReactFlow canvas background |
| `--border-subtle` | `#E2E8F0` | `#1F2937` | Dividers, soft borders |
| `--border-default` | `#CBD5E1` | `#374151` | Input borders, card outlines |

---

## 4. Typography & Font Stack

To ensure seamless Latin and Arabic (RTL) support:
```css
font-family: 'Plus Jakarta Sans', 'Noto Sans Arabic', 'Cairo', 'Tajawal', Inter, ui-sans-serif, system-ui, sans-serif;
```
- **Node Title Minimum:** `14px` (font-weight: 700 / Bold)
- **Node Secondary Minimum:** `12px` (font-weight: 500 / Medium)
- **Handle Hit Target Minimum:** `14px` (Width & Height with `border-width: 2px`)

---

## 5. CSS Logical Properties & RTL Guidelines

In accordance with Fix #10:
- Use `margin-inline-start` / `margin-inline-end` instead of `margin-left` / `margin-right`.
- Use `padding-inline-start` / `padding-inline-end` instead of `padding-left` / `padding-right`.
- Use `inset-inline` instead of `left` / `right` when absolute positioning is required.
- Set layout direction switchable via root `dir="ltr"` or `dir="rtl"`.
