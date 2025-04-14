using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace PubgOverlay;

public class OpenCvManager
{
    public static readonly Point MapPos = new(1430, 593);
    public static readonly Size MapSize = new(457, 457);
    public static readonly CudaTemplateMatching Matcher = new(DepthType.Cv8U, 3, TemplateMatchingType.CcoeffNormed);

    public List<GpuMat> PlayerTemplatesGpu { get; }
    public List<GpuMat> TargetTemplatesGpu { get; }

    public OpenCvManager()
    {
        using var allPersonMat = CvInvoke.Imread($"assets/persons_1K.png");
        PlayerTemplatesGpu =
        [
            new GpuMat(new Mat(allPersonMat, new Rectangle(0 , 0, 18, 18))),
            new GpuMat(new Mat(allPersonMat, new Rectangle(18, 0, 18, 18))),
            new GpuMat(new Mat(allPersonMat, new Rectangle(36, 0, 18, 18))),
            new GpuMat(new Mat(allPersonMat, new Rectangle(54, 0, 18, 18)))
        ];
        using var allPointMat = CvInvoke.Imread($"assets/points_1K.png");
        TargetTemplatesGpu =
        [
            new GpuMat(new Mat(allPointMat, new Rectangle(0 , 0, 16, 20))),
            new GpuMat(new Mat(allPointMat, new Rectangle(16, 0, 16, 20))),
            new GpuMat(new Mat(allPointMat, new Rectangle(32, 0, 16, 20))),
            new GpuMat(new Mat(allPointMat, new Rectangle(48, 0, 16, 20)))
        ];
        // download
    }

    public enum TemplateType
    {
        Player = 1,
        Target = 2
    }

    public GpuMat GetTemplateGpu(TemplateType type, int index)
    {
        return type switch
        {
            TemplateType.Player => PlayerTemplatesGpu[index],
            TemplateType.Target => TargetTemplatesGpu[index],
            _ => new GpuMat()
        };
    }

    public Vector2 PlayerTemplateSize(int team)
    {
        return S2V(new Size(PlayerTemplatesGpu[team].Size.Width, PlayerTemplatesGpu[team].Size.Height));
    }

    public Vector2 TargetTemplateSize(int team)
    {
        return S2V(new Size(TargetTemplatesGpu[team].Size.Width, TargetTemplatesGpu[team].Size.Height));
    }

    public (double distance, Vector2 playerPos, Vector2 targetPos)? GetDistance(int playerIndex, int targetIndex,
        Size size, bool fullScreen = false)
    {
        using var scope = new ScopeTimer("GetDistance");
        using var playerTemplate = GetTemplateGpu(TemplateType.Player, playerIndex);
        using var targetTemplate = GetTemplateGpu(TemplateType.Target, targetIndex);
        var capOffset = new Size(100, 100);
        var beginX = fullScreen ? capOffset.Width : MapPos.X;
        var beginY = fullScreen ? capOffset.Height : MapPos.Y;
        var sizeX = fullScreen ? size.Width - capOffset.Width : MapSize.Width;
        var sizeY = fullScreen ? size.Height - capOffset.Height : MapSize.Height;

        var mapMat = ScreenReader.Capture(beginX, beginY, sizeX, sizeY);
        var mapMatCpu = new Mat();
        mapMat.Download(mapMatCpu);
        // I failed to make it gpu since it will bail "Unable to find an entry point named 'cudaInRange' in DLL 'cvextern'."
        using var leachedMap = LeachColorGpu(mapMatCpu);
        using var leachedMapGpu = new GpuMat(leachedMap);
        Console.WriteLine(leachedMapGpu.NumberOfChannels);
        using var playerTemplateGpu = new GpuMat(playerTemplate);
        using var targetTemplateGpu = new GpuMat(targetTemplate);
        using var playerResultGpu = new GpuMat();
        using var targetResultGpu = new GpuMat();
        using (var _ = new ScopeTimer("MatchTemplate for player"))
        {
            Matcher.Match(leachedMapGpu, playerTemplateGpu, playerResultGpu);
        }

        using (var _ = new ScopeTimer("MatchTemplate for target"))
        {
            Matcher.Match(leachedMapGpu, targetTemplateGpu, targetResultGpu);
        }

        // Find player location
        var playerMinLoc = new Point();
        var playerMinVal = 0.0;
        var playerMaxLoc = new Point();
        var playerMax = 0.0;
        CudaInvoke.MinMaxLoc(playerResultGpu, ref playerMinVal, ref playerMax, ref playerMinLoc, ref playerMaxLoc);

        // Find target location
        var targetMinLoc = new Point();
        var targetMinVal = 0.0;
        var targetMaxLoc = new Point();
        var targetMax = 0.0;
        CudaInvoke.MinMaxLoc(targetResultGpu, ref targetMinVal, ref targetMax, ref targetMinLoc, ref targetMaxLoc);

        mapMat.Dispose();

        if (targetMax < 0.5 || playerMax < 0.5)
        {
            return null;
        }

        var tOffset = TargetTemplateSize(targetIndex);
        tOffset.X /= 2;
        var pOffset = PlayerTemplateSize(playerIndex);
        pOffset.X /= 2;
        var playerPos = new Point
        {
            X = playerMaxLoc.X + (int)Math.Round(pOffset.X) + capOffset.Width,
            Y = playerMaxLoc.Y + (int)Math.Round(pOffset.Y) + capOffset.Height
        };
        var targetPos = new Point
        {
            X = targetMaxLoc.X + (int)Math.Round(tOffset.X) + capOffset.Width,
            Y = targetMaxLoc.Y + (int)Math.Round(tOffset.Y) + capOffset.Height
        };

        return (PointDistance(playerPos, targetPos), P2V(playerPos), P2V(targetPos));
    }

    private static double PointDistance(Point a, Point b)
    {
        return double.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    // point to vector 2 conversion
    private static Vector2 P2V(Point p)
    {
        return new Vector2(p.X, p.Y);
    }

    // size to vector 2 conversion
    private static Vector2 S2V(Size s)
    {
        return new Vector2(s.Width, s.Height);
    }


    /// <summary>
    /// 去除mat杂色
    /// </summary>
    /// <param name="inputImage">需要去色的Mat图像, 要求像素格式为RGB</param>
    /// <returns>去除杂色后的mat图像</returns>
    public static Mat LeachColorGpu(Mat inputImage)
    {
        var lower = new ScalarArray(new MCvScalar(1.0, 100.0, 100.0));
        var upper = new ScalarArray(new MCvScalar(109.0, 255.0, 255.0));

        using var hsvImage = new Mat();
        CvInvoke.CvtColor(inputImage, hsvImage, ColorConversion.Rgb2Hsv);

        using var mask = new Mat();
        CvInvoke.InRange(hsvImage, lower, upper, mask);

        var resultRgb = new Mat();
        CvInvoke.BitwiseAnd(inputImage, inputImage, resultRgb, mask);
        // var resultRgb = new GpuMat();
        // CudaInvoke.CvtColor(resultRgba, resultRgb, ColorConversion.Rgba2Rgb);
        return resultRgb;
    }
}