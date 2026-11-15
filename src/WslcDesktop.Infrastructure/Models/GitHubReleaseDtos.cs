using System.Text.Json.Serialization;

namespace WslcDesktop.Models;

internal sealed class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("draft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAssetDto>? Assets { get; set; }
}

internal sealed class GitHubAssetDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? DownloadUrl { get; set; }
}

[JsonSerializable(typeof(GitHubReleaseDto))]
[JsonSerializable(typeof(List<GitHubReleaseDto>))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
