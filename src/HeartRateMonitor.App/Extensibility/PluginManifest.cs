using System.IO;
using System.Text.Json;

namespace HeartRateMonitor.App.Extensibility;

/// <summary>
/// 外部插件清单（plugins/&lt;plugin&gt;/plugin.json）。
/// </summary>
internal sealed class PluginManifest
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string EntryAssembly { get; init; } = string.Empty;
    public string EntryType { get; init; } = string.Empty;

    /// <summary>清单文件所在目录（运行期填充，不参与反序列化）。</summary>
    public string Directory { get; set; } = string.Empty;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>读取并校验清单；失败时返回 null 并给出错误信息。</summary>
    public static PluginManifest? Load(string directory, out string? error)
    {
        error = null;
        directory = Path.GetFullPath(directory);

        var manifestPath = Path.Combine(directory, "plugin.json");
        if (!File.Exists(manifestPath))
        {
            error = "缺少 plugin.json";
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), Options);
            if (manifest is null)
            {
                error = "plugin.json 解析结果为空";
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                error = "plugin.json 缺少 id";
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
            {
                error = "plugin.json 缺少 entryAssembly";
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.EntryType))
            {
                error = "plugin.json 缺少 entryType";
                return null;
            }

            manifest.Directory = directory;
            return manifest;
        }
        catch (Exception ex)
        {
            error = $"plugin.json 解析失败: {ex.Message}";
            return null;
        }
    }
}
