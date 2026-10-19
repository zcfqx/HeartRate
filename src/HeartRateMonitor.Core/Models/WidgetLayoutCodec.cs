using System.Text.Json;

namespace HeartRateMonitor.Core.Models;

/// <summary>
/// 组件看板布局的序列化工具：布局就是一份有序的组件 Id 列表（JSON 字符串），
/// 持久化在设置的 <c>Widgets</c> 键中。纯逻辑，方便单元测试。
/// </summary>
public static class WidgetLayoutCodec
{
    /// <summary>
    /// 解析布局；JSON 为空或损坏时回退到 <paramref name="fallback"/>（通常是插件的默认组件）。
    /// 结果去重且保持顺序。
    /// </summary>
    public static List<string> Parse(string? json, IEnumerable<string>? fallback = null)
    {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids is not null)
                {
                    AddRange(result, ids);
                }
            }
            catch (JsonException)
            {
                // 布局损坏时静默回退到默认布局
            }
        }

        if (result.Count == 0 && fallback is not null)
        {
            AddRange(result, fallback);
        }

        return result;
    }

    /// <summary>把组件 Id 列表序列化为 JSON；空白 Id 会被忽略。</summary>
    public static string Serialize(IEnumerable<string>? ids)
        => JsonSerializer.Serialize((ids ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList());

    private static void AddRange(List<string> target, IEnumerable<string> ids)
    {
        foreach (var raw in ids)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var id = raw.Trim();
            if (!target.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(id);
            }
        }
    }
}
