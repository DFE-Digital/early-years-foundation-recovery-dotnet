using EarlyYearsFoundationRecovery.Web.Content;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using System.Text.RegularExpressions;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class CertificateRendererTests
{
    private readonly CertificateRenderer _renderer = new(new TrainingContentRenderer());

    [Fact]
    public void CertificateEncodesPersonalDetailsAndSanitizesCriteria()
    {
        var certificate = new CertificateData(
            "<script>Alex</script>", "<img src=x onerror=alert(1)>",
            "## Learning\n\n- **Child development**\n\n[Unsafe](javascript:alert%281%29)",
            DateTimeOffset.Parse("2026-06-30T23:30:00Z"));

        var html = _renderer.RenderBody(certificate).Value!;

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.Contains("&lt;script&gt;Alex&lt;/script&gt;", html);
        Assert.Contains("Date completed: 1 July 2026", html);
        Assert.Contains("<strong>Child development</strong>", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.Contains("Department for Education logo", html);
    }

    [Fact]
    public async Task GeneratesAReadableA4PdfWithCertificateDetails()
    {
        var certificate = new CertificateData(
            "Alex Example", "Understanding child development and the EYFS",
            "- Understand how children learn\n- Support individual development",
            DateTimeOffset.Parse("2026-10-23T12:00:00Z"));

        var bytes = await _renderer.GenerateAsync(certificate, CancellationToken.None);

        Assert.True(bytes.AsSpan().StartsWith("%PDF-"u8));
        using var pdf = PdfDocument.Open(bytes);
        var page = Assert.Single(pdf.GetPages());
        var text = Regex.Replace(ContentOrderTextExtractor.GetText(page), @"\s+", " ");
        Assert.Contains("Alex Example", text);
        Assert.Contains("Understanding child development and the EYFS", text);
        Assert.Contains("23 October 2026", text);
        Assert.Contains("Understand how children learn", text);
        Assert.Contains("Certified by the Department for Education", text);
        Assert.InRange(page.Width, 590, 600);
        Assert.InRange(page.Height, 835, 850);
        Assert.NotEmpty(page.GetImages());
    }

    [Theory]
    [InlineData("2026-06-30T23:30:00Z", "1 July 2026")]
    [InlineData("2026-10-27T23:30:00Z", "27 October 2026")]
    public void CompletionDateUsesUkTimeInSummerAndWinter(string completedAt, string expected)
    {
        var certificate = new CertificateData("Alex", "Module", "", DateTimeOffset.Parse(completedAt));

        Assert.Equal(expected, certificate.CompletionDate);
    }

    [Fact]
    public async Task CancelledRequestDoesNotStartPdfGeneration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var certificate = new CertificateData("Alex", "Module", "", DateTimeOffset.UtcNow);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _renderer.GenerateAsync(certificate, cancellation.Token));
    }
}
