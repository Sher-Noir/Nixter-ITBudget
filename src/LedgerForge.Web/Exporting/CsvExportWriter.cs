using System.Globalization;
using System.Text;

namespace LedgerForge.Web.Exporting;

public static class CsvExportWriter
{
    public static byte[] Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, headers.Cast<object?>());
        foreach (var row in rows) AppendRow(builder, row);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<object?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(Escape(Format(value)));
        }
        builder.Append("\r\n");
    }

    private static string Format(object? value)
        => value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset timestamp => timestamp.ToString("O", CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.####", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => ProtectSpreadsheetFormula(value.ToString() ?? string.Empty)
        };

    private static string ProtectSpreadsheetFormula(string value)
    {
        if (value.Length == 0) return value;
        var first = value[0];
        return first is '=' or '+' or '-' or '@' ? "'" + value : value;
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
            return value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
