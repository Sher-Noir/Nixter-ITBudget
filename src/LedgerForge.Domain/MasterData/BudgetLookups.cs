namespace LedgerForge.Domain.MasterData;

public sealed class BudgetSection : ManagedLookupEntity
{
    private BudgetSection() { }
    public BudgetSection(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class FinanceType : ManagedLookupEntity
{
    private FinanceType() { }
    public FinanceType(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class FinanceCategory : ManagedLookupEntity
{
    private FinanceCategory() { }
    public FinanceCategory(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class FinanceAccount : ManagedLookupEntity
{
    private FinanceAccount() { }
    public FinanceAccount(string code, string name, Guid? financeCategoryId = null, int sortOrder = 0) : base(code, name, sortOrder)
    {
        FinanceCategoryId = financeCategoryId;
    }
    public Guid? FinanceCategoryId { get; private set; }
}

public sealed class InternalCategory : ManagedLookupEntity
{
    private InternalCategory() { }
    public InternalCategory(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class Department : ManagedLookupEntity
{
    private Department() { }
    public Department(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class Location : ManagedLookupEntity
{
    private Location() { }
    public Location(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class NeedLevel : ManagedLookupEntity
{
    private NeedLevel() { }
    public NeedLevel(string code, string name, int numericValue, int sortOrder = 0) : base(code, name, sortOrder)
    {
        SetNumericValue(numericValue);
    }
    public int NumericValue { get; private set; }

    public void SetNumericValue(int numericValue)
    {
        if (numericValue < 0) throw new ArgumentOutOfRangeException(nameof(numericValue));
        NumericValue = numericValue;
    }
}

public sealed class PriorityLookup : ManagedLookupEntity
{
    private PriorityLookup() { }
    public PriorityLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class Frequency : ManagedLookupEntity
{
    private Frequency() { }
    public Frequency(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class PurchaseTypeLookup : ManagedLookupEntity
{
    private PurchaseTypeLookup() { }
    public PurchaseTypeLookup(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}

public sealed class UnitOfMeasure : ManagedLookupEntity
{
    private UnitOfMeasure() { }
    public UnitOfMeasure(string code, string name, int sortOrder = 0) : base(code, name, sortOrder) { }
}
