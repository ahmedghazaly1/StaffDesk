namespace StaffDesk.Core.Constants;

public static class JobTypes
{
    public const string SlaEvaluate = "SLA_EVALUATE";
    public const string BulkStatus = "BULK_STATUS";
    public const string BulkAssignee = "BULK_ASSIGNEE";
    public const string BulkArchive = "BULK_ARCHIVE";
    public const string BulkDelete = "BULK_DELETE";
    public const string BulkAddTags = "BULK_ADD_TAGS";
    public const string BulkRemoveTags = "BULK_REMOVE_TAGS";

    // Part A — Audit & governance
    public const string AuditExport = "AUDIT_EXPORT";
    public const string SubjectAccessExport = "SUBJECT_ACCESS_EXPORT";
    public const string Purge = "PURGE";
    public const string Erasure = "ERASURE";

    // Part D — Analytics
    public const string MetricRollup = "METRIC_ROLLUP";
    public const string MetricRebuild = "METRIC_REBUILD";
    public const string AnalyticsExport = "ANALYTICS_EXPORT";

    // Part F — Webhooks
    public const string WebhookDispatch = "WEBHOOK_DISPATCH";
}
