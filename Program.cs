using ClickableTransparentOverlay;
using PubgOverlay;
using PubgOverlay.uiaccess;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

string? updateUrl = null;
{
    Console.WriteLine("正在检测更新...");
    Console.WriteLine($"当前版本：{UpdateHelper.CurrentVersion}");

    // 访问 GitHub API 获取最新发布信息
    const string owner = "ChickenPige0n";
    const string repo = "PUBG-Overlay";
    // 硬编码了AccessToken，别搞o(╥﹏╥)o
    // ReSharper disable once StringLiteralTypo
    const string apiUrl =
        $"https://api.gitcode.com/api/v5/repos/{owner}/{repo}/releases/latest?access_token=M7MBrsiAhLWYELNhzGnB8bGx";
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
    try
    {
        var response = await httpClient.GetStringAsync(apiUrl);
        var release = JsonSerializer.Deserialize<GitHubRelease>(response);
        if (release != null)
        {
            var latestVersion = new Version(release.TagName.Replace("v", ""));
            if (latestVersion > UpdateHelper.CurrentVersion)
            {
                Console.WriteLine($"发现新版本：{release.TagName}，请访问以下链接下载更新：");
                Console.WriteLine("https://gitcode.com/ChickenPige0n/PUBG-Overlay/releases");
                foreach (var asset in release.Assets.Where(asset => asset.Type != "source"))
                {
                    if (asset.Name.EndsWith("win-x64.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        updateUrl = asset.BrowserDownloadUrl;
                        Console.WriteLine($"下载链接：{updateUrl}");
                    }
                    else
                    {
                        Console.WriteLine($"其他文件：{asset.Name} - {asset.BrowserDownloadUrl}");
                    }
                }
            }
            else
            {
                Console.WriteLine("当前已是最新版本。");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"检测更新失败：{ex.Message}");
    }
}

var isUiAccess = !args.Contains("--disable-uiaccess");
Console.WriteLine("UI access is " + (isUiAccess ? "enabled" : "disabled"));
var hideSettingsOnDisable = args.Contains("--hide-settings-on-disable");
Console.WriteLine("Hide settings on disable is " + hideSettingsOnDisable);

if (isUiAccess)
{
    Console.WriteLine("Preparing for UI access...");
    var result = UiAccessHelper.PrepareForUiAccess();
    if (result != 0)
    {
        Console.WriteLine($"PrepareForUIAccess failed with error code: {result}");
        return;
    }
}

using var overlay = new PubgOverlayRenderer(hideSettingsOnDisable, updateUrl);
overlay.ReplaceFont("assets/font.ttf", 15, FontGlyphRangeType.ChineseFull);
await overlay.Run();


public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public required string TagName { get; set; }
    [JsonPropertyName("assets")] public required List<ReleaseAsset> Assets { get; set; }

    public class ReleaseAsset
    {
        [JsonPropertyName("browser_download_url")]
        public required string BrowserDownloadUrl { get; set; }

        [JsonPropertyName("name")] public required string Name { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}