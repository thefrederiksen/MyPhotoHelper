# Breaking Change - Database Schema v1

## Overview
The database schema has been refactored to use a flexible key-value settings table instead of a fixed-column approach. This is a **breaking change** that requires manual database deletion.

## What Changed
- `tbl_app_settings` changed from a single-row table with many columns to a key-value table
- Old structure: One row with columns like `EnableAIImageAnalysis`, `AIProvider`, etc.
- New structure: Multiple rows, each with `SettingName`, `SettingType`, and `SettingValue`

## Migration Steps
Since this is a development-phase change:

1. **Stop the application**
2. **Delete the database file**: `Database\dev_facevault.db` (or `dev_myphotohelper.db`)
3. **Run the application** - it will create a new database with the new schema
4. **Reconfigure settings** - all settings will be reset to defaults

## Benefits
- No schema changes needed when adding new settings
- More flexible and maintainable
- Better performance with proper indexing
- Foundation for future features

## Code Changes Required
The existing code still references the old model structure. To minimize changes:
- New code should use `ISettingsService`
- Existing code will be migrated gradually
- A compatibility layer can be added if needed

## Future Work
- Create migration tool for production deployments
- Update all components to use SettingsService
- Remove old model references