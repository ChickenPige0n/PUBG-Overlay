using System.Numerics;
using OpenCvSharp;

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
            PlayerTemplates.Add(Cv2.ImRead($"assets/person1K_{i}.png"));
            TargetTemplates.Add(Cv2.ImRead($"assets/point1K_{i}.png"));
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
        return S2V(PlayerTemplates[team].Size());
    }

    public Vector2 TargetTemplateSize(int team)
    {
        return S2V(TargetTemplates[team].Size());
    }

    public (double distance, Vector2 playerPos, Vector2 targetPos)? GetDistance(int playerIndex, int targetIndex,
        bool fullScreen = false)
    {
        var playerTemplate = GetTemplate(1, playerIndex);
        var targetTemplate = GetTemplate(2, targetIndex);
        var beginX = fullScreen ? 0 : MapPos.X;
        var beginY = fullScreen ? 0 : MapPos.Y;
        var sizeX = fullScreen ? 1920 : MapSize.Width;
        var sizeY = fullScreen ? 1080 : MapSize.Height;

        var mapMat = ScreenReader.Capture(beginX, beginY, sizeX, sizeY);
        var playerMatLeach = LeachColor(mapMat, playerIndex);
        var targetMatLeach = LeachColor(mapMat, targetIndex);

        var playerResultMat = new Mat();
        Cv2.MatchTemplate(playerMatLeach, playerTemplate, playerResultMat, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(playerResultMat, out _, out var playerMax, out _, out var playerMaxLoc);

        var targetResultMat = new Mat();
        Cv2.MatchTemplate(targetMatLeach, targetTemplate, targetResultMat, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(targetResultMat, out _, out var targetMax, out _, out var targetMaxLoc);

        mapMat.Dispose();
        playerResultMat.Dispose();
        targetResultMat.Dispose();
        playerMatLeach.Dispose();
        targetMatLeach.Dispose();
        playerTemplate.Dispose();
        targetTemplate.Dispose();

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
        var playerPos = new Point();
        playerPos.X = playerMaxLoc.X + (int)Math.Round(pOffset.X);
        playerPos.Y = playerMaxLoc.Y + (int)Math.Round(pOffset.Y);
        var targetPos = new Point();
        targetPos.X = targetMaxLoc.X + (int)Math.Round(tOffset.X);
        targetPos.Y = targetMaxLoc.Y + (int)Math.Round(tOffset.Y);
        return (playerPos.DistanceTo(targetPos), P2V(playerPos), P2V(targetPos));
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
    /// <param name="colorIndex">希望保留的颜色</param>
    /// <returns>去除杂色后的mat图像</returns>
    public static Mat LeachColor(Mat inputImage, int colorIndex)
    {
        Scalar lower;
        Scalar upper;
        switch (colorIndex)
        {
            case 2:
                lower = new Scalar(10, 100, 100);
                upper = new Scalar(15, 255, 255);
                break;
            case 3:
                lower = new Scalar(100, 100, 100);
                upper = new Scalar(124, 255, 255);
                break;
            case 4:
                lower = new Scalar(60, 100, 100);
                upper = new Scalar(100, 255, 255);
                break;
            default:
                lower = new Scalar(25, 100, 100);
                upper = new Scalar(30, 255, 255);
                break;
        }

        //转换到HSV色彩空间
        var rgbImage = inputImage.CvtColor(ColorConversionCodes.BGRA2RGB);
        var hsvImage = rgbImage.CvtColor(ColorConversionCodes.RGB2HSV);
        //创建Mask
        var hsvImageMask = new Mat();
        Cv2.InRange(hsvImage, lower, upper, hsvImageMask);
        //应用Mask
        var resultHsv = new Mat();
        Cv2.BitwiseAnd(inputImage, inputImage, resultHsv, hsvImageMask);
        rgbImage.Dispose();
        hsvImageMask.Dispose();
        hsvImage.Dispose();
        return resultHsv.CvtColor(ColorConversionCodes.RGBA2RGB);
    }
}