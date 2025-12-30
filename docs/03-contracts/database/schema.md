# Database Schema Contract

Source of truth: `src/MGF.Infrastructure/Migrations/*`, `src/MGF.Infrastructure/Data/AppDbContextModelBuilder.cs`, `docs/db_design/schema_csv/_core/**`
Change control: Update when migrations or schema CSVs change.
Last verified: 2025-12-30

## Source of truth hierarchy
- EF Core migrations are the executable schema source.
- Schema CSVs are design-time documentation and review artifacts.
