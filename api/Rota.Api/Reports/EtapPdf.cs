using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Reports;

public sealed record EtapPdfData(OfficerProfile Profile, Person Person, int Year, int Month, EtapDto Claim);

/// <summary>
/// The officer's ETAP claim, written over the official form (Borang Penyata Tuntutan ETAP, 30/10/2025; US Letter,
/// 612 x 792 pt). The positions below were measured from that PDF: each value sits on its dotted line or in its table cell.
/// Nothing is stored: the PDF is built for the officer who asks and streamed back.
/// </summary>
public static class EtapPdf
{
    private static readonly string[] Months =
        ["JANUARI", "FEBRUARI", "MAC", "APRIL", "MEI", "JUN", "JULAI", "OGOS", "SEPTEMBER", "OKTOBER", "NOVEMBER", "DISEMBER"];
    private static readonly string[] Days = ["AHAD", "ISNIN", "SELASA", "RABU", "KHAMIS", "JUMAAT", "SABTU"];

    /// <summary>Rows of the patient table on page 3; more go to the page 5 attachment (24 rows).</summary>
    public const int Page3Patients = 6, Page5Patients = 24;

    private sealed record Item(float X, float Y, float W, string Text, float Size, bool Center, bool Right = false);

    public static byte[] Render(EtapPdfData d)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var p = d.Profile;
        var c = d.Claim;
        bool attachment = c.Patients.Count > Page3Patients;

        // ---- page 1: section A ----
        var p1 = new List<Item>
        {
            new(226.2f, 110.5f, 92.3f, Months[d.Month - 1], 10, true),
            new(356.4f, 110.5f, 61.7f, d.Year.ToString(CultureInfo.InvariantCulture), 10, true),
        };
        // Values sit on the dotted line (from x 331, after the colon); a long one wraps onto the following dotted lines.
        void Field(float y, string? text) { if (!string.IsNullOrWhiteSpace(text)) p1.Add(new(331, y - 2.5f, 187, text, 9, false)); }
        void Lines(float[] ys, string? text) { foreach (var (line, y) in Wrap(text, 34).Take(ys.Length).Zip(ys)) Field(y, line); }
        Field(195.0f, p.FullName);
        Field(220.4f, FormatIc(p.IcNo));
        Field(245.8f, p.Jawatan);
        Field(271.1f, p.Gred);
        Field(296.3f, p.TarikhPewartaanPakar?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        Lines([321.6f, 346.9f, 372.3f, 397.5f], p.UnitTempatBertugas);
        Lines([422.7f, 448.0f, 473.3f, 498.6f], p.AlamatRumah);
        Field(524.0f, p.JarakKm?.ToString("0.#", CultureInfo.InvariantCulture));
        Field(549.2f, p.NoGaji);
        Field(574.5f, p.NoTelefonPejabat);
        Field(599.7f, d.Person.Phone);
        Field(625.0f, d.Person.Email);
        Field(650.3f, p.NoAkaunBank);
        Field(674.6f, p.NamaCawanganBank);

        // ---- page 2: section B, 30 rows ----
        var p2 = new List<Item>();
        foreach (var (r, i) in c.Rows.Take(Etap.MaxRows).Select((r, i) => (r, i)))
        {
            float y = 141.0f + i * 19.45f + 5;
            p2.Add(new(66.9f, y, 78.3f, r.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), 8, true));
            p2.Add(new(145.2f, y, 54.5f, Days[(int)r.Date.DayOfWeek] + (r.DayType == EtapDayType.Holiday ? " (CUTI)" : ""), 7, true));
            p2.Add(new(199.7f, y, 73.0f, r.FromTime.ToString("HH:mm", CultureInfo.InvariantCulture), 8, true));
            p2.Add(new(272.7f, y, 37.7f, r.FromDate.ToString("dd/MM", CultureInfo.InvariantCulture), 7, true));
            p2.Add(new(310.4f, y, 74.9f, r.ToTime.ToString("HH:mm", CultureInfo.InvariantCulture), 8, true));
            p2.Add(new(385.3f, y, 40.5f, r.ToDate.ToString("dd/MM", CultureInfo.InvariantCulture), 7, true));
            p2.Add(new(425.8f, y, 57.7f, $"{r.Minutes / 60} jam", 8, true));
            p2.Add(new(483.5f, y, 57.2f, $"{r.Minutes % 60:00} min", 8, true));
        }

        // ---- page 3: section C amounts and the >4h patient table ----
        var p3 = new List<Item>();
        var rowTop = new Dictionary<string, float> { ["2a"] = 372.4f, ["2b"] = 386.2f, ["3a"] = 494.0f, ["3b"] = 507.8f };
        float amountX = c.Specialist ? 495 : 383, amountW = c.Specialist ? 43 : 40;
        foreach (var line in c.Lines.Where(l => l.Days > 0))
        {
            p3.Add(new(272.8f, rowTop[line.Category], 36.6f, line.Days.ToString(CultureInfo.InvariantCulture), 9, true));
            p3.Add(new(amountX, rowTop[line.Category], amountW, line.Amount.ToString("N0", CultureInfo.InvariantCulture), 9, false, Right: true));
        }
        p3.Add(new(272.8f, 553.1f, 36.6f, c.TotalDays.ToString(CultureInfo.InvariantCulture), 9, true));
        p3.Add(new(c.Specialist ? 426.0f : 309.4f, 553.1f, c.Specialist ? 114.5f : 116.6f,
            "RM " + c.TotalAmount.ToString("N0", CultureInfo.InvariantCulture), 10, true));
        AddPatients(p3, c.Patients.Take(Page3Patients), 646.9f, 13.06f, 2.5f, 6.8f, [66.9f, 119.3f, 226.6f, 309.4f, 366.9f, 445.0f, 540.8f]);

        // ---- page 5: the attachment for patients that don't fit on page 3 ----
        var p5 = new List<Item>();
        if (attachment)
            AddPatients(p5, c.Patients.Skip(Page3Patients).Take(Page5Patients), 153.4f, 17.52f, 4f, 7.2f,
                [73.1f, 119.2f, 236.3f, 310.7f, 369.3f, 460.5f, 550.1f]);

        var pages = attachment ? new[] { p1, p2, p3, [], p5 } : new[] { p1, p2, p3, new List<Item>() };
        byte[] overlay = Document.Create(doc =>
        {
            foreach (var items in pages)
                doc.Page(page =>
                {
                    page.Size(612, 792);
                    page.Margin(0);
                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer().Height(792);
                        foreach (var it in items)
                        {
                            var box = layers.Layer().PaddingLeft(it.X).PaddingTop(it.Y).Width(it.W);
                            var text = it.Center ? box.AlignCenter() : it.Right ? box.AlignRight() : box.AlignLeft();
                            text.Text(it.Text).FontSize(it.Size).FontColor(Colors.Blue.Darken4).ClampLines(1);
                        }
                    });
                });
        }).GeneratePdf();

        string dir = Path.Combine(Path.GetTempPath(), "etap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string template = Path.Combine(dir, "template.pdf"), over = Path.Combine(dir, "overlay.pdf"), output = Path.Combine(dir, "etap.pdf");
            using (var res = typeof(EtapPdf).Assembly.GetManifestResourceStream("etap-template.pdf")
                             ?? throw new InvalidOperationException("The ETAP template is missing from the build."))
            using (var f = File.Create(template)) res.CopyTo(f);
            File.WriteAllBytes(over, overlay);
            DocumentOperation.LoadFile(template).TakePages(attachment ? "1-5" : "1-4")
                .OverlayFile(new DocumentOperation.LayerConfiguration { FilePath = over })
                .Save(output);
            return File.ReadAllBytes(output);
        }
        finally
        {
            // The PDF holds personal and patient data: nothing is left behind on disk.
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private static void AddPatients(List<Item> items, IEnumerable<EtapPatientDto> patients, float top, float rowHeight, float pad, float size,
        float[] x)
    {
        foreach (var (pt, i) in patients.Select((pt, i) => (pt, i)))
        {
            float y = top + i * rowHeight + pad;
            string[] cells =
            [
                pt.Date.ToString("dd/MM/yy", CultureInfo.InvariantCulture), pt.Name ?? "", pt.RegNo ?? "",
                $"{pt.Minutes / 60}j {pt.Minutes % 60:00}m", pt.DiagnosisAndTreatment ?? "", pt.Notes ?? "",
            ];
            for (int col = 0; col < cells.Length; col++)
                if (cells[col].Length > 0) items.Add(new(x[col] + 2, y, x[col + 1] - x[col] - 4, cells[col], size, col is 0 or 3));
        }
    }

    /// <summary>
    /// Splits text into lines of at most <paramref name="width"/> characters at spaces (34 upper-case characters of 9 pt fill a
    /// dotted line); the last line takes whatever is left and is cut off with "..." if it still doesn't fit.
    /// </summary>
    internal static IEnumerable<string> Wrap(string? text, int width)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var line = "";
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))   // null = any whitespace
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width) { yield return line; line = ""; }
            line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) yield return line;
    }

    private static string? FormatIc(string? ic) => ic is { Length: 12 } ? $"{ic[..6]}-{ic[6..8]}-{ic[8..]}" : ic;
}
