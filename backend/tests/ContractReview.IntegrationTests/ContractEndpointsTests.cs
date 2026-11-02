using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Api.Routes;
using ContractReview.Domain.Errors;

namespace ContractReview.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ContractEndpointsTests(ApiFactory factory)
{
    private const string PdfType = "application/pdf";
    private const long TenMb = 10L * 1024 * 1024;
    private readonly HttpClient client = factory.CreateClient();

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static MultipartFormDataContent Form(string? title, byte[]? file, string fileName = "contract.pdf", string contentType = PdfType)
    {
        var form = new MultipartFormDataContent();
        if (title is not null)
        {
            form.Add(new StringContent(title), "title");
        }

        if (file is not null)
        {
            var part = new ByteArrayContent(file);
            part.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(part, "file", fileName);
        }

        return form;
    }

    private async Task<JsonElement> ListAsync() =>
        await client.GetFromJsonAsync<JsonElement>(ApiRoutes.Contracts);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Upload_then_list_then_get_returns_stored_contract()
    {
        var title = Unique("Happy");
        var pdf = TestPdfs.WithText("Payment is due within thirty days.", "Second page terms.");

        var created = await client.PostAsync(ApiRoutes.Contracts, Form(title, pdf));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = summary.GetProperty("id").GetGuid();
        Assert.Equal($"{ApiRoutes.Contracts}/{id}", created.Headers.Location?.AbsolutePath);
        Assert.Equal(title, summary.GetProperty("title").GetString());
        Assert.Equal("contract.pdf", summary.GetProperty("fileName").GetString());
        Assert.Equal(pdf.Length, summary.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(2, summary.GetProperty("pageCount").GetInt32());

        var list = await ListAsync();
        var item = list.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == id);
        Assert.Equal(title, item.GetProperty("title").GetString());
        Assert.False(item.TryGetProperty("extractedText", out _));

        var detail = await client.GetFromJsonAsync<JsonElement>($"{ApiRoutes.Contracts}/{id}");
        Assert.Equal(title, detail.GetProperty("title").GetString());
        var text = detail.GetProperty("extractedText").GetString();
        Assert.Contains("Payment is due within thirty days.", text);
        Assert.Contains("Second page terms.", text);
    }

    [Fact]
    public async Task List_returns_newest_first()
    {
        var first = Unique("First");
        var second = Unique("Second");
        await client.PostAsync(ApiRoutes.Contracts, Form(first, TestPdfs.WithText("one")));
        await Task.Delay(50);
        await client.PostAsync(ApiRoutes.Contracts, Form(second, TestPdfs.WithText("two")));

        var titles = (await ListAsync()).EnumerateArray().Select(e => e.GetProperty("title").GetString()).ToList();

        Assert.True(titles.IndexOf(second) >= 0 && titles.IndexOf(second) < titles.IndexOf(first));
    }

    [Fact]
    public async Task Upload_returns_400_when_file_is_not_a_pdf_by_extension()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(Unique("T"), TestPdfs.WithText("x"), "contract.txt"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractFileNotPdf);
    }

    [Fact]
    public async Task Upload_returns_400_when_content_type_is_not_pdf()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(Unique("T"), TestPdfs.WithText("x"), contentType: "text/plain"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractFileNotPdf);
    }

    [Fact]
    public async Task Upload_returns_400_when_header_is_not_pdf_and_stores_nothing()
    {
        var title = Unique("NoHeader");
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(title, "just plain text"u8.ToArray()));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractFileNotPdf);
        Assert.DoesNotContain((await ListAsync()).EnumerateArray(), e => e.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task Upload_returns_400_when_file_is_over_limit_and_stores_nothing()
    {
        var title = Unique("Big");
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(title, TestPdfs.PaddedTo(TenMb + 1)));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractFileTooLarge);
        Assert.DoesNotContain((await ListAsync()).EnumerateArray(), e => e.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task Upload_accepts_file_exactly_at_limit()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(Unique("Max"), TestPdfs.PaddedTo(TenMb)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Upload_returns_400_when_title_is_blank(string title)
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(title, TestPdfs.WithText("x")));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractTitleInvalid);
    }

    [Fact]
    public async Task Upload_returns_400_when_title_is_missing()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(null, TestPdfs.WithText("x")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_returns_400_when_title_is_201_characters()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(new string('a', 201), TestPdfs.WithText("x")));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractTitleInvalid);
    }

    [Fact]
    public async Task Upload_accepts_title_of_200_characters()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(new string('a', 200), TestPdfs.WithText("x")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_returns_400_when_file_is_missing()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(Unique("T"), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_returns_400_when_pdf_has_no_text()
    {
        var title = Unique("Scanned");
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(title, TestPdfs.ImageOnly()));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractNoText);
        Assert.DoesNotContain((await ListAsync()).EnumerateArray(), e => e.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task Upload_returns_400_when_pdf_is_corrupt()
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(Unique("Corrupt"), TestPdfs.Corrupt()));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ContractPdfUnreadable);
    }

    [Fact]
    public async Task Get_returns_404_when_id_is_unknown()
    {
        var response = await client.GetAsync($"{ApiRoutes.Contracts}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ErrorCodes.ContractNotFound);
    }

    [Fact]
    public async Task Get_returns_404_when_id_is_not_a_guid()
    {
        var response = await client.GetAsync($"{ApiRoutes.Contracts}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
