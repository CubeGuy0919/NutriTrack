using System.Text;

namespace NutriTrack.Core.Services;

/// <summary>Small file helpers shared by the pantry, receipt and shopping-list services.</summary>
internal static class TextFile
{
    /// <summary>Commas would break the CSV layout, so they are stored as %2C (and a literal % as %25).</summary>
    public static string Escape(string? text) =>
        (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("%", "%25").Replace(",", "%2C");

    public static string Unescape(string text) =>
        text.Trim().Replace("%2C", ",").Replace("%25", "%");

    /// <summary>Encodes a field for the pipe-delimited TXT data files.</summary>
    public static string EncodeField(string? value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    public static string DecodeField(string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value));

    public static string[] SplitRecord(string line) => line.Split('|');

    public static DateTime? ParseNullableDate(string value) =>
        string.IsNullOrWhiteSpace(value) ? null :
        DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);

    /// <summary>Writes to a temp file first, keeps the previous file as .bak, then swaps - a crash cannot leave half a file.</summary>
    public static void WriteAtomic(string filePath, string content)
    {
        string tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(false));

        if (File.Exists(filePath))
            File.Copy(filePath, filePath + ".bak", overwrite: true);

        File.Move(tempPath, filePath, overwrite: true);
    }
}
