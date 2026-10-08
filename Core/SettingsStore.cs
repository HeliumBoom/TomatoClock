using System.Text.Json;

namespace TomatoClock.Core;

public sealed class SettingsStore(string path)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TomatoClock", "settings.json");

    public AppSettings Load()
    {
        var result = new AppSettings();
        try
        {
            if (!File.Exists(path)) return result;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return result;
            result.WorkMinutes = ReadInt(root, nameof(result.WorkMinutes), 25, 1, 180);
            result.BreakMinutes = ReadInt(root, nameof(result.BreakMinutes), 5, 1, 180);
            result.Width = ReadInt(root, nameof(result.Width), 400, 120, 16384);
            result.Height = ReadInt(root, nameof(result.Height), 420, 64, 16384);
            result.NormalWidth = ReadInt(root, nameof(result.NormalWidth), 400, 360, 16384);
            result.NormalHeight = ReadInt(root, nameof(result.NormalHeight), 420, 280, 16384);
            result.AlwaysOnTop = ReadBool(root, nameof(result.AlwaysOnTop));
            result.SoundEnabled = ReadBool(root, nameof(result.SoundEnabled));
            result.TrayOnly = ReadBool(root, nameof(result.TrayOnly));
            result.OpacityPercent = ReadInt(root, nameof(result.OpacityPercent), 100, 10, 100);
            result.ClickThrough = ReadBool(root, nameof(result.ClickThrough));
            result.Compact = ReadBool(root, nameof(result.Compact));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException)
        { /* A corrupt or inaccessible preference file must not prevent startup. */ }
        return result;
    }

    public string? Save(AppSettings settings)
    {
        string? temporary = null;
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return "设置未能保存，本次使用不受影响。请检查本地应用数据目录的写入权限。"; }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private static int ReadInt(JsonElement root, string name, int fallback, int min, int max) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int number) && number >= min && number <= max ? number : fallback;

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
