using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using dailyMonthly.Services;
namespace dailyMonthly.Api;
public static class ApiEndpoints
{ public static void Map(WebApplication app)
{ var api = app.MapGroup("/api").RequireAuthorization();
api.MapPost("/search", async
Task<Results<Ok<object>, ProblemHttpResult,
StatusCodeHttpResult>> ([FromBody] SearchRequest body, HttpContext http,
RateLimitService svc) =>
{ try
    {
        var result = await svc.TrySearchAsync(http.User, body.term);
        // rate-limit headers
        http.Response.Headers.Append("X-RateLimit-Limit-Day",
        RateLimitService.DailyLimit.ToString());
        http.Response.Headers.Append("X-RateLimit-Remaining-Day",
        result.Usage.DayRemaining.ToString());
        http.Response.Headers.Append("X-RateLimit-Limit-Month",
        RateLimitService.MonthlyLimit.ToString());
        http.Response.Headers.Append("X-RateLimit-Remaining-Month",
        result.Usage.MonthRemaining.ToString());
        return TypedResults.Ok<object>(new
        {
            items = result.Items,
            usage = new
            {
                dayRemaining = result.Usage.DayRemaining,
                monthRemaining = result.Usage.MonthRemaining
            }
        });
    }
    catch (RateLimitService.LimitExceededException ex) {
        return TypedResults.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status429TooManyRequests,
            extensions: new Dictionary<string, object?> {
                ["code"] = ex.Code }); } });
        api.MapGet("/usage", async (HttpContext http,
        RateLimitService svc) =>
        { var usage = await svc.GetUsageAsync(http.User);
            return Results.Ok(new {
                dayUsed = usage.DayUsed,
                dayRemaining = usage.DayRemaining,
                monthUsed = usage.MonthUsed,
                monthRemaining = usage.MonthRemaining,
                dayResetAt = usage.DayResetAt,
                monthResetAt = usage.MonthResetAt }); }); }
    public record SearchRequest(string term); }