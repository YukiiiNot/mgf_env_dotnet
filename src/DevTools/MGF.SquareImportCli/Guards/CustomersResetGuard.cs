namespace MGF.SquareImportCli.Guards;

using MGF.Data.Configuration;

internal readonly record struct ResetGuardDecision(bool Allowed, string? ErrorMessage, bool RequiresConfirmation);

internal static class CustomersResetGuard
{
    public static ResetGuardDecision Evaluate(
        MgfEnvironment env,
        bool destructiveFlag,
        bool nonInteractive,
        bool inputRedirected)
    {
        if (env != MgfEnvironment.Dev)
        {
            return new ResetGuardDecision(
                Allowed: false,
                ErrorMessage: $"square-import customers: --reset blocked (DEV only). Current MGF_ENV={env}.",
                RequiresConfirmation: false
            );
        }

        if (!destructiveFlag)
        {
            return new ResetGuardDecision(
                Allowed: false,
                ErrorMessage:
                    "square-import customers: --reset requires --i-understand-this-will-destroy-data to proceed.",
                RequiresConfirmation: false
            );
        }

        if (inputRedirected && !nonInteractive)
        {
            return new ResetGuardDecision(
                Allowed: false,
                ErrorMessage:
                    "square-import customers: non-interactive session detected; pass --non-interactive to proceed.",
                RequiresConfirmation: false
            );
        }

        return new ResetGuardDecision(
            Allowed: true,
            ErrorMessage: null,
            RequiresConfirmation: !nonInteractive
        );
    }
}


