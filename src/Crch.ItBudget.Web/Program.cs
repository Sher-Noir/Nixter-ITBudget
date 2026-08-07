using Crch.ItBudget.ImportExport.Fy2027;
using Crch.ItBudget.Infrastructure.Importing;
using Crch.ItBudget.Infrastructure.Persistence;
using Crch.ItBudget.Infrastructure.Persistence.Seeding;
using Crch.ItBudget.Infrastructure.Security;
using Crch.ItBudget.Web.Security;
using LedgerForge.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection(BrandingOptions.SectionName));
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.AddScoped<IAuthorizationHandler, ApplicationRoleAuthorizationHandler>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var configuredMaxImportFileSize = builder.Configuration.GetValue<long?>("Imports:MaxFileSizeBytes");
var maxImportFileSize = configuredMaxImportFileSize is > 0
    ? configuredMaxImportFileSize.Value
    : 25L * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxImportFileSize);

var connectionString = builder.Configuration.GetConnectionString("LedgerForge")
    ?? throw new InvalidOperationException("Connection string 'LedgerForge' is required.");
builder.Services.AddDbContext<ItBudgetDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<Fy2027WorkbookReader>();
builder.Services.AddScoped<Fy2027ImportPreviewService>();
builder.Services.AddScoped<ManagedLookupInitializer>();
builder.Services.AddScoped<SecurityAdministrationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/Home/AccessDenied", "?code={0}");
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
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
