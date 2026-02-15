using System;
using System.Collections.Generic;
using System.Linq;
// TAG: #FEATURE_BULK_OPERATIONS #MODEL #VERSION_2_0

namespace NecessaryAdminTool.Models
{
    /// <summary>
    /// Results from a bulk operation execution
    /// TAG: #BULK_OPERATIONS #RESULTS_MODEL
    /// </summary>
    public class BulkOperationResult
    {
        public string OperationId { get; set; }
        public BulkOperationType OperationType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public List<ComputerOperationResult> ComputerResults { get; set; }
        public BulkOperationStatus FinalStatus { get; set; }
        public string ErrorMessage { get; set; }

        // Computed properties
        public int TotalTargets => ComputerResults?.Count ?? 0;
        public int SuccessCount => ComputerResults?.Count(r => r.Success) ?? 0;
        public int FailureCount => ComputerResults?.Count(r => !r.Success && !r.Skipped) ?? 0;
        public int SkippedCount => ComputerResults?.Count(r => r.Skipped) ?? 0;
        public double SuccessRate => TotalTargets > 0 ? (double)SuccessCount / TotalTargets * 100 : 0;

        public BulkOperationResult()
        {
            ComputerResults = new List<ComputerOperationResult>();
            StartTime = DateTime.Now;
            FinalStatus = BulkOperationStatus.Pending;
        }

        public static BulkOperationResult Cancelled()
        {
            return new BulkOperationResult
            {
                FinalStatus = BulkOperationStatus.Cancelled,
                EndTime = DateTime.Now,
                ErrorMessage = "Operation cancelled by user"
            };
        }

        public static BulkOperationResult Failed(string errorMessage)
        {
            return new BulkOperationResult
            {
                FinalStatus = BulkOperationStatus.Failed,
                EndTime = DateTime.Now,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Result for a single computer in a bulk operation
    /// TAG: #BULK_OPERATIONS #COMPUTER_RESULT
    /// </summary>
    public class ComputerOperationResult
    {
        public string ComputerName { get; set; }
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public string Message { get; set; }
        public string ErrorDetails { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ExecutionTime => EndTime - StartTime;
        public int RetryCount { get; set; }
        public Dictionary<string, object> ResultData { get; set; }

        public ComputerOperationResult()
        {
            ResultData = new Dictionary<string, object>();
            StartTime = DateTime.Now;
        }

        public static ComputerOperationResult Succeeded(string computerName, string message = "Operation completed successfully")
        {
            return new ComputerOperationResult
            {
                ComputerName = computerName,
                Success = true,
                Skipped = false,
                Message = message,
                EndTime = DateTime.Now
            };
        }

        public static ComputerOperationResult Failed(string computerName, string errorMessage)
        {
            return new ComputerOperationResult
            {
                ComputerName = computerName,
                Success = false,
                Skipped = false,
                Message = "Operation failed",
                ErrorDetails = errorMessage,
                EndTime = DateTime.Now
            };
        }

        public static ComputerOperationResult Skipped(string computerName, string reason)
        {
            return new ComputerOperationResult
            {
                ComputerName = computerName,
                Success = false,
                Skipped = true,
                Message = $"Skipped: {reason}",
                EndTime = DateTime.Now
            };
        }
    }
}
