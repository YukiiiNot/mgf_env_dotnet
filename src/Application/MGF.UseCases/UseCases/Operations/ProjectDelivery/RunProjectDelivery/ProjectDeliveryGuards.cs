namespace MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;

internal static class ProjectDeliveryGuards
{
    internal const string StatusReady = "ready_to_deliver";
    internal const string StatusDelivering = "delivering";
    internal const string StatusDelivered = "delivered";
    internal const string StatusDeliveryFailed = "delivery_failed";

    internal static bool TryValidateStart(
        string? statusKey,
        bool force,
        out string? error,
        out bool alreadyDelivering)
    {
        alreadyDelivering = string.Equals(statusKey, StatusDelivering, StringComparison.OrdinalIgnoreCase);
        if (alreadyDelivering)
        {
            error = "Project is already delivering.";
            return false;
        }

        if (force)
        {
            error = null;
            return true;
        }

        if (string.Equals(statusKey, StatusReady, StringComparison.OrdinalIgnoreCase)
            || string.Equals(statusKey, StatusDeliveryFailed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(statusKey, StatusDelivered, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        error = "Project status is not eligible for delivery.";
        return false;
    }
}
