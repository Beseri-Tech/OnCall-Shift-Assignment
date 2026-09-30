using System.Net;
using System.Text;

namespace Rota.Api.Services;

/// <summary>
/// One email's content, rendered as both plain text and branded HTML (header with logo, card, button, footer).
/// HTML uses tables and inline styles because that is what mail apps (Gmail, Outlook) reliably render.
/// </summary>
public sealed record EmailContent(
    string Subject,
    string Heading,
    IReadOnlyList<string> Paragraphs,
    string? ButtonText = null,
    string? ButtonUrl = null,
    IReadOnlyList<(string Label, string Value)>? Details = null,
    string? Note = null)
{
    private const string Teal = "#0e7490", TealDark = "#164e63", Ink = "#0f172a", Muted = "#64748b", Line = "#e2e8f0";

    public string Text()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Heading).AppendLine();
        foreach (var p in Paragraphs) sb.AppendLine(p).AppendLine();
        foreach (var (label, value) in Details ?? []) sb.AppendLine($"{label}: {value}");
        if (Details is { Count: > 0 }) sb.AppendLine();
        if (ButtonUrl is not null) sb.AppendLine($"{ButtonText}: {ButtonUrl}").AppendLine();
        if (Note is not null) sb.AppendLine(Note).AppendLine();
        sb.AppendLine("--").AppendLine("On-Call Rota · Dental Officers, Perlis").AppendLine("This is an automated message.");
        return sb.ToString();
    }

    /// <param name="baseUrl">The web app's address; the logo is served from there (mail apps don't show SVG).</param>
    public string Html(string baseUrl)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);

        var body = new StringBuilder();
        body.Append($"""<h1 style="margin:0 0 16px;font-size:22px;line-height:1.3;color:{TealDark};font-weight:700;">{E(Heading)}</h1>""");
        foreach (var p in Paragraphs)
            body.Append($"""<p style="margin:0 0 14px;font-size:15px;line-height:1.6;color:{Ink};">{E(p)}</p>""");

        if (Details is { Count: > 0 })
        {
            body.Append($"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:8px 0 20px;background:#f0f9fb;border:1px solid #cffafe;border-radius:8px;">""");
            foreach (var (label, value) in Details)
                body.Append($"""
                    <tr><td style="padding:10px 16px;font-size:13px;color:{Muted};width:40%;">{E(label)}</td>
                    <td style="padding:10px 16px;font-size:15px;color:{Ink};font-weight:700;font-family:Consolas,'Courier New',monospace;">{E(value)}</td></tr>
                    """);
            body.Append("</table>");
        }

        if (ButtonUrl is not null)
        {
            body.Append($"""
                <table role="presentation" cellpadding="0" cellspacing="0" style="margin:8px 0 20px;"><tr>
                <td style="border-radius:8px;background:{Teal};">
                <a href="{E(ButtonUrl)}" style="display:inline-block;padding:13px 28px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;">{E(ButtonText ?? "Open")}</a>
                </td></tr></table>
                <p style="margin:0 0 14px;font-size:12px;line-height:1.5;color:{Muted};">If the button doesn't work, copy this link into your browser:<br>
                <a href="{E(ButtonUrl)}" style="color:{Teal};word-break:break-all;">{E(ButtonUrl)}</a></p>
                """);
        }

        if (Note is not null)
            body.Append($"""<p style="margin:16px 0 0;padding-top:16px;border-top:1px solid {Line};font-size:13px;line-height:1.5;color:{Muted};">{E(Note)}</p>""");

        string logo = string.IsNullOrEmpty(baseUrl) ? "" : $"""
            <td style="padding-right:12px;" valign="middle"><table role="presentation" cellpadding="0" cellspacing="0"><tr>
            <td style="background:#ffffff;border-radius:10px;padding:5px;"><img src="{E(baseUrl)}/email-logo.png" width="30" height="30" alt="" style="display:block;border:0;"></td>
            </tr></table></td>
            """;

        return $"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(Subject)}</title></head>
            <body style="margin:0;padding:0;background:#f3f8fa;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f3f8fa;"><tr><td align="center" style="padding:24px 12px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;">
              <tr><td style="background:{TealDark};background-image:linear-gradient(100deg,{TealDark},{Teal});border-radius:12px 12px 0 0;padding:18px 24px;">
                <table role="presentation" cellpadding="0" cellspacing="0"><tr>{logo}
                <td valign="middle"><div style="font-size:18px;font-weight:700;color:#ffffff;line-height:1.2;">On-Call Rota</div>
                <div style="font-size:12px;color:#cffafe;line-height:1.3;">Dental Officers · Perlis</div></td>
                </tr></table>
              </td></tr>
              <tr><td style="background:#ffffff;padding:28px 24px;border:1px solid {Line};border-top:0;border-radius:0 0 12px 12px;">{body}</td></tr>
              <tr><td style="padding:18px 24px;text-align:center;font-size:12px;line-height:1.6;color:{Muted};">
                On-Call Rota · Dental Officers, Perlis<br>
                This is an automated message. Please don't reply; contact your rota administrator if you need help.
              </td></tr>
            </table>
            </td></tr></table>
            </body></html>
            """;
    }
}
