# Initial ERD

```mermaid
erDiagram
  FiscalYear ||--o{ BudgetVersion : has
  FiscalYear ||--o{ BudgetItem : contains
  BudgetVersion ||--o{ BudgetItem : versions
  BudgetItem ||--o{ BudgetItemAllocation : allocates
  BudgetItem ||--o{ ActualTransaction : receives
  Vendor ||--o{ BudgetItem : supplies
  Vendor ||--o{ PurchaseOrder : supplies
  PurchaseOrder ||--|{ PurchaseOrderLine : contains
  BudgetItem ||--o{ PurchaseOrderLine : funds
  PurchaseOrderLine ||--o{ InvoiceAllocation : matched_by
  Invoice ||--|{ InvoiceAllocation : splits
  BudgetItem ||--o{ InvoiceAllocation : charged_to
  Vendor ||--o{ Invoice : issues
  Vendor ||--o{ Contract : contracts
  Contract ||--o{ Renewal : renews
  BudgetItem ||--o{ Renewal : budgets
  FiscalYear ||--o{ BudgetAmendment : changes
  BudgetAmendment ||--|{ BudgetAmendmentLine : contains
  ApprovalRequest ||--|{ ApprovalAction : history
  Document ||--o{ DocumentLink : linked
  ImportBatch ||--o{ ActualTransaction : sources
  ImportBatch ||--o{ ImportException : raises
  AuditEvent }o--|| UserIdentity : actor
```

The physical schema will add explicit lookup tables, bridge tables, uniqueness constraints, check constraints, filtered indexes, and `rowversion` columns as each module is implemented.
