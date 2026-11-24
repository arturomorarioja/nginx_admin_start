using System.Net;

namespace Learning.Web;

public static class Ui
{
    public static string E(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "");

    public static string Page(string title, string body) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{{E(title)}} — Cedar Learning</title>
            <link rel="stylesheet" href="/assets/site.css">
            <style>
                .inner{max-width:1040px;margin:0 auto;padding:44px 32px 80px}
                .inner h1{font-size:38px;letter-spacing:-.025em}.inner h2{font-size:23px}
                .cards{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}
                .panel{border:1px solid #dce3e6;border-radius:7px;padding:22px;background:#fff}
                .panel p{color:#617181}.meta{font-size:12px;text-transform:uppercase;letter-spacing:.08em;color:#2c6558}
                .field{display:block;margin:12px 0}.field input{display:block;width:100%;max-width:450px;margin-top:6px;padding:11px;border:1px solid #bdcbd0;border-radius:5px}
                button{border:0;cursor:pointer}.muted{color:#617181}.notice{background:#e9f1ed;padding:15px;border-radius:5px}
                .table{border-collapse:collapse;width:100%}.table td,.table th{padding:12px;border-bottom:1px solid #dce3e6;text-align:left}
                .inline-form{display:inline}.inline-form button{font-size:13px;padding:8px 12px}
                @media(max-width:650px){.cards{grid-template-columns:1fr}.inner{padding:26px 20px} }
            </style>
        </head>
        <body>
            <header class="site-header"><div class="wrap header-inner">
              <a class="wordmark" href="/">CEDAR <span>LEARNING</span></a>
              <nav><a href="/courses/">Courses</a><a href="/account/">My learning</a><a href="/downloads/course-guide.txt">Course guide</a></nav>
            </div></header>
            <main class="inner">{{body}}</main>
            <footer><div class="wrap">© 2026 Cedar Learning</div></footer>
        </body></html>
        """;
}
