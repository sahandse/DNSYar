using Microsoft.UI.Xaml.Media;

namespace DNSYar.Services;

internal static class AppFonts
{
    public static FontFamily Vazirmatn { get; } = Load("Vazirmatn-Variable.ttf", ["Vazirmatn", "Vazirmatn Regular"], "Segoe UI");
    public static FontFamily Inter { get; } = Load("InterVariable.ttf", ["Inter Variable", "Inter"], "Segoe UI");
    public static FontFamily Mono { get; } = new("Consolas");

    private static FontFamily Load(string fileName, string[] familyNames, string fallback)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", fileName);
            if (File.Exists(path))
            {
                var fileUri = new Uri(path, UriKind.Absolute).AbsoluteUri;
                foreach (var name in familyNames)
                    return new FontFamily($"{fileUri}#{name}");
            }
        }
        catch
        {
            // Fall back to a system face so XAML/window creation cannot throw.
        }

        return new FontFamily(fallback);
    }
}
