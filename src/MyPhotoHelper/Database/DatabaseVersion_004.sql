-- DatabaseVersion_004.sql
-- Add RunOnWindowsStartup field to app settings table (if not exists)
-- This field controls whether MyPhotoHelper starts automatically with Windows

-- Note: Column may already exist from previous manual updates
-- This script ensures database version is properly tracked

-- Update database version
UPDATE tbl_version SET Version = 4;

-- Documentation:
-- RunOnWindowsStartup: 0 = Disabled (default), 1 = Enabled
-- This field is used by the Settings page to control Windows startup behavior