using HPPH;
using OpenCvSharp;
namespace PubgOverlay;
using ScreenCapture.NET;

public class ScreenReader
{
    
    public static ScreenReader Instance { get; } = new();
    private static IScreenCaptureService screenCaptureService;
    public ScreenReader()
    {
        screenCaptureService = new DX11ScreenCaptureService();
    }
    
    public static Mat Capture(int beginX, int beginY, int sizeX, int sizeY)
    {
        // Get all available graphics cards
        var graphicsCards = screenCaptureService.GetGraphicsCards();
        
        // Get the displays from the graphics card(s) you are interested in
        var displays = screenCaptureService.GetDisplays(graphicsCards.First());

        // Create a screen-capture for all screens you want to capture
        IScreenCapture? screenCapture = screenCaptureService.GetScreenCapture(displays.First());
        
        var partialArea = screenCapture!.RegisterCaptureZone(beginX, beginY, sizeX, sizeY);
        
        // Capture the screen
        // This should be done in a loop on a separate thread as CaptureScreen blocks if the screen is not updated (still image).
        screenCapture.CaptureScreen();
        
        // Do something with the captured image - e.g. access all pixels (same could be done with topLeft)
        
        //Lock the zone to access the data. Remember to dispose the returned disposable to unlock again.
        using (partialArea.Lock())
        {
            var image = partialArea.Image.AsRefImage<ColorBGRA>();
            var mat = new Mat(image.Height, image.Width, MatType.CV_8UC4);
            unsafe
            {
                image.CopyTo(new Span<ColorBGRA>(mat.DataPointer, image.Width * image.Height));
            }
            return mat;
        }
    }
}