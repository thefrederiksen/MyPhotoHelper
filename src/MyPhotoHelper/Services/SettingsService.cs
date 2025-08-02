using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using System.Globalization;

namespace MyPhotoHelper.Services;

public interface ISettingsService
{
    Task<T> GetSettingAsync<T>(string settingName, T defaultValue = default!);
    Task SetSettingAsync<T>(string settingName, T value);
    Task<Dictionary<string, string>> GetAllSettingsAsync();
    Task SetMultipleSettingsAsync(Dictionary<string, object> settings);
    void ClearCache();
}

public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<MyPhotoHelperDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SettingsService> _logger;
    private const string CacheKeyPrefix = "setting_";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    public SettingsService(
        IDbContextFactory<MyPhotoHelperDbContext> contextFactory,
        IMemoryCache cache,
        ILogger<SettingsService> logger)
    {
        _contextFactory = contextFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetSettingAsync<T>(string settingName, T defaultValue = default!)
    {
        // Check cache first
        var cacheKey = $"{CacheKeyPrefix}{settingName}";
        if (_cache.TryGetValue<T>(cacheKey, out var cachedValue))
        {
            return cachedValue!;
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        
        var setting = await context.tbl_app_settings
            .FirstOrDefaultAsync(s => s.SettingName == settingName);

        if (setting == null)
        {
            _logger.LogDebug("Setting {SettingName} not found, returning default value", settingName);
            return defaultValue;
        }

        try
        {
            var value = ConvertFromString<T>(setting.SettingValue, setting.SettingType);
            
            // Cache the value
            _cache.Set(cacheKey, value, _cacheExpiration);
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting setting {SettingName} value", settingName);
            return defaultValue;
        }
    }

    public async Task SetSettingAsync<T>(string settingName, T value)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var settingType = GetTypeString<T>();
        var stringValue = ConvertToString(value);

        var setting = await context.tbl_app_settings
            .FirstOrDefaultAsync(s => s.SettingName == settingName);

        if (setting == null)
        {
            // Create new setting
            setting = new tbl_app_settings
            {
                SettingName = settingName,
                SettingType = settingType,
                SettingValue = stringValue,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };
            context.tbl_app_settings.Add(setting);
            _logger.LogInformation("Creating new setting {SettingName}", settingName);
        }
        else
        {
            // Update existing setting
            setting.SettingType = settingType;
            setting.SettingValue = stringValue;
            setting.ModifiedDate = DateTime.UtcNow;
            _logger.LogInformation("Updating setting {SettingName}", settingName);
        }

        await context.SaveChangesAsync();

        // Clear cache for this setting
        var cacheKey = $"{CacheKeyPrefix}{settingName}";
        _cache.Remove(cacheKey);
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        var settings = await context.tbl_app_settings
            .ToDictionaryAsync(s => s.SettingName, s => s.SettingValue);

        return settings;
    }

    public async Task SetMultipleSettingsAsync(Dictionary<string, object> settings)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        foreach (var kvp in settings)
        {
            var settingName = kvp.Key;
            var value = kvp.Value;
            var settingType = GetTypeString(value.GetType());
            var stringValue = ConvertToString(value);

            var setting = await context.tbl_app_settings
                .FirstOrDefaultAsync(s => s.SettingName == settingName);

            if (setting == null)
            {
                setting = new tbl_app_settings
                {
                    SettingName = settingName,
                    SettingType = settingType,
                    SettingValue = stringValue,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                };
                context.tbl_app_settings.Add(setting);
            }
            else
            {
                setting.SettingType = settingType;
                setting.SettingValue = stringValue;
                setting.ModifiedDate = DateTime.UtcNow;
            }

            // Clear cache for this setting
            var cacheKey = $"{CacheKeyPrefix}{settingName}";
            _cache.Remove(cacheKey);
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Updated {Count} settings", settings.Count);
    }

    public void ClearCache()
    {
        // In a real implementation, you might want to track all cache keys
        // For now, this is a placeholder
        _logger.LogInformation("Settings cache cleared");
    }

    private static T ConvertFromString<T>(string value, string settingType)
    {
        if (string.IsNullOrEmpty(value))
        {
            return default!;
        }

        var targetType = typeof(T);

        // Handle nullable types
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrEmpty(value))
                return default!;
            
            targetType = Nullable.GetUnderlyingType(targetType)!;
        }

        return settingType switch
        {
            "bool" => (T)(object)bool.Parse(value),
            "int" => (T)(object)int.Parse(value),
            "double" => (T)(object)double.Parse(value, CultureInfo.InvariantCulture),
            "datetime" => (T)(object)(string.IsNullOrEmpty(value) ? DateTime.MinValue : DateTime.Parse(value)),
            "string" => (T)(object)value,
            _ => (T)(object)value
        };
    }

    private static string ConvertToString<T>(T value)
    {
        if (value == null)
            return string.Empty;

        return value switch
        {
            bool b => b ? "1" : "0",
            DateTime dt => dt == DateTime.MinValue ? string.Empty : dt.ToString("O"),
            double d => d.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string GetTypeString<T>()
    {
        return GetTypeString(typeof(T));
    }

    private static string GetTypeString(Type type)
    {
        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        return type.Name.ToLower() switch
        {
            "boolean" => "bool",
            "int32" => "int",
            "double" => "double",
            "datetime" => "datetime",
            "string" => "string",
            _ => "string"
        };
    }
}