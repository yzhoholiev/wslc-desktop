using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using WslcDesktop.Services;

namespace WslcDesktop.Models;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ContainerDto))]
[JsonSerializable(typeof(List<ContainerDto>))]
[JsonSerializable(typeof(ContainerStatsDto))]
[JsonSerializable(typeof(List<ContainerStatsDto>))]
[JsonSerializable(typeof(ImageDto))]
[JsonSerializable(typeof(List<ImageDto>))]
[JsonSerializable(typeof(ImageInspectDto))]
[JsonSerializable(typeof(List<ImageInspectDto>))]
[JsonSerializable(typeof(VolumeDto))]
[JsonSerializable(typeof(List<VolumeDto>))]
[JsonSerializable(typeof(NetworkDto))]
[JsonSerializable(typeof(List<NetworkDto>))]
[JsonSerializable(typeof(WslcInfoDto))]
internal sealed partial class WslcJsonContext : JsonSerializerContext
{
    /// <summary>
    ///     Parses wslc list output, which is either a JSON array or one JSON object per line.
    /// </summary>
    public static List<T> ParseList<T>(string output, JsonTypeInfo<T> itemInfo, JsonTypeInfo<List<T>> listInfo)
    {
        string trimmed = output.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        try
        {
            if (trimmed[0] == '[')
            {
                return JsonSerializer.Deserialize(trimmed, listInfo) ?? [];
            }

            var items = new List<T>();
            foreach (string line in trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith('{') && JsonSerializer.Deserialize(line, itemInfo) is { } item)
                {
                    items.Add(item);
                }
            }

            return items;
        }
        catch (JsonException ex)
        {
            throw new CliException($"Unexpected wslc output: {ex.Message}");
        }
    }
}
