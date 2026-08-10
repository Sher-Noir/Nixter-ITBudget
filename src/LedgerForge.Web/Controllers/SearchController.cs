using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Web.Models.Search;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("search")]
public sealed class SearchController(
    LedgerForgeDbContext dbContext,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();
        if (query.Length < 2)
            return View(new SearchViewModel(query, []));
        if (query.Length > 100)
            query = query[..100];

        var results = new List<SearchResultViewModel>(50);
        var canViewBudget = await CanViewAsync(LedgerForgeModule.Budget);
        var canViewVendors = await CanViewAsync(LedgerForgeModule.Vendors);
        var canViewProcurement = await CanViewAsync(LedgerForgeModule.Procurement);
        var canViewContracts = await CanViewAsync(LedgerForgeModule.Contracts);

        if (canViewBudget)
        {
            var budgetItems = await dbContext.BudgetItems.AsNoTracking()
                .Where(x => x.ItemNumber.Contains(query) || x.Description.Contains(query) || (x.ReasonPurpose != null && x.ReasonPurpose.Contains(query)))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(10)
                .Select(x => new { x.Id, x.ItemNumber, x.Description, x.Status })
                .ToListAsync(cancellationToken);
            results.AddRange(budgetItems.Select(x => new SearchResultViewModel(
                "Budget item", x.ItemNumber, x.Description, x.Status.ToString(), "/budget/items/" + x.Id)));
        }

        if (canViewVendors)
        {
            var vendors = await dbContext.Vendors.AsNoTracking()
                .Where(x => x.Code.Contains(query) || x.Name.Contains(query) || (x.ContactName != null && x.ContactName.Contains(query)))
                .OrderBy(x => x.Name)
                .Take(10)
                .Select(x => new { x.Id, x.Code, x.Name, x.IsActive })
                .ToListAsync(cancellationToken);
            results.AddRange(vendors.Select(x => new SearchResultViewModel(
                "Vendor", x.Code, x.Name, x.IsActive ? "Active" : "Inactive", "/vendors")));
        }

        if (canViewProcurement)
        {
            var purchaseOrders = await dbContext.PurchaseOrders.AsNoTracking()
                .Where(x => x.Number.Contains(query) || x.Description.Contains(query))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(10)
                .Select(x => new { x.Id, x.Number, x.Description, x.State })
                .ToListAsync(cancellationToken);
            results.AddRange(purchaseOrders.Select(x => new SearchResultViewModel(
                "Purchase order", x.Number, x.Description, x.State.ToString(), "/purchase-orders/" + x.Id)));

            var invoices = await dbContext.Invoices.AsNoTracking()
                .Where(x => x.InvoiceNumber.Contains(query) || x.Description.Contains(query))
                .OrderByDescending(x => x.InvoiceDate)
                .Take(10)
                .Select(x => new { x.Id, x.InvoiceNumber, x.Description, x.State })
                .ToListAsync(cancellationToken);
            results.AddRange(invoices.Select(x => new SearchResultViewModel(
                "Invoice", x.InvoiceNumber, x.Description, x.State.ToString(), "/invoices/" + x.Id)));
        }

        if (canViewContracts)
        {
            var contracts = await dbContext.Contracts.AsNoTracking()
                .Where(x => x.ContractNumber.Contains(query) || x.Name.Contains(query) || (x.Description != null && x.Description.Contains(query)))
                .OrderBy(x => x.EndDate)
                .Take(10)
                .Select(x => new { x.Id, x.ContractNumber, x.Name, x.State })
                .ToListAsync(cancellationToken);
            results.AddRange(contracts.Select(x => new SearchResultViewModel(
                "Contract", x.ContractNumber, x.Name, x.State.ToString(), "/contracts/" + x.Id)));
        }

        return View(new SearchViewModel(
            query,
            results.OrderBy(x => x.Type).ThenBy(x => x.Reference).ToArray()));
    }

    private async Task<bool> CanViewAsync(LedgerForgeModule module)
        => (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ModulePolicy(module, ModuleAccessLevel.View))).Succeeded;
}