using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ContractReview.IntegrationTests;

public static class TestPdfs
{
    public static byte[] WithText(params string[] pageTexts)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pageTexts)
        {
            var page = builder.AddPage(595, 842);
            page.AddText(text, 12, new PdfPoint(50, 700), font);
        }

        return builder.Build();
    }

    public static byte[] ImageOnly()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842);
        return builder.Build();
    }

    public static byte[] Corrupt() => Encoding.ASCII.GetBytes("%PDF-1.4\nthis is not a real pdf body\n");

    /// <summary>A valid PDF padded with trailing newlines so its size is exactly the requested size.</summary>
    public static byte[] PaddedTo(long size)
    {
        var pdf = WithText("Padded contract text");
        var result = new byte[size];
        Array.Copy(pdf, result, pdf.Length);
        Array.Fill(result, (byte)10, pdf.Length, result.Length - pdf.Length);
        return result;
    }
}
