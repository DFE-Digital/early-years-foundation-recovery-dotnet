using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.Web.Content;

public sealed record CertificateData(string LearnerName, string ModuleTitle, string Criteria, DateTimeOffset CompletedAt)
{
    public string CompletionDate => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(CompletedAt, "Europe/London")
        .ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
}

public interface ICertificatePdfRenderer
{
    Task<byte[]> GenerateAsync(CertificateData certificate, CancellationToken cancellationToken);
}

public sealed class CertificateRenderer(TrainingContentRenderer contentRenderer) : ICertificatePdfRenderer
{
    private static readonly Lazy<string> Logo = new(() =>
    {
        using var stream = typeof(CertificateRenderer).Assembly.GetManifestResourceStream(
            "EarlyYearsFoundationRecovery.Web.CertificateLogo.png")
            ?? throw new InvalidOperationException("The certificate logo resource is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Convert.ToBase64String(buffer.ToArray());
    });

    public HtmlString RenderBody(CertificateData certificate)
    {
        var encode = HtmlEncoder.Default;
        var criteria = string.IsNullOrWhiteSpace(certificate.Criteria)
            ? string.Empty
            : $"<p>This module has covered:</p>{contentRenderer.Render(certificate.Criteria).Value}";
        return new HtmlString($"""
            <article class="certificate-award" aria-label="Certificate of achievement">
              <p>Certificate of achievement</p>
              <h1>{encode.Encode(certificate.LearnerName)}</h1>
              <p>has completed the following module:</p>
              <h2>{encode.Encode(certificate.ModuleTitle)}</h2>
              <p>Date completed: {encode.Encode(certificate.CompletionDate)}</p>
              {criteria}
              <p>Certified by the Department for Education</p>
              <img src="data:image/png;base64,{Logo.Value}" width="220" alt="Department for Education logo">
            </article>
            """);
    }

    public async Task<byte[]> GenerateAsync(CertificateData certificate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Timeout = 30_000
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ServiceWorkers = ServiceWorkerPolicy.Block
        });
        await context.RouteAsync("**/*", route => route.AbortAsync());
        var page = await context.NewPageAsync();
        await page.SetContentAsync($$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>Certificate of achievement</title>
              <style>
                @page { size: A4; margin: 18mm; }
                body { font: 12pt Arial, sans-serif; color: #0b0c0c; margin: 0; }
                h1 { font-size: 30pt; overflow-wrap: anywhere; }
                h2 { font-size: 22pt; }
                p { margin: 12pt 0; }
                li { margin: 4pt 0; }
                img { margin-top: 12pt; }
                .certificate-award { border-top: 5pt solid #1d70b8; padding-top: 16pt; }
              </style>
            </head>
            <body>{{RenderBody(certificate).Value}}</body>
            </html>
            """, new PageSetContentOptions { Timeout = 30_000 });
        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PreferCSSPageSize = true,
            PrintBackground = true,
            Tagged = true
        });
    }
}
