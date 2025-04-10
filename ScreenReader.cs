using System.Numerics;
using HPPH;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace PubgOverlay;

using ScreenCapture.NET;

public class ScreenReader
{
    public static ScreenReader Instance { get; } = new();
    private static readonly IScreenCaptureService ScreenCaptureService;

    static ScreenReader()
    {
        ScreenCaptureService = new DX11ScreenCaptureService();
        // Get all available graphics cards
        var graphicsCards = ScreenCaptureService.GetGraphicsCards();
        // Get the displays from the graphics card(s) you are interested in
        var displays = ScreenCaptureService.GetDisplays(graphicsCards.First());
        // Create a screen-capture for all screens you want to capture
        var enumerable = displays as Display[] ?? displays.ToArray();
        var screenCapture = ScreenCaptureService.GetScreenCapture(enumerable.First());
        // Register a capture zone for the entire screen
        BigCaptureZone = screenCapture.RegisterCaptureZone(0, 0, enumerable.First().Width, enumerable.First().Height);
    }

    public static (int width, int height) GetDisplaySize()
    {
        var display = ScreenCaptureService.GetDisplays(ScreenCaptureService.GetGraphicsCards().First()).First();
        return (display.Width, display.Height);
    }

    private static readonly ICaptureZone BigCaptureZone;

    public static Mat Capture(int beginX, int beginY, int sizeX, int sizeY)
    {
        // Get all available graphics cards
        var graphicsCards = ScreenCaptureService.GetGraphicsCards();

        // Get the displays from the graphics card(s) you are interested in
        var displays = ScreenCaptureService.GetDisplays(graphicsCards.First());

        // Create a screen-capture for all screens you want to capture
        var screenCapture = ScreenCaptureService.GetScreenCapture(displays.First());

        // Capture the screen
        // This should be done in a loop on a separate thread as CaptureScreen blocks if the screen is not updated (still image).
        screenCapture.CaptureScreen();

        // Do something with the captured image - e.g. access all pixels (same could be done with topLeft)

        //Lock the zone to access the data. Remember to dispose the returned disposable to unlock again.
        using (BigCaptureZone.Lock())
        {
            var image = BigCaptureZone.Image.AsRefImage<ColorBGRA>();
            var matrix = new Mat(image.Height, image.Width, DepthType.Cv8U, ColorBGRA.ColorFormat.BytesPerPixel);
            unsafe
            {
                image.CopyTo(new Span<ColorBGRA>((void*)matrix.DataPointer, image.Width * image.Height));
            }

            return matrix;
        }
    }
}