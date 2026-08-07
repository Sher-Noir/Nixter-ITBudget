using Crch.ItBudget.ImportExport.Fy2027;
using Crch.ItBudget.Infrastructure.Importing;
using Crch.ItBudget.Infrastructure.Persistence;
using Crch.ItBudget.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var connectionString = builder.Configuration.GetConnectionString("ItBudget")
    ?? throw new InvalidOperationException("Connection string 'ItBudget' is required.");
builder.Services.AddDbContext<ItBudgetDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<Fy2027WorkbookReader>();
builder.Services.AddScoped<Fy2027ImportPreviewService>();
builder.Services.AddScoped<ManagedLookupInitializer>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
