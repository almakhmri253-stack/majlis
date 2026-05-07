using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using MajlisManagement.Data;
using MajlisManagement.DTOs;
using MajlisManagement.Models;
using MajlisManagement.Services.Interfaces;

namespace MajlisManagement.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes    = 15;

    public AuthService(AppDbContext db, IConfiguration config, IMemoryCache cache)
    {
        _db     = db;
        _config = config;
        _cache  = cache;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقاً");

        if (await _db.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber))
            throw new InvalidOperationException("رقم الجوال مستخدم مسبقاً");

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "User"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // صلاحيات افتراضية للمستخدمين الجدد
        _db.UserPermissions.Add(new UserPermission
        {
            UserId         = user.Id,
            ViewDashboard  = true,
            ViewBookings   = true,
            CreateBookings = true,
        });
        await _db.SaveChangesAsync();

        return await BuildResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ipAddress = null)
    {
        var emailKey = $"lockout:email:{dto.Email.ToLower()}";
        var ipKey    = $"lockout:ip:{ipAddress}";

        int emailFails = 0, ipFails = 0;
        _cache.TryGetValue(emailKey, out emailFails);
        if (ipAddress != null) _cache.TryGetValue(ipKey, out ipFails);

        // فحص الإغلاق بالبريد
        if (emailFails >= MaxFailedAttempts)
            throw new InvalidOperationException($"تم إيقاف تسجيل الدخول مؤقتاً بسبب المحاولات المتكررة. حاول بعد {LockoutMinutes} دقيقة");

        // فحص الإغلاق بالـ IP
        if (ipAddress != null && ipFails >= MaxFailedAttempts * 3)
            throw new InvalidOperationException($"تم إيقاف تسجيل الدخول مؤقتاً من هذا الجهاز. حاول بعد {LockoutMinutes} دقيقة");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        bool failed = user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

        if (failed)
        {
            // زيادة عداد الفشل
            _cache.Set(emailKey, (emailFails) + 1, TimeSpan.FromMinutes(LockoutMinutes));
            if (ipAddress != null)
                _cache.Set(ipKey, (ipFails) + 1, TimeSpan.FromMinutes(LockoutMinutes));

            throw new InvalidOperationException("بيانات الدخول غير صحيحة");
        }

        if (!user!.IsActive)
            throw new InvalidOperationException("الحساب موقوف");

        // مسح عداد الفشل عند النجاح
        _cache.Remove(emailKey);
        _cache.Remove(ipKey);

        _db.LoginLogs.Add(new MajlisManagement.Models.UserLoginLog
        {
            UserId    = user.Id,
            LoginAt   = DateTime.UtcNow,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync();

        return await BuildResponse(user);
    }

    private async Task<AuthResponseDto> BuildResponse(User user)
    {
        PermissionsDto perms;
        if (user.Role == "Admin")
        {
            perms = PermissionsDto.AdminAll();
        }
        else
        {
            var p = await _db.UserPermissions.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (p == null)
            {
                p = new UserPermission { UserId = user.Id };
                _db.UserPermissions.Add(p);
                await _db.SaveChangesAsync();
            }
            perms = new PermissionsDto
            {
                ViewBookings      = p.ViewBookings,
                CreateBookings    = p.CreateBookings,
                ConfirmBookings   = p.ConfirmBookings,
                DeleteBookings    = p.DeleteBookings,
                ViewMembers       = p.ViewMembers,
                ManageMembers     = p.ManageMembers,
                ViewAllComplaints = p.ViewAllComplaints,
                RespondComplaints = p.RespondComplaints,
                ViewDashboard     = p.ViewDashboard,
                ViewReports       = p.ViewReports,
            };
        }
        return GenerateToken(user, perms);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("كلمة المرور الحالية غير صحيحة");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();
    }

    private AuthResponseDto GenerateToken(User user, PermissionsDto perms)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(
            int.TryParse(_config["Jwt:ExpireDays"], out var days) ? days : 7);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new AuthResponseDto
        {
            Token       = new JwtSecurityTokenHandler().WriteToken(token),
            FullName    = user.FullName,
            Email       = user.Email,
            Role        = user.Role,
            ExpiresAt   = expires,
            Permissions = perms
        };
    }
}
