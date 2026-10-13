using System.Windows;
using System.Windows.Media;

namespace PaneSet;

internal static class UiPalette
{
    public static readonly SolidColorBrush Canvas = Brush();
    public static readonly SolidColorBrush Surface = Brush();
    public static readonly SolidColorBrush Panel = Brush();
    public static readonly SolidColorBrush Header = Brush();
    public static readonly SolidColorBrush HeaderInk = Brush();
    public static readonly SolidColorBrush HeaderSub = Brush();
    public static readonly SolidColorBrush Ink = Brush();
    public static readonly SolidColorBrush Muted = Brush();
    public static readonly SolidColorBrush Mint = Brush();
    public static readonly SolidColorBrush MintPale = Brush();
    public static readonly SolidColorBrush Slate = Brush();
    public static readonly SolidColorBrush Line = Brush();
    public static readonly SolidColorBrush LineSoft = Brush();
    public static readonly SolidColorBrush Focus = Brush();
    public static readonly SolidColorBrush Primary = Brush();
    public static readonly SolidColorBrush PrimaryInk = Brush();
    public static readonly SolidColorBrush Number = Brush();
    public static readonly SolidColorBrush NumberInk = Brush();
    public static readonly SolidColorBrush Preview = Brush();
    public static readonly SolidColorBrush PreviewPane = Brush();
    public static readonly SolidColorBrush Quick = Brush();
    public static bool IsDark { get; private set; }

    static UiPalette() => Apply(false);

    public static void Apply(bool dark)
    {
        IsDark = dark;
        Set(Canvas, dark ? "#132332" : "#F8F9F7");
        Set(Surface, dark ? "#1B3041" : "#FFFFFF");
        Set(Panel, dark ? "#1A2D3D" : "#F7F8F7");
        Set(Header, dark ? "#132332" : "#FFFFFF");
        Set(HeaderInk, dark ? "#E9F1F0" : "#233039");
        Set(HeaderSub, dark ? "#A9BAC0" : "#617079");
        Set(Ink, dark ? "#E9F1F0" : "#233039");
        Set(Muted, dark ? "#A9BAC0" : "#617079");
        Set(Mint, dark ? "#AEDFC1" : "#CAE9D8");
        Set(MintPale, dark ? "#2B514E" : "#E6F4EB");
        Set(Slate, dark ? "#1A2D3D" : "#F7F8F7");
        Set(Line, dark ? "#3A5260" : "#D7DFDF");
        Set(LineSoft, dark ? "#304655" : "#E7ECEA");
        Set(Focus, dark ? "#AEDFC1" : "#276F62");
        Set(Primary, dark ? "#AEDFC1" : "#25464C");
        Set(PrimaryInk, dark ? "#163236" : "#F7FFF9");
        Set(Number, dark ? "#AEDFC1" : "#294D51");
        Set(NumberInk, dark ? "#142F31" : "#E9FFF1");
        Set(Preview, dark ? "#0D1C2B" : "#E8EEEA");
        Set(PreviewPane, dark ? "#376A68" : "#CAE9D8");
        Set(Quick, dark ? "#25433E" : "#E6F4EB");
        var resources = Application.Current?.Resources;
        if (resources == null) return;
        foreach (var (name, brush) in new (string Name, SolidColorBrush Brush)[]
        {
            (nameof(Canvas), Canvas), (nameof(Surface), Surface), (nameof(Panel), Panel),
            (nameof(Header), Header), (nameof(HeaderInk), HeaderInk), (nameof(HeaderSub), HeaderSub),
            (nameof(Ink), Ink), (nameof(Muted), Muted), (nameof(Mint), Mint),
            (nameof(MintPale), MintPale), (nameof(Slate), Slate), (nameof(Line), Line),
            (nameof(LineSoft), LineSoft), (nameof(Focus), Focus), (nameof(Primary), Primary),
            (nameof(PrimaryInk), PrimaryInk), (nameof(Number), Number), (nameof(NumberInk), NumberInk),
            (nameof(Preview), Preview), (nameof(PreviewPane), PreviewPane), (nameof(Quick), Quick)
        }) resources[name] = new SolidColorBrush(brush.Color);
    }

    private static SolidColorBrush Brush() => new();
    private static void Set(SolidColorBrush brush, string value) => brush.Color = (Color)ColorConverter.ConvertFromString(value);
}
