using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MajlisManagement.Data;
using MajlisManagement.Middleware;
using MajlisManagement.Services;
using MajlisManagement.Services.Interfaces;

// Npgsql legacy timestamp behaviour (DateTime instead of DateTimeOffset)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// رفع حد حجم الطلب على مستوى Kestrel (الافتراضي 30MB)
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = 500 * 1024 * 1024);

// رفع حدود نموذج multipart على مستوى التطبيق كله
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit    = 500L * 1024 * 1024; // 500 MB
    o.ValueLengthLimit            = int.MaxValue;
    o.MultipartHeadersLengthLimit = int.MaxValue;
});

// ── قاعدة البيانات ──────────────────────────────────────────────────────────
var rawConn = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("No database connection string configured");

// Convert postgres:// URI → Npgsql key=value format
static string ToNpgsqlConnStr(string s)
{
    if (!s.StartsWith("postgres://") && !s.StartsWith("postgresql://")) return s;
    var uri = new Uri(s);
    var userInfo = uri.UserInfo.Split(':');
    var sb = new System.Text.StringBuilder();
    sb.Append($"Host={uri.Host};");
    if (uri.Port > 0) sb.Append($"Port={uri.Port};");
    sb.Append($"Database={uri.AbsolutePath.TrimStart('/')};");
    sb.Append($"Username={Uri.UnescapeDataString(userInfo[0])};");
    if (userInfo.Length > 1) sb.Append($"Password={Uri.UnescapeDataString(userInfo[1])};");
    sb.Append("SSL Mode=Require;Trust Server Certificate=true;");
    return sb.ToString();
}

var connStr = ToNpgsqlConnStr(rawConn);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connStr));
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connStr));

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

// ── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    // حماية نقاط تسجيل الدخول والتسجيل: 10 طلبات/دقيقة لكل IP
    opt.AddSlidingWindowLimiter("auth", o =>
    {
        o.PermitLimit         = 10;
        o.Window              = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow   = 4;
        o.QueueLimit          = 0;
    });

    // حماية عامة لبقية الـ API: 120 طلب/دقيقة لكل IP
    opt.AddSlidingWindowLimiter("api", o =>
    {
        o.PermitLimit         = 120;
        o.Window              = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow   = 4;
        o.QueueLimit          = 0;
    });

    opt.RejectionStatusCode = 429;
    opt.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode  = 429;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"message\":\"تجاوزت الحد المسموح من الطلبات، حاول بعد قليل\",\"statusCode\":429}");
    };
});

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddHostedService<MajlisManagement.Services.DbWarmupService>();

// ── Controllers + JSON ───────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // إبقاء أسماء الـ Enums كنصوص بدلاً من أرقام
        opt.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        opt.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ── CORS (للواجهة الأمامية) ──────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "مجلس المخامرة الشرقي - API",
        Version = "v1",
        Description = "نظام إدارة مجلس المخامرة الشرقي"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل التوكن: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware (الترتيب مهم) ──────────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// الواجهة الأمامية أولاً — index.html بدون كاش دائماً
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.EndsWith(".html") || name == "sw.js")
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers["Pragma"]        = "no-cache";
            ctx.Context.Response.Headers["Expires"]       = "0";
        }
    }
});

// Swagger على مسار مختلف
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Majlis API v1");
    c.RoutePrefix = "swagger";
});
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── تطبيق الـ Migrations وإنشاء المدير الافتراضي ─────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Email == "admin@majlis.com"))
    {
        db.Users.Add(new MajlisManagement.Models.User
        {
            FullName     = "مدير النظام",
            Email        = "admin@majlis.com",
            PhoneNumber  = "0500000000",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role         = "Admin",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

app.Run();
