using LedgerForge.ImportExport.Spreadsheets;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Importing;
using LedgerForge.Infrastructure.MasterData;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Persistence.Seeding;
using LedgerForge.Infrastructure.Security;
using LedgerForge.Web.Configuration;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection(BrandingOptions.SectionName));
builder.Services.Configure<LegacyImportOptions>(builder.Configuration.GetSection(LegacyImportOptions.SectionName));
builder.Services.AddSingleton<OrganizationSettingsStore>();
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.AddScoped<IAuthorizationHandler, ApplicationRoleAuthorizationHandler>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var configuredMaxImportFileSize = builder.Configuration.GetValue<long?>("Imports:MaxFileSizeBytes");
var maxImportFileSize = configuredMaxImportFileSize is > 0 ? configuredMaxImportFileSize.Value : 25L * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxImportFileSize);

var connectionString = builder.Configuration.GetConnectionString("LedgerForge")
    ?? throw new InvalidOperationException("Connection string 'LedgerForge' is required.");
builder.Services.AddDbContext<LedgerForgeDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<LegacyBudgetWorkbookReader>();
builder.Services.AddScoped<LegacyBudgetImportPreviewService>();
builder.Services.AddScoped<ImportReviewService>();
builder.Services.AddScoped<LegacyBudgetImportCommitService>();
builder.Services.AddScoped<ManagedLookupInitializer>();
builder.Services.AddScoped<LookupAdministrationService>();
builder.Services.AddScoped<FinanceAdministrationService>();
builder.Services.AddScoped<SecurityAdministrationService>();
builder.Services.AddScoped<FiscalYearAdministrationService>();
builder.Services.AddScoped<BudgetPlanningService>();
builder.Services.AddScoped<ActualLedgerService>();

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
