# Complete entity inventory

## Budgeting and fiscal control
- FiscalYear
- FiscalPeriod
- BudgetVersion
- BudgetItem
- BudgetItemAllocation
- BudgetItemLineage
- BudgetAmendment
- BudgetAmendmentLine
- ForecastScenario
- ForecastLine
- SavedView
- SharedView

## Master and lookup data
- Vendor
- VendorMergeHistory
- Department
- Location
- FinanceAccount
- FinanceCategory
- InternalCategory
- BudgetSection
- NeedLevel
- Priority
- Frequency
- PurchaseTypeLookup
- UnitOfMeasure
- DocumentType
- ApprovalStatusLookup
- TransactionTypeLookup
- POStatusLookup
- InvoiceStatusLookup
- RenewalStatusLookup
- ContractStatusLookup
- LookupValueAlias

## Procure-to-pay
- PurchaseOrder
- PurchaseOrderLine
- PurchaseOrderChangeOrder
- Invoice
- InvoiceAllocation
- ActualTransaction
- ActualTransactionSplit
- ActualReversal
- ReconciliationMatch

## Contracts and renewals
- Contract
- ContractFiscalYear
- ContractBudgetItem
- ContractPurchaseOrder
- Renewal
- RenewalDecisionHistory
- RenewalChecklistItem

## Documents
- Document
- DocumentVersion
- DocumentLink
- DocumentAccessEvent
- DocumentScanResult

## Workflow and collaboration
- ApprovalRequest
- ApprovalAction
- ApprovalRule
- ApprovalRuleStep
- Delegation
- Comment
- CommentEditHistory
- Task
- Notification
- NotificationPreference

## Import/export/reporting
- ImportProfile
- ImportProfileColumn
- ImportBatch
- ImportRow
- ImportException
- ExportProfile
- ExportProfileColumn
- ExportRun
- ReportSavedParameter
- ReportFavorite

## Security/configuration/audit
- ApplicationRoleDefinition
- AdGroupMapping
- UserRoleException
- ApplicationSetting
- FeatureFlag
- AuditEvent
- DataCorrectionRequest
- DiagnosticCheckResult
- BackgroundJobRecord (only if an internal job processor is introduced)
