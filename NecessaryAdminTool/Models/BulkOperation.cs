using System;
using System.Collections.Generic;
// TAG: #FEATURE_BULK_OPERATIONS #MODEL #VERSION_2_0

namespace NecessaryAdminTool.Models
{
    /// <summary>
    /// Represents a bulk operation to be executed on multiple computers
    /// TAG: #BULK_OPERATIONS #DATA_MODEL
    /// </summary>
    public class BulkOperation
    {
        public string OperationId { get; set; }
        public BulkOperationType OperationType { get; set; }
        public List<string> Targets { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public BulkOperationStatus Status { get; set; }
        public int MaxDegreeOfParallelism { get; set; }
        public int TimeoutPerComputerMs { get; set; }
        public int MaxRetryAttempts { get; set; }

        public BulkOperation()
        {
            OperationId = Guid.NewGuid().ToString();
            Targets = new List<string>();
            Parameters = new Dictionary<string, object>();
            CreatedAt = DateTime.Now;
            Status = BulkOperationStatus.Pending;
            MaxDegreeOfParallelism = 10; // Default 10 parallel threads
            TimeoutPerComputerMs = 300000; // Default 5 minutes
            MaxRetryAttempts = 3; // Default 3 retry attempts
        }
    }

    /// <summary>
    /// Types of bulk operations supported
    /// TAG: #BULK_OPERATIONS #OPERATION_TYPES
    /// </summary>
    public enum BulkOperationType
    {
        RestartComputers,
        RunPowerShellScript,
        InstallWindowsUpdates,
        EnableService,
        DisableService,
        CollectSystemInventory,
        ChangeGroupMembership,
        DeploySoftware,
        ExecuteRemoteCommand,
        PingTest,
        WMIScan,
        CustomOperation
    }

    /// <summary>
    /// Status of a bulk operation
    /// TAG: #BULK_OPERATIONS #STATUS_TRACKING
    /// </summary>
    public enum BulkOperationStatus
    {
        Pending,
        Running,
        Completed,
        PartiallyCompleted,
        Failed,
        Cancelled
    }
}
