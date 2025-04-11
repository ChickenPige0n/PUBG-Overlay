using System.Numerics;
using HPPH;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace PubgOverlay;

using ScreenCapture.NET;

public class ScreenReader
{
    public static ScreenReader Instance { get; } = new();
    // static int count = 0;
    private static readonly IScreenCaptureService ScreenCaptureService;
    private static (int x, int y, int w, int h) _captureSize;

    static ScreenReader()
    {
        ScreenCaptureService = new DX11ScreenCaptureService();
        var size = GetDisplaySize();
        SetCaptureZone(100, 100, size.width - 100, size.height - 100);
    }

    public static void SetCaptureZone(int x, int y, int w, int h)
    {
        Console.WriteLine($"set capture zone: {x}, {y}, {w}, {h}");
        // Get all available graphics cards
        var graphicsCards = ScreenCaptureService.GetGraphicsCards();
        // Get the displays from the graphics card(s) you are interested in
        var displays = ScreenCaptureService.GetDisplays(graphicsCards.First());
        var screenCapture = ScreenCaptureService.GetScreenCapture(displays.First());
        screenCapture.UnregisterCaptureZone(_captureZone);
        // Register a capture zone for the entire screen
        _captureZone = screenCapture.RegisterCaptureZone(x, y, w, h);
        _captureSize = (x, y, w, h);
    }

    public static (int width, int height) GetDisplaySize()
    {
        var display = ScreenCaptureService.GetDisplays(ScreenCaptureService.GetGraphicsCards().First()).First();
        return (display.Width, display.Height);
    }

    private static ICaptureZone _captureZone = null!;

    public static Mat Capture(int beginX, int beginY, int sizeX, int sizeY)
    {
        if (beginX != _captureSize.x || beginY != _captureSize.y || sizeX != _captureSize.w || sizeY != _captureSize.h)
        {
            SetCaptureZone(beginX, beginY, sizeX, sizeY);
        }
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
        using (_captureZone.Lock())
        {
            var image = _captureZone.Image.AsRefImage<ColorBGRA>();
            // save image as png 


            var matrix = new Mat(image.Height, image.Width, DepthType.Cv8U, ColorBGRA.ColorFormat.BytesPerPixel);
            unsafe
            {
                image.CopyTo(new Span<ColorBGRA>((void*)matrix.DataPointer, image.Width * image.Height));
            }

            //matrix.Save($"assets/screenshoot_{count}.png");
            // count++;
            return matrix;
        }
    }
}