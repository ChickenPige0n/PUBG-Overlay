using System.Drawing;
using System.Numerics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace PubgOverlay;

public class OpenCvManager
{
    public static readonly Point MapPos = new(1430, 593);
    public static readonly Size MapSize = new(457, 457);

    public List<Mat> PlayerTemplates { get; } = [];
    public List<Mat> TargetTemplates { get; } = [];


    public OpenCvManager()
    {
        PlayerTemplates.Add(new Mat());
        TargetTemplates.Add(new Mat());
        var allMat = CvInvoke.Imread($"assets/persons_1K.png");
        PlayerTemplates =
        [
            new Mat(allMat, new Rectangle(0, 0, 18, 18)),
            new Mat(allMat, new Rectangle(18, 0, 18, 18)),
            new Mat(allMat, new Rectangle(36, 0, 18, 18)),
            new Mat(allMat, new Rectangle(54, 0, 18, 18))
        ];
        allMat = CvInvoke.Imread($"assets/points_1K.png");
        TargetTemplates =
        [
            new Mat(allMat, new Rectangle(0, 0, 16, 20)),
            new Mat(allMat, new Rectangle(16, 0, 16, 20)),
            new Mat(allMat, new Rectangle(32, 0, 16, 20)),
            new Mat(allMat, new Rectangle(48, 0, 16, 20))
        ];
    }

    public enum TemplateType
    {
        Player = 1,
        Target = 2
    }

    public Mat GetTemplate(TemplateType type, int index)
    {
        return type switch
        {
            TemplateType.Player => PlayerTemplates[index].Clone(),
            TemplateType.Target => TargetTemplates[index].Clone(),
            _ => new Mat()
        };
    }

    public Vector2 PlayerTemplateSize(int team)
    {
        return S2V(new Size(PlayerTemplates[team].Cols, PlayerTemplates[team].Rows));
    }

    public Vector2 TargetTemplateSize(int team)
    {
        return S2V(new Size(TargetTemplates[team].Cols, TargetTemplates[team].Rows));
    }

    public (double distance, Vector2 playerPos, Vector2 targetPos)? GetDistance(int playerIndex, int targetIndex,
        Size size, bool fullScreen = false)
    {
        using var playerTemplate = GetTemplate(TemplateType.Player, playerIndex);
        using var targetTemplate = GetTemplate(TemplateType.Target, targetIndex);
        var capOffset = new Size(100, 100);
        var beginX = fullScreen ? capOffset.Width : MapPos.X;
        var beginY = fullScreen ? capOffset.Height : MapPos.Y;
        var sizeX = fullScreen ? size.Width - capOffset.Width : MapSize.Width;
        var sizeY = fullScreen ? size.Height - capOffset.Height : MapSize.Height;

        var mapMat = ScreenReader.Capture(beginX, beginY, sizeX, sizeY);
        
        using var leachedMap = LeachColor(mapMat);
        using var leachedMap1 = leachedMap.Clone();
        using var playerResultMat = new Mat();
        CvInvoke.MatchTemplate(leachedMap, playerTemplate, playerResultMat, TemplateMatchingType.CcoeffNormed);
        
        var playerMinLoc = new Point();
        var playerMinVal = 0.0;
        
        var playerMaxLoc = new Point();
        var playerMax = 0.0;
        CvInvoke.MinMaxLoc(playerResultMat, ref playerMinVal, ref playerMax, ref playerMinLoc, ref playerMaxLoc);

        using var targetResultMat = new Mat();
        CvInvoke.MatchTemplate(leachedMap1, targetTemplate, targetResultMat, TemplateMatchingType.CcoeffNormed);
        
        var targetMinLoc = new Point();
        var targetMinVal = 0.0;
        
        var targetMaxLoc = new Point();
        var targetMax = 0.0;
        CvInvoke.MinMaxLoc(targetResultMat, ref targetMinVal, ref targetMax, ref targetMinLoc, ref targetMaxLoc);

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
        // TODO: support different screen resolution
        // var scaleFactor = new Vector2(size.Width, size.Height) / new Vector2(1920, 1080);
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
    /// <param name="inputImage">传递的mat图像</param>
    /// <returns>去除杂色后的mat图像</returns>
    public static Mat LeachColor(Mat inputImage)
    {
        var lower = new ScalarArray(new MCvScalar(1.0, 100.0, 100.0));
        var upper = new ScalarArray(new MCvScalar(109.0, 255.0, 255.0));

        using var rgbImage = new Mat();
        CvInvoke.CvtColor(inputImage, rgbImage, ColorConversion.Bgra2Rgb);
        using var hsvImage = new Mat();
        CvInvoke.CvtColor(rgbImage, hsvImage, ColorConversion.Rgb2Hsv);

        using var mask = new Mat();
        CvInvoke.InRange(hsvImage, lower, upper, mask);

        using var resultRgba = new Mat();
        CvInvoke.BitwiseAnd(inputImage, inputImage, resultRgba, mask);
        using var resultRgb = new Mat();
        CvInvoke.CvtColor(resultRgba, resultRgb, ColorConversion.Rgba2Rgb);
        return resultRgb.Clone();
    }
}