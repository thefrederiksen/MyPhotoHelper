-- Database Version 002: Fix DateTaken to never be null
-- This ensures DateTaken always has a value by using the oldest available date
-- This script is idempotent - safe to run multiple times

-- First, update any NULL DateTaken values to use the image's DateCreated
UPDATE tbl_image_metadata
SET DateTaken = (
    SELECT DateCreated 
    FROM tbl_images 
    WHERE tbl_images.ImageId = tbl_image_metadata.ImageId
)
WHERE DateTaken IS NULL;

-- Fix any DateTaken values that are in the future
UPDATE tbl_image_metadata
SET DateTaken = (
    SELECT DateCreated 
    FROM tbl_images 
    WHERE tbl_images.ImageId = tbl_image_metadata.ImageId
)
WHERE DateTaken > datetime('now', '+1 day');

-- Fix any DateTaken values that are too old (before 1990)
UPDATE tbl_image_metadata
SET DateTaken = (
    SELECT DateCreated 
    FROM tbl_images 
    WHERE tbl_images.ImageId = tbl_image_metadata.ImageId
)
WHERE DateTaken < '1990-01-01';

-- Update the database version (idempotent - won't fail if already at version 2)
UPDATE tbl_version SET Version = 2;