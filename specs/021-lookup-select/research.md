# Research: Lookup Select

Read on 2026-11-26 from the installed packages: Angular 22.3 Signal Forms (`@angular/forms/signals`, `FormValueControl` in `signals.d.ts`), Angular Material 22.3 (autocomplete, chips), Vitest 5 with jsdom 31. The lookups endpoint is from 020, with the 0.4.1 paging fix.

## R1. Signal Forms integration

- **Decision**: `LookupSelect` implements `FormValueControl<LookupSelectValue>`, where `LookupSelectValue = string | null | readonly string[]`.
  - `value` is a `model()`. In single mode it holds a code or id, or `null`. In multiple mode it holds a list.
  - The optional `disabled`, `readonly`, `required`, `errors` and `touched` inputs come from the form. The `touch` output fires on blur.
- **Use in a form**: `[formField]="f.status"` binds it like a native input.
- **Use as a filter**: `[value]` with `(valueChange)`.
- **Why**: this is the custom-control contract Signal Forms documents (`FormValueControl`). It needs no `ControlValueAccessor`, which belongs to Reactive Forms (018 chose Signal Forms).
- **Rejected**: implementing `MatFormFieldControl` so the component could sit inside a caller's `mat-form-field`. That is more code (focus, describedby, empty state), and the component can render its own `mat-form-field`, with the label as an input.

## R2. Building blocks

- **Single mode**:
  - a `mat-form-field` with a text input and `matAutocomplete`
  - a search icon button as `matSuffix`
  - when the panel is closed, the input shows the chosen value's name
- **Multiple mode**: a `mat-chip-grid` with one `mat-chip-row` per chosen value, and the same input as `matChipInputFor` plus `matAutocomplete`.
- **Empty choice**: when `emptyLabel` is set (single mode only), the first option is that label with value `null`. The Users page uses "All".
- **Chosen values in the list**: in multiple mode they show a check mark, and picking one again does nothing.
- **Clear**: an optional single field without `emptyLabel` shows a clear icon button (`aria-label="Clear <label>"`) while a value is chosen. It sets the value to `null`. A required field has no clear button.

## R3. Explicit search (owner decision, 2026-11-26)

- **Typing** only updates the text; it never sends a request.
- **Searching**:
  - the search button runs the search with the trimmed text (empty text means no filter)
  - Enter runs the search when `MatAutocompleteTrigger.activeOption` is null
  - when an option is highlighted, Enter is left to the autocomplete, which picks the option
- **Each search** restarts at the first page and opens the panel.
- **Stale answers**: a generation counter makes an answer that arrives after a newer search be ignored.
- **Replaces 019**: this replaces the 300 ms automatic search of 019 research R10 and its contract.
- **Users page**: the "Search email" box becomes a text field with a search button. Enter or the button applies `q` and restarts with the total. The 300 ms timer and `searchDelay` are removed.

## R4. Paging

- **Size**: pages of 50 (`size=50`), and `nextCursor` is kept.
- **Next page**: when the autocomplete panel opens, the component listens to the panel element's scroll (`MatAutocomplete.panel`). When `scrollTop + clientHeight >= scrollHeight - 48` and a `nextCursor` exists and no request is running, it asks for the next page with `after` and appends the result.
- **Rejected**: a "Load more" option. Scrolling is the expected behaviour for long lists.
- **First page**: loaded on the first panel open, and kept until a new search.

## R5. Names of chosen values

- **Lookup**: when the value has codes or ids whose names are unknown, the component requests them with `codes=` or `ids=` (repeated parameters) in chunks of 200 (the endpoint limit), one request per chunk, all sent together, and stores the names in a map. Names that come with option pages are stored too.
- **Missing names**: a chosen value the answer doesn't contain shows as "Unavailable". If the request fails, every unknown value shows "Unavailable". Codes are never shown (owner rule, `ui-holds-no-lists`).
- **When it runs**: whenever the value changes from outside to codes or ids whose names are unknown, and not for values just picked from the list.

## R6. API

- **New method**: `LookupsApi.page(typeCode, { q?, ids?, codes?, size, after? }, service?)` returns `{ items, nextCursor }`.
- **Service address**: `service` defaults to `/api/identity`, as the `service` input on the component.
- **Existing method**: `values(typeCode)` stays as it is (shell, Users page rows).

## R7. Errors and states

- **Panel states**: a progress bar while loading, "No matches" when a page is empty, and "Options could not be loaded." with a "Try again" button that repeats the last request.
- **Retry banner**: 503 `identity-unavailable` is already handled by the interceptor's retry banner. The request waits, and the panel shows the loading state.

## R8. Accessibility and keyboard

- **Roles**: Material autocomplete provides `role="combobox"`, `aria-expanded`, `aria-controls` and `aria-activedescendant` on the input, and `role="listbox"` and `role="option"` in the panel.
- **Labels**: the search button has `aria-label="Search <label>"`, and each chip's remove button has `aria-label="Remove <name>"`.
- **Backspace**: on an empty input in multiple mode it removes the last chip. `mat-chip-grid` does this by default.

## R9. Testing

- **Approach**: jsdom has no layout, so tests dispatch a `scroll` event on the panel. With zero sizes, the "near the end" check passes.
- **Signal Forms**: a small host component with `form()` checks the value binding, the required error and disabled.
- **Overlay**: options are read from the panel that `aria-controls` points to (019 lesson).

## R10. Findings during implementation (2026-11-26)

- **The panel needs an option to open.** Material shows the autocomplete panel, and emits `opened`, only when it has at least one option. Without one, the field could never open and load. A "loading" option now shows until the first page arrives.
- **Enter after a keyboard pick.** Material picks the highlighted option on Enter without marking the event handled, so the field's own Enter handler then searched too. The field now skips the search for the key press that picked an option.
- **Backspace.** Material's chip input needs two presses (focus the chip, then remove it). As the spec asks, one Backspace on an empty input removes the last chip.
- **Errors.** `mat-error` depends on Material's error state, which isn't reliable for a custom control. The form's messages show as an error-styled hint once the field is touched, and the outline still turns red through an error state matcher.
- **Clicking × or a chip's remove button** focuses the field, which opens the options and loads the first page. This is expected, and the tests cover it.
- **Tests open the panel through `MatAutocompleteTrigger.openPanel()`.** jsdom doesn't open the panel on focus. The real-browser behaviour is checked in the e2e staff test: open, type, Enter to search, pick Disabled.
- **The e2e flake was not caused by 021.** Playwright could fill a dialog before Material's autofocus moved focus back to the first field, so the password landed in the Email field. The tests now wait for that focus (staff and tenants). The suite then passed 8 runs in a row.
- **Bundle.** Initial raw size 646.0 → 654.9 kB, transfer 143.80 → 144.99 kB. Overlay and accessibility code shared with the shell's menu moved into the initial chunk. The component itself is in the lazy Users chunk.

