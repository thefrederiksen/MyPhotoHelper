# Database Initialization

## Overview

FaceVault uses SQLite as its database engine. The database is automatically created and initialized when the application starts if it doesn't exist.

## Database Location

The database is stored in the user's application data directory:
- **Windows**: `%APPDATA%\FaceVault\Database\facevault.db`
- **Example**: `C:\Users\YourName\AppData\Roaming\FaceVault\Database\facevault.db`

## Automatic Database Creation

When FaceVault starts, it performs the following steps:

1. **Directory Creation**: The PathService ensures all required directories exist, including:
   - `%APPDATA%\FaceVault\`
   - `%APPDATA%\FaceVault\Database\`
   - `%APPDATA%\FaceVault\Logs\`
   - `%APPDATA%\FaceVault\Settings\`
   - `%APPDATA%\FaceVault\Temp\`

2. **Database Initialization**: The DatabaseInitializationService checks if the database exists:
   - If the database file doesn't exist, SQLite creates it automatically
   - If the database is empty (0 tables), it runs `DatabaseVersion_001.sql`
   - If the database exists with tables, it checks the version and applies any migrations

3. **Schema Creation**: The `DatabaseVersion_001.sql` script creates:
   - `tbl_version` - Tracks database schema version
   - `tbl_images` - Core image file information
   - `tbl_image_metadata` - Image dimensions and EXIF data
   - `tbl_image_analysis` - AI analysis results
   - `tbl_app_settings` - Application settings (singleton)

4. **Optimization**: After creation, SQLite optimizations are applied:
   - `PRAGMA journal_mode=WAL;` - Write-Ahead Logging for better concurrency
   - `PRAGMA synchronous=NORMAL;` - Balance between safety and speed
   - `PRAGMA foreign_keys=ON;` - Enable foreign key constraints

## Manual Database Reset

To reset the database and start fresh:

1. **Close FaceVault** completely
2. **Delete the database file**:
   - Navigate to `%APPDATA%\FaceVault\Database\`
   - Delete `facevault.db` (and any .db-wal or .db-shm files)
3. **Start FaceVault** - A new database will be created automatically

## Database Status Page

The application includes a Database Status page (`/database-status`) that shows:
- Current database path and file size
- Database version
- Connection status
- Table statistics and row counts
- Quick stats (total images, analyzed images, etc.)
- Option to open the database folder in Windows Explorer

## Troubleshooting

If the database fails to initialize:

1. **Check Permissions**: Ensure the user has write permissions to `%APPDATA%\FaceVault\`
2. **Check Disk Space**: Ensure sufficient disk space is available
3. **Check Logs**: Review logs in `%APPDATA%\FaceVault\Logs\` for detailed error messages
4. **Manual Creation**: Use the Database Status page to manually trigger database creation

## Development Workflow

When making changes to the database schema during development:

### 1. Update SQL Scripts
First, modify the appropriate `DatabaseVersion_*.sql` file in the Database folder to include your schema changes.

### 2. Create Development Database
Run the Python database manager to create a fresh development database with your changes:

```bash
cd src/MyPhotoHelper/Python
python database_manager.py
```

This will:
- Create `dev_facevault.db` in the Database folder
- Apply all SQL scripts in version order
- Report success/failure of database creation

### 3. Regenerate EF Models
After the database is created, regenerate the Entity Framework models using Visual Studio:

1. **Open Visual Studio** and load the MyPhotoHelper project
2. **Open Package Manager Console**: Tools → NuGet Package Manager → Package Manager Console
3. **Select Project**: Ensure "MyPhotoHelper" is selected in the Default project dropdown
4. **Run Scaffolding Command**:
   ```powershell
   Scaffold-DbContext "Data Source=Database\dev_facevault.db" Microsoft.EntityFrameworkCore.Sqlite -OutputDir Models -ContextDir Data -Context MyPhotoHelperDbContext -Force -NoPluralize -UseDatabaseNames -NoOnConfiguring
   ```

This will regenerate:
- `Models/tbl_*.cs` - Updated entity models with your schema changes
- `Data/MyPhotoHelperDbContext.cs` - Updated DbContext with new tables/columns

### 4. Test and Build
After regenerating models:
1. Build the solution to check for compilation errors
2. Test your changes with the updated models
3. Commit both the SQL scripts and generated models

### Alternative CLI Approach
If you prefer command line over Visual Studio Package Manager Console:

```bash
# First ensure you have the EF Core CLI tools installed
dotnet tool install --global dotnet-ef

# Then run the scaffolding command
dotnet ef dbcontext scaffold "Data Source=Database\dev_facevault.db" Microsoft.EntityFrameworkCore.Sqlite --output-dir Models --context-dir Data --context MyPhotoHelperDbContext --force --no-pluralize --use-database-names --no-onconfiguring
```

## Development Notes

- The database uses Entity Framework Core with a database-first approach
- Always use the Python script to create dev databases to ensure consistency
- Models are auto-generated - never edit them manually as they'll be overwritten
- All database operations use async methods for better performance
- The schema is designed to be modular with separate tables for different concerns