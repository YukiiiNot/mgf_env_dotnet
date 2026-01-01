# MGF.UseCases

This project contains business use-cases/workflows as the only write boundary.
UI/API/Worker should call use-cases, not repositories/DbContext.
No integrations, no EF, no raw SQL here.
