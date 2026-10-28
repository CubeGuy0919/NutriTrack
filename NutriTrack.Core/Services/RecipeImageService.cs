using System.Text;

namespace NutriTrack.Core.Services;

/// <summary>
/// File handling for recipe images. Pure System.IO (no WPF) so it stays in Core.
/// Images live in the application's Assets/Images folder and recipes reference them by file name.
/// </summary>
public class RecipeImageService
{
    public const string DefaultImageFileName = "default_food.png";
    private const long MaxImageBytes = 20L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    public static string FileDialogFilter =>
        "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp";

    public static bool IsSupportedImage(string path) =>
        AllowedExtensions.Contains(Path.GetExtension(path ?? string.Empty));

    /// <summary>Letters/digits only, other characters become '_': "Mom's Pie!" gives "Mom_s_Pie".</summary>
    public static string ToSafeFileStem(string recipeName)
    {
        var sb = new StringBuilder();
        bool lastUnderscore = false;
        foreach (char c in recipeName ?? string.Empty)
        {
            if (char.IsLetterOrDigit(c) && c < 128)
            {
                sb.Append(c);
                lastUnderscore = false;
            }
            else if (!lastUnderscore && sb.Length > 0)
            {
                sb.Append('_');
                lastUnderscore = true;
            }
        }

        string stem = sb.ToString().Trim('_');
        if (stem.Length > 60) stem = stem.Substring(0, 60).Trim('_');
        return stem.Length == 0 ? "Recipe" : stem;
    }

    /// <summary>
    /// Copies the picked image into <paramref name="imagesDirectory"/> under a safe name such as
    /// "My_Recipe_1.png" and returns that file name. Never overwrites: if the name is taken the number is increased.
    /// </summary>
    public string ImportImage(string sourcePath, string imagesDirectory, string recipeName)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The selected image file no longer exists.", sourcePath);
        if (!IsSupportedImage(sourcePath))
            throw new InvalidOperationException("Unsupported image type. Use PNG, JPG, BMP, GIF or WEBP.");
        if (new FileInfo(sourcePath).Length > MaxImageBytes)
            throw new InvalidOperationException("The image is larger than 20 MB.");

        Directory.CreateDirectory(imagesDirectory);

        string stem = ToSafeFileStem(recipeName);
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        for (int n = 1; n < 10_000; n++)
        {
            string fileName = $"{stem}_{n}{extension}";
            string target = Path.Combine(imagesDirectory, fileName);
            if (File.Exists(target)) continue;

            try
            {
                File.Copy(sourcePath, target, overwrite: false);
                return fileName;
            }
            catch (IOException) when (File.Exists(target))
            {
                // someone created it between the check and the copy - try the next number
            }
        }

        throw new IOException("Could not find a free image file name.");
    }

    /// <summary>Removes an image previously imported (used to roll back when saving the recipe fails).</summary>
    public void TryDelete(string imagesDirectory, string fileName)
    {
        try
        {
            string path = Path.Combine(imagesDirectory, Path.GetFileName(fileName));
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Full path of the first listed image that exists, otherwise the default placeholder if present,
    /// otherwise null. Callers just hide the image when null.
    /// </summary>
    public string? ResolveImagePath(IEnumerable<string> imageFileNames, string imagesDirectory)
    {
        foreach (var name in imageFileNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            string path = Path.Combine(imagesDirectory, Path.GetFileName(name.Trim()));
            if (File.Exists(path)) return path;
        }

        string fallback = Path.Combine(imagesDirectory, DefaultImageFileName);
        return File.Exists(fallback) ? fallback : null;
    }
}
