-- MyPhotoHelper Database Schema
-- Version: 001 (Complete consolidated schema)
-- Description: Complete database schema with flexible settings system
-- Date: 2025-08-02

-- Create Version table for tracking database version
CREATE TABLE IF NOT EXISTS tbl_version (
    Version INTEGER NOT NULL
);

-- Insert initial version
INSERT INTO tbl_version (Version) VALUES (1);

-- Create ScanDirectory table - Must be created before Images due to foreign key
CREATE TABLE IF NOT EXISTS tbl_scan_directory (
    ScanDirectoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    DirectoryPath TEXT NOT NULL UNIQUE,
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Create index for ScanDirectory
CREATE INDEX IF NOT EXISTS IX_tbl_scan_directory_DirectoryPath ON tbl_scan_directory (DirectoryPath);

-- Create Images table - Only file system information (no file opening required)
CREATE TABLE IF NOT EXISTS tbl_images (
    ImageId INTEGER PRIMARY KEY AUTOINCREMENT,
    RelativePath TEXT NOT NULL,
    FileName TEXT NOT NULL,
    FileExtension TEXT,
    FileHash TEXT NOT NULL,
    FileSizeBytes INTEGER NOT NULL,
    DateCreated DATETIME NOT NULL,
    DateModified DATETIME NOT NULL,
    
    -- File Status
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    FileExists INTEGER NOT NULL DEFAULT 1,
    DateDeleted DATETIME,
    
    -- Scan Directory Reference
    ScanDirectoryId INTEGER NOT NULL,
    
    FOREIGN KEY (ScanDirectoryId) REFERENCES tbl_scan_directory(ScanDirectoryId) ON DELETE CASCADE,
    CONSTRAINT UK_tbl_images_RelativePath_ScanDirectoryId UNIQUE (RelativePath, ScanDirectoryId)
);

-- Create all indexes for Images table (including performance optimizations)
CREATE INDEX IF NOT EXISTS IX_tbl_images_FileHash ON tbl_images (FileHash);
CREATE INDEX IF NOT EXISTS IX_tbl_images_DateCreated ON tbl_images (DateCreated);
CREATE INDEX IF NOT EXISTS IX_tbl_images_IsDeleted ON tbl_images (IsDeleted);
CREATE INDEX IF NOT EXISTS IX_tbl_images_ScanDirectoryId ON tbl_images (ScanDirectoryId);
CREATE INDEX IF NOT EXISTS IX_tbl_images_RelativePath ON tbl_images (RelativePath);
CREATE INDEX IF NOT EXISTS IX_tbl_images_FileExists_IsDeleted ON tbl_images (FileExists, IsDeleted);

-- Create ImageMetadata table - Information that requires opening the file
-- Uses same primary key as Images table (one-to-one relationship)
CREATE TABLE IF NOT EXISTS tbl_image_metadata (
    ImageId INTEGER PRIMARY KEY,
    
    -- Basic Image Properties
    Width INTEGER,
    Height INTEGER,
    ColorSpace TEXT,
    BitDepth INTEGER,
    Orientation TEXT,
    ResolutionX REAL,
    ResolutionY REAL,
    ResolutionUnit TEXT,
    
    -- Date/Time Information
    DateTaken DATETIME,
    DateDigitized DATETIME,
    DateModified DATETIME,
    TimeZone TEXT,
    
    -- GPS/Location Data
    Latitude REAL,
    Longitude REAL,
    Altitude REAL,
    GPSDirection TEXT,
    GPSSpeed REAL,
    GPSProcessingMethod TEXT,
    LocationName TEXT,
    
    -- Camera Information
    CameraMake TEXT,
    CameraModel TEXT,
    CameraSerial TEXT,
    LensModel TEXT,
    LensMake TEXT,
    LensSerial TEXT,
    
    -- Camera Settings
    FocalLength REAL,
    FocalLength35mm REAL,
    FNumber TEXT,
    ExposureTime TEXT,
    ISO INTEGER,
    ExposureMode TEXT,
    ExposureProgram TEXT,
    MeteringMode TEXT,
    Flash TEXT,
    WhiteBalance TEXT,
    SceneCaptureType TEXT,
    
    -- Software/Processing
    Software TEXT,
    ProcessingSoftware TEXT,
    Artist TEXT,
    Copyright TEXT,
    
    -- Technical Details
    ColorProfile TEXT,
    ExposureBias REAL,
    MaxAperture REAL,
    SubjectDistance TEXT,
    LightSource TEXT,
    SensingMethod TEXT,
    FileSource TEXT,
    SceneType TEXT,
    
    -- Additional Properties
    ImageDescription TEXT,
    UserComment TEXT,
    Keywords TEXT,
    Subject TEXT,
    
    FOREIGN KEY (ImageId) REFERENCES tbl_images(ImageId) ON DELETE CASCADE
);

-- Create indexes for ImageMetadata (including performance optimizations)
CREATE INDEX IF NOT EXISTS IX_tbl_image_metadata_Location ON tbl_image_metadata (Latitude, Longitude);
CREATE INDEX IF NOT EXISTS IX_tbl_image_metadata_DateTaken ON tbl_image_metadata (DateTaken);
CREATE INDEX IF NOT EXISTS IX_tbl_image_metadata_ImageId ON tbl_image_metadata (ImageId);

-- Create ImageAnalysis table - AI analysis results
-- Uses same primary key as Images table (one-to-one relationship)
CREATE TABLE IF NOT EXISTS tbl_image_analysis (
    ImageId INTEGER PRIMARY KEY,
    ImageCategory TEXT,
    PhotoSubcategory TEXT,
    AIAnalysisJson TEXT,
    AIDescription TEXT,
    AIAnalyzedAt DATETIME,
    AIModelUsed TEXT,
    AIKeywords TEXT,
    
    FOREIGN KEY (ImageId) REFERENCES tbl_images(ImageId) ON DELETE CASCADE
);

-- Create index for ImageAnalysis
CREATE INDEX IF NOT EXISTS IX_tbl_image_analysis_ImageCategory ON tbl_image_analysis (ImageCategory);

-- Create new flexible AppSettings table (key-value design)
CREATE TABLE IF NOT EXISTS tbl_app_settings (
    SettingName TEXT PRIMARY KEY,
    SettingType TEXT NOT NULL CHECK(SettingType IN ('bool','int','string','datetime','double')),
    SettingValue TEXT NOT NULL,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Create index for settings lookup
CREATE INDEX IF NOT EXISTS IX_tbl_app_settings_SettingName ON tbl_app_settings (SettingName);

-- Insert default settings
INSERT INTO tbl_app_settings (SettingName, SettingType, SettingValue) VALUES
    -- Feature Toggles
    ('EnableDuplicateDetection', 'bool', '1'),
    ('EnableAIImageAnalysis', 'bool', '0'),
    ('AutoScanOnStartup', 'bool', '0'),
    ('RunOnWindowsStartup', 'bool', '0'),
    ('ScanSubdirectories', 'bool', '1'),
    
    -- File Type Support
    ('SupportJpeg', 'bool', '1'),
    ('SupportPng', 'bool', '1'),
    ('SupportHeic', 'bool', '0'),
    ('SupportGif', 'bool', '1'),
    ('SupportBmp', 'bool', '1'),
    ('SupportWebp', 'bool', '1'),
    
    -- Performance Settings
    ('BatchSize', 'int', '100'),
    ('MaxConcurrentTasks', 'int', '4'),
    
    -- AI Settings
    ('AIProvider', 'string', ''),
    ('AIApiKey', 'string', ''),
    ('AIApiEndpoint', 'string', ''),
    ('AIModel', 'string', 'gpt-4o-mini'),
    ('AITemperature', 'double', '0.7'),
    
    -- Other Settings
    ('ThemeName', 'string', 'default'),
    ('LastScanDate', 'datetime', '')
;

-- Data migration from version 2: Fix DateTaken to never be null
-- Update any NULL DateTaken values to use the image's DateCreated
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