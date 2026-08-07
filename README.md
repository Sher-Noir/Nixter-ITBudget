# CRCH IT Budget Management System

Internal IT budget management system for Charles River Community Health (CRCH). This repository is being built as a secure ASP.NET Core MVC modular monolith for IIS, Windows Authentication, and SQL Server.

## Current status

Initial architecture and buildable application scaffold. The implementation roadmap and source-workbook migration mapping are under `docs/`.

## Technology baseline

- .NET 10 LTS / ASP.NET Core MVC + Razor
- Microsoft SQL Server / Entity Framework Core
- IIS in-process hosting
- Integrated Windows Authentication
- Server-rendered UI with progressive enhancement
- xUnit, integration tests, and Playwright UI tests

## Local build

```powershell
dotnet restore Crch.ItBudget.slnx
dotnet build Crch.ItBudget.slnx --configuration Release --no-restore
dotnet test Crch.ItBudget.slnx --configuration Release --no-build
```

Production deployment must use explicit database migration steps; destructive migrations must not run automatically at application startup.

## Security note

This system is not intended to store PHI or clinical records. Do not upload clinical data.
