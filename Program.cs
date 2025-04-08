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
    // 获取当前应用程序的版本号
    var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version;
    if (currentVersion == null)
    {
        Console.WriteLine("无法获取当前版本号。");
        return;
    }
    Console.WriteLine($"当前版本：{currentVersion}");

    // 访问 GitHub API 获取最新发布信息
    const string owner = "ChickenPige0n";
    const string repo = "PUBG-Overlay";
    const string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
    try
    {
        var response = await httpClient.GetStringAsync(apiUrl);
        var release = JsonSerializer.Deserialize<GitHubRelease>(response);
        if (release != null)
        {
            var latestVersion = new Version(release.TagName.Replace("v", ""));
            // 比较当前版本和最新版本
            if (latestVersion > currentVersion)
            {
                Console.WriteLine($"发现新版本：{release.TagName}，请访问以下链接下载更新：");
                Console.WriteLine(release.HtmlUrl);
                updateUrl = release.HtmlUrl;
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


// GitHub Releases API 返回的 JSON 结构
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public required string TagName { get; set; }
    [JsonPropertyName("html_url")]
    public required string HtmlUrl { get; set; }
}
