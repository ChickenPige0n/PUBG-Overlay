using System.Drawing;
using System.Numerics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace PubgOverlay;

public class OpenCvManager
{
    public static readonly Point MapPos = new Point(1430, 593);
    public static readonly Size MapSize = new Size(457, 457);

    public List<Mat> PlayerTemplates { get; } = [];
    public List<Mat> TargetTemplates { get; } = [];


    public OpenCvManager()
    {
        PlayerTemplates.Add(new Mat());
        TargetTemplates.Add(new Mat());
        foreach (var i in Enumerable.Range(1, 4))
        {
            PlayerTemplates.Add(CvInvoke.Imread($"assets/person1K_{i}.png"));
            TargetTemplates.Add(CvInvoke.Imread($"assets/point1K_{i}.png"));
        }
    }

    public Mat GetTemplate(int type, int index)
    {
        return type switch
        {
            1 => PlayerTemplates[index].Clone(),
            2 => TargetTemplates[index].Clone(),
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
        using var playerTemplate = GetTemplate(1, playerIndex);
        using var targetTemplate = GetTemplate(2, targetIndex);
        var beginX = fullScreen ? 100 : MapPos.X;
        var beginY = fullScreen ? 100 : MapPos.Y;
        var sizeX = fullScreen ? size.Height : MapSize.Width;
        var sizeY = fullScreen ? size.Width : MapSize.Height;

        var mapMat = ScreenReader.Capture(beginX, beginY, sizeX, sizeY);
        using var leachedMap = LeachColor(mapMat);

        using var playerResultMat = new Mat();
        CvInvoke.MatchTemplate(leachedMap, playerTemplate, playerResultMat, TemplateMatchingType.CcoeffNormed);
        var minVal = 0.0;
        var minLoc = new Point();
        var playerMaxLoc = new Point();
        var playerMax = 0.0;
        CvInvoke.MinMaxLoc(playerResultMat, ref minVal,   ref playerMax, ref minLoc, ref playerMaxLoc);

        using var targetResultMat = new Mat();
        CvInvoke.MatchTemplate(leachedMap, targetTemplate, targetResultMat, TemplateMatchingType.CcoeffNormed);

        var targetMaxLoc = new Point();
        var targetMax = 0.0;
        CvInvoke.MinMaxLoc(targetResultMat, ref minVal, ref targetMax , ref minLoc, ref targetMaxLoc);

        mapMat.Dispose();

        if (targetMax < 0.5 || playerMax < 0.5)
        {
            return null;
        }

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
            X = playerMaxLoc.X + (int)Math.Round(pOffset.X),
            Y = playerMaxLoc.Y + (int)Math.Round(pOffset.Y)
        };
        var targetPos = new Point
        {
            X = targetMaxLoc.X + (int)Math.Round(tOffset.X),
            Y = targetMaxLoc.Y + (int)Math.Round(tOffset.Y)
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