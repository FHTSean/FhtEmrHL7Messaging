namespace FHTMessageService.Logging;

public static class Log
{
    private const string AnsiEsc = "\u001b[";
    private const char AnsiSep = ';';
    private const char AnsiSgr = 'm';

    public static void WriteStream(TextWriter stream, object line, params LogFormat[] format)
    {
        stream.Write(AnsiEsc);
        IEnumerable<int> formatCodes = format.Select(x => (int)x);
        stream.Write(string.Join(AnsiSep, formatCodes));
        stream.Write(AnsiSgr);

        stream.Write(line);

        stream.Write(AnsiEsc);
        stream.Write(AnsiSgr);
    }

    public static void WriteStreamLine(TextWriter stream)
    {
        stream.WriteLine();
    }

    public static void Write(object line, params LogFormat[] format)
    {
        WriteStream(Console.Out, line, format);
    }

    public static void WriteError(object line, params LogFormat[] format)
    {
        WriteStream(Console.Error, line, format);
    }

    public static void WriteLine()
    {
        WriteStreamLine(Console.Out);
    }

    public static void WriteLine(object line, params LogFormat[] format)
    {
        Write(line, format);
        WriteStreamLine(Console.Out);
    }

    public static void WriteErrorLine()
    {
        WriteStreamLine(Console.Error);
    }

    public static void WriteErrorLine(object line, params LogFormat[] format)
    {
        WriteError(line, format);
        WriteStreamLine(Console.Error);
    }
}
