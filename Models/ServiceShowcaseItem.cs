using Microsoft.UI.Xaml.Media;

namespace DNSYar.Models;

public sealed class ServiceShowcaseItem
{
    public string Name { get; set; } = "";
    public string Initials { get; set; } = "";
    public Brush Badge { get; set; } = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));
}
