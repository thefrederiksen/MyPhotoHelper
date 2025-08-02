# Database Schema Change Guide

This document outlines the **required process** for making any database schema changes in MyPhotoHelper.

## Overview

MyPhotoHelper uses a **database-first** approach with versioned SQL migration scripts. All schema changes must follow this exact process to ensure consistency between the database schema and Entity Framework models.

## Process for Database Schema Changes

### Step 1: Create Migration Script

1. **Check current version**: Look at existing `DatabaseVersion_XXX.sql` files in the Database directory
2. **Create new script**: Create a new file named `DatabaseVersion_XXX.sql` where XXX is the next version number
   - Example: If current highest is `DatabaseVersion_001.sql`, create `DatabaseVersion_002.sql`
   - **IMPORTANT**: Use exactly this naming pattern - no variations!
   - Always use 3-digit zero-padded numbers (001, 002, 003, etc.)

3. **Script contents**:
   ```sql
   -- Database Version XXX: Brief description of changes
   -- Date: YYYY-MM-DD
   -- Author: Your name

   -- Your schema changes here
   -- ALWAYS use idempotent operations (safe to run multiple times)
   ALTER TABLE tbl_example ADD COLUMN new_column TEXT;

   -- Always update version at the end
   UPDATE tbl_version SET Version = XXX;
   ```

4. **CRITICAL: Make scripts idempotent** - All scripts from DatabaseVersion_002.sql onwards must be safe to run multiple times without errors. This means:
   - Check if tables exist before creating them
   - Check if columns exist before adding them
   - Check if indexes exist before creating them
   - Use `IF NOT EXISTS` clauses wherever possible
   - Handle cases where objects already exist gracefully

### Step 2: Apply to Development Database

1. **Run database manager**:
   ```bash
   cd src/MyPhotoHelper/Python
   python database_manager.py
   ```

2. **Verify success**: The script will show:
   - Current database version
   - Scripts to be applied
   - Success/failure status
   - Final database version

### Step 3: Update Entity Framework Models

1. **Run EF Core scaffolding**:
   ```powershell
   cd src/MyPhotoHelper/Database
   .\GenerateEFModels.ps1
   ```

2. **Verify changes**: Check that the generated models in `Models/` reflect your schema changes

### Step 4: Commit Changes

Commit all of the following:
- Your new `DatabaseVersion_XXX.sql` script
- Updated Entity Framework models
- Any code changes that use the new schema

## Important Rules

### ✅ DO:
- Always increment version numbers sequentially
- Always update `tbl_version` at the end of your script
- Test your script on a fresh database before committing
- Include comments explaining what the migration does
- **ALWAYS make scripts idempotent** (safe to run multiple times)
- Use `IF NOT EXISTS` clauses for all CREATE operations
- Check for existing objects before creating them
- Handle existing data gracefully when modifying tables

### ❌ DON'T:
- Never modify existing DatabaseVersion_XXX.sql files
- Never skip version numbers
- Never rename or renumber migration files
- Never make schema changes without creating a migration script
- Never manually edit Entity Framework models (they will be overwritten)

## Idempotent Script Requirements

### Why Idempotent Scripts Are Critical

Starting from DatabaseVersion_002.sql, all migration scripts must be **idempotent** - meaning they can be run multiple times without causing errors. This is essential because:

1. **Database version resets** - If the database version is reset, all subsequent scripts will run again
2. **Partial failures** - If a script fails partway through, it may need to be re-run
3. **Development testing** - Developers may run scripts multiple times during testing
4. **Production safety** - Prevents errors if scripts are accidentally run multiple times

### How to Make Scripts Idempotent

#### For Tables
```sql
-- ✅ GOOD: Safe to run multiple times
CREATE TABLE IF NOT EXISTS tbl_new_table (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);

-- ❌ BAD: Will fail if table already exists
CREATE TABLE tbl_new_table (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);
```

#### For Indexes
```sql
-- ✅ GOOD: Safe to run multiple times
CREATE INDEX IF NOT EXISTS IX_tbl_images_DateCreated ON tbl_images(DateCreated);

-- ❌ BAD: Will fail if index already exists
CREATE INDEX IX_tbl_images_DateCreated ON tbl_images(DateCreated);
```

#### For Columns (SQLite Limitation)
SQLite doesn't support `IF NOT EXISTS` for `ALTER TABLE ADD COLUMN`. You have several options:

**Option 1: Check in application code**
```csharp
// In your migration service
var columns = await context.Database.SqlQueryRaw<string>(
    "PRAGMA table_info(tbl_app_settings)").ToListAsync();
if (!columns.Any(c => c.Contains("NewColumn")))
{
    await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE tbl_app_settings ADD COLUMN NewColumn TEXT");
}
```

**Option 2: Use a different approach**
```sql
-- Create a new table with the desired schema
CREATE TABLE IF NOT EXISTS tbl_app_settings_new (
    -- all columns including the new one
);

-- Copy data from old table to new table
INSERT INTO tbl_app_settings_new SELECT * FROM tbl_app_settings;

-- Drop old table and rename new table
DROP TABLE tbl_app_settings;
ALTER TABLE tbl_app_settings_new RENAME TO tbl_app_settings;
```

#### For Views
```sql
-- ✅ GOOD: Safe to run multiple times
CREATE VIEW IF NOT EXISTS vw_image_summary AS
SELECT COUNT(*) as TotalImages FROM tbl_images;

-- ❌ BAD: Will fail if view already exists
CREATE VIEW vw_image_summary AS
SELECT COUNT(*) as TotalImages FROM tbl_images;
```

#### For Triggers
```sql
-- ✅ GOOD: Safe to run multiple times
CREATE TRIGGER IF NOT EXISTS trg_image_updated
AFTER UPDATE ON tbl_images
BEGIN
    UPDATE tbl_images SET ModifiedDate = DATETIME('now') WHERE id = NEW.id;
END;

-- ❌ BAD: Will fail if trigger already exists
CREATE TRIGGER trg_image_updated
AFTER UPDATE ON tbl_images
BEGIN
    UPDATE tbl_images SET ModifiedDate = DATETIME('now') WHERE id = NEW.id;
END;
```

## Example Migration Scripts
```sql
-- Database Version 002: Add LastLoginDate to settings
-- Date: 2025-01-15
-- Author: John Doe

-- Check if column exists before adding it (idempotent)
PRAGMA table_info(tbl_app_settings);
-- If LastLoginDate column doesn't exist, add it
-- Note: SQLite doesn't support IF NOT EXISTS for ALTER TABLE ADD COLUMN
-- So we need to handle this in the application logic or use a different approach

-- Alternative approach: Use a more robust method
-- This would require checking column existence in application code
-- or using a different database system that supports this feature

-- Update version
UPDATE tbl_version SET Version = 2;
```

### Creating an Index
```sql
-- Database Version 003: Add performance indexes
-- Date: 2025-01-20
-- Author: Jane Smith

-- Add index for faster queries (idempotent with IF NOT EXISTS)
CREATE INDEX IF NOT EXISTS IX_tbl_images_FileSize ON tbl_images(FileSizeBytes);

-- Update version
UPDATE tbl_version SET Version = 3;
```

### Adding a Table
```sql
-- Database Version 004: Add user preferences table
-- Date: 2025-01-25
-- Author: Bob Johnson

-- Create new table (idempotent with IF NOT EXISTS)
CREATE TABLE IF NOT EXISTS tbl_user_preferences (
    UserId INTEGER PRIMARY KEY,
    PreferenceName TEXT NOT NULL,
    PreferenceValue TEXT,
    UNIQUE(UserId, PreferenceName)
);

-- Update version
UPDATE tbl_version SET Version = 4;
```

## Troubleshooting

### Database won't update
- Check SQL syntax in your migration script
- Ensure version number is correct
- Look for error messages in database_manager.py output

### EF Models don't reflect changes
- Make sure database was updated successfully first
- Check that GenerateEFModels.ps1 completed without errors
- Verify you're scaffolding from the correct database file

### Migration fails on other machines
- Make scripts defensive with `IF NOT EXISTS` clauses
- Test on a fresh database before committing
- Consider data migration needs, not just schema changes
- Ensure all scripts are idempotent (safe to run multiple times)
- Check for existing objects before creating them

## Database File Locations

- **Development database**: `src/MyPhotoHelper/Database/dev_facevault.db`
- **Migration scripts**: `src/MyPhotoHelper/Database/DatabaseVersion_XXX.sql`
- **EF Models**: `src/MyPhotoHelper/Models/`
- **Database manager**: `src/MyPhotoHelper/Python/database_manager.py`

## Quick Reference Commands

```bash
# 1. Create your DatabaseVersion_XXX.sql file

# 2. Apply migration
cd src/MyPhotoHelper/Python
python database_manager.py

# 3. Update EF models
cd ../Database
.\GenerateEFModels.ps1

# 4. Commit all changes
git add Database/DatabaseVersion_XXX.sql Models/*.cs
git commit -m "Add database migration XXX: description"
```

## Notes

- The database manager will only run migrations with version numbers higher than the current database version
- Each migration is run in a transaction - if it fails, the database is unchanged
- Always test migrations on a copy of production data if available
- Consider performance impact of schema changes on large databases
- **CRITICAL**: All scripts from DatabaseVersion_002.sql onwards must be idempotent (safe to run multiple times)
- Scripts should check for existing objects before creating them to avoid errors
- Use `IF NOT EXISTS` clauses wherever possible for CREATE operations
- Handle existing data gracefully when modifying table structures