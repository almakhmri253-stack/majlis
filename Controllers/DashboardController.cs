using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MajlisManagement.Data;
using MajlisManagement.Middleware;
using MajlisManagement.Models;

namespace MajlisManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMemoryCache _cache;
    internal const string CacheKey = "dashboard:full";

    public DashboardController(AppDbContext db, IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache)
    {
        _db = db;
        _dbFactory = dbFactory;
        _cache = cache;
    }

    [HttpGet("stats")]
    [RequirePermission("ViewDashboard")]
    public async Task<IActionResult> GetStats()
    {
        var now          = DateTime.Now;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfYear  = new DateTime(now.Year, 1, 1);

        var bk = await _db.Bookings
            .GroupBy(_ => true)
            .Select(g => new
            {
                Total     = g.Count(),
                Pending   = g.Count(b => b.Status == BookingStatus.Pending),
                Confirmed = g.Count(b => b.Status == BookingStatus.Confirmed),
                Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled),
                Completed = g.Count(b => b.Status == BookingStatus.Completed),
                ThisMonth = g.Count(b => b.CreatedAt >= startOfMonth),
                MonthWed  = g.Count(b => b.CreatedAt >= startOfMonth && b.Type == BookingType.Wedding),
                MonthCond = g.Count(b => b.CreatedAt >= startOfMonth && b.Type == BookingType.Condolence),
                MonthGen  = g.Count(b => b.CreatedAt >= startOfMonth && b.Type == BookingType.General),
                YearWed   = g.Count(b => b.CreatedAt >= startOfYear  && b.Type == BookingType.Wedding),
                YearCond  = g.Count(b => b.CreatedAt >= startOfYear  && b.Type == BookingType.Condolence)
            })
            .FirstOrDefaultAsync();

        var revenue      = await _db.Payments.SumAsync(p => (decimal?)p.PaidAmount) ?? 0;
        var pendingPay   = await _db.Payments.SumAsync(p => (decimal?)(p.TotalAmount - p.PaidAmount)) ?? 0;
        var overdueCount = await _db.Payments.CountAsync(p =>
            p.Status != PaymentStatus.Paid && p.Booking.Status == BookingStatus.Confirmed);
        var newComplaints   = await _db.Complaints.CountAsync(c => c.Status == ComplaintStatus.New);
        var totalComplaints = await _db.Complaints.CountAsync();
        var totalMembers    = await _db.Members.CountAsync();
        var activeMembers   = await _db.Members.CountAsync(m => m.Status == MemberStatus.Active);
        var memberTotalPaid = await _db.Members.SumAsync(m => (decimal?)m.PaidAmount) ?? 0;
        var memberTotalOverdue = await _db.Members
            .Where(m => m.TotalPaymentDue > m.PaidAmount)
            .SumAsync(m => (decimal?)(m.TotalPaymentDue - m.PaidAmount)) ?? 0;

        return Ok(new
        {
            TotalBookings        = bk?.Total      ?? 0,
            PendingBookings      = bk?.Pending    ?? 0,
            ConfirmedBookings    = bk?.Confirmed  ?? 0,
            CancelledBookings    = bk?.Cancelled  ?? 0,
            CompletedBookings    = bk?.Completed  ?? 0,
            ThisMonthBookings    = bk?.ThisMonth  ?? 0,
            ThisMonthWeddings    = bk?.MonthWed   ?? 0,
            ThisMonthCondolences = bk?.MonthCond  ?? 0,
            ThisMonthGeneral     = bk?.MonthGen   ?? 0,
            ThisYearWeddings     = bk?.YearWed    ?? 0,
            ThisYearCondolences  = bk?.YearCond   ?? 0,
            TotalRevenue         = revenue,
            PendingPayments      = pendingPay,
            OverdueCount         = overdueCount,
            NewComplaints        = newComplaints,
            TotalComplaints      = totalComplaints,
            TotalMembers         = totalMembers,
            ActiveMembers        = activeMembers,
            MemberTotalPaid      = memberTotalPaid,
            MemberTotalOverdue   = memberTotalOverdue
        });
    }

    /// <summary>جميع بيانات الرئيسية في طلب واحد — مع كاش 60 ث</summary>
    [HttpGet("full")]
    [RequirePermission("ViewDashboard")]
    public async Task<IActionResult> Full()
    {
        if (_cache.TryGetValue(CacheKey, out object? cached))
            return Ok(cached);

        var now        = DateTime.Now;
        var today      = DateTime.Today;
        var startMonth = new DateTime(now.Year, now.Month, 1);
        var nextWeek   = today.AddDays(7);

        // ── فرع 1: إحصائيات الحجوزات + المالية ──────────────────────────────
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
                .FirstOrDefaultAsync();

            var py = await ctx.Payments
                .GroupBy(_ => true)
                .Select(g => new
                {
                    Revenue    = g.Sum(p => (decimal?)p.PaidAmount) ?? 0,
                    PendingPay = g.Sum(p => (decimal?)(p.TotalAmount - p.PaidAmount)) ?? 0
                })
                .FirstOrDefaultAsync();

            return (bk, py);
        });

        // ── فرع 2: متأخرات + شكاوى + أعضاء ────────────────────────────────
        var countsTask = BranchAsync(async ctx =>
        {
            var overdue = await ctx.Payments.CountAsync(p =>
                p.Status != PaymentStatus.Paid && p.Booking.Status == BookingStatus.Confirmed);

            var cp = await ctx.Complaints
                .GroupBy(_ => true)
                .Select(g => new { New = g.Count(c => c.Status == ComplaintStatus.New), Total = g.Count() })
                .FirstOrDefaultAsync();

            var mb = await ctx.Members
                .GroupBy(_ => true)
                .Select(g => new { Total = g.Count(), Active = g.Count(m => m.Status == MemberStatus.Active) })
                .FirstOrDefaultAsync();

            var memberTotalPaid = await ctx.Members.SumAsync(m => (decimal?)m.PaidAmount) ?? 0;
            var memberTotalOverdue = await ctx.Members
                .Where(m => m.TotalPaymentDue > m.PaidAmount)
                .SumAsync(m => (decimal?)(m.TotalPaymentDue - m.PaidAmount)) ?? 0;

            return (overdue, cp, mb, memberTotalPaid, memberTotalOverdue);
        });

        // ── فرع 3: حجوزات قادمة ────────────────────────────────────────────
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
            .ToListAsync());

        // ── فرع 4: أحداث + لوحة إعلانات + شكاوى حديثة ──────────────────
        var displayTask = BranchAsync(async ctx =>
        {
            var events = await ctx.Events
                .OrderByDescending(e => e.CreatedAt)
                .Take(12)
                .Select(e => new {
                    e.Id, e.Title, e.Description,
                    MediaUrl  = e.MediaPath,
                    e.MediaType, e.CreatedAt,
                    CreatedBy = e.CreatedBy != null ? e.CreatedBy.FullName : null,
                    Media = e.Media.OrderBy(m => m.SortOrder)
                                   .Select(m => new { MediaUrl = m.MediaPath, m.MediaType })
                                   .ToList()
                })
                .ToListAsync();

            var board = await ctx.Complaints
                .Where(c => c.IsPublic && c.Status == ComplaintStatus.Resolved)
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .Select(c => new {
                    c.Id, c.Title, c.Content, c.IsAnonymous,
                    Type       = c.Type.ToString(),
                    c.AdminResponse, c.CreatedAt,
                    SenderName = c.User != null ? c.User.FullName : null
                })
                .ToListAsync();

            var recent = await ctx.Complaints
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new {
                    c.Id, c.Title, c.IsAnonymous,
                    Type       = c.Type.ToString(),
                    Status     = c.Status.ToString(),
                    c.CreatedAt,
                    SenderName = c.User != null ? c.User.FullName : null
                })
                .ToListAsync();

            return (events, board, recent);
        });

        await Task.WhenAll(statsTask, countsTask, upcomingTask, displayTask);

        var (bkStats, pyStats)       = statsTask.Result;
        var (overdueCount, cpStats, mbStats, memberTotalPaid, memberTotalOverdue) = countsTask.Result;
        var (eventsList, boardList, recentList) = displayTask.Result;

        var stats = new
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
            TotalMembers         = mbStats?.Total      ?? 0,
            ActiveMembers        = mbStats?.Active     ?? 0,
            MemberTotalPaid      = memberTotalPaid,
            MemberTotalOverdue   = memberTotalOverdue
        };

        var result = new
        {
            stats,
            upcoming         = upcomingTask.Result,
            events           = eventsList,
            publicBoard      = boardList,
            recentComplaints = recentList
        };

        _cache.Set(CacheKey, result, TimeSpan.FromMinutes(10));
        return Ok(result);
    }

    // Helper: creates a fresh DbContext, runs the async delegate, then disposes
    private async Task<T> BranchAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var ctx = await _dbFactory.CreateDbContextAsync();
        return await work(ctx);
    }

    [HttpGet("report/bookings")]
    [RequirePermission("ViewReports")]
    public async Task<IActionResult> BookingsReport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null)
    {
        from ??= new DateTime(DateTime.Now.Year, 1, 1);
        to   ??= DateTime.Now;

        var bookings = await _db.Bookings
            .Where(b => b.CreatedAt >= from && b.CreatedAt <= to)
            .GroupBy(b => new { b.Type, b.Status })
            .Select(g => new
            {
                Type      = g.Key.Type.ToString(),
                Status    = g.Key.Status.ToString(),
                Count     = g.Count(),
                TotalCost = g.Sum(b => b.Cost)
            })
            .ToListAsync();

        return Ok(new { from, to, bookings });
    }

    [HttpGet("upcoming")]
    [RequirePermission("ViewDashboard")]
    public async Task<IActionResult> Upcoming()
    {
        var now      = DateTime.Today;
        var nextWeek = now.AddDays(7);

        var upcoming = await _db.Bookings
            .Where(b => b.StartDate >= now && b.StartDate <= nextWeek
                     && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartDate)
            .Select(b => new
            {
                b.Id, b.GuestName, b.PhoneNumber,
                b.StartDate, b.EndDate,
                Type   = b.Type.ToString(),
                Status = b.Status.ToString(),
                b.Cost
            })
            .ToListAsync();

        return Ok(upcoming);
    }

    [HttpGet("report/financial")]
    [RequirePermission("ViewReports")]
    public async Task<IActionResult> FinancialReport([FromQuery] int year = 0)
    {
        if (year == 0) year = DateTime.Now.Year;

        var monthly = await _db.Payments
            .Include(p => p.Booking)
            .Where(p => p.Booking.StartDate.Year == year)
            .GroupBy(p => p.Booking.StartDate.Month)
            .Select(g => new
            {
                Month       = g.Key,
                TotalAmount = g.Sum(p => p.TotalAmount),
                PaidAmount  = g.Sum(p => p.PaidAmount),
                Remaining   = g.Sum(p => p.TotalAmount - p.PaidAmount),
                Count       = g.Count()
            })
            .OrderBy(x => x.Month)
            .ToListAsync();

        var memberRevenue = await _db.MemberPayments
            .Where(p => p.IsPaid && p.Year == year)
            .SumAsync(p => p.Amount);

        return Ok(new { year, monthly, memberRevenue });
    }
}
