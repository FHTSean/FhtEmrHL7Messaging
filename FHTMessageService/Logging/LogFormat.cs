namespace FHTMessageService.Logging;

/// <summary>
/// ANSI SGR format codes
/// </summary>
public enum LogFormat
{
    // Font
    Reset = 0,
    Bold,
    Faint,
    Italic,
    Underline,
    SlowBlink,
    RapidBlink,
    Invert,
    Conceal,
    Strikethrough,
    // Reset
    NoBold = 22,
    NoItalic,
    NoUnderline,
    NoBlinking,
    ProportionalSpacing,
    NoReverse,
    NoConceal,
    NoStrikethrough,
    // Foreground color
    Black = 30,
    Red,
    Green,
    Yellow,
    Blue,
    Magenta,
    Cyan,
    White,
    Color,
    DefaultColor,
    // Background color
    BgBlack = 40,
    BgRed,
    BgGreen,
    BgYellow,
    BgBlue,
    BgMagenta,
    BgCyan,
    BgWhite,
    BgColor,
    BgDefaultColor,
    // Foreground bright color
    BrightBlack = 90,
    BrightRed,
    BrightGreen,
    BrightYellow,
    BrightBlue,
    BrightMagenta,
    BrightCyan,
    BrightWhite,
    // Background bright color
    BgBrightBlack = 100,
    BgBrightRed,
    BgBrightGreen,
    BgBrightYellow,
    BgBrightBlue,
    BgBrightMagenta,
    BgBrightCyan,
    BgBrightWhite,
}
