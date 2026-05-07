using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MajlisManagement.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=majlis_dev;Username=postgres;Password=postgres";

        if (connStr.StartsWith("postgres://") || connStr.StartsWith("postgresql://"))
        {
            var uri      = new Uri(connStr);
            var userInfo = uri.UserInfo.Split(':');
            var sb       = new System.Text.StringBuilder();
            sb.Append($"Host={uri.Host};");
            if (uri.Port > 0) sb.Append($"Port={uri.Port};");
            sb.Append($"Database={uri.AbsolutePath.TrimStart('/')};");
            sb.Append($"Username={Uri.UnescapeDataString(userInfo[0])};");
            if (userInfo.Length > 1) sb.Append($"Password={Uri.UnescapeDataString(userInfo[1])};");
            sb.Append("SSL Mode=Require;Trust Server Certificate=true;");
            connStr = sb.ToString();
        }

        optionsBuilder.UseNpgsql(connStr);
        return new AppDbContext(optionsBuilder.Options);
    }
}
