-- ============================================================
-- Event Loop Detection — Recursive CTE
-- Status: UNTESTED — proposed for server-side detection
-- Origin: NXP-EventsManager/docs/sql-proposals.md
-- ============================================================
-- The desktop tool currently uses client-side detection (C# port
-- of the TypeScript algorithm). This CTE is preserved as a
-- reference for future server-side optimization.
--
-- To use for dashboard count:
--   SELECT COUNT(*) AS EventLoopCount FROM (...) loops
-- ============================================================

WITH PotentialNodes AS (
    SELECT e.Events, e.EventsParent, e.EventsNo, e.ShortNote, e.EventKind
    FROM Events e
    WHERE e.EventsParent IS NOT NULL
      AND e.Events <> e.EventsParent
      AND e.Events IN (
          SELECT DISTINCT EventsParent
          FROM Events
          WHERE EventsParent IS NOT NULL
            AND EventsParent <> Events
      )
),
ChainWalk AS (
    SELECT
        n.Events                                    AS StartNode,
        n.Events                                    AS CurrentNode,
        n.EventsParent                              AS NextNode,
        CAST(',' + n.Events + ',' AS VARCHAR(MAX))  AS Visited
    FROM PotentialNodes n

    UNION ALL

    SELECT
        cw.StartNode,
        e.Events,
        e.EventsParent,
        cw.Visited + e.Events + ','
    FROM Events e
    INNER JOIN ChainWalk cw ON e.Events = cw.NextNode
    WHERE cw.NextNode IS NOT NULL
      AND cw.Visited NOT LIKE '%,' + e.Events + ',%'
),
CycleNodes AS (
    SELECT DISTINCT CurrentNode AS Events
    FROM ChainWalk
    WHERE NextNode IS NOT NULL
      AND Visited LIKE '%,' + NextNode + ',%'
)
-- Self-references
SELECT e.Events, e.EventsParent, e.EventsNo, e.ShortNote, e.EventKind,
       'Self-reference' AS LoopType
FROM Events e
WHERE e.Events = e.EventsParent

UNION ALL

-- Multi-node cycle participants
SELECT e.Events, e.EventsParent, e.EventsNo, e.ShortNote, e.EventKind,
       'Cycle' AS LoopType
FROM Events e
INNER JOIN CycleNodes cn ON cn.Events = e.Events
OPTION (MAXRECURSION 200)
