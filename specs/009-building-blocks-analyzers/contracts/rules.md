# Contract: Analyzer rules

- IAO1001 and IAO1002 are both **Error** severity by default. Services MUST NOT downgrade them in `.editorconfig`.
- Suppressing either needs `[SuppressMessage(..., Justification = "...")]`, which is visible in review.
- They are delivered with BuildingBlocks.AspNetCore, EntityFrameworkCore, Messaging and Validation. No configuration is needed.
