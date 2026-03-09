using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SABepInExManager.Models;

namespace SABepInExManager.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<AppConfig> LoadAsync()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
        {
            return new AppConfig();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AppConfig>(stream, JsonOptions) ?? new AppConfig();
    }

    public async Task SaveAsync(AppConfig config)
    {
        var path = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions);
    }

    private static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "SABepInExManager");
        return Path.Combine(appFolder, PathConstants.ConfigFileName);
    }
}


