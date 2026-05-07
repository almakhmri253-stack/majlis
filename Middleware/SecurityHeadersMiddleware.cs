namespace MajlisManagement.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"]  = "nosniff";
        h["X-Frame-Options"]         = "DENY";
        h["X-XSS-Protection"]        = "1; mode=block";
        h["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        h["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
        h["X-Permitted-Cross-Domain-Policies"] = "none";
        await _next(context);
    }
}
