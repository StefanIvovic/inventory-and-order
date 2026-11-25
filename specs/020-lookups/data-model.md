# Data Model: Lookups

## LookupType (`lookup_types`, every service that uses lookups)

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid | fixed in code; primary key |
| `Code` | string | required; 1–100 characters; lower-case words joined by `-` (`^[a-z0-9]+(-[a-z0-9]+)*$`); unique; permanent |
| `Name` | string | required; 1–200 characters; display text |

## LookupValue (`lookup_values`)

| Field | Type | Rule |
|---|---|---|
| `Id` | uuid | fixed in code; primary key |
| `Code` | string | same format as `LookupType.Code`; unique within its type; permanent |
| `Name` | string | required; 1–200 characters |
| `LookupTypeId` | uuid | required; references `LookupType.Id`; cannot be deleted while referenced |

## ListLookupValues (query, `GET /lookups/{typeCode}/values`)

| Parameter | Type | Rule |
|---|---|---|
| `typeCode` | string | an existing `LookupType.Code`; otherwise 404 `lookup-type-not-found` |
| `q` | string | optional; trimmed; at most 100 characters; contained in the name (ignoring case) or the code |
| `ids` | uuid[] | optional; repeated parameter; at most 200 |
| `codes` | string[] | optional; repeated parameter; at most 200 |
| `size` | integer | 1–200, default 50 |
| `after` | string | optional; the previous page's `nextCursor`; malformed gives 400 `validation-failed` |

## LookupValuePage (answer)

| Field | Type | Rule |
|---|---|---|
| `items` | `{ id, code, name }[]` | sorted by name, then id |
| `nextCursor` | string \| null | null on the last page |

## Identity catalog

| Type code | Name | Values (code: name) |
|---|---|---|
| `user-type` | User type | `staff`: Staff, `customer`: Customer, `platform`: Platform |
| `staff-status` | Staff status | `active`: Active, `disabled`: Disabled |

## AppUser change (Identity, `identity_users`)

| Before | After |
|---|---|
| `UserType varchar(20)` holding `staff`, `customer` or `platform` | `UserTypeId uuid not null`, FK to `lookup_values.Id` |
| check `ck_identity_users_type_tenant` on the text values | the same check on the three `user-type` ids |
| filtered unique indexes on `"UserType"` text | the same indexes filtered on `"UserTypeId"` |

In C#, `AppUser.UserType` stays the code string, mapped to `UserTypeId` by a value converter (research.md R7).
