using LedgerForge.Application.Abstractions;
using LedgerForge.ImportExport.Spreadsheets;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Approvals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Dashboard;
using LedgerForge.Infrastructure.Importing;
using LedgerForge.Infrastructure.MasterData;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Persistence.Auditing;
using LedgerForge.Infrastructure.Persistence.Seeding;
using LedgerForge.Infrastructure.Procurement;
using LedgerForge.Infrastructure.Reporting;
using LedgerForge.Infrastructure.Security;
using LedgerForge.Web.Auditing;
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
builder.Services.AddHttpContextAccessor();

var configuredMaxImportFileSize = builder.Configuration.GetValue<long?>("Imports:MaxFileSizeBytes");
var maxImportFileSize = configuredMaxImportFileSize is > 0 ? configuredMaxImportFileSize.Value : 25L * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxImportFileSize);

var connectionString = builder.Configuration.GetConnectionString("LedgerForge")
    ?? throw new InvalidOperationException("Connection string 'LedgerForge' is required.");
var httpsRedirectionEnabled = builder.Configuration.GetValue("Deployment:HttpsRedirection", true);
builder.Services.AddScoped<IAuditRequestContext, HttpAuditRequestContext>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<LedgerForgeDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
});
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
builder.Services.AddScoped<BudgetAmendmentService>();
builder.Services.AddScoped<ForecastService>();
builder.Services.AddScoped<ActualLedgerService>();
builder.Services.AddScoped<ProcurementService>();
builder.Services.AddScoped<InvoiceQueryService>();
builder.Services.AddScoped<InvoiceWorkflowService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<OutstandingCommitmentService>();
builder.Services.AddScoped<ApprovalQueueService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<RenewalCalendarService>();
builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (httpsRedirectionEnabled)
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

app.MapGet("/health", async (LedgerForgeDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        return Results.Ok(new { status = "ready" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
