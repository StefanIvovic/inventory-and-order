# Contract: Lookup Select and the Users page search

## Lookup select

Usage:

```html
<app-lookup-select typeCode="staff-status" label="Status" emptyLabel="All" [value]="status()" (valueChange)="setStatus($event)" />
<app-lookup-select typeCode="user-type" label="User types" [multiple]="true" [formField]="f.userTypes" />
```

| Situation | What the user sees |
|---|---|
| closed, nothing chosen | the label and an empty field |
| closed, value chosen (single) | the value's name in the field |
| chosen values (multiple) | one chip per value with its name and a remove button |
| a chosen value's name is unknown or failed to load | "Unavailable" |
| optional single field with a value, no empty choice | a × button clears it |
| panel loading | a progress bar in the panel |
| panel, no options | "No matches" |
| panel, load failed | "Options could not be loaded." and "Try again" |
| typing | the text changes; no request |
| Enter (no option highlighted) or the search button | the panel shows the first page of matching values |
| Enter on a highlighted option, or a click | picks it (single: closes the panel; multiple: adds a chip and clears the text) |
| scrolling to the end of the options | the next page is appended, if there is one |
| form errors | under the field, like other fields |

## Users page

- **Status filter**: `app-lookup-select` over `staff-status`, with "All" as the empty choice. Picking a value restarts at the first page with the total, as before.
- **Search email**: a text field with a search icon button (`aria-label="Search email"`).
  - Enter or the button applies the text and restarts at the first page with the total.
  - Typing alone sends nothing.
  - Empty text means no email filter.
