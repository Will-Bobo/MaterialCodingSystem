namespace MaterialCodingSystem.Application.Logging;

/// <summary>与 docs/MCS_LOG_V1.md §11 action_name 一致。</summary>
public static class McsActions
{
    public const string MaterialCreateCategory = "material.create_category";
    public const string MaterialListCategories = "material.list_categories";
    public const string MaterialResolveGroupIdByItemCode = "material.resolve_group_id_by_item_code";
    public const string MaterialGetGroupInfo = "material.get_group_info";
    public const string MaterialAllocateNextGroupSerial = "material.allocate_next_group_serial";
    public const string MaterialCreateMaterialItemA = "material.create_material_item_a";
    public const string MaterialCreateMaterialItemManual = "material.create_material_item_manual";
    public const string MaterialCreateReplacement = "material.create_replacement";
    public const string MaterialCreateReplacementByCode = "material.create_replacement_by_code";
    public const string MaterialDeprecateMaterialItem = "material.deprecate_material_item";
    public const string MaterialSearchByCode = "material.search_by_code";
    public const string MaterialSearchBySpec = "material.search_by_spec";
    public const string MaterialSearchCandidatesBySpecOnly = "material.search_candidates_by_spec_only";
    public const string MaterialSearchBySpecAll = "material.search_by_spec_all";
    public const string MaterialExportActiveMaterials = "material.export_active_materials";

    public const string BomAnalyze = "bom.analyze";
    public const string BomParse = "bom.parse";
    public const string BomImportNewMaterials = "bom.import_new_materials";
    public const string BomCanArchive = "bom.can_archive";
    public const string BomArchive = "bom.archive";
    public const string BomArchiveService = "bom.archive_service";
    public const string BomListArchive = "bom.list_archive";
    public const string BomValidateArchiveIntegrity = "bom.validate_archive_integrity";
    public const string BomConfigureArchiveRoot = "bom.configure_archive_root";

    public const string BackupExportDatabase = "backup.export_database";
    public const string BackupCreateAutoBackup = "backup.create_auto_backup";
    public const string BackupRestoreDatabase = "backup.restore_database";

    public const string SystemAppStarted = "system.app_started";

    /// <summary>运维门禁争用（Wait 排队）。</summary>
    public const string SystemMaintenanceGate = "system.maintenance_gate";

    /// <summary>Infrastructure：SQLite VACUUM INTO 失败（Phase3）。</summary>
    public const string InfraVacuumInto = "infra.backup.vacuum_into";

    /// <summary>Infrastructure：物料仓储技术异常。</summary>
    public const string RepoSqliteMaterialRepository = "repo.sqlite_material_repository";
}
