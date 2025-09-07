using dailyMonthly.Components;
using dailyMonthly.Services;
using dailyMonthly.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using dailyMonthly.Data;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Services
// --------------------

// Blazor + Interactive Server Components
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddRazorPages(); // Identity UI requires Razor Pages

// EF Core (SQLite)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

// Identity (with built-in UI)
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultUI();

// Authentication & Authorization
// builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
//        .AddIdentityCookies();
builder.Services.AddAuthorization();

// Our custom services
builder.Services.AddScoped<RateLimitService>();

// No-op email sender for Identity
builder.Services.AddTransient<IEmailSender<ApplicationUser>, NoOpEmailSender>();

// HttpClient for APIs
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:5133/") });

var app = builder.Build();

// --------------------
// Middleware pipeline
// --------------------

// Exception handling & HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Auth middlewares
app.UseAuthentication();
app.UseAuthorization();

// Anti-forgery middleware (must be AFTER auth, BEFORE endpoints)
app.UseAntiforgery();

// Identity UI pages
app.MapRazorPages();

// Blazor components
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// Your minimal APIs
ApiEndpoints.Map(app);

app.Run();

/// <summary>
/// No-op email sender (logs instead of sending).
/// Replace with real email service later.
/// </summary>
public class NoOpEmailSender : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        Console.WriteLine($"[EmailStub] Confirmation link for {email}: {confirmationLink}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        Console.WriteLine($"[EmailStub] Password reset link for {email}: {resetLink}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        Console.WriteLine($"[EmailStub] Password reset code for {email}: {resetCode}");
        return Task.CompletedTask;
    }
}
