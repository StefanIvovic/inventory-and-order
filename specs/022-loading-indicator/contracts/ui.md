# Contract: App-Wide Loading Bar

## Shell

Markup order under the toolbar:

```html
<mat-toolbar>…</mat-toolbar>
<div class="loading">
  @if (loading.visible()) {
    <mat-progress-bar mode="indeterminate" aria-label="Loading" />
  }
</div>
```

When `loading.visible()` turns true, the shell announces "Loading" through CDK `LiveAnnouncer` (politely).

The retry banner, the side navigation and the content follow, as before.

| Situation | What the user sees |
|---|---|
| no wait running | an empty 4 px strip under the toolbar; no layout change |
| a wait under 200 ms | nothing |
| a wait over 200 ms | an indeterminate bar across the full width under the toolbar |
| several waits | one bar until the last one ends |
| one wait ends and another starts at once | the bar stays; no flicker |
| a request fails or is cancelled | the bar hides once no other wait runs |
| sign-in service unavailable | the bar stays while the request waits for "Try again"; the banner explains why |
| screen reader | "Loading" is announced once when the bar shows; nothing is announced when it hides |
| signed out | no shell, so no bar |

## Code

```ts
this.api.list(request, { background: true });
this.lookups.values('user-type', { background: true });
this.http.get(url, { context: background() });
await this.loading.track(firstStep().then(secondStep));
```

## Pages

| Page | Change |
|---|---|
| Users | the reload bar under the filters is removed; Previous and Next are still disabled while a page loads; the first-load bar stays |
| Roles, Tenants | unchanged; first-load bars stay |
| Lookup select | unchanged; the "Loading options" row stays, and its requests never show the global bar |
| Add staff user, Create tenant | the bar shows while saving, until the dialog closes or shows its errors |
| Roles dialog | the bar shows while a role switch is saved |
