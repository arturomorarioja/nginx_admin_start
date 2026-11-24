using System.Globalization;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:7002");
var app = builder.Build();
var repository = new CertificateRepository("/var/lib/cedar/certificates.json");

app.MapGet("/health", () => Results.Ok(new { status = "ready", service = "certificates" }));

app.MapPost("/issue", (IssueRequest request) =>
{
    if (request.StudentName.Length is < 2 or > 80 ||
        request.CourseTitle.Length is < 2 or > 140 ||
        request.EnrollmentId == Guid.Empty)
        return Results.BadRequest("Invalid certificate data.");

    var issued = repository.Issue(request);
    return Results.Ok(new IssueResponse(issued.Id));
});

app.MapGet("/certificates/{filename}", (string filename) =>
{
    if (!filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
        !Guid.TryParse(filename[..^4], out var certificateId))
        return Results.NotFound();

    var certificate = repository.Find(certificateId);
    if (certificate is null) return Results.NotFound();

    var pdf = CertificatePdf.Create(certificate);
    return Results.File(pdf, "application/pdf", $"cedar-certificate-{certificate.Id:N}.pdf");
});

app.Run();

record IssueRequest(Guid EnrollmentId, string StudentName, string CourseTitle, DateTimeOffset CompletedAt);
record IssueResponse(Guid Id);
record Certificate(Guid Id, Guid EnrollmentId, string StudentName, string CourseTitle, DateTimeOffset CompletedAt);

sealed class CertificateRepository
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly List<Certificate> _issued;

    public CertificateRepository(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _issued = File.Exists(path)
            ? JsonSerializer.Deserialize<List<Certificate>>(File.ReadAllText(path)) ?? []
            : [];
    }

    public Certificate Issue(IssueRequest request)
    {
        lock (_gate)
        {
            var existing = _issued.FirstOrDefault(c => c.EnrollmentId == request.EnrollmentId);
            if (existing is not null) return existing;

            var issued = new Certificate(Guid.NewGuid(), request.EnrollmentId,
                request.StudentName, request.CourseTitle, request.CompletedAt);
            _issued.Add(issued);
            File.WriteAllText(_path, JsonSerializer.Serialize(_issued));
            return issued;
        }
    }

    public Certificate? Find(Guid id)
    {
        lock (_gate) { return _issued.FirstOrDefault(c => c.Id == id); }
    }
}

static class CertificatePdf
{
    public static byte[] Create(Certificate cert)
    {
        static string Escape(string text)
        {
            var ascii = new string(text.Normalize(NormalizationForm.FormD)
                .Where(c => c >= 32 && c <= 126).ToArray());
            return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        var title = Escape(cert.CourseTitle);
        var student = Escape(cert.StudentName);
        var date = cert.CompletedAt.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
        var identifier = cert.Id.ToString("N")[..12].ToUpperInvariant();
        var stream = $$"""
            BT
            /F1 16 Tf 60 755 Td (CEDAR LEARNING) Tj
            /F1 29 Tf 0 -125 Td (Certificate of Completion) Tj
            /F1 13 Tf 0 -65 Td (This certifies that) Tj
            /F1 23 Tf 0 -40 Td ({{student}}) Tj
            /F1 13 Tf 0 -52 Td (has completed the course) Tj
            /F1 19 Tf 0 -40 Td ({{title}}) Tj
            /F1 12 Tf 0 -78 Td (Completed: {{date}}) Tj
            0 -28 Td (Certificate reference: {{identifier}}) Tj
            ET
            """;
        var content = Encoding.ASCII.GetBytes(stream + "\n");

        using var buffer = new MemoryStream();
        var offsets = new List<long> { 0 };
        void Put(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            buffer.Write(bytes, 0, bytes.Length);
        }
        void Obj(int number, string value)
        {
            offsets.Add(buffer.Position);
            Put($"{number} 0 obj\n{value}\nendobj\n");
        }

        Put("%PDF-1.4\n");
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] " +
            "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
        offsets.Add(buffer.Position);
        Put($"4 0 obj\n<< /Length {content.Length} >>\nstream\n");
        buffer.Write(content, 0, content.Length);
        Put("endstream\nendobj\n");
        Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var xref = buffer.Position;
        Put("xref\n0 6\n0000000000 65535 f \n");
        for (var n = 1; n <= 5; n++)
            Put(offsets[n].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Put($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return buffer.ToArray();
    }
}
