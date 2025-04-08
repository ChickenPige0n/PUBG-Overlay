namespace PubgOverlay;

public class PubgUtils
{
    public static float IndexToAngle(int i)
    {
        return i switch
        {
            1 => 0.56f,
            2 => 1.69f,
            3 => 2.82f,
            4 => 3.94f,
            5 => 5.06f,
            6 => 6.18f,
            7 => 7.29f,
            8 => 8.4f,
            9 => 9.5f,
            10 => 10.59f,
            11 => 11.67f,
            12 => 12.75f,
            13 => 13.82f,
            14 => 14.88f,
            15 => 15.92f,
            16 => 16.96f,
            17 => 17.99f,
            18 => 19f,
            19 => 20f,
            20 => 20.99f,
            21 => 21.97f,
            22 => 22.93f,
            23 => 23.88f,
            24 => 24.82f,
            25 => 25.74f,
            _ => 0f
        };
    }

    /// <summary>
    /// 将归一化屏幕高度坐标转换为仰角(以度为单位)
    /// </summary>
    /// <param name="y">屏幕上的归一化坐标(-1.0 到 1.0)</param>
    /// <param name="fov">视场角(Field of View)，默认为80度</param>
    /// <param name="aspect">屏幕宽高比，默认为16:9</param>
    /// <returns>对应的角度值(以弧度为单位)</returns>
    public static double ScreenPointToAngle(float y, int fov = 80, float aspect = 16f / 9f)
    {
        var fovRadians = fov * Math.PI / 180.0;
        var tanHalfFov = Math.Tan(fovRadians / 2.0);
        return Math.Atan(y * tanHalfFov / aspect);
    }

    /// <summary>
    /// 根据距离和仰角计算迫击炮距离，公式 by -- 绝地King-of-Mortar
    /// </summary>
    /// <param name="hDistance">水平距离</param>
    /// <param name="beta">仰角(弧度制)</param>
    /// <returns>得出的迫击炮密位</returns>
    public static double MortarDistance(double hDistance, double beta)
    {
        // 重力加速度
        const double g = 9.800000190734863;
        // 炮弹初速度
        const double v = 82.80000305175781;

        const double v2 = v * v;
        const double v4 = v2 * v2;

        const double p1 = v4 / (g * g);
        var p2 = 2.0 * hDistance * v2 * Math.Tan(beta) / g;
        var p3 = hDistance * hDistance;

        var root = Math.Sqrt(p1 - p2 - p3);

        var numerator = hDistance + Math.Tan(beta) * (v2 / g - root);
        var denominator = Math.Tan(beta) * Math.Tan(beta) + 1;

        return numerator / denominator;
    }
}