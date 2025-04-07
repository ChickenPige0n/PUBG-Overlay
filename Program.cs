using ClickableTransparentOverlay;
using PubgOverlay;
using System.Runtime.InteropServices;

var isUiAccess = !args.Contains("--disable-uiaccess");
Console.WriteLine("UI access is " + (isUiAccess ? "enabled" : "disabled"));
var hideSettingsOnDisable = args.Contains("--hide-settings-on-disable");
Console.WriteLine("Hide settings on disable is " + hideSettingsOnDisable);

if (isUiAccess)
{
    Console.WriteLine("Preparing for UI access...");
    var result = PrepareForUIAccess();
    if (result != 0)
    {
        Console.WriteLine($"PrepareForUIAccess failed with error code: {result}");
        return;
    }
}
using var overlay = new PubgOverlayRenderer(hideSettingsOnDisable);
overlay.ReplaceFont("font.ttf", 15, FontGlyphRangeType.ChineseFull);
await overlay.Run();
return;

[DllImport("uiaccess.dll", EntryPoint = "PrepareForUIAccess", CallingConvention = CallingConvention.Cdecl)]
static extern int PrepareForUIAccess();