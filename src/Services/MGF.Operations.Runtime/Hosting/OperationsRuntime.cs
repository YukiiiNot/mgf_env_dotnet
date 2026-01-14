namespace MGF.Operations.Runtime;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MGF.Data.Data;
using MGF.Data.Stores.Operations;
using MGF.Contracts.Abstractions.Operations.StorageRoots;
using MGF.Hosting.Configuration;
using MGF.UseCases.Operations.Jobs.EnqueueProjectArchiveJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectBootstrapJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryEmailJob;
using MGF.UseCases.Operations.Jobs.EnqueueProjectDeliveryJob;
using MGF.UseCases.Operations.Jobs.EnqueueRootIntegrityJob;
using MGF.UseCases.Operations.Jobs.GetRootIntegrityJobs;
using MGF.UseCases.Operations.Jobs.RequeueStaleJobs;
using MGF.UseCases.Operations.Jobs.ResetProjectJobs;
using MGF.UseCases.Operations.Projects.CreateTestProject;
using MGF.UseCases.Operations.Projects.GetDeliveryEmailPreviewData;
using MGF.UseCases.Operations.Projects.GetProjectSnapshot;
using MGF.UseCases.Operations.Projects.ListProjects;
using MGF.UseCases.Operations.Projects.UpdateProjectStatus;

public static class OperationsRuntimeConfiguration
{
    public static IConfiguration BuildConfiguration()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                MgfHostConfiguration.ConfigureMgfConfiguration(context, config);
            })
            .Build();

        return host.Services.GetRequiredService<IConfiguration>();
    }
}

public sealed class OperationsRuntime
{
    public IEnqueueProjectBootstrapJobUseCase EnqueueProjectBootstrapJob { get; }
    public IEnqueueProjectArchiveJobUseCase EnqueueProjectArchiveJob { get; }
    public IEnqueueProjectDeliveryJobUseCase EnqueueProjectDeliveryJob { get; }
    public IEnqueueProjectDeliveryEmailJobUseCase EnqueueProjectDeliveryEmailJob { get; }
    public IEnqueueRootIntegrityJobUseCase EnqueueRootIntegrityJob { get; }
    public IResetProjectJobsUseCase ResetProjectJobs { get; }
    public IRequeueStaleJobsUseCase RequeueStaleJobs { get; }
    public IGetRootIntegrityJobsUseCase GetRootIntegrityJobs { get; }
    public IGetProjectSnapshotUseCase GetProjectSnapshot { get; }
    public IListProjectsUseCase ListProjects { get; }
    public IUpdateProjectStatusUseCase UpdateProjectStatus { get; }
    public IGetDeliveryEmailPreviewDataUseCase GetDeliveryEmailPreviewData { get; }
    public ICreateTestProjectUseCase CreateTestProject { get; }
    public IStorageRootContractStore StorageRootContracts { get; }

    private OperationsRuntime(
        IEnqueueProjectBootstrapJobUseCase enqueueProjectBootstrapJob,
        IEnqueueProjectArchiveJobUseCase enqueueProjectArchiveJob,
        IEnqueueProjectDeliveryJobUseCase enqueueProjectDeliveryJob,
        IEnqueueProjectDeliveryEmailJobUseCase enqueueProjectDeliveryEmailJob,
        IEnqueueRootIntegrityJobUseCase enqueueRootIntegrityJob,
        IResetProjectJobsUseCase resetProjectJobs,
        IRequeueStaleJobsUseCase requeueStaleJobs,
        IGetRootIntegrityJobsUseCase getRootIntegrityJobs,
        IGetProjectSnapshotUseCase getProjectSnapshot,
        IListProjectsUseCase listProjects,
        IUpdateProjectStatusUseCase updateProjectStatus,
        IGetDeliveryEmailPreviewDataUseCase getDeliveryEmailPreviewData,
        ICreateTestProjectUseCase createTestProject,
        IStorageRootContractStore storageRootContracts)
    {
        EnqueueProjectBootstrapJob = enqueueProjectBootstrapJob;
        EnqueueProjectArchiveJob = enqueueProjectArchiveJob;
        EnqueueProjectDeliveryJob = enqueueProjectDeliveryJob;
        EnqueueProjectDeliveryEmailJob = enqueueProjectDeliveryEmailJob;
        EnqueueRootIntegrityJob = enqueueRootIntegrityJob;
        ResetProjectJobs = resetProjectJobs;
        RequeueStaleJobs = requeueStaleJobs;
        GetRootIntegrityJobs = getRootIntegrityJobs;
        GetProjectSnapshot = getProjectSnapshot;
        ListProjects = listProjects;
        UpdateProjectStatus = updateProjectStatus;
        GetDeliveryEmailPreviewData = getDeliveryEmailPreviewData;
        CreateTestProject = createTestProject;
        StorageRootContracts = storageRootContracts;
    }

    public static OperationsRuntime Create(IConfiguration configuration)
    {
        var jobStore = new JobOpsStore(configuration);
        var projectStore = new ProjectOpsStore(configuration);
        var rootContracts = new StorageRootContractStore(configuration);

        return new OperationsRuntime(
            new EnqueueProjectBootstrapJobUseCase(jobStore),
            new EnqueueProjectArchiveJobUseCase(jobStore),
            new EnqueueProjectDeliveryJobUseCase(jobStore),
            new EnqueueProjectDeliveryEmailJobUseCase(jobStore),
            new EnqueueRootIntegrityJobUseCase(jobStore),
            new ResetProjectJobsUseCase(jobStore),
            new RequeueStaleJobsUseCase(jobStore),
            new GetRootIntegrityJobsUseCase(jobStore),
            new GetProjectSnapshotUseCase(jobStore, projectStore),
            new ListProjectsUseCase(projectStore),
            new UpdateProjectStatusUseCase(projectStore),
            new GetDeliveryEmailPreviewDataUseCase(projectStore),
            new CreateTestProjectUseCase(projectStore),
            rootContracts);
    }
}
