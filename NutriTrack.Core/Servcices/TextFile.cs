using System.Text;

namespace NutriTrack.Core.Services;

/// <summary>Small helpers shared by the Pantry.txt and ShoppingList.txt readers/writers.</summary>
internal static class TextFile
{
    /// <summary>Commas would break the CSV layout, so they are stored as %2C (and a literal % as %25).</summary>
    public static string Escape(string? text) =>
        (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("%", "%25").Replace(",", "%2C");

    public static string Unescape(string text) =>
        text.Trim().Replace("%2C", ",").Replace("%25", "%");

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
