namespace MGF.UseCases.Operations.Projects.GetDeliveryEmailPreviewData;

using MGF.Contracts.Abstractions.Operations.Projects;

public sealed class GetDeliveryEmailPreviewDataUseCase : IGetDeliveryEmailPreviewDataUseCase
{
    private readonly IProjectOpsStore projectStore;

    public GetDeliveryEmailPreviewDataUseCase(IProjectOpsStore projectStore)
    {
        this.projectStore = projectStore;
    }

    public async Task<GetDeliveryEmailPreviewDataResult?> ExecuteAsync(
        GetDeliveryEmailPreviewDataRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var project = await projectStore.GetProjectAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var clientName = await projectStore.GetClientNameAsync(project.ClientId, cancellationToken);

        var result = new DeliveryEmailPreviewProject(
            project.ProjectId,
            project.ProjectCode,
            project.ProjectName,
            project.ClientId,
            project.MetadataJson,
            clientName);

        return new GetDeliveryEmailPreviewDataResult(result);
    }
}
