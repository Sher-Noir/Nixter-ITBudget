namespace LedgerForge.Domain.MasterData;

public sealed class DocumentType : ManagedLookupEntity
{
    private DocumentType() { }
    public DocumentType(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class ApprovalStatusLookup : ManagedLookupEntity
{
    private ApprovalStatusLookup() { }
    public ApprovalStatusLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class TransactionTypeLookup : ManagedLookupEntity
{
    private TransactionTypeLookup() { }
    public TransactionTypeLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class PurchaseOrderStatusLookup : ManagedLookupEntity
{
    private PurchaseOrderStatusLookup() { }
    public PurchaseOrderStatusLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class InvoiceStatusLookup : ManagedLookupEntity
{
    private InvoiceStatusLookup() { }
    public InvoiceStatusLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class RenewalStatusLookup : ManagedLookupEntity
{
    private RenewalStatusLookup() { }
    public RenewalStatusLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class ContractStatusLookup : ManagedLookupEntity
{
    private ContractStatusLookup() { }
    public ContractStatusLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}
