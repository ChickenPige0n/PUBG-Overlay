using ClickableTransparentOverlay;
using PubgOverlay;
using PubgOverlay.uiaccess;

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
using var overlay = new PubgOverlayRenderer(hideSettingsOnDisable);
overlay.ReplaceFont("assets/font.ttf", 15, FontGlyphRangeType.ChineseFull);
await overlay.Run();
