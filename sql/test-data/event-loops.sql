-- ============================================================
-- Test Data: Event Loops
-- Created: 2026-07-06
-- Updated: 2026-07-21
-- Run against: eval database
-- GUID prefix: ARJTEST01
-- ============================================================
-- Creates 12 events across 6 loop structures to validate the
-- Event Loops dashboard count and drill-down tab.
--
--   Loop 1: Two-node cycle   — D ↔ O       (matter @Matter1, random)
--   Loop 2: Three-node cycle — O → O → N   (matter @Matter2, random)
--   Loop 3: Self-reference   — O → O       (matter @Matter1)
--   Loop 4: Two-node cycle   — D ↔ O       (matter @Matter3, random)
--   Loop 5: Three-node cycle — O → O → N   (matter @Matter4, random)
--   Loop 6: Self-reference   — O → O       (matter @Matter3)
--
-- Display numbers:
--   D events use EventsNo (90001, 90003)
--   N events use EventsNo (90002, 90004)
--   O events use RTF      (TEST-LOOP-1B, 2A, 2B, SELF, 4B, 5A, 5B, 6SELF)
--
-- Side effects (known):
--   Folderless Events (+12): No EventClassFolderParent rows added.
--   Overdue Dockets (none): Docket events use a future EventDate.
--   Unassembled Documents (none): Uses a non-assembleable document
--     EventType (no FormRTF in EventTypesDocuments).
--   Events with No Professional (none): Uses an existing ProfSet.
--   Orphaned Events (none): EventMatters rows inserted for all events.
--
-- Cleanup:
--   DELETE em FROM EventMatters em
--     INNER JOIN Events e ON e.Events = em.Events
--     WHERE e.Events LIKE 'ARJTEST01%'
--   DELETE FROM Events WHERE Events LIKE 'ARJTEST01%'
-- ============================================================

SET XACT_ABORT ON

BEGIN TRANSACTION

-- ── Cleanup: remove any existing ARJTEST01 records ────────────────────────────

DELETE em FROM EventMatters em
    INNER JOIN Events e ON e.Events = em.Events
    WHERE e.Events LIKE 'ARJTEST01%'

DELETE FROM Events WHERE Events LIKE 'ARJTEST01%'

-- ── GUIDs ─────────────────────────────────────────────────────────────────────

DECLARE @L1A    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L1B    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L2A    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L2B    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L2C    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @LSelf  varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L4A    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L4B    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L5A    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L5B    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L5C    varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)
DECLARE @L6Self varchar(36) = LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36)

-- ── Lookups ───────────────────────────────────────────────────────────────────

DECLARE @Matter1 varchar(36), @Matter2 varchar(36), @Matter3 varchar(36), @Matter4 varchar(36)

SELECT @Matter1 = Matters FROM (
    SELECT Matters, ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
    FROM Matters WHERE MatterID IS NOT NULL
) t WHERE rn = 1

SELECT @Matter2 = Matters FROM (
    SELECT Matters, ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
    FROM Matters WHERE MatterID IS NOT NULL AND Matters <> @Matter1
) t WHERE rn = 1

SELECT @Matter3 = Matters FROM (
    SELECT Matters, ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
    FROM Matters WHERE MatterID IS NOT NULL AND Matters NOT IN (@Matter1, @Matter2)
) t WHERE rn = 1

SELECT @Matter4 = Matters FROM (
    SELECT Matters, ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
    FROM Matters WHERE MatterID IS NOT NULL AND Matters NOT IN (@Matter1, @Matter2, @Matter3)
) t WHERE rn = 1

-- A docket EventType (arbitrary — first alphabetically)
DECLARE @DocketType varchar(36) = (
    SELECT TOP 1 EventTypes FROM EventTypes WHERE EventKind = 'D' ORDER BY EventTypes
)

-- A non-assembleable document EventType: EventKind = 'O' with no FormRTF in
-- EventTypesDocuments, so test events do not appear in Unassembled Documents.
DECLARE @DocumentType varchar(36) = (
    SELECT TOP 1 et.EventTypes
    FROM EventTypes et
    WHERE et.EventKind = 'O'
      AND NOT EXISTS (
          SELECT 1 FROM EventTypesDocuments etd
          WHERE etd.EventTypes = et.EventTypes
            AND etd.FormRTF IS NOT NULL
      )
    ORDER BY et.EventTypes
)

-- A note EventType
DECLARE @NoteType varchar(36) = (
    SELECT TOP 1 EventTypes FROM EventTypes WHERE EventKind = 'N' ORDER BY EventTypes
)

-- An existing ProfSet so test events do not appear in Events with No Professional
DECLARE @ProfSet varchar(36) = (
    SELECT TOP 1 ProfSet FROM EventProfs ORDER BY ProfSet
)

-- Verify all lookups resolved before inserting anything
IF @Matter1      IS NULL THROW 50000, 'No matters found in Matters (need at least 4 with MatterID)', 1
IF @Matter2      IS NULL THROW 50000, 'Fewer than 2 distinct matters found in Matters', 1
IF @Matter3      IS NULL THROW 50000, 'Fewer than 3 distinct matters found in Matters', 1
IF @Matter4      IS NULL THROW 50000, 'Fewer than 4 distinct matters found in Matters', 1
IF @DocketType   IS NULL THROW 50000, 'No Docket (D) EventType found', 1
IF @DocumentType IS NULL THROW 50000, 'No non-assembleable Document (O) EventType found', 1
IF @NoteType     IS NULL THROW 50000, 'No Note (N) EventType found', 1
IF @ProfSet      IS NULL THROW 50000, 'No ProfSet found in EventProfs', 1

-- ── Loop 1: Two-node cycle D ↔ O ─────────────────────────────────────────────

INSERT INTO Events (Events, EventKind, EventTypes, ShortNote, EventDate, AddingDateTime, ProfSet, EventsNo)
VALUES (
    @L1A, 'D', @DocketType,
    'TEST-LOOP-1A: 2-node loop anchor (D)',
    DATEADD(DAY, 30, GETDATE()),   -- future date: not overdue
    GETDATE(), @ProfSet, 90001
)

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (
    @L1B, 'O', @DocumentType,
    'TEST-LOOP-1B',
    'TEST-LOOP-1B: 2-node loop peer (O)',
    GETDATE(), GETDATE(), @ProfSet
)

INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L1A, @Matter1)
INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L1B, @Matter1)

UPDATE Events SET EventsParent = @L1B WHERE Events = @L1A
UPDATE Events SET EventsParent = @L1A WHERE Events = @L1B

-- ── Loop 2: Three-node cycle O → O → N ───────────────────────────────────────

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (@L2A, 'O', @DocumentType, 'TEST-LOOP-2A', 'TEST-LOOP-2A: 3-node loop, node 1 (O)', GETDATE(), GETDATE(), @ProfSet)

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (@L2B, 'O', @DocumentType, 'TEST-LOOP-2B', 'TEST-LOOP-2B: 3-node loop, node 2 (O)', GETDATE(), GETDATE(), @ProfSet)

INSERT INTO Events (Events, EventKind, EventTypes, ShortNote, EventDate, AddingDateTime, ProfSet, EventsNo)
VALUES (@L2C, 'N', @NoteType, 'TEST-LOOP-2C: 3-node loop, node 3 (N)', GETDATE(), GETDATE(), @ProfSet, 90002)

INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L2A, @Matter2)
INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L2B, @Matter2)
INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L2C, @Matter2)

UPDATE Events SET EventsParent = @L2B WHERE Events = @L2A
UPDATE Events SET EventsParent = @L2C WHERE Events = @L2B
UPDATE Events SET EventsParent = @L2A WHERE Events = @L2C

-- ── Loop 3: Self-reference O → O ─────────────────────────────────────────────

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (@LSelf, 'O', @DocumentType, 'TEST-LOOP-SELF', 'TEST-LOOP-SELF: self-reference (O)', GETDATE(), GETDATE(), @ProfSet)

INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @LSelf, @Matter1)

UPDATE Events SET EventsParent = @LSelf WHERE Events = @LSelf

-- ── Loop 4: Two-node cycle D ↔ O ─────────────────────────────────────────────

INSERT INTO Events (Events, EventKind, EventTypes, ShortNote, EventDate, AddingDateTime, ProfSet, EventsNo)
VALUES (
    @L4A, 'D', @DocketType,
    'TEST-LOOP-4A: 2-node loop anchor (D)',
    DATEADD(DAY, 30, GETDATE()),   -- future date: not overdue
    GETDATE(), @ProfSet, 90003
)

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (
    @L4B, 'O', @DocumentType,
    'TEST-LOOP-4B',
    'TEST-LOOP-4B: 2-node loop peer (O)',
    GETDATE(), GETDATE(), @ProfSet
)

INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L4A, @Matter3)
INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L4B, @Matter3)

UPDATE Events SET EventsParent = @L4B WHERE Events = @L4A
UPDATE Events SET EventsParent = @L4A WHERE Events = @L4B

-- ── Loop 5: Three-node cycle O → O → N ───────────────────────────────────────

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (@L5A, 'O', @DocumentType, 'TEST-LOOP-5A', 'TEST-LOOP-5A: 3-node loop, node 1 (O)', GETDATE(), GETDATE(), @ProfSet)

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (@L5B, 'O', @DocumentType, 'TEST-LOOP-5B', 'TEST-LOOP-5B: 3-node loop, node 2 (O)', GETDATE(), GETDATE(), @ProfSet)

INSERT INTO Events (Events, EventKind, EventTypes, ShortNote, EventDate, AddingDateTime, ProfSet, EventsNo)
VALUES (@L5C, 'N', @NoteType, 'TEST-LOOP-5C: 3-node loop, node 3 (N)', GETDATE(), GETDATE(), @ProfSet, 90004)

INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L5A, @Matter4)
INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L5B, @Matter4)
INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L5C, @Matter4)

UPDATE Events SET EventsParent = @L5B WHERE Events = @L5A
UPDATE Events SET EventsParent = @L5C WHERE Events = @L5B
UPDATE Events SET EventsParent = @L5A WHERE Events = @L5C

-- ── Loop 6: Self-reference O → O ─────────────────────────────────────────────

INSERT INTO Events (Events, EventKind, EventTypes, RTF, ShortNote, EventDate, AddingDateTime, ProfSet)
VALUES (@L6Self, 'O', @DocumentType, 'TEST-LOOP-6SELF', 'TEST-LOOP-6SELF: self-reference (O)', GETDATE(), GETDATE(), @ProfSet)

INSERT INTO EventMatters (EventMatters, Events, Matters) VALUES (LEFT('ARJTEST01' + CAST(NEWID() AS varchar(36)), 36), @L6Self, @Matter3)

UPDATE Events SET EventsParent = @L6Self WHERE Events = @L6Self

-- ── Summary ───────────────────────────────────────────────────────────────────

PRINT 'Event Loop test records inserted. Expected dashboard count: 12'
PRINT '  Loop 1 (2-node D<->O, matter ' + @Matter1 + '): ' + @L1A + ', ' + @L1B
PRINT '  Loop 2 (3-node O->O->N, matter ' + @Matter2 + '): ' + @L2A + ', ' + @L2B + ', ' + @L2C
PRINT '  Loop 3 (self-ref O, matter ' + @Matter1 + '): ' + @LSelf
PRINT '  Loop 4 (2-node D<->O, matter ' + @Matter3 + '): ' + @L4A + ', ' + @L4B
PRINT '  Loop 5 (3-node O->O->N, matter ' + @Matter4 + '): ' + @L5A + ', ' + @L5B + ', ' + @L5C
PRINT '  Loop 6 (self-ref O, matter ' + @Matter3 + '): ' + @L6Self
PRINT ''
PRINT 'NOTE: All 12 events will also appear in Folderless Events (+12).'

COMMIT TRANSACTION
