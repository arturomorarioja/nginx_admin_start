using System.Net.Http.Json;
using System.Text;
using Learning.Web;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:7001");
builder.Services.AddSingleton<LearningData>();
builder.Services.AddHttpClient("certificates", client =>
    client.BaseAddress = new Uri("http://127.0.0.1:7002/"));

var app = builder.Build();

static IResult Html(string title, string body) =>
    Results.Content(Ui.Page(title, body), "text/html; charset=utf-8");

app.MapGet("/health", () => Results.Ok(new { status = "ready", service = "learning" }));

app.MapGet("/courses/", (LearningData data) =>
{
    var blocks = new StringBuilder();
    foreach (var course in data.Courses())
    {
        blocks.Append($$"""
            <article class="panel"><p class="meta">{{Ui.E(course.Category)}} · {{course.Hours}} hours</p>
            <h2>{{Ui.E(course.Title)}}</h2><p>{{Ui.E(course.Summary)}}</p>
            <a class="button" href="/courses/{{course.Id}}">Course details</a></article>
            """);
    }
    return Html("Course catalogue", $$"""
        <p class="eyebrow">Course catalogue</p><h1>Find your next course</h1>
        <p class="muted">Explore professional development courses and manage your enrolments online.</p>
        <div class="cards">{{blocks}}</div>
        """);
});

app.MapGet("/courses/{id:int}", (int id, LearningData data) =>
{
    var course = data.FindCourse(id);
    if (course is null) return Results.NotFound();
    var count = data.Enrollments().Count(e => e.CourseId == id);
    return Html(course.Title, $$"""
        <p><a href="/courses/">← Course catalogue</a></p>
        <p class="eyebrow">{{Ui.E(course.Category)}} · {{course.Hours}} hours</p>
        <h1>{{Ui.E(course.Title)}}</h1><p class="lead">{{Ui.E(course.Summary)}}</p>
        <p class="muted">Places available: {{course.Capacity - count}} of {{course.Capacity}}</p>
        <section class="panel"><h2>Enrol in this course</h2>
        <form method="post" action="/courses/{{course.Id}}/enrol">
            <label class="field">Full name<input name="fullName" maxlength="80" required></label>
            <label class="field">Email address<input type="email" name="email" maxlength="120" required></label>
            <button class="button" type="submit">Confirm enrolment</button>
        </form></section>
        """);
});

app.MapPost("/courses/{id:int}/enrol", async (int id, HttpRequest request, LearningData data) =>
{
    var form = await request.ReadFormAsync();
    var name = form["fullName"].ToString().Trim();
    var email = form["email"].ToString().Trim();
    if (name.Length is < 2 or > 80 || email.Length is < 5 or > 120 || !email.Contains('@'))
        return Results.BadRequest("Enter a valid name and email address.");
    try
    {
        data.Enroll(id, name, email);
        return Results.Redirect("/account/?email=" + Uri.EscapeDataString(email), false);
    }
    catch (ArgumentException) { return Results.NotFound(); }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/account/", (string? email, LearningData data) =>
{
    if (string.IsNullOrWhiteSpace(email))
        return Html("My learning", """
            <p class="eyebrow">Student portal</p><h1>My learning</h1>
            <p>Enter the email address used for your enrolments.</p>
            <form method="get" action="/account/">
                <label class="field">Email address<input name="email" type="email" required></label>
                <button type="submit" class="button">Find my courses</button>
            </form>
            """);

    var blocks = new StringBuilder();
    foreach (var e in data.EnrollmentsFor(email))
    {
        var course = data.FindCourse(e.CourseId);
        if (course is null) continue;
        var status = e.CompletedAt.HasValue ? "Completed" : "In progress";
        var action = e.CertificateId is Guid certId
            ? $"<a class=\"button\" href=\"/certificates/{certId}.pdf\">Download certificate</a>"
            : $$"""
                <form method="post" action="/account/complete/{{e.Id}}">
                    <button class="button" type="submit">Mark course completed</button>
                </form>
                """;
        blocks.Append($$"""
            <article class="panel"><p class="meta">{{Ui.E(status)}}</p>
            <h2>{{Ui.E(course.Title)}}</h2>
            <p>Enrolled {{e.EnrolledAt:dd MMM yyyy}}</p>{{action}}</article>
            """);
    }
    var content = blocks.Length > 0 ? blocks.ToString() :
        "<p>No enrolments were found for this email address.</p>";
    return Html("My learning", $$"""
        <p class="eyebrow">Student portal</p><h1>My learning</h1>
        <p>Enrolments for <strong>{{Ui.E(email)}}</strong></p>
        <div class="cards">{{content}}</div><p><a href="/courses/">Browse more courses</a></p>
        """);
});

app.MapPost("/account/complete/{id:guid}", async (
    Guid id, LearningData data, IHttpClientFactory clientFactory) =>
{
    var enrolment = data.GetEnrollment(id);
    if (enrolment is null) return Results.NotFound();
    if (enrolment.CertificateId.HasValue)
        return Results.Redirect("/account/?email=" + Uri.EscapeDataString(enrolment.Email));

    var course = data.FindCourse(enrolment.CourseId);
    if (course is null) return Results.NotFound();
    var issue = new CertificateIssue(enrolment.Id, enrolment.FullName, course.Title, DateTimeOffset.UtcNow);
    try
    {
        // This is a direct loopback HTTP request, not a request to the public NGINX listener.
        var client = clientFactory.CreateClient("certificates");
        using var response = await client.PostAsJsonAsync("issue", issue);
        if (!response.IsSuccessStatusCode) return Results.StatusCode(502);
        var certificate = await response.Content.ReadFromJsonAsync<CertificateResponse>();
        if (certificate is null) return Results.StatusCode(502);
        data.Complete(id, certificate.Id);
        return Results.Redirect("/account/?email=" + Uri.EscapeDataString(enrolment.Email));
    }
    catch (HttpRequestException) { return Results.StatusCode(502); }
    catch (TaskCanceledException) { return Results.StatusCode(502); }
});

// Internal endpoints consumed by the staff application; not exposed through NGINX.
app.MapGet("/internal/courses", (LearningData data) => Results.Ok(data.Courses()));
app.MapGet("/internal/enrollments", (LearningData data) => Results.Ok(data.Enrollments()));
app.MapPost("/internal/courses/{id:int}/capacity", (int id, CapacityUpdate update, LearningData data) =>
{
    try
    {
        data.SetCapacity(id, update.Capacity);
        return Results.NoContent();
    }
    catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.Run();
