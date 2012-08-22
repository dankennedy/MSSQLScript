SET NOCOUNT ON;
DECLARE @type_conversions TABLE (typecode char(2) COLLATE Latin1_General_CI_AS_KS_WS, enum_name nvarchar(50))
INSERT INTO @type_conversions
SELECT 'U', 'Table' UNION ALL
SELECT 'P', 'StoredProcedure' UNION ALL
SELECT 'V', 'View' UNION ALL
SELECT 'FN', 'UserDefinedFunction' UNION ALL
SELECT 'FS', 'UserDefinedFunction' UNION ALL
SELECT 'IF', 'UserDefinedFunction' UNION ALL
SELECT 'TF', 'UserDefinedFunction'

select name From sys.schemas
where name NOT LIKE 'db_%' AND name != 'INFORMATION_SCHEMA'

select SCHEMA_NAME(schema_id) AS schema_name,
		name,
		tc.enum_name AS database_object_type	
from sys.all_objects INNER JOIN @type_conversions tc ON sys.all_objects.type = tc.typecode
WHERE	SCHEMA_NAME(schema_id) != 'sys' AND SCHEMA_NAME(schema_id) != 'INFORMATION_SCHEMA'

SELECT	SCHEMA_NAME(o.schema_id) AS referencing_schema_name, 
		o.name AS referencing_entity_name,
		tc1.enum_name AS referencing_database_object_type,
		COALESCE(dep.referenced_schema_name, 'dbo') AS referenced_schema_name,
		dep.referenced_entity_name,
		tc2.enum_name AS referenced_database_object_type		
FROM sys.all_objects o INNER JOIN sys.sql_expression_dependencies dep ON o.object_id = dep.referencing_id
	INNER JOIN sys.all_objects o2 ON o2.object_id = dep.referenced_id
	INNER JOIN @type_conversions tc1 ON o.type = tc1.typecode
	INNER JOIN @type_conversions tc2 ON o2.type = tc2.typecode
WHERE	SCHEMA_NAME(o.schema_id) != 'sys' AND SCHEMA_NAME(o.schema_id) != 'INFORMATION_SCHEMA'
UNION ALL
select distinct
		OBJECT_SCHEMA_NAME(fk.parent_object_id, DB_ID()),
		OBJECT_NAME(fk.parent_object_id),
		'Table',
		OBJECT_SCHEMA_NAME(fk.referenced_object_id, DB_ID()),
		OBJECT_NAME(fk.referenced_object_id),
		'Table'
from sys.foreign_keys fk INNER JOIN sys.all_objects obj_parent ON fk.parent_object_id = obj_parent.[object_id]
	INNER JOIN sys.all_objects obj_child ON fk.referenced_object_id = obj_child.[object_id]