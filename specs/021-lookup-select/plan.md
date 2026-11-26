# Implementation Plan: Lookup Select

**Branch**: `021-lookup-select` | **Date**: 2026-11-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/021-lookup-select/spec.md`

## Summary

A reusable `app-lookup-select` in the backoffice gives search, single select and multiple select over any lookup list.
- **Building blocks** (R1, R2): it implements Signal Forms' `FormValueControl`, so `[formField]` works, and it also works as a plain filter. It renders a Material autocomplete, with chips for multiple choice.
- **Search** (R3): runs only on Enter or the search button, as the owner decided.
- **Paging** (R4): pages of 50 load as the user scrolls.
- **Chosen values** (R5): their names come from one `codes`/`ids` request, and unknown names show as "Unavailable".
- **Users page**: the Status filter becomes this component, and its email search becomes explicit (Enter or button). No backend change.

## Technical Context

**Language/Version**: TypeScript 6.0, Angular 22.3

**Primary Dependencies**: Angular Material 22.3 (autocomplete, chips, form-field, input, icon button, progress-bar), Signal Forms. No new packages.

**Storage**: none

**Testing**: Vitest 5 with jsdom; Playwright end-to-end in `e2e-test`

**Target Platform**: evergreen browsers

**Project Type**: web frontend (existing backoffice)

**Performance Goals**:
- opening a field makes one request
- showing N chosen values makes at most one extra request
- typing makes no request

**Constraints**:
- no comments
- no option lists in the UI
- no codes shown in place of names
- explicit search only

**Scale/Scope**:
- about 3 new files, plus changes to `lookups-api.ts` and `users-page.ts`
- 1 e2e spec updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Independently deployable | ✅ | Backoffice only; it uses the existing 020 endpoint |
| Lookups (v1.6.0) | ✅ | The component addresses lists by type code; options and names always come from the service |
| VI. Authorization | ✅ | The lookup endpoint is signed-in; the Users page controls still follow the API flags |
| VII. Tests isolated | ✅ | Unit tests with `HttpTestingController`; e2e in `e2e-test` |
| IX. No comments, X. No attribution | ✅ | |
| II, III, IV, V, VIII | ✅ n/a | No backend or package change |

**Gate: PASS.** **Post-design re-check**: PASS.

## Project Structure

### Documentation

```text
specs/021-lookup-select/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/ui.md
└── tasks.md
```

### Source Code

```text
src/web/backoffice/src/app/
├── lookups/
│   ├── lookups-api.ts            + page()
│   ├── lookup-select.ts          the component
│   └── lookup-select.spec.ts
└── users/
    ├── users-page.ts             Status filter uses app-lookup-select; explicit email search
    └── users-page.spec.ts
tests/e2e/specs/backoffice-staff.spec.ts   presses Enter to search
```

**Structure Decision**: the component sits in `lookups/` next to its API service.

## Complexity Tracking

No deviations.
