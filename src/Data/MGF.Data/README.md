# MGF.Data

This project owns persistence concerns (EF model, migrations, seeding, and raw SQL stores).

Folder conventions:
- Abstractions/: public Data interfaces used by hosts.
- Data/: DbContext, entity configs, repositories, and data seams.
- Stores/: raw SQL stores organized by domain (e.g., Stores/Jobs, Stores/Counters, Stores/Delivery).
- Migrations/: EF migrations.
- Options/Configuration: data layer config types.

Rule: Hosts do not run raw SQL directly; expose a Data interface and implement it here.
