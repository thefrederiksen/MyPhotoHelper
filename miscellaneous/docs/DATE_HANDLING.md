# Date Handling in MyPhotoHelper

## Overview
The application uses a consistent approach for handling photo dates: **Always use the OLDEST available date as the true date when the photo was taken**.

## Date Extraction Logic

When extracting metadata from photos, the system checks multiple date sources and uses the OLDEST valid date:

1. **EXIF DateTimeOriginal** (`date_taken`) - When the photo was originally taken
2. **EXIF DateTimeDigitized** (`date_digitized`) - When the photo was digitized 
3. **EXIF DateTime** (`date_modified`) - When the file was modified
4. **File System DateCreated** - When the file was created on disk
5. **File System DateModified** - When the file was last modified

The `GetOldestDate()` method in `MetadataExtractionService.cs`:
- Filters out invalid dates (before 1990 or in the future)
- Selects the minimum (oldest) date from all available sources
- Falls back to file creation date if no valid EXIF dates exist

## Database Storage

- The `DateTaken` field in `tbl_image_metadata` stores the selected oldest date
- This field should NEVER be null (enforced by extraction logic)
- All other date fields are preserved for reference but not used for display

## Display Logic

All pages and components should use `DateTaken` for displaying and sorting photos:
- Gallery page groups by DateTaken year/month
- Memories page calculates "years ago" from DateTaken
- All sorting should be based on DateTaken

## Date Format Handling

The system handles multiple date formats:
1. **ISO Format**: `2014-10-25T15:30:45` (from Python)
2. **EXIF Format**: `2014:10:25 15:30:45` (standard EXIF)
3. **Standard formats**: Various culture-specific formats

## Validation Rules

Dates are considered invalid if:
- Before January 1, 1990 (unlikely for digital photos)
- More than 1 day in the future
- Cannot be parsed into a valid DateTime

## Migration and Fixes

The `DatabaseVersion_002_FixDates.sql` script ensures:
- All NULL DateTaken values are populated with DateCreated
- Future dates are replaced with DateCreated
- Dates before 1990 are replaced with DateCreated

## Best Practices

1. **Always use DateTaken** for display and sorting
2. **Never display DateModified** as the photo date
3. **Log date selection** for debugging (which date was chosen and why)
4. **Handle NULL DateTaken** gracefully by falling back to DateCreated