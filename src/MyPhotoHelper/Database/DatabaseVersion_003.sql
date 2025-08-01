-- Database Version 3: Add indexes to improve metadata scanning performance
-- This migration adds indexes to speed up JOIN operations
-- This script is idempotent - safe to run multiple times

-- Add index on ImageId in tbl_image_metadata if it doesn't exist
-- This improves performance when joining with tbl_images during metadata scanning
CREATE INDEX IF NOT EXISTS IX_tbl_image_metadata_ImageId ON tbl_image_metadata (ImageId);

-- Add compound index for checking existence of metadata
-- This specifically helps the query that finds images without metadata
CREATE INDEX IF NOT EXISTS IX_tbl_images_FileExists_IsDeleted ON tbl_images (FileExists, IsDeleted);

-- Update the database version (idempotent - won't fail if already at version 3)
UPDATE tbl_version SET Version = 3;