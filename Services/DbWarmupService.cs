using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;
using MajlisManagement.Controllers;
using MajlisManagement.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MajlisManagement.Services;

public class DbWarmupService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DbWarmupService> _log;

    public DbWarmupService(
        IDbContextFactory<AppDbContext> factory,
        IMemoryCache cache,
        ILogger<DbWarmupService> log)
    {
        _factory = factory;
        _cache   = cache;
        _log     = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // انتظر 5 ثوانٍ فقط بعد بدء التطبيق
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshCacheAsync(ct);
                _log.LogInformation("DbWarmup: cache refreshed at {Time}", DateTime.Now);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning("DbWarmup failed: {Msg}", ex.Message);
            }

            // تجديد كل 9 دقائق (قبل انتهاء TTL البالغ 10 دقائق)
            await Task.Delay(TimeSpan.FromMinutes(9), ct);
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct)
    {
        var now        = DateTime.Now;
        var today      = DateTime.Today;
        var startMonth = new DateTime(now.Year, now.Month, 1);
        var nextWeek   = today.AddDays(7);

        // ── فرع 1: إحصائيات الحجوزات + المالية (context مستقل) ─────────────
        var statsTask = BranchAsync(async ctx =>
        {
            var bk = await ctx.Bookings
                .GroupBy(_ => true)
                .Select(g => new
                {
                    Total     = g.Count(),
                    Pending   = g.Count(b => b.Status == BookingStatus.Pending),
                    Confirmed = g.Count(b => b.Status == BookingStatus.Confirmed),
                    Completed = g.Count(b => b.Status == BookingStatus.Completed),
                    Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled),
                    ThisMonth = g.Count(b => b.CreatedAt >= startMonth)
                })
                .FirstOrDefaultAsync(ct);

            // نفس الـ context لكن بشكل تسلسلي (sequential) — آمن تماماً
            var py = await ctx.Payments
                .GroupBy(_ => true)
                .Select(g => new
                {
                    Revenue    = g.Sum(p => (decimal?)p.PaidAmount) ?? 0,
                    PendingPay = g.Sum(p => (decimal?)(p.TotalAmount - p.PaidAmount)) ?? 0
                })
                .FirstOrDefaultAsync(ct);

            return (bk, py);
        }, ct);

        // ── فرع 2: متأخرات + شكاوى + أعضاء (context مستقل) ─────────────────
        var countsTask = BranchAsync(async ctx =>
        {
            var overdue = await ctx.Payments.CountAsync(
                p => p.Status != PaymentStatus.Paid &&
                     p.Booking.Status == BookingStatus.Confirmed, ct);

            var cp = await ctx.Complaints
                .GroupBy(_ => true)
                .Select(g => new
                {
                    New   = g.Count(c => c.Status == ComplaintStatus.New),
                    Total = g.Count()
                })
                .FirstOrDefaultAsync(ct);

            var mb = await ctx.Members
                .GroupBy(_ => true)
                .Select(g => new
                {
                    Total  = g.Count(),
                    Active = g.Count(m => m.Status == MemberStatus.Active)
                })
                .FirstOrDefaultAsync(ct);

            return (overdue, cp, mb);
        }, ct);

        // ── فرع 3: حجوزات قادمة (context مستقل) ─────────────────────────────
        var upcomingTask = BranchAsync(ctx => ctx.Bookings
            .Where(b => b.StartDate >= today && b.StartDate <= nextWeek
                     && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartDate)
            .Select(b => new {
                b.Id, b.GuestName, b.PhoneNumber, b.StartDate, b.EndDate,
                Type   = b.Type.ToString(),
                Status = b.Status.ToString(),
                b.Cost
            })
            .ToListAsync(ct), ct);

        // ── فرع 4: أحداث + لوحة الإعلانات + شكاوى حديثة (context مستقل) ────
        var displayTask = BranchAsync(async ctx =>
        {
            var events = await ctx.Events
                .OrderByDescending(e => e.CreatedAt).Take(12)
                .Select(e => new {
                    e.Id, e.Title, e.Description,
                    MediaUrl  = e.MediaPath,
                    e.MediaType, e.CreatedAt,
                    CreatedBy = e.CreatedBy != null ? e.CreatedBy.FullName : null,
                    Media = e.Media.OrderBy(m => m.SortOrder)
                                   .Select(m => new { MediaUrl = m.MediaPath, m.MediaType })
                                   .ToList()
                })
                .ToListAsync(ct);

            var board = await ctx.Complaints
                .Where(c => c.IsPublic && c.Status == ComplaintStatus.Resolved)
                .OrderByDescending(c => c.CreatedAt).Take(10)
                .Select(c => new {
                    c.Id, c.Title, c.Content, c.IsAnonymous,
                    Type       = c.Type.ToString(),
                    c.AdminResponse, c.CreatedAt,
                    SenderName = c.User != null ? c.User.FullName : null
                })
                .ToListAsync(ct);

            var recent = await ctx.Complaints
                .OrderByDescending(c => c.CreatedAt).Take(5)
                .Select(c => new {
                    c.Id, c.Title, c.IsAnonymous,
                    Type       = c.Type.ToString(),
                    Status     = c.Status.ToString(),
                    c.CreatedAt,
                    SenderName = c.User != null ? c.User.FullName : null
                })
                .ToListAsync(ct);

            return (events, board, recent);
        }, ct);

        await Task.WhenAll(statsTask, countsTask, upcomingTask, displayTask);

        var (bkStats, pyStats)               = statsTask.Result;
        var (overdueCount, cpStats, mbStats)  = countsTask.Result;
        var (eventsList, boardList, recentList) = displayTask.Result;

        var result = new
        {
            stats = new
            {
                ThisMonthBookings = bkStats?.ThisMonth  ?? 0,
                PendingBookings   = bkStats?.Pending    ?? 0,
                ConfirmedBookings = bkStats?.Confirmed  ?? 0,
                CompletedBookings = bkStats?.Completed  ?? 0,
                CancelledBookings = bkStats?.Cancelled  ?? 0,
                TotalBookings     = bkStats?.Total      ?? 0,
                TotalRevenue      = pyStats?.Revenue    ?? 0,
                PendingPayments   = pyStats?.PendingPay ?? 0,
                OverdueCount      = overdueCount,
                NewComplaints     = cpStats?.New        ?? 0,
                TotalComplaints   = cpStats?.Total      ?? 0,
                TotalMembers      = mbStats?.Total      ?? 0,
                ActiveMembers     = mbStats?.Active     ?? 0,
            },
            upcoming         = upcomingTask.Result,
            events           = eventsList,
            publicBoard      = boardList,
            recentComplaints = recentList,
        };

        _cache.Set(DashboardController.CacheKey, (object)result, TimeSpan.FromMinutes(10));
    }

    private async Task<T> BranchAsync<T>(Func<AppDbContext, Task<T>> work, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await work(ctx);
    }
}
