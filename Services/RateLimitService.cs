using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using dailyMonthly.Data;
namespace dailyMonthly.Services;

public class RateLimitService
{
    public const int DailyLimit = 5;
    public const int MonthlyLimit = 20;
    private readonly AppDbContext _db;
    private readonly TimeZoneInfo _tz;
    public RateLimitService(AppDbContext db)
    {
        _db = db;
        _tz = GetIstanbulTz();
    }
    // ---- Public DTOs ----
    public record UsageDto(int DayUsed, int DayRemaining, int MonthUsed, int MonthRemaining, DateTime DayResetAt, DateTime MonthResetAt);
    public record SearchResultDto(string[] Items, UsageDto Usage);
    public class LimitExceededException : Exception
    {
        public string Code { get; }
        public UsageDto Usage { get; }
        public LimitExceededException(string code, string message, UsageDto usage) : base(message) { Code = code; Usage = usage; }
    }
    // ---- Public API ---- 
    public async Task<UsageDto> GetUsageAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        var nowUtc = DateTime.UtcNow;
        var (dStart, dEnd, mStart, mNext) = GetWindows(nowUtc);
        var dailyUsed = await _db.QueryLogs.CountAsync(x =>
        x.UserId == userId && x.CreatedAtUtc >= dStart && x.CreatedAtUtc < dEnd, ct);
        var monthUsed = await _db.QueryLogs.CountAsync(x => x.UserId == userId && x.CreatedAtUtc >= mStart && x.CreatedAtUtc < mNext, ct);
        return new UsageDto(dailyUsed, Math.Max(0, DailyLimit - dailyUsed), monthUsed, Math.Max(0, MonthlyLimit - monthUsed), dEnd, mNext);
    }
    public async Task<SearchResultDto> TrySearchAsync(ClaimsPrincipal user, string term, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        var nowUtc = DateTime.UtcNow;
        var (dStart, dEnd, mStart, mNext) = GetWindows(nowUtc);
        var dailyUsed = await _db.QueryLogs.CountAsync(x => x.UserId == userId && x.CreatedAtUtc >= dStart && x.CreatedAtUtc < dEnd, ct);
        if (dailyUsed >= DailyLimit) throw new LimitExceededException("DAILY_LIMIT_EXCEEDED", "Günlük limitiniz dolmuştur.", await GetUsageAsync(user, ct));
        var monthUsed = await _db.QueryLogs.CountAsync(x => x.UserId == userId && x.CreatedAtUtc >= mStart && x.CreatedAtUtc < mNext, ct);
        if (monthUsed >= MonthlyLimit) throw new LimitExceededException("MONTHLY_LIMIT_EXCEEDED", "Aylık limitiniz dolmuştur.", await GetUsageAsync(user, ct));
        _db.QueryLogs.Add(new QueryLog { UserId = userId, Term = term, CreatedAtUtc = nowUtc });
        await _db.SaveChangesAsync(ct);
        var usage = await GetUsageAsync(user, ct);
        var items = new[] { $"Anahtar kelime '{term}'" };
        // mock results for assignment
        return new SearchResultDto(items, usage);
    }
    // ---- Helpers ---- 
    private static string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Not authenticated");
    private static TimeZoneInfo GetIstanbulTz()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); } // Windows
    }
    private (DateTime dayStartUtc, DateTime dayEndUtc, DateTime monthStartUtc, DateTime nextMonthStartUtc) GetWindows(DateTime nowUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _tz);
        var todayStartLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified); var todayEndLocal = todayStartLocal.AddDays(1);
        var monthStartLocal = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextMonthStartLocal = monthStartLocal.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(todayStartLocal, _tz), TimeZoneInfo.ConvertTimeToUtc(todayEndLocal, _tz), TimeZoneInfo.ConvertTimeToUtc(monthStartLocal, _tz), TimeZoneInfo.ConvertTimeToUtc(nextMonthStartLocal, _tz));
    }
}