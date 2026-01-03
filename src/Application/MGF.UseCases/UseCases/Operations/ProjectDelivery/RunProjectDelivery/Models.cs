namespace MGF.UseCases.Operations.ProjectDelivery.RunProjectDelivery;

using MGF.Contracts.Abstractions.ProjectDelivery;

public sealed record RunProjectDeliveryRequest(
    ProjectDeliveryPayload Payload,
    string JobId);

public sealed record RunProjectDeliveryResult(
    ProjectDeliveryRunResult Result);
