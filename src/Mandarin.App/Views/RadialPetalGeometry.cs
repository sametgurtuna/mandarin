using System;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Mandarin.App.Views;

/// <summary>
/// Mathematical engine for rendering perfectly curved, rounded radial petals
/// matching Tangerine's radial wheel geometry and precise hit testing.
/// </summary>
public static class RadialPetalGeometry
{
    public const double InnerRadius = 48.0;
    public const double OuterRadius = 138.0;
    public const double Gap = 4.0;
    public const double MaxCorner = 7.0;
    public const double LabelRadius = 93.0;

    public static Point PolarToCartesian(double radius, double angleRad)
    {
        return new Point(Math.Cos(angleRad) * radius, -Math.Sin(angleRad) * radius);
    }

    public static double AngleForIndex(int index, int totalCount)
    {
        double baseAngle = (totalCount == 3) ? Math.PI : (Math.PI / 2.0);
        return baseAngle - (2.0 * Math.PI * index / totalCount);
    }

    public static Point GetLabelCenter(int index, int totalCount)
    {
        double theta = AngleForIndex(index, totalCount);
        return PolarToCartesian(LabelRadius, theta);
    }

    public static Geometry CreatePetalGeometry(
        int index,
        int totalCount,
        double innerRadius = InnerRadius,
        double outerRadius = OuterRadius,
        double gap = Gap,
        double maxCorner = MaxCorner)
    {
        if (totalCount <= 0) return Geometry.Empty;

        if (totalCount == 1)
        {
            var donut = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(0, 0), outerRadius, outerRadius),
                new EllipseGeometry(new Point(0, 0), innerRadius, innerRadius));
            donut.Freeze();
            return donut;
        }

        double centerAngle = AngleForIndex(index, totalCount);
        double slice = 2.0 * Math.PI / totalCount;
        double halfSlice = slice / 2.0;

        // Angular gap based on mid-radius
        double midRadius = (innerRadius + outerRadius) / 2.0;
        double gapAngle = gap / midRadius;
        double halfGap = gapAngle / 2.0;

        double thetaLeft = centerAngle + halfSlice - halfGap;
        double thetaRight = centerAngle - halfSlice + halfGap;
        double span = thetaLeft - thetaRight;

        if (span <= 0.05) return Geometry.Empty;

        // Dynamic corner radius clamping
        double corner = Math.Max(2.0, Math.Min(maxCorner, Math.Min((outerRadius - innerRadius) * 0.35, innerRadius * span * 0.35)));

        double dThetaOut = corner / outerRadius;
        double dThetaIn = corner / innerRadius;
        double rOutInner = outerRadius - corner;
        double rInOuter = innerRadius + corner;

        double aOutStart = thetaLeft - dThetaOut;
        double aOutEnd = thetaRight + dThetaOut;
        double aInEnd = thetaRight + dThetaIn;
        double aInStart = thetaLeft - dThetaIn;

        bool largeOuter = (aOutStart - aOutEnd) > Math.PI;
        bool largeInner = (aInStart - aInEnd) > Math.PI;

        var figure = new PathFigure
        {
            StartPoint = PolarToCartesian(rInOuter, thetaLeft),
            IsClosed = true,
            IsFilled = true
        };

        // 1. Line outward along left radial spoke
        figure.Segments.Add(new LineSegment(PolarToCartesian(rOutInner, thetaLeft), true));

        // 2. Rounded corner 1 (top-left) into outer arc
        figure.Segments.Add(new ArcSegment(PolarToCartesian(outerRadius, aOutStart), new Size(corner, corner), 0, false, SweepDirection.Clockwise, true));

        // 3. Outer circular arc (clockwise from top-left to top-right)
        figure.Segments.Add(new ArcSegment(PolarToCartesian(outerRadius, aOutEnd), new Size(outerRadius, outerRadius), 0, largeOuter, SweepDirection.Clockwise, true));

        // 4. Rounded corner 2 (top-right) into right radial spoke
        figure.Segments.Add(new ArcSegment(PolarToCartesian(rOutInner, thetaRight), new Size(corner, corner), 0, false, SweepDirection.Clockwise, true));

        // 5. Line inward along right radial spoke
        figure.Segments.Add(new LineSegment(PolarToCartesian(rInOuter, thetaRight), true));

        // 6. Rounded corner 3 (bottom-right) into inner arc
        figure.Segments.Add(new ArcSegment(PolarToCartesian(innerRadius, aInEnd), new Size(corner, corner), 0, false, SweepDirection.Clockwise, true));

        // 7. Inner circular arc (counter-clockwise from bottom-right to bottom-left)
        figure.Segments.Add(new ArcSegment(PolarToCartesian(innerRadius, aInStart), new Size(innerRadius, innerRadius), 0, largeInner, SweepDirection.Counterclockwise, true));

        // 8. Rounded corner 4 (bottom-left) back to left spoke
        figure.Segments.Add(new ArcSegment(PolarToCartesian(rInOuter, thetaLeft), new Size(corner, corner), 0, false, SweepDirection.Clockwise, true));

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(figure);
        pathGeometry.Freeze();
        return pathGeometry;
    }

    public static int? HitTestAngle(Point offsetFromCenter, int totalCount, double innerRadius = InnerRadius, double outerRadius = OuterRadius)
    {
        double dist = Math.Sqrt(offsetFromCenter.X * offsetFromCenter.X + offsetFromCenter.Y * offsetFromCenter.Y);
        if (totalCount <= 0 || dist < innerRadius - 6.0 || dist > outerRadius + 8.0)
            return null;

        // Angle in radians: math standard where 0 is East, PI/2 is North
        double angle = Math.Atan2(-offsetFromCenter.Y, offsetFromCenter.X);
        double slice = 2.0 * Math.PI / totalCount;
        double baseAngle = (totalCount == 3) ? Math.PI : (Math.PI / 2.0);

        double diff = ((baseAngle - angle) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2);
        int sector = (int)Math.Round(diff / slice) % totalCount;

        return sector;
    }
}
