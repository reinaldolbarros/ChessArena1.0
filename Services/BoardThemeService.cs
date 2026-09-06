namespace ChessMAUI.Services;

public static class BoardThemeService
{
    public enum Theme { Classic, BlackWhite, Emerald, Walnut, Coral }

    private const string PrefKey = "board_theme";

    public static Theme Current { get; private set; } =
        (Theme)Preferences.Default.Get(PrefKey, (int)Theme.Classic);

    public static event Action? ThemeChanged;

    public static void SetTheme(Theme t)
    {
        Current = t;
        Preferences.Default.Set(PrefKey, (int)t);
        ThemeChanged?.Invoke();
    }

    public static (Color Light, Color Dark) BoardColors => Current switch
    {
        Theme.BlackWhite => (Color.FromArgb("#F0F0F0"), Color.FromArgb("#484848")),
        Theme.Emerald    => (Color.FromArgb("#CEEAD6"), Color.FromArgb("#4E9A65")),
        Theme.Walnut     => (Color.FromArgb("#DEB887"), Color.FromArgb("#8B4513")),
        Theme.Coral      => (Color.FromArgb("#F4C2C2"), Color.FromArgb("#B85050")),
        _                => (Color.FromArgb("#F0D9B5"), Color.FromArgb("#B58863")),
    };

    public static (Color OnLight, Color OnDark) CoordColors => Current switch
    {
        Theme.BlackWhite => (Color.FromArgb("#888888"), Color.FromArgb("#BBBBBB")),
        Theme.Emerald    => (Color.FromArgb("#4E9A65"), Color.FromArgb("#CEEAD6")),
        Theme.Walnut     => (Color.FromArgb("#8B4513"), Color.FromArgb("#DEB887")),
        Theme.Coral      => (Color.FromArgb("#B85050"), Color.FromArgb("#F4C2C2")),
        _                => (Color.FromArgb("#B58863"), Color.FromArgb("#F0D9B5")),
    };

    public static string[] ThemeLabels =>
    [
        "♟ Clássico",
        "⬜ Preto e Branco",
        "🌿 Esmeralda",
        "🪵 Nogueira",
        "🌸 Coral",
    ];
}
