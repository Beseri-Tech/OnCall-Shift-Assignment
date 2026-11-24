using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rota.Api.Data;

namespace Rota.Api.Reports;

/// <summary>One on-call shift of 4 hours or more and its patients, for the patient-details attachment.</summary>
public sealed record EtapLongShift(DateOnly Date, int Minutes, IReadOnlyList<OnCallCase> Cases);

/// <summary>
/// "Lampiran: butiran pesakit" - the proof for passive calls of 4 hours or more that officers used to screenshot from the
/// Google Sheet. One block per on-call date with every patient seen (from the case log), the officer's details and sign-off
/// lines. Appended to the ETAP form so both go to the office together. Confidential patients show SULIT instead of name and number.
/// </summary>
public static class EtapPatientsPdf
{
    private static readonly string Ink = "#0F172A", Muted = "#64748B", Rule = "#CBD5E1", Head = "#E2E8F0", Brand = "#0E7490";
    private static readonly CultureInfo Ms = CultureInfo.GetCultureInfo("ms-MY");

    public static byte[] Render(string hospital, OfficerProfile profile, Person person, int year, int month, IReadOnlyList<EtapLongShift> shifts,
        DateTimeOffset generatedAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        string monthName = new DateOnly(year, month, 1).ToString("MMMM yyyy", Ms).ToUpperInvariant();
        int patients = shifts.Sum(s => s.Cases.Count);

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontSize(8.5f).FontColor(Ink));

            page.Header().PaddingBottom(10).Column(h =>
            {
                h.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(hospital.ToUpperInvariant()).FontSize(9).SemiBold().FontColor(Brand);
                        c.Item().Text("LAMPIRAN: BUTIRAN PESAKIT BAGI TUNTUTAN PANGGILAN PASIF MELEBIHI 4 JAM").FontSize(13).Bold();
                        c.Item().Text($"Penyata Tuntutan Elaun Tugas Atas Panggilan (ETAP) · Bulan {monthName}").FontSize(9).FontColor(Muted);
                    });
                    r.ConstantItem(170).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text($"{shifts.Count} hari · {patients} pesakit").SemiBold();
                        c.Item().AlignRight().Text($"Jumlah masa: {Hm(shifts.Sum(s => s.Minutes))}").FontColor(Muted);
                    });
                });
                h.Item().PaddingTop(8).Border(0.6f).BorderColor(Rule).Background("#F8FAFC").Padding(6).Row(r =>
                {
                    void Pair(string k, string? v) => r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(k).FontSize(7).FontColor(Muted);
                        c.Item().Text(v ?? "-").SemiBold();
                    });
                    Pair("NAMA PEGAWAI", profile.FullName);
                    Pair("NO. KAD PENGENALAN", FormatIc(profile.IcNo));
                    Pair("JAWATAN / GRED", $"{profile.Jawatan} / {profile.Gred}");
                    Pair("UNIT / TEMPAT BERTUGAS", profile.UnitTempatBertugas);
                });
            });

            page.Content().Column(col =>
            {
                col.Spacing(10);
                foreach (var shift in shifts)
                {
                    col.Item().EnsureSpace(90).Column(block =>
                    {
                        block.Item().Background(Head).PaddingVertical(4).PaddingHorizontal(6).Row(r =>
                        {
                            r.RelativeItem().Text($"Tarikh panggilan: {shift.Date.ToString("dddd, d MMMM yyyy", Ms)}").Bold();
                            r.AutoItem().Text($"{shift.Cases.Count} pesakit · jumlah {Hm(shift.Minutes)}").SemiBold();
                        });
                        block.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(22); c.ConstantColumn(64); c.ConstantColumn(38); c.RelativeColumn(1.6f); c.RelativeColumn(1.2f);
                                c.ConstantColumn(70); c.RelativeColumn(1.2f); c.RelativeColumn(3f); c.RelativeColumn(3f); c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.5f);
                            });
                            t.Header(hd =>
                            {
                                foreach (var label in new[] { "Bil", "Masa", "Jam", "Nama pesakit", "No. MyKad / daftar", "Jantina / umur",
                                             "Rujukan / jabatan", "Diagnosa", "Rawatan", "Pelan", "DSA" })
                                    hd.Cell().BorderBottom(0.8f).BorderColor(Rule).PaddingVertical(3).PaddingHorizontal(3)
                                        .Text(label).FontSize(7).SemiBold().FontColor(Muted);
                            });
                            foreach (var (c, i) in shift.Cases.Select((c, i) => (c, i)))
                            {
                                string[] cells =
                                [
                                    $"{i + 1}.",
                                    $"{c.TimeStart:HH\\:mm}–{c.TimeFinish:HH\\:mm}" + (c.Date != shift.Date ? $"\n({c.Date.ToString("d MMM", Ms)})" : ""),
                                    $"{c.Minutes / 60}:{c.Minutes % 60:00}",
                                    c.Confidential ? "SULIT" : c.PatientName,
                                    c.Confidential ? "SULIT" : c.PatientIdNo,
                                    $"{c.Gender}{(c.Age is null ? "" : $" / {c.Age}")}",
                                    string.Join(" / ", new[] { c.ReferralSource, c.Department }.Where(x => x is not null)),
                                    c.Diagnosis ?? "-", c.Management ?? "-", c.Plan ?? "-",
                                    string.Join("\n", new[] { c.DsaName, c.DsaStatus }.Where(x => x is not null)),
                                ];
                                foreach (var cell in cells)
                                    t.Cell().BorderBottom(0.4f).BorderColor(Rule).PaddingVertical(3).PaddingHorizontal(3).Text(cell);
                            }
                        });
                    });
                }

                // Verified by another officer, so the signature space is left blank for them.
                col.Item().PaddingTop(16).ShowEntire().Column(c =>
                {
                    c.Item().Text("Saya mengesahkan butiran di atas adalah betul dan direkodkan semasa bertugas atas panggilan.").FontColor(Muted);
                    c.Item().PaddingTop(10).Text("Disahkan oleh:").FontColor(Muted);
                    c.Item().PaddingTop(36).Width(220).BorderTop(0.6f).BorderColor(Ink);
                });
            });

            page.Footer().BorderTop(0.6f).BorderColor(Rule).PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Muted));
                    t.Span($"Dijana oleh Rota-OC pada {generatedAt.ToOffset(TimeSpan.FromHours(8)).ToString("d MMMM yyyy, HH:mm", Ms)}. Sulit: maklumat pesakit.");
                });
                r.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Muted));
                    t.Span("Muka ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static string Hm(int minutes) => $"{minutes / 60} jam {minutes % 60:00} min";
    private static string? FormatIc(string? ic) => ic is { Length: 12 } ? $"{ic[..6]}-{ic[6..8]}-{ic[8..]}" : ic;
}
