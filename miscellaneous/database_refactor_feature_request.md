## Feature Request: Database Schema Refactoring and Query Optimization

### Overview
Complete database schema refactoring to consolidate all database versions into a single version and restructure the app settings table for better maintainability and flexibility. This is a breaking change that requires manual database deletion and application restart.

### Goals
- Consolidate all database versions into a single Version 1
- Restructure app settings table for better design
- Improve database query performance
- Remove backwards compatibility constraints
- Create a more maintainable database schema

### Requirements

#### 1. Database Version Consolidation
- [ ] **Remove all version scripts** - Delete DatabaseVersion_002.sql, DatabaseVersion_003.sql, etc.
- [ ] **Merge all changes** - Consolidate all schema changes into DatabaseVersion_001.sql
- [ ] **Single version approach** - Only maintain one database version going forward
- [ ] **Remove version tracking** - Simplify database initialization process
- [ ] **Update migration logic** - Modify DatabaseInitializationService to use single version

#### 2. App Settings Table Restructure
- [ ] **Current Problem**: Settings table has one column per setting (poor design)
- [ ] **New Design**: Key-value pair structure with flexible data types
- [ ] **Table Structure**:
  - SettingName (string, primary key)
  - SettingType (string - 'bool', 'int', 'string', 'datetime', etc.)
  - SettingValue (string - serialized value)
- [ ] **Benefits**:
  - No schema changes needed for new settings
  - Flexible data types
  - Better maintainability
  - Easier to add new settings

#### 3. Database Query Optimization
- [ ] **Review all queries** - Audit existing database queries for performance
- [ ] **Add missing indexes** - Create indexes for frequently queried columns
- [ ] **Optimize joins** - Improve query performance with better join strategies
- [ ] **Reduce N+1 queries** - Eliminate inefficient query patterns
- [ ] **Add query caching** - Implement caching for frequently accessed data

#### 4. Schema Improvements
- [ ] **Add foreign key constraints** - Ensure referential integrity
- [ ] **Add check constraints** - Validate data at database level
- [ ] **Improve data types** - Use appropriate data types for each column
- [ ] **Add indexes** - Create indexes for performance-critical queries
- [ ] **Normalize data** - Reduce data redundancy where appropriate

### Technical Implementation Details

#### New Settings Table Schema
```sql
-- New flexible settings table
CREATE TABLE tbl_app_settings (
    SettingName NVARCHAR(255) PRIMARY KEY,
    SettingType NVARCHAR(50) NOT NULL,
    SettingValue NVARCHAR(MAX) NOT NULL,
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    ModifiedDate DATETIME2 DEFAULT GETDATE()
);

-- Example settings
INSERT INTO tbl_app_settings (SettingName, SettingType, SettingValue) VALUES
('StartWithWindows', 'bool', 'true'),
('AutoUpdate', 'bool', 'true'),
('UpdateCheckInterval', 'int', '6'),
('LastUpdateCheck', 'datetime', '2025-01-01T00:00:00'),
('ScanDirectories', 'string', 'C:\Photos,D:\Images');
```

#### Settings Service Implementation
```csharp
public class SettingsService
{
    private readonly IDbContext _context;

    public T GetSetting<T>(string settingName, T defaultValue = default(T))
    {
        var setting = _context.AppSettings.FirstOrDefault(s => s.SettingName == settingName);
        if (setting == null) return defaultValue;

        return ConvertSettingValue<T>(setting.SettingValue, setting.SettingType);
    }

    public void SetSetting<T>(string settingName, T value)
    {
        var setting = _context.AppSettings.FirstOrDefault(s => s.SettingName == settingName);
        var settingType = GetSettingType<T>();
        var stringValue = ConvertToString(value);

        if (setting == null)
        {
            setting = new AppSetting
            {
                SettingName = settingName,
                SettingType = settingType,
                SettingValue = stringValue,
                CreatedDate = DateTime.UtcNow
            };
            _context.AppSettings.Add(setting);
        }
        else
        {
            setting.SettingValue = stringValue;
            setting.SettingType = settingType;
            setting.ModifiedDate = DateTime.UtcNow;
        }

        _context.SaveChanges();
    }

    private T ConvertSettingValue<T>(string value, string type)
    {
        try
        {
            return type switch
            {
                "bool" => (T)(object)bool.Parse(value),
                "int" => (T)(object)int.Parse(value),
                "datetime" => (T)(object)DateTime.Parse(value),
                "string" => (T)(object)value,
                _ => (T)(object)value
            };
        }
        catch
        {
            return default(T);
        }
    }
}
```

#### Database Initialization Service Update
```csharp
public class DatabaseInitializationService
{
    public async Task InitializeDatabaseAsync()
    {
        // Create database if it doesn't exist
        await _context.Database.EnsureCreatedAsync();

        // Apply single version schema
        await ApplySchemaVersion1Async();

        // Initialize default settings
        await InitializeDefaultSettingsAsync();
    }

    private async Task ApplySchemaVersion1Async()
    {
        var sql = await File.ReadAllTextAsync("Database/DatabaseVersion_001.sql");
        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task InitializeDefaultSettingsAsync()
    {
        var settingsService = new SettingsService(_context);

        // Set default settings if they don't exist
        if (settingsService.GetSetting<bool>("StartWithWindows", false) == false)
        {
            settingsService.SetSetting("StartWithWindows", true);
        }

        if (settingsService.GetSetting<bool>("AutoUpdate", false) == false)
        {
            settingsService.SetSetting("AutoUpdate", true);
        }

        if (settingsService.GetSetting<int>("UpdateCheckInterval", 0) == 0)
        {
            settingsService.SetSetting("UpdateCheckInterval", 6);
        }
    }
}
```

### Migration Strategy

#### Breaking Change Approach
- [ ] **Manual Database Deletion** - Users must manually delete existing databases
- [ ] **Application Restart** - Full application restart required
- [ ] **No Backwards Compatibility** - Not maintaining compatibility with old schemas
- [ ] **Clean Slate** - Start fresh with new schema design
- [ ] **User Notification** - Clear instructions for database deletion

#### Migration Steps
1. **Backup existing data** (if needed)
2. **Delete existing database files**
3. **Restart application**
4. **New schema applied automatically**
5. **Default settings initialized**

### Performance Optimizations

#### Query Improvements
```sql
-- Add indexes for performance
CREATE INDEX IX_tbl_images_DateCreated ON tbl_images(DateCreated);
CREATE INDEX IX_tbl_images_RelativePath ON tbl_images(RelativePath);
CREATE INDEX IX_tbl_image_metadata_DateTaken ON tbl_image_metadata(DateTaken);
CREATE INDEX IX_tbl_image_analysis_ImageCategory ON tbl_image_analysis(ImageCategory);
CREATE INDEX IX_tbl_app_settings_SettingName ON tbl_app_settings(SettingName);

-- Optimize common queries
-- Before: Multiple queries for settings
-- After: Single query with proper indexing
SELECT SettingName, SettingType, SettingValue
FROM tbl_app_settings
WHERE SettingName IN ('StartWithWindows', 'AutoUpdate', 'UpdateCheckInterval');
```

#### Caching Strategy
```csharp
public class CachedSettingsService
{
    private readonly IMemoryCache _cache;
    private readonly SettingsService _settingsService;
    private const string CacheKey = "AppSettings";

    public T GetSetting<T>(string settingName, T defaultValue = default(T))
    {
        var cacheKey = $"{CacheKey}_{settingName}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return _settingsService.GetSetting<T>(settingName, defaultValue);
        });
    }

    public void SetSetting<T>(string settingName, T value)
    {
        _settingsService.SetSetting(settingName, value);
        _cache.Remove($"{CacheKey}_{settingName}");
    }
}
```

### File Structure Changes

#### Database Scripts
- [ ] **Keep only**: DatabaseVersion_001.sql
- [ ] **Remove**: DatabaseVersion_002.sql, DatabaseVersion_003.sql, etc.
- [ ] **Update**: SCHEMA.md with new structure
- [ ] **Update**: README.md with migration instructions

#### Code Changes
- [ ] **Update**: DatabaseInitializationService
- [ ] **Create**: New SettingsService
- [ ] **Update**: All services that use settings
- [ ] **Update**: Configuration classes
- [ ] **Update**: Unit tests

### Testing Requirements

#### Database Tests
- [ ] Test new settings table structure
- [ ] Verify data type conversions
- [ ] Test performance with large datasets
- [ ] Verify index effectiveness
- [ ] Test concurrent access

#### Integration Tests
- [ ] Test database initialization
- [ ] Test settings persistence
- [ ] Test migration process
- [ ] Test application startup
- [ ] Test settings UI integration

#### Performance Tests
- [ ] Benchmark query performance
- [ ] Test with large photo collections
- [ ] Measure memory usage
- [ ] Test startup time impact
- [ ] Verify caching effectiveness

### User Impact and Communication

#### Breaking Change Notice
- [ ] **Clear Documentation** - Explain why this change is necessary
- [ ] **Migration Instructions** - Step-by-step guide for users
- [ ] **Data Backup Warning** - Remind users to backup important data
- [ ] **Benefits Explanation** - Explain improvements this brings
- [ ] **Timeline** - When this change will be implemented

#### User Instructions
```markdown
## Database Migration Required

Due to significant database improvements, you need to manually delete your existing database:

1. Close MyPhotoHelper completely
2. Delete the database file: `%LocalAppData%\MyPhotoHelper\MyPhotoHelper.db` (or similar)
3. Restart MyPhotoHelper
4. The application will create a new, improved database automatically

**Note**: This will reset all your settings and scan history. Photos will need to be rescanned.
```

### Implementation Priority

#### Phase 1: Schema Design (Week 1)
- [ ] Design new settings table structure
- [ ] Create consolidated DatabaseVersion_001.sql
- [ ] Update database models
- [ ] Create SettingsService

#### Phase 2: Implementation (Week 2)
- [ ] Update DatabaseInitializationService
- [ ] Implement new SettingsService
- [ ] Update all services to use new settings
- [ ] Add performance indexes

#### Phase 3: Testing and Migration (Week 3)
- [ ] Comprehensive testing
- [ ] Performance optimization
- [ ] Create migration documentation
- [ ] Prepare user communication

### Success Metrics

#### Performance Improvements
- [ ] Faster database queries (50%+ improvement)
- [ ] Reduced memory usage
- [ ] Faster application startup
- [ ] Better concurrent access performance
- [ ] Reduced database file size

#### Maintainability Improvements
- [ ] Easier to add new settings
- [ ] No schema changes for new features
- [ ] Better code organization
- [ ] Simplified database management
- [ ] Reduced technical debt

### Future Benefits

#### Scalability
- [ ] Better performance with large datasets
- [ ] Easier to add new features
- [ ] More flexible configuration system
- [ ] Better caching capabilities
- [ ] Improved query optimization

#### Developer Experience
- [ ] Easier to add new settings
- [ ] Better debugging capabilities
- [ ] Simplified database operations
- [ ] More maintainable codebase
- [ ] Better testing capabilities

### Priority and Impact

**High Priority** - This refactoring will significantly improve database performance, maintainability, and developer experience. The breaking change is acceptable since the application is not in production.

**Technical Impact**
- Improved database performance and scalability
- Better maintainability and code organization
- Reduced technical debt
- Foundation for future feature development
- Simplified settings management

### Related Issues
- Integrates with automatic startup feature (uses new settings)
- Supports UI/UX improvements (better performance)
- Enables future feature development (flexible settings)
- May affect existing user workflows (requires migration)

### Acceptance Criteria
- [ ] All database versions consolidated into single version
- [ ] New flexible settings table implemented
- [ ] SettingsService with type conversion working
- [ ] Performance indexes added and tested
- [ ] Database initialization updated
- [ ] All existing functionality preserved
- [ ] Migration documentation created
- [ ] User communication prepared
- [ ] Comprehensive testing completed
- [ ] Performance benchmarks met 