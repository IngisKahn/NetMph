using System.Numerics;
using System.Runtime.InteropServices;

namespace NetMph.ProductLog;


internal interface IPolynomial1
{
    static abstract double Coefficient { get; }
}
internal interface IPolynomial2
{
    static abstract double Coefficient(double x);
}

internal class BranchPoint0 : IPolynomial1 { public static double Coefficient => -1; }
internal class BranchPoint1 : IPolynomial1 { public static double Coefficient => 1; }
internal class BranchPoint2 : IPolynomial1 { public static double Coefficient => -0.333333333333333333e0; }
internal class BranchPoint3 : IPolynomial1 { public static double Coefficient => 0.152777777777777777e0; }
internal class BranchPoint4 : IPolynomial1 { public static double Coefficient => -0.79629629629629630e-1; }
internal class BranchPoint5 : IPolynomial1 { public static double Coefficient => 0.44502314814814814e-1; }
internal class BranchPoint6 : IPolynomial1 { public static double Coefficient => -0.25984714873603760e-1; }
internal class BranchPoint7 : IPolynomial1 { public static double Coefficient => 0.15635632532333920e-1; }
internal class BranchPoint8 : IPolynomial1 { public static double Coefficient => -0.96168920242994320e-2; }
internal class BranchPoint9 : IPolynomial1 { public static double Coefficient => 0.60145432529561180e-2; }
internal class BranchPoint10 : IPolynomial1 { public static double Coefficient => -0.38112980348919993e-2; }
internal class BranchPoint11 : IPolynomial1 { public static double Coefficient => 0.24408779911439826e-2; }
internal class BranchPoint12 : IPolynomial1 { public static double Coefficient => -0.15769303446867841e-2; }
internal class BranchPoint13 : IPolynomial1 { public static double Coefficient => 0.10262633205076071e-2; }
internal class BranchPoint14 : IPolynomial1 { public static double Coefficient => -0.67206163115613620e-3; }
internal class BranchPoint15 : IPolynomial1 { public static double Coefficient => 0.44247306181462090e-3; }
internal class BranchPoint16 : IPolynomial1 { public static double Coefficient => -0.29267722472962746e-3; }
internal class BranchPoint17 : IPolynomial1 { public static double Coefficient => 0.19438727605453930e-3; }
internal class BranchPoint18 : IPolynomial1 { public static double Coefficient => -0.12957426685274883e-3; }
internal class BranchPoint19 : IPolynomial1 { public static double Coefficient => 0.86650358052081260e-4; }
internal class AsymptoticPolynomialB2X0 : IPolynomial1 { public static double Coefficient => 0; }
internal class AsymptoticPolynomialB2X1 : IPolynomial1 { public static double Coefficient => -1; }
internal class AsymptoticPolynomialB2X2 : IPolynomial1 { public static double Coefficient => 1d / 2; }
internal class AsymptoticPolynomialB3X0 : IPolynomial1 { public static double Coefficient => 0; }
internal class AsymptoticPolynomialB3X1 : IPolynomial1 { public static double Coefficient => 1; }
internal class AsymptoticPolynomialB3X2 : IPolynomial1 { public static double Coefficient => -3d / 2; }
internal class AsymptoticPolynomialB3X3 : IPolynomial1 { public static double Coefficient => 1d / 3; }
internal class AsymptoticPolynomialB4X0 : IPolynomial1 { public static double Coefficient => 0; }
internal class AsymptoticPolynomialB4X1 : IPolynomial1 { public static double Coefficient => -1; }
internal class AsymptoticPolynomialB4X2 : IPolynomial1 { public static double Coefficient => 3; }
internal class AsymptoticPolynomialB4X3 : IPolynomial1 { public static double Coefficient => -11d / 6; }
internal class AsymptoticPolynomialB4X4 : IPolynomial1 { public static double Coefficient => 1d / 4; }
internal class AsymptoticPolynomialB5X0 : IPolynomial1 { public static double Coefficient => 0; }
internal class AsymptoticPolynomialB5X1 : IPolynomial1 { public static double Coefficient => 1; }
internal class AsymptoticPolynomialB5X2 : IPolynomial1 { public static double Coefficient => -5; }
internal class AsymptoticPolynomialB5X3 : IPolynomial1 { public static double Coefficient => 35d / 6; }
internal class AsymptoticPolynomialB5X4 : IPolynomial1 { public static double Coefficient => -25d / 12; }
internal class AsymptoticPolynomialB5X5 : IPolynomial1 { public static double Coefficient => 1d / 5; }
internal class AsymptoticPolynomialA0 : IPolynomial2 { public static double Coefficient(double y) => -y; }
internal class AsymptoticPolynomialA1 : IPolynomial2 { public static double Coefficient(double y) => y; }

internal class AsymptoticPolynomialA2 : IPolynomial2 { public static double Coefficient(double y) => (y / 2 - 1) * y; }
internal class AsymptoticPolynomialA3 : IPolynomial2 { public static double Coefficient(double y) => ((y / 3 - 3d / 2) * y + 1) * y; }
internal class AsymptoticPolynomialA4 : IPolynomial2 { public static double Coefficient(double y) => (((y / 4 - 11d / 6) * y + 3) * y - 1) * y; }
internal class AsymptoticPolynomialA5 : IPolynomial2 { public static double Coefficient(double y) => ((((y / 5 - 25d / 12) * y + 35d / 6) * y - 5) * y + 1) * y; }

internal static class Branch0
{
    public static double AsymptoticExpansion5(double x)
    {
        var logsX = double.Log(x);
        var logsLogsX = double.Log(logsX);
        var logsXInverse = 1 / logsX;
        return logsX + ((((AsymptoticPolynomialA5.Coefficient(logsLogsX) * logsXInverse +
                        AsymptoticPolynomialA4.Coefficient(logsLogsX)) * logsXInverse +
                        AsymptoticPolynomialA3.Coefficient(logsLogsX)) * logsXInverse +
                        AsymptoticPolynomialA2.Coefficient(logsLogsX)) * logsXInverse + logsLogsX) * logsXInverse - logsLogsX;
    }

    public static double PointExpansion8(double x)
    {
        x = double.Sqrt(2 * (double.E * x + 1));
        return (((((((-0.96168920242994320e-2 * x + 0.15635632532333920e-1) * x - 0.25984714873603760e-1) * x + 0.44502314814814814e-1) * x - 0.79629629629629630e-1) * x + 0.152777777777777777e0) * x + -0.333333333333333333e0) * x + 1) * x - 1;
    }

    public static double PointExpansion10(double x)
    {
        x = double.Sqrt(2 * (double.E * x + 1));
        return (((((((((-0.38112980348919993e-2 * x + 0.60145432529561180e-2) * x - 0.96168920242994320e-2) * x + 0.15635632532333920e-1) * x - 0.25984714873603760e-1) * x + 0.44502314814814814e-1) * x - 0.79629629629629630e-1) * x + 0.152777777777777777e0) * x + -0.333333333333333333e0) * x + 1) * x - 1;
    }
}

internal static class BranchM1
{
    public static double LogRecursion3(double x)
    {
        x = double.Log(-x);
        return x - double.Log(-x + double.Log(-x + double.Log(-x)));
    }
    public static double PointExpansion4(double x)
    {
        x = -double.Sqrt(2 * (double.E * x + 1));
        return (((-0.79629629629629630e-1 * x + 0.152777777777777777e0) * x + -0.333333333333333333e0) * x + 1) * x - 1;
    }
    public static double PointExpansion8(double x)
    {
        x = -double.Sqrt(2 * (double.E * x + 1));
        return (((((((-0.96168920242994320e-2 * x + 0.15635632532333920e-1) * x - 0.25984714873603760e-1) * x + 0.44502314814814814e-1) * x - 0.79629629629629630e-1) * x + 0.152777777777777777e0) * x + -0.333333333333333333e0) * x + 1) * x - 1;
    }
}

public static class Lambert
{
    private static double HalleyStep(double x, double w)
    {
        var ew = double.Exp(w);
        var wew = w * ew;
        var wewx = wew - x;
        var w1 = w + 1;
        return w - wewx / (ew * w1 - (w + 2) * wewx / (2 * w1));
    }

    private static double Pade0X1(double x)
    {
        return x * ((((0.07066247420543414 * x + 2.4326814530577687) * x + 6.39672835731526) * x + 4.663365025836821) * x + 0.99999908757381)
            / ((((1.2906660139511692 * x + 7.164571775410987) * x + 10.559985088953114) * x + 5.66336307375819) * x + 1);
    }

    private static double Pade0X2(double x)
    {
        x = double.Log(.5 * x) - 2;

        return 2 + x * (((0.00006979269679670452 * x + 0.017110368846615806) * x + 0.19338607770900237) * x + 0.6666648896499793)
            / ((0.0188060684652668 * x + 0.23451269827133317) * x + 1);
    }

    private static double PadeM1X4(double x) =>
        ((((-2793.4565508841197 * x - 1987.3632221106518) * x + 385.7992853617571) * x + 277.2362778379572) * x - 7.840776922133643)
            / ((((280.6156995997829 * x + 941.9414019982657) * x + 190.64429338894644) * x - 63.93540494358966) * x + 1);

    private static double PadeM1X5(double x)
    {
        x = double.Log(-x);

        x = (((0.16415668298255184 * x - 3.334873920301941) * x + 2.4831415860003747) * x + 4.173424474574879)
            / (((0.031239411487374164 * x - 1.2961659693400076) * x + 4.517178492772906) * x + 1);

        return -double.Exp(x);
    }

    private static double PadeM1X6(double x)
    {
        x = double.Log(-x);

        x = ((((0.000026987243254533254 * x - 0.007692106448267341) * x + 0.28793461719300206) * x - 1.5267058884647018) * x - 0.5370669268991288)
            / ((((3.6006502104930343e-6 * x - 0.0015552463555591487) * x + 0.08801194682489769) * x - 0.8973922357575583) * x + 1);

        return -double.Exp(x);
    }

    private static double PadeM1X7(double x)
    {
        x = ((((988.0070769375508 * x + 1619.8111957356814) * x + 989.2017745708083) * x + 266.9332506485452) * x + 26.875022558546036)
            / ((((-205.50469464210596 * x - 270.0440832897079) * x - 109.554245632316) * x - 11.275355431307334) * x + 1);

        return -1 - double.Sqrt(x);
    }

    public static double Degree0(double x)
    {
        return
            x < 1.38 
                ? x < -0.311 
                    ? x < -0.367679 
                        ? Branch0.PointExpansion8(x)
                        : HalleyStep(x, Branch0.PointExpansion10(x))
                    : HalleyStep(x, Pade0X1(x))
                : x < 236
                    ? HalleyStep(x, Pade0X2(x))
                    : HalleyStep(x, Branch0.AsymptoticExpansion5(x))
            ;
    }
    public static double DegreeM1(double x)
    {
        return
            x < -0.0509
                ? (x < -0.366079
                    ? (x < -0.367579
                        ? BranchM1.PointExpansion8(x) //(Branch < d, -1 >::BranchPointExpansion < 8 > (x)) 
                        : HalleyStep(x, BranchM1.PointExpansion4(x))) //(Branch < d, -1 >::BranchPointExpansion < 4 > (x)))
                    : (x < -0.289379
                        ? HalleyStep(x, PadeM1X7(x)) //(Pade < d, -1, 7 >::Approximation(x)) 
                        : HalleyStep(x, PadeM1X4(x)))) //(Pade < d, -1, 4 >::Approximation(x)))
                : (x < -0.000131826
                    ? HalleyStep(x, PadeM1X5(x)) //(Pade < d, -1, 5 >::Approximation(x))
                    : (x < -6.30957e-31
                        ? HalleyStep(x, PadeM1X6(x)) //(Pade < d, -1, 6 >::Approximation(x)) 
                        : HalleyStep(x, BranchM1.LogRecursion3(x)))); //(Branch < d, -1 >::LogRecursion < 3 > (x))));
    }
}