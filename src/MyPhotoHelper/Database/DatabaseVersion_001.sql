-- MyPhotoHelper Database Schema
-- Version: 001 (Complete schema with Scan Directory support)
-- Description: Initial database creation with scan directory management
-- Date: 2025-01-29

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

-- Create indexes for Images table
CREATE INDEX IF NOT EXISTS IX_tbl_images_FileHash ON tbl_images (FileHash);
CREATE INDEX IF NOT EXISTS IX_tbl_images_DateCreated ON tbl_images (DateCreated);
CREATE INDEX IF NOT EXISTS IX_tbl_images_IsDeleted ON tbl_images (IsDeleted);
CREATE INDEX IF NOT EXISTS IX_tbl_images_ScanDirectoryId ON tbl_images (ScanDirectoryId);

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

-- Create index for location-based queries
CREATE INDEX IF NOT EXISTS IX_tbl_image_metadata_Location ON tbl_image_metadata (Latitude, Longitude);

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

-- Create AppSettings table (singleton)
CREATE TABLE IF NOT EXISTS tbl_app_settings (
    Id INTEGER PRIMARY KEY,
    
    -- Feature Toggles
    EnableDuplicateDetection INTEGER,
    EnableAIImageAnalysis INTEGER,
    
    -- Directory Settings (PhotoDirectory removed - now using tbl_scan_directory)
    AutoScanOnStartup INTEGER,
    ScanSubdirectories INTEGER,
    
    -- File Type Support
    SupportJpeg INTEGER,
    SupportPng INTEGER,
    SupportHeic INTEGER,
    SupportGif INTEGER,
    SupportBmp INTEGER,
    SupportWebp INTEGER,
    
    -- Performance Settings
    BatchSize INTEGER,
    MaxConcurrentTasks INTEGER,
    
    -- AI Settings
    AIProvider TEXT,
    AIApiKey TEXT,
    AIApiEndpoint TEXT,
    AIModel TEXT,
    AITemperature REAL,
    
    -- Other Settings
    ThemeName TEXT,
    DateCreated DATETIME NOT NULL,
    DateModified DATETIME NOT NULL,
    LastScanDate DATETIME
);

-- Insert minimal settings record
INSERT INTO tbl_app_settings (
    Id, DateCreated, DateModified
) VALUES (
    1, 
    datetime('now'),
    datetime('now')
);