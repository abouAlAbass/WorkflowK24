---
name: tamam-frontend-conventions
description: >-
  Frontend React 19 / TypeScript conventions for Tamam Workflow Studio. Trigger
  whenever creating or editing React components, views, Zustand stores,
  repositories, or form rendering logic.
---

# Tamam Frontend Conventions (React 19 + TypeScript)

## Architecture & State
- **Never** call raw `fetch()` directly inside UI components or Zustand stores when a repository method exists; route API calls through `src/services/` (`workflowRepository.ts`, `httpWorkflowRepository.ts`).
- Keep `SmartFormRenderer.tsx` as the single shared runtime for Preview, Service Portal, and Task Inbox.
- Never hardcode business fallbacks (e.g. `workflowName.includes(...)`) or mock data in views.

## API Contracts & Security
- **Never** send `completedBy`, `userId`, or `role` in request bodies for authorization; the backend extracts identity from the JWT.
- Send an `Idempotency-Key` header (UUID via `crypto.randomUUID()`) on form submissions and operational commands (`suspend`, `resume`, `cancel`, `retry`).
- Send `expectedVersion` on entity updates and handle `409 Conflict` (`CONCURRENT_MODIFICATION`) gracefully in the UI.
- Strip hidden fields (masked by visibility rules) before submitting payloads or `formPatch`.

## UI, Accessibility (A11y) & i18n
- Every form control must have a predictable `id` paired with `<label htmlFor={...}>`.
- Mark invalid inputs with `aria-invalid="true"` and `aria-describedby`, and wrap global error banners in `role="alert"`.
- Preserve native **EN / FR / AR (RTL)** support: use logical CSS/Tailwind properties (`ms-`, `me-`, `ps-`, `pe-`, `text-start`) instead of hardcoded `left`/`right` when adding new layouts.

## Context-Saving Reading Rule
- Read `.tsx` / `.ts` files in **large chunks of 250–400 lines minimum** (or full file when <= 400 lines). Never paginate in 30-line slices.