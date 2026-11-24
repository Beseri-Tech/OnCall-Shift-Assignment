using System.Text;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>
/// A case as the WhatsApp passover message for the department group, in the seniors' template:
/// referral, S/T, S/B, demographic, name + IC, P/W (trauma) or C/O, Ix ticked, Dx, Mx, plan and Rx. Empty parts are left out.
/// </summary>
public static class CaseText
{
    public static string WhatsApp(OnCallCase c)
    {
        var sb = new StringBuilder();
        void Block(params string?[] lines)
        {
            var present = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (present.Count == 0) return;
            if (sb.Length > 0) sb.Append('\n');
            foreach (var l in present) sb.Append(l).Append('\n');
        }
        string? Section(string label, string? text) => string.IsNullOrWhiteSpace(text) ? null : $"{label}:\n{text}";

        Block(c.Department is null ? "Dear Officer" : $"Dear {c.Department} Officer", c.ReferralSource is null ? null : $"N/C from {c.ReferralSource}{(c.EdZone is null ? "" : $" {c.EdZone} ZONE")}");
        Block(c.SpokenTo is null ? null : $"S/T {c.SpokenTo}", c.SeenBy is null ? null : $"S/B {c.SeenBy}");
        string age = c.Age is { } a && a.All(char.IsAsciiDigit) ? a + "Y" : c.Age ?? "";
        Block(string.Join("/", new[] { age, Initial(c.Race), Initial(c.Gender) }.Where(x => x.Length > 0)), c.PatientName, FormatId(c.PatientIdNo));
        Block(c.Presentation is null ? null : $"{(c.CaseType == "TRAUMA" ? "P/W" : "C/O")}: {c.Presentation}");
        Block(Section("Ix", c.Investigations is null ? null : string.Join("\n", c.Investigations.Split(", ").Select(x => x + " ✅"))));
        Block(Section("Dx", c.Diagnosis));
        Block(Section("Mx", c.Management));
        Block(Section("Plan", c.Plan));
        Block(c.Prescription is null ? null : $"Rx: {c.Prescription}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>MALAY -> M, FEMALE -> F; anything unusual stays as typed.</summary>
    private static string Initial(string? s) => s switch
    {
        null or "" => "",
        "MALAY" or "MALE" => "M",
        "FEMALE" => "F",
        "CHINESE" => "C",
        "INDIAN" => "I",
        "SIAMESE" => "S",
        _ => s,
    };

    private static string FormatId(string id) =>
        id.Length == 12 && id.All(char.IsAsciiDigit) ? $"{id[..6]}-{id[6..8]}-{id[8..]}" : id;
}
