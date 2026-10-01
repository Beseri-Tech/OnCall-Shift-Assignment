using System.Text;

namespace Rota.Api.Services;

/// <summary>Minimal CSV reader (RFC 4180 quoting). The delimiter is taken from the header: comma, semicolon or tab
/// (Excel saves with semicolons in some locales; a paste from a spreadsheet is tab-separated).</summary>
public static class Csv
{
    /// <summary>Rows with their 1-based line number in the text (a quoted field may span lines).</summary>
    public static List<(int Line, string[] Fields)> Read(string text)
    {
        text = text.TrimStart('\uFEFF');
        char delimiter = Delimiter(text);
        var rows = new List<(int, string[])>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        int line = 1, rowLine = 1;

        void EndField() { fields.Add(field.ToString().Trim()); field.Clear(); }
        void EndRow()
        {
            EndField();
            if (fields.Any(f => f.Length > 0)) rows.Add((rowLine, fields.ToArray()));
            fields.Clear();
            rowLine = line;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
            }
            else if (c == '"' && field.ToString().Trim().Length == 0) { field.Clear(); quoted = true; }
            else if (c == delimiter) EndField();
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                line++;
                EndRow();
            }
            else field.Append(c);
        }
        EndRow();
        return rows;
    }

    private static char Delimiter(string text)
    {
        int end = text.IndexOfAny(['\r', '\n']);
        string header = end < 0 ? text : text[..end];
        return header.Contains('\t') ? '\t' : header.Count(c => c == ';') > header.Count(c => c == ',') ? ';' : ',';
    }
}
