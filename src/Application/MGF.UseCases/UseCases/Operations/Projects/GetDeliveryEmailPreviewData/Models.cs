namespace MGF.UseCases.Operations.Projects.GetDeliveryEmailPreviewData;

public sealed record GetDeliveryEmailPreviewDataRequest(
    string ProjectId);

public sealed record DeliveryEmailPreviewProject(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    string ClientId,
    string MetadataJson,
    string? ClientName);

public sealed record GetDeliveryEmailPreviewDataResult(
    DeliveryEmailPreviewProject Project);
