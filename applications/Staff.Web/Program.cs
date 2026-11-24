using System.Net;
using System.Net.Http.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:7003");
builder.Services.AddHttpClient("learning", c => c.BaseAddress = new Uri("http://127.0.0.1:7001/"));
var app = builder.Build();

static string E(object? s) => WebUtility.HtmlEncode(s?.ToString() ?? "");
static IResult Page(string title, string content) => Results.Content($$"""
    <!doctype html><html lang="en"><head><meta charset="utf-8">
    <meta name="viewport" content="width=device-width,initial-scale=1">
    <title>{{E(title)}} — Cedar Learning Staff</title>
    <style>
      :root{--ink:#172b3e;--accent:#245a68;--light:#f4f7f8;--border:#dde6e8}
      body{margin:0;background:var(--light);color:var(--ink);font-family:system-ui,'Segoe UI',sans-serif;line-height:1.5}
      header{background:#fff;border-bottom:1px solid var(--border)}
      .wrap{max-width:1090px;margin:0 auto;padding:0 28px}
      header .wrap{display:flex;gap:28px;align-items:center;justify-content:space-between;min-height:80px}
      header strong{letter-spacing:.08em}nav{display:flex;gap:20px;flex-wrap:wrap}nav a{font-weight:600;text-decoration:none;font-size:14px}
      a{color:var(--accent)}main{padding-top:36px!important;padding-bottom:70px!important}
      h1{font-size:36px;margin-bottom:8px}h2{font-size:22px}p.muted{color:#59707a}
      .grid{display:grid;grid-template-columns:repeat(3,1fr);gap:15px}.tile,.panel{background:#fff;padding:24px;border:1px solid var(--border);border-radius:7px}
      .tile strong{font-size:37px;display:block}.tile span{font-size:14px;color:#59707a}
      table{border-collapse:collapse;width:100%;background:white}td,th{border-bottom:1px solid var(--border);text-align:left;padding:12px}
      .panel{margin:20px 0}.small{font-size:13px;color:#59707a}input{border:1px solid #bdcbd0;border-radius:4px;padding:8px;width:100px;margin-right:12px}
      button{border:0;background:var(--accent);color:white;padding:10px 13px;border-radius:4px;cursor:pointer}
      .notice{background:#e4eef1;padding:16px;border-radius:4px}
      @media(max-width:750px){header .wrap{display:block;padding-top:18px;padding-bottom:18px}.grid{grid-template-columns:1fr}table{font-size:12px} }
    </style></head><body>
    <header><div class="wrap"><strong>CEDAR LEARNING · STAFF</strong>
    <nav><a href="/">Dashboard</a><a href="/courses/">Courses</a>
    <a href="/registrations/">Registrations</a><a href="/reports/">Reports</a>
    <a href="/internal/">Operations</a><a href="/settings/">Settings</a></nav></div></header>
    <main class="wrap">{{content}}</main></body></html>
    """, "text/html; charset=utf-8");

var contactEmail = "support@cedarlearning.example";

static async Task<T?> Get<T>(IHttpClientFactory factory, string path)
{
    try { return await factory.CreateClient("learning").GetFromJsonAsync<T>(path); }
    catch (HttpRequestException) { return default; }
    catch (TaskCanceledException) { return default; }
}

app.MapGet("/health", () => Results.Ok(new { status = "ready", service = "staff" }));

app.MapGet("/", async (IHttpClientFactory factory) =>
{
    var courses = await Get<List<Course>>(factory, "internal/courses");
    var enrolments = await Get<List<Enrollment>>(factory, "internal/enrollments");
    if (courses is null || enrolments is null) return Results.StatusCode(502);
    var completed = enrolments.Count(e => e.CompletedAt.HasValue);
    return Page("Dashboard", $$"""
        <p class="small">ADMINISTRATION / OVERVIEW</p><h1>Training operations</h1>
        <p class="muted">Current course activity and student progress.</p>
        <div class="grid"><div class="tile"><strong>{{courses.Count}}</strong><span>Available courses</span></div>
        <div class="tile"><strong>{{enrolments.Count}}</strong><span>Total enrolments</span></div>
        <div class="tile"><strong>{{completed}}</strong><span>Completed courses</span></div></div>
        <div class="panel"><h2>Quick access</h2><p><a href="/courses/">Manage course capacity</a> ·
        <a href="/registrations/">Review registrations</a> · <a href="/reports/">View completion report</a></p></div>
        """);
});

app.MapGet("/courses/", async (IHttpClientFactory factory) =>
{
    var courses = await Get<List<Course>>(factory, "internal/courses");
    var registrations = await Get<List<Enrollment>>(factory, "internal/enrollments");
    if (courses is null || registrations is null) return Results.StatusCode(502);
    var rows = new StringBuilder();
    foreach (var c in courses)
    {
        var occupied = registrations.Count(e => e.CourseId == c.Id);
        rows.Append($$"""
            <tr><td>{{E(c.Title)}}</td><td>{{E(c.Category)}}</td><td>{{occupied}} / {{c.Capacity}}</td>
            <td><form action="/courses/{{c.Id}}/capacity" method="post">
            <input type="number" name="capacity" min="1" max="200" value="{{c.Capacity}}" aria-label="Capacity for {{E(c.Title)}}">
            <button>Update</button></form></td></tr>
            """);
    }
    return Page("Course administration", $$"""
        <p class="small">ADMINISTRATION / COURSES</p><h1>Course capacity</h1>
        <p class="muted">Update the maximum number of enrolments available for each course.</p>
        <table><thead><tr><th>Course</th><th>Subject</th><th>Enrolled</th><th>Capacity</th></tr></thead>
        <tbody>{{rows}}</tbody></table>
        """);
});

app.MapPost("/courses/{id:int}/capacity", async (
    int id, HttpRequest request, IHttpClientFactory factory) =>
{
    var form = await request.ReadFormAsync();
    if (!int.TryParse(form["capacity"].ToString(), out var capacity) || capacity < 1 || capacity > 200)
        return Results.BadRequest("Capacity must be between 1 and 200.");
    try
    {
        using var response = await factory.CreateClient("learning")
            .PostAsJsonAsync($"internal/courses/{id}/capacity", new CapacityUpdate(capacity));
        if (!response.IsSuccessStatusCode) return Results.BadRequest("Capacity cannot be set below current enrolments.");
        return Results.Redirect("/courses/");
    }
    catch (HttpRequestException) { return Results.StatusCode(502); }
});

app.MapGet("/registrations/", async (IHttpClientFactory factory) =>
{
    var courses = await Get<List<Course>>(factory, "internal/courses");
    var registrations = await Get<List<Enrollment>>(factory, "internal/enrollments");
    if (courses is null || registrations is null) return Results.StatusCode(502);
    var rows = new StringBuilder();
    foreach (var e in registrations.OrderByDescending(e => e.EnrolledAt))
    {
        var title = courses.FirstOrDefault(c => c.Id == e.CourseId)?.Title ?? "Unknown course";
        rows.Append($$"""
            <tr><td>{{E(e.FullName)}}</td><td>{{E(e.Email)}}</td>
            <td>{{E(title)}}</td><td>{{e.EnrolledAt:dd MMM yyyy}}</td>
            <td>{{(e.CompletedAt.HasValue ? "Completed" : "In progress")}}</td></tr>
            """);
    }
    return Page("Registrations", $$"""
        <p class="small">ADMINISTRATION / REGISTRATIONS</p><h1>Registrations</h1>
        <p class="muted">{{registrations.Count}} registration(s)</p>
        <table><thead><tr><th>Participant</th><th>Email</th><th>Course</th><th>Enrolled</th><th>Status</th></tr></thead>
        <tbody>{{rows}}</tbody></table>
        """);
});

app.MapGet("/reports/", async (IHttpClientFactory factory) =>
{
    var courses = await Get<List<Course>>(factory, "internal/courses");
    var registrations = await Get<List<Enrollment>>(factory, "internal/enrollments");
    if (courses is null || registrations is null) return Results.StatusCode(502);
    var rows = new StringBuilder();
    foreach (var course in courses)
    {
        var enrolled = registrations.Count(e => e.CourseId == course.Id);
        var completed = registrations.Count(e => e.CourseId == course.Id && e.CompletedAt.HasValue);
        rows.Append($"<tr><td>{E(course.Title)}</td><td>{enrolled}</td><td>{completed}</td><td>{(enrolled == 0 ? 0 : completed * 100 / enrolled)}%</td></tr>");
    }
    return Page("Completion report", $$"""
        <p class="small">ADMINISTRATION / REPORTS</p><h1>Course completion</h1>
        <p class="muted">Enrolment and completion figures per course.</p>
        <table><thead><tr><th>Course</th><th>Enrolled</th><th>Completed</th><th>Completion rate</th></tr></thead>
        <tbody>{{rows}}</tbody></table>
        """);
});

app.MapGet("/internal/", () => Page("Operations", """
    <p class="small">ADMINISTRATION / OPERATIONS</p><h1>Internal operations</h1>
    <p class="notice">Restricted operational information for the training team.</p>
    <div class="panel"><h2>Service contacts</h2>
    <p>Student support: support@cedarlearning.example</p>
    <p>Teaching operations: operations@cedarlearning.example</p></div>
    """));

app.MapGet("/settings/", () => Page("Settings", $$"""
    <p class="small">ADMINISTRATION / SETTINGS</p><h1>Communication settings</h1>
    <div class="panel"><p>Contact email shown to training coordinators:</p>
    <form method="post" action="/settings/">
       <input style="width:300px" type="email" name="email" required value="{{E(contactEmail)}}">
       <button type="submit">Save</button>
    </form></div>
    """));

app.MapPost("/settings/", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    if (!email.Contains('@') || email.Length > 120) return Results.BadRequest("Invalid contact email.");
    contactEmail = email;
    return Results.Redirect("/settings/");
});

app.Run();

record Course(int Id, string Slug, string Title, string Category, int Hours, string Summary, int Capacity);
record Enrollment(Guid Id, int CourseId, string FullName, string Email,
    DateTimeOffset EnrolledAt, DateTimeOffset? CompletedAt, Guid? CertificateId);
record CapacityUpdate(int Capacity);
