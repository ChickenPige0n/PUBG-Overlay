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
    public static float AngleToHeight(float distance, float angle)
    {
        return (float)(distance * Math.Tan(angle * Math.PI / 180.0));
    }
    public static double MortarAngle(double distance, double height)
    {
        var g = 9.800000190734863;
        var v = 82.80000305175781;
        var a = -g * distance / (2.0 * v * v);
        var b = 1.0;
        var c = a - height / distance;
        var delta = b * b - 4.0 * a * c;
        var theta = (-1.0 - Math.Sqrt(delta)) / (2.0 * a);
        var radian = Math.Atan(theta);
        var angle = radian * 57.29577951308232;
        return Math.Round(angle, 2);
    }
    public static double MortarDistance(double angle)
    {
        var radian = angle * 3.141592653589793 / 180.0;
        var v = 82.80000305175781;
        var dis = v * v * Math.Cos(radian) * Math.Sin(radian) / 4.9;
        return Math.Round(dis, 0);
    }
}