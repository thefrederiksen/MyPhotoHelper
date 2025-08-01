# Database Migration Script Rules

This document defines the rules and conventions for creating database migration scripts to ensure they are consistent, reliable, and won't break during application startup.

## Naming Convention

- **Format**: `DatabaseVersion_XXX.sql` where XXX is a zero-padded 3-digit version number
- **Examples**: 
  - `DatabaseVersion_001.sql` (initial schema)
  - `DatabaseVersion_002.sql` 
  - `DatabaseVersion_003.sql`
- **No descriptive names**: Don't add descriptions to filenames (e.g., ~~`DatabaseVersion_002_FixDates.sql`~~)
- **Sequential numbering**: Each new migration must increment the version by 1

## Script Execution Rules

1. **Sequential Execution**: Scripts are executed in numerical order based on version number
2. **Version Checking**: Only scripts with versions **HIGHER** than the current database version are executed
   - If database is at version 3, only scripts 004 and above will run
   - The current version's script is NEVER re-run
   - If no higher version scripts exist, no migrations are executed
3. **Automatic Execution**: Scripts are automatically applied during application startup
4. **Transaction Safety**: Each script runs within a transaction - if any statement fails, the entire script is rolled back

### Example Execution Flow
- Database current version: 2
- Available scripts: 001, 002, 003, 004
- Scripts that will run: 003, 004 (in that order)
- Scripts that will NOT run: 001, 002 (already applied)

## Script Content Rules

### 1. Idempotency (CRITICAL)
Every script (except version 001) MUST be idempotent - safe to run multiple times without errors:

```sql
-- ✅ GOOD: Uses IF NOT EXISTS
CREATE INDEX IF NOT EXISTS IX_tbl_images_FileHash ON tbl_images (FileHash);

-- ❌ BAD: Will fail if index already exists
CREATE INDEX IX_tbl_images_FileHash ON tbl_images (FileHash);
```

### 2. Version Update
Every script MUST end with updating the version number:

```sql
-- Always put this at the END of your script
UPDATE tbl_version SET Version = 3;
```

- Don't use WHERE clauses on version updates
- Don't check the previous version
- Just set it to the script's version number

### 3. Use Safe SQL Patterns

#### Creating Objects
```sql
-- Tables
CREATE TABLE IF NOT EXISTS tbl_new_table (...);

-- Indexes
CREATE INDEX IF NOT EXISTS IX_index_name ON table_name (column);

-- Unique Indexes
CREATE UNIQUE INDEX IF NOT EXISTS UIX_index_name ON table_name (column);
```

#### Modifying Data
```sql
-- Use WHERE clauses to prevent duplicate updates
UPDATE tbl_images SET new_column = 'default' WHERE new_column IS NULL;

-- Use INSERT OR REPLACE for settings
INSERT OR REPLACE INTO tbl_settings (key, value) VALUES ('setting', 'value');
```

#### Adding Columns
```sql
-- SQLite doesn't support IF NOT EXISTS for columns, so be careful
-- Check if migration has already run via version number instead
ALTER TABLE tbl_images ADD COLUMN new_column TEXT DEFAULT NULL;
```

### 4. Comments and Documentation
```sql
-- Database Version XXX: Brief description of what this migration does
-- Additional details about why this change is needed
-- This script is idempotent - safe to run multiple times

-- Comment each major section
-- Add index for performance improvement
CREATE INDEX IF NOT EXISTS ...
```

## Testing Your Migration

Before committing a migration script:

1. **Test on a fresh database**: Ensure it works when all migrations run in sequence
2. **Test idempotency**: Run the script twice on the same database - it should not error
3. **Test rollback**: Ensure your transaction will rollback on any error
4. **Verify version update**: Check that `tbl_version` is updated correctly

## Migration Script Template

```sql
-- Database Version XXX: Brief description
-- Detailed explanation of what this migration does and why
-- This script is idempotent - safe to run multiple times

-- Main migration logic here
-- Use IF NOT EXISTS, WHERE clauses, etc. to ensure idempotency

-- Example: Add new index
CREATE INDEX IF NOT EXISTS IX_table_column ON table_name (column_name);

-- Example: Add new column with safe default
-- Note: SQLite doesn't support IF NOT EXISTS for columns
ALTER TABLE tbl_images ADD COLUMN new_field TEXT DEFAULT NULL;

-- Example: Update existing data safely
UPDATE tbl_images 
SET new_field = 'calculated_value' 
WHERE new_field IS NULL;

-- ALWAYS end with version update (no WHERE clause)
UPDATE tbl_version SET Version = XXX;
```

## Common Pitfalls to Avoid

1. **Don't use DROP statements** - These are destructive and not idempotent
2. **Don't assume data exists** - Always use WHERE clauses to check
3. **Don't use complex version checking** - The system handles this
4. **Don't forget the version update** - Must be the last statement
5. **Don't use semicolons in comments** - The script splitter uses `;` as delimiter

## Special Considerations

### Version 001 (Initial Schema)
- This is the only script that doesn't need to be idempotent
- Creates all initial tables, indexes, and seed data
- Must create the `tbl_version` table with initial version

### Data Migrations
When migrating data:
```sql
-- Safe data migration pattern
UPDATE target_table
SET column = (SELECT value FROM source_table WHERE ...)
WHERE column IS NULL OR column = '';
```

### Performance Considerations
- Add indexes AFTER inserting large amounts of data
- Use transactions for bulk operations
- Consider the impact on application startup time

## Troubleshooting

If a migration fails:
1. Check the application logs for specific error messages
2. Verify the script syntax is valid SQLite SQL
3. Ensure all referenced tables/columns exist
4. Check that the script is idempotent
5. Verify the version number is sequential

Remember: A failed migration will prevent the application from starting, so always test thoroughly!