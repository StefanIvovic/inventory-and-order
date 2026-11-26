# Data Model: Lookup Select

## LookupSelect inputs and model

| Name | Type | Rule |
|---|---|---|
| `typeCode` | string | required; the lookup list, for example `staff-status` |
| `label` | string | required; the field label, also used in the search button's `aria-label` |
| `multiple` | boolean | default `false` |
| `valueBy` | `'code'` \| `'id'` | default `'code'`; which part of a value the form receives |
| `emptyLabel` | string \| null | single mode only; adds a first option with value `null` (for example "All") |
| `service` | string | default `/api/identity`; the service whose `/lookups` endpoint is used |
| `value` | model: `string \| null` (single), `readonly string[]` (multiple) | the chosen code(s) or id(s); `null` or `[]` when nothing is chosen |
| `disabled`, `readonly`, `required`, `touched` | boolean inputs | from Signal Forms |
| `errors` | `ValidationError[]` input | from Signal Forms; shown as `mat-error` |
| `touch` | output | emitted on blur |

## LookupPageRequest (`LookupsApi.page`)

| Field | Type | Rule |
|---|---|---|
| `q` | string? | trimmed, sent only when not empty |
| `ids` / `codes` | string[]? | repeated query parameters, at most 200 |
| `size` | number | 50 for option pages, the number of names asked for in a names request |
| `after` | string? | the previous page's `nextCursor` |

## LookupValuePage (answer, 020)

| Field | Type |
|---|---|
| `items` | `{ id, code, name }[]` |
| `nextCursor` | string \| null |
