namespace NutriTrack.Core.Models;

/// <summary>
/// One row of a simple bar chart, for example "TESCO  ->  12 400 Ft".
/// The screen only has to show these three things, it does no maths itself.
/// </summary>
public class ChartBar
{
    /// <summary>The text on the left, e.g. a store name or a month.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The real number (money, count ...). Used to work out the bar length.</summary>
    public double Value { get; set; }

    /// <summary>The number as text on the right, e.g. "12 400 Ft" or "3 items".</summary>
    public string ValueText { get; set; } = string.Empty;

    /// <summary>Bar length from 0 to 100. The biggest row in a chart always gets 100.</summary>
    public double Percent { get; set; }
}
