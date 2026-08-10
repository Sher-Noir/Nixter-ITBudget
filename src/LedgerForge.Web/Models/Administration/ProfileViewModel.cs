using LedgerForge.Web.Security;

namespace LedgerForge.Web.Models.Administration;

public sealed record ProfileModuleAccessRow(LedgerForgeModule Module, ModuleAccessLevel Access);

public sealed record ProfileViewModel(
    string Identity,
    IReadOnlyList<string> ConfigurableRoles,
    IReadOnlyList<ProfileModuleAccessRow> ModuleAccess);
