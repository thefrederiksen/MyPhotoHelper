using Microsoft.Data.Sqlite;
using System.Text;
using System.Windows.Forms;

namespace MyPhotoHelper.Services;

public interface IDatabaseInitializationService
{
    Task<bool> InitializeDatabaseAsync(string connectionString);
    Task<int> GetCurrentVersionAsync(string connectionString);
    Task<bool> ApplyMigrationAsync(string connectionString, string scriptPath);
}

public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly ILogger<DatabaseInitializationService> _logger;
    private readonly string _databaseScriptsPath;

    public DatabaseInitializationService(ILogger<DatabaseInitializationService> logger)
    {
        _logger = logger;
        _databaseScriptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database");
        
        // Log the scripts path for debugging
        _logger.LogInformation($"Database scripts path: {_databaseScriptsPath}");
        
        if (!Directory.Exists(_databaseScriptsPath))
        {
            _logger.LogError($"Database scripts directory does not exist: {_databaseScriptsPath}");
        }
        else
        {
            var sqlFiles = Directory.GetFiles(_databaseScriptsPath, "*.sql");
            _logger.LogInformation($"Found {sqlFiles.Length} SQL files in database scripts directory");
            foreach (var file in sqlFiles)
            {
                _logger.LogDebug($"SQL file: {Path.GetFileName(file)}");
            }
        }
    }

    public async Task<bool> InitializeDatabaseAsync(string connectionString)
    {
        try
        {
            // Create the database file if it doesn't exist
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;
            var dbDirectory = Path.GetDirectoryName(dbPath);
            
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                _logger.LogInformation("Created database directory: {Directory}", dbDirectory);
            }

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // Check if database is empty (new)
            var tableCount = await GetTableCountAsync(connection);
            
            if (tableCount == 0)
            {
                _logger.LogInformation("Database is empty. Running initial creation script...");
                
                // Run the initial database script
                var initialScriptPath = Path.Combine(_databaseScriptsPath, "DatabaseVersion_001.sql");
                if (!File.Exists(initialScriptPath))
                {
                    _logger.LogError("Initial database script not found at: {Path}", initialScriptPath);
                    
                    // Show error to user
                    var message = $"Critical Error: Database initialization script not found!\n\n" +
                                 $"Expected location: {initialScriptPath}\n\n" +
                                 $"The application cannot start without the database scripts.\n" +
                                 $"Please ensure the Database folder is included in the application directory.";
                    
                    MessageBox.Show(message, "MyPhotoHelper - Critical Error", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    
                    return false;
                }

                var script = await File.ReadAllTextAsync(initialScriptPath);
                return await ExecuteSqlScriptAsync(connection, script);
            }
            else
            {
                _logger.LogDebug("Database already exists with {TableCount} tables", tableCount);
                
                // Check current version and apply any pending migrations
                var currentVersion = await GetCurrentVersionAsync(connection);
                _logger.LogDebug("Current database version: {Version}", currentVersion);
                
                // Apply any pending migration scripts
                var migrationFiles = Directory.GetFiles(_databaseScriptsPath, "DatabaseVersion_*.sql")
                    .Where(f => !f.EndsWith("_001.sql")) // Skip initial script
                    .Select(f => new 
                    { 
                        Path = f, 
                        FileName = Path.GetFileNameWithoutExtension(f),
                        Version = int.Parse(Path.GetFileNameWithoutExtension(f).Split('_')[1])
                    })
                    .Where(m => m.Version > currentVersion)
                    .OrderBy(m => m.Version) // Ensure sequential execution
                    .ToList();
                
                _logger.LogInformation($"Current database version: {currentVersion}. Found {migrationFiles.Count} pending migrations.");
                
                foreach (var migration in migrationFiles)
                {
                    _logger.LogInformation($"Applying migration version {migration.Version}: {Path.GetFileName(migration.Path)}");
                    
                    var script = await File.ReadAllTextAsync(migration.Path);
                    var success = await ExecuteSqlScriptAsync(connection, script);
                    
                    if (!success)
                    {
                        _logger.LogError($"Failed to apply migration version {migration.Version}");
                        
                        // Show error to user
                        var message = $"Database Migration Failed!\n\n" +
                                     $"Failed to apply database migration version {migration.Version}.\n" +
                                     $"Script: {Path.GetFileName(migration.Path)}\n\n" +
                                     $"Please check the application logs for more details.\n" +
                                     $"The application may not function correctly.";
                        
                        MessageBox.Show(message, "MyPhotoHelper - Migration Error", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        
                        return false;
                    }
                    
                    // Verify the version was updated correctly
                    var newVersion = await GetCurrentVersionAsync(connection);
                    if (newVersion != migration.Version)
                    {
                        _logger.LogError($"Migration {migration.Version} did not update version correctly. Expected {migration.Version}, got {newVersion}");
                        return false;
                    }
                    
                    _logger.LogInformation($"Successfully applied migration version {migration.Version}");
                }
                
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            return false;
        }
    }

    public async Task<int> GetCurrentVersionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            return await GetCurrentVersionAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current database version");
            return 0;
        }
    }

    private async Task<int> GetCurrentVersionAsync(SqliteConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Version FROM tbl_version LIMIT 1";
            
            var result = await command.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out var version))
            {
                return version;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Version table doesn't exist or is empty: {Message}", ex.Message);
        }
        
        return 0;
    }

    private async Task<int> GetTableCountAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) 
            FROM sqlite_master 
            WHERE type = 'table' 
            AND name NOT LIKE 'sqlite_%'";
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<bool> ExecuteSqlScriptAsync(SqliteConnection connection, string script)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            // Split the script by GO statements (if any) or semicolons
            var statements = script.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var statement in statements)
            {
                var sql = statement.Trim();
                if (string.IsNullOrWhiteSpace(sql))
                    continue;

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                
                _logger.LogDebug("Executing SQL: {Sql}", sql.Length > 100 ? sql.Substring(0, 100) + "..." : sql);
                
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (SqliteException sqlEx)
                {
                    _logger.LogError($"SQL Error executing statement: {sqlEx.Message}");
                    _logger.LogError($"Failed SQL: {sql}");
                    throw;
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Successfully executed database script");
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to execute database script");
            return false;
        }
    }

    public async Task<bool> ApplyMigrationAsync(string connectionString, string scriptPath)
    {
        try
        {
            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Migration script not found: {Path}", scriptPath);
                return false;
            }

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var script = await File.ReadAllTextAsync(scriptPath);
            var success = await ExecuteSqlScriptAsync(connection, script);
            
            if (success)
            {
                _logger.LogInformation("Successfully applied migration: {ScriptPath}", scriptPath);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply migration: {ScriptPath}", scriptPath);
            return false;
        }
    }
}