namespace uTest;

internal static class MathHelper
{
    public static double Clamp01(double d)
    {
        if (d <= 0)
            return 0;
        if (d >= 1)
            return 1;
        return d;
    }
}
