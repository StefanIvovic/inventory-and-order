# Contract: Tenants page

Layout, top to bottom:
- **Header**: "Tenants", and "Create tenant" when `canCreate`.
- **Search**: "Search tenants", a text field with a search icon button.
- **Table**: Identifier, Name, Created.
- **Footer**: "Rows per page" (10, 25, 50), the range, Previous and Next.

| Situation | What the user sees |
|---|---|
| not the platform admin | "This page isn't available to you."; no request |
| first load | the page's own loading bar |
| first load fails | "Tenants could not be loaded." and "Try again" |
| loaded | the first 25 tenants A–Z by identifier, and "1–25 of N tenants" |
| no tenants, no search | "No tenants yet." |
| typing in the search box | nothing is sent |
| Enter or the search button | the first page of matches, with the total |
| no match | "No tenants match this search." and "Clear search" |
| Next | the next page at once if prefetched, otherwise after the request (global bar, 022) |
| Previous | the page seen before, with no request |
| first page / last page | Previous / Next disabled |
| page size changed | the first page with the total |
| tenant created | a confirmation; the first page with the total, keeping the search and size |
| invalid page token | silently back to the first page with the total |
| a page change fails | "Tenants could not be loaded." and "Try again" |

The prefetch never shows the global loading bar.
