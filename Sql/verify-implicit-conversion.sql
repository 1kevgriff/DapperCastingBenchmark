-- Verify the implicit conversion problem
-- Run these in SSMS with "Include Actual Execution Plan" enabled (Ctrl+M)

USE DapperBenchmarkCasting;
GO

-- 1. VARCHAR parameter (CORRECT) - should show Index SEEK
DECLARE @code_varchar VARCHAR(100) = 'ART-ELEC-0500000-A';
SELECT TOP 1 * FROM dbo.Products WHERE ProductCode = @code_varchar;
GO

-- 2. NVARCHAR parameter (DAPPER DEFAULT) - should show Index SCAN
DECLARE @code_nvarchar NVARCHAR(4000) = N'ART-ELEC-0500000-A';
SELECT TOP 1 * FROM dbo.Products WHERE ProductCode = @code_nvarchar;
GO

-- 3. Compare logical reads
SET STATISTICS IO ON;
GO

PRINT '--- VARCHAR parameter (correct) ---';
DECLARE @v VARCHAR(100) = 'ART-ELEC-0500000-A';
SELECT TOP 1 * FROM dbo.Products WHERE ProductCode = @v;
GO

PRINT '--- NVARCHAR parameter (Dapper default) ---';
DECLARE @n NVARCHAR(4000) = N'ART-ELEC-0500000-A';
SELECT TOP 1 * FROM dbo.Products WHERE ProductCode = @n;
GO

SET STATISTICS IO OFF;
GO

-- 4. Check cached plans for CONVERT_IMPLICIT after running the C# benchmark
SELECT
    qs.execution_count,
    qs.total_logical_reads,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
    SUBSTRING(st.text, 1, 200) AS query_text,
    CASE
        WHEN CAST(qp.query_plan AS NVARCHAR(MAX)) LIKE '%CONVERT_IMPLICIT%' THEN 'YES - IMPLICIT CONVERSION'
        ELSE 'No implicit conversion'
    END AS has_implicit_conversion
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
WHERE st.text LIKE '%ProductCode%'
    AND st.text NOT LIKE '%dm_exec%'
ORDER BY qs.total_logical_reads DESC;
GO

-- 5. Table and index summary
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc,
    p.rows,
    ic.column_id,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length
FROM sys.tables t
JOIN sys.indexes i ON t.object_id = i.object_id
JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id AND ic.is_included_column = 0
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name IN ('Products', 'Orders')
ORDER BY t.name, i.name, ic.key_ordinal;
GO
