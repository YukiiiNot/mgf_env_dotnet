namespace MGF.Worker.ProjectBootstrap;

internal static class ProjectStorageRootSql
{
    internal const string UpsertStorageRoot =
        """
        WITH upsert AS (
          INSERT INTO public.project_storage_roots (
              project_storage_root_id,
              project_id,
              storage_provider_key,
              root_key,
              folder_relpath,
              share_url,
              is_primary
          )
          VALUES (
              @project_storage_root_id,
              @project_id,
              @storage_provider_key,
              @root_key,
              @folder_relpath,
              NULL,
              true
          )
          ON CONFLICT (project_id, storage_provider_key, root_key)
          DO UPDATE SET
              folder_relpath = EXCLUDED.folder_relpath,
              share_url = NULL,
              is_primary = true
          RETURNING project_id, storage_provider_key, root_key
        )
        UPDATE public.project_storage_roots
        SET is_primary = CASE WHEN root_key = @root_key THEN true ELSE false END
        WHERE project_id = @project_id
          AND storage_provider_key = @storage_provider_key;
        """;
}
