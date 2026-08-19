# Grid: Add PK columns and resizable cells

**Objective:** Improve the loop detail grid with two changes:

1. **Add primary key columns** — Show the raw `Events` GUID (PK) and `EventsParent` GUID in the grid, so admins can see the underlying keys alongside the display numbers.
2. **Resizable columns** — Allow the user to drag column edges to adjust width so they can read truncated content (notes, long matter IDs, etc.). Overall grid width stays fixed; the Note column absorbs width changes. Row heights are locked.

**Status:** Complete
**Completed:** 2026-08-19

## Acceptance criteria

- [x] Grid has an "Events (PK)" column showing the Events GUID
- [x] Grid has an "EventsParent (PK)" column showing the EventsParent GUID
- [x] All grid columns are user-resizable by dragging the column header border
- [x] Overall grid width stays constant — Note column absorbs width redistribution
- [x] Row heights are not resizable
- [x] CSV export and print include the new PK columns
- [x] Published as single-file self-contained exe (tested on client server)
