# Data Model: App-Wide Loading Indicator

No stored data. These are the in-memory shapes.

## LoadingService (root)

| Member | Type | Rule |
|---|---|---|
| `visible` | `Signal<boolean>` | true only after a wait has run for `loadingDelay` and while at least one wait is running |
| `begin()` | `() => () => void` | adds one wait; the returned `end` removes it, once only |
| `track(work)` | `<T>(work: Promise<T>) => Promise<T>` | begins a wait, settles like `work`, and ends the wait either way |
| `loadingDelay` | exported constant, `200` ms | the only place the threshold is set |

### States

| Count | Show timer | Hide check | `visible` |
|---|---|---|---|
| 0 | none | none | false |
| > 0, under 200 ms | running | none | false |
| > 0, at or after 200 ms | none | none | true |
| fell to 0 | may be running | scheduled for the next task | unchanged until the check runs |

### Transitions

- **begin**: cancels a scheduled hide check. If the bar is hidden and no show timer is running, it starts one.
- **show timer fires**: sets `visible` to true.
- **end, when the count reaches 0**: schedules a hide check.
- **hide check runs, with the count still at 0**: clears the show timer and sets `visible` to false.

## Request marking

| Name | Type | Rule |
|---|---|---|
| `BACKGROUND` | `HttpContextToken<boolean>`, default `false` | when `true`, `loadingInterceptor` does not count the request |
| `background()` | `() => HttpContext` | a context with `BACKGROUND` set to `true` |
| `RequestOptions` | `{ background?: boolean }` | optional last argument of `UsersApi.list`, `LookupsApi.values` and `LookupsApi.page` |

## Wait sources

| Source | Counts | How |
|---|---|---|
| Users list: first page, filter, page change, total | yes | interceptor |
| Users next-page prefetch | no | `{ background: true }` |
| Disable user, role switches, roles list, tenants list | yes | interceptor |
| Add staff user save, Create tenant save | yes | interceptor (research R8) |
| Lookup select option pages and names | no | `{ background: true }` |
| Users page status list (first load) | yes | interceptor |
| Shell `user-type` names | no | `{ background: true }` |
| Session check `/bff/backoffice/user` | no | `context: background()` |
