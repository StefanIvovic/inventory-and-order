# Data Model: BuildingBlocks Validation

## Validation problem (400 body)

| Field | Value | Rules |
|---|---|---|
| type | `https://tools.ietf.org/html/rfc9110#section-15.5.1` | From the framework defaults |
| title | `One or more validation errors occurred.` | Fixed |
| status | 400 | Fixed |
| code | `validation-failed` | Fixed |
| errors | object: field → string[] | Field = camel-cased path with indexers. Messages in order, deduplicated. At least one field |
| traceId | string | As for every error (feature 002) |

## MappedProblem (extension point)

| Field | Type | Rules |
|---|---|---|
| Status | int | 4xx or 5xx |
| Code | string? | Required for 4xx |
| Detail | string? | Never includes exception internals for 5xx |
| Extensions | IReadOnlyDictionary<string, object?> | Added to the body as-is |
| LogLevel | LogLevel | Information for 4xx unless the mapper chooses otherwise |
| Title | string? | Optional. When null, the framework default title for the status is used |
