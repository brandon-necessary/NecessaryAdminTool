using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NecessaryAdminTool.Managers;
using NecessaryAdminTool.Managers.UI;
using NecessaryAdminTool.Models;
using NecessaryAdminTool.Security;
// TAG: #FEATURE_BULK_OPERATIONS #WINDOW #FLUENT_DESIGN #ASYNC_OPERATIONS #VERSION_2_0

namespace NecessaryAdminTool.Windows
{
    /// <summary>
    /// Bulk Operations Window - Multi-computer management interface
    /// TAG: #BULK_OPERATIONS #UI #FLUENT_DESIGN
    /// </summary>
    public partial class BulkOperationsWindow : Window
    {
        private BulkOperationManager _manager;
        private CancellationTokenSource _cancellationTokenSource;
        private ObservableCollection<ResultGridItem> _resultsCollection;
        private BulkOperationResult _lastResult;

        public BulkOperationsWindow()
        {
            InitializeComponent();
            _manager = new BulkOperationManager();
            _resultsCollection = new ObservableCollection<ResultGridItem>();
            ResultsGrid.ItemsSource = _resultsCollection;

            // Bind slider to text
            SliderThreads.ValueChanged += (s, e) => TxtThreadCount.Text = ((int)e.NewValue).ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogManager.LogInfo("[BulkOperationsWindow] Window loaded");
            CmbOperationType.SelectedIndex = 0; // Select first operation type
        }

        /// <summary>
        /// Operation type selection changed
        /// TAG: #UI_EVENT_HANDLER
        /// </summary>
        private void CmbOperationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Future: Show/hide operation-specific parameters
        }

        /// <summary>
        /// Import targets from CSV file
        /// TAG: #CSV_IMPORT #FILE_OPERATION
        /// </summary>
        private void BtnImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "Import Computer List"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // TAG: #SECURITY_CRITICAL #FILE_PATH_VALIDATION
                    string filename = Path.GetFileName(openFileDialog.FileName);
                    if (!SecurityValidator.IsValidFilename(filename))
                    {
                        ToastManager.ShowError("Invalid filename detected");
                        return;
                    }

                    var lines = File.ReadAllLines(openFileDialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .ToList();

                    TxtTargets.Text = string.Join(Environment.NewLine, lines);
                    UpdateTargetCount();

                    ToastManager.ShowSuccess($"Imported {lines.Count} targets from CSV");
                    LogManager.LogInfo($"[BulkOperationsWindow] Imported {lines.Count} targets from {filename}");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[BulkOperationsWindow] Failed to import CSV", ex);
                ToastManager.ShowError($"Import failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load targets from Active Directory
        /// TAG: #AD_INTEGRATION
        /// </summary>
        private void BtnLoadFromAD_Click(object sender, RoutedEventArgs e)
        {
            // Future: Implement AD computer selection dialog
            ToastManager.ShowInfo("AD integration coming soon");
        }

        /// <summary>
        /// Execute bulk operation
        /// TAG: #BULK_OPERATIONS #ASYNC_OPERATIONS #SECURITY_CRITICAL
        /// </summary>
        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parse targets
                var targets = TxtTargets.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                if (targets.Count == 0)
                {
                    ToastManager.ShowWarning("No targets specified");
                    return;
                }

                // Get operation type
                var selectedItem = CmbOperationType.SelectedItem as ComboBoxItem;
                if (selectedItem == null)
                {
                    ToastManager.ShowWarning("Please select an operation type");
                    return;
                }

                string operationTag = selectedItem.Tag?.ToString();
                BulkOperationType operationType;
                if (!Enum.TryParse(operationTag, out operationType))
                {
                    ToastManager.ShowError("Invalid operation type");
                    return;
                }

                // Parse execution options
                int threads = (int)SliderThreads.Value;
                int timeoutSeconds = int.TryParse(TxtTimeout.Text, out var t) ? t : 300;
                int retryAttempts = int.TryParse(TxtRetryAttempts.Text, out var r) ? r : 3;

                // TAG: #SECURITY_CRITICAL #USER_CONFIRMATION
                // Confirmation dialog
                ToastManager.ShowWarning(
                    $"Execute {operationType} on {targets.Count} computers? Threads: {threads}, Timeout: {timeoutSeconds}s, Retries: {retryAttempts}.",
                    "Execute",
                    async () => await ExecuteBulkOperationConfirmedAsync(operationType, targets, threads, timeoutSeconds, retryAttempts));
                return;
            }
            catch (Exception ex)
            {
                LogManager.LogError("[BulkOperationsWindow] Bulk operation execution error", ex);
                ToastManager.ShowError($"Execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// Performs the actual bulk operation execution after user confirms
        /// TAG: #BULK_OPERATIONS #ASYNC_OPERATIONS
        /// </summary>
        private async Task ExecuteBulkOperationConfirmedAsync(BulkOperationType operationType, List<string> targets, int threads, int timeoutSeconds, int retryAttempts)
        {
            try
            {
                // Create operation
                var operation = new BulkOperation
                {
                    OperationType = operationType,
                    Targets = targets,
                    CreatedBy = Environment.UserName,
                    MaxDegreeOfParallelism = threads,
                    TimeoutPerComputerMs = timeoutSeconds * 1000,
                    MaxRetryAttempts = retryAttempts
                };

                // Add operation-specific parameters
                AddOperationParameters(operation, operationType);

                // Clear previous results
                _resultsCollection.Clear();
                ProgressOverall.Value = 0;
                TxtProgress.Text = "0%";

                // Update UI state
                BtnExecute.IsEnabled = false;
                BtnCancel.IsEnabled = true;
                BtnExportCsv.IsEnabled = false;

                // Create cancellation token
                _cancellationTokenSource = new CancellationTokenSource();

                // Execute with progress tracking
                var progress = new Progress<BulkOperationProgress>(UpdateProgress);
                ToastManager.ShowInfo($"Starting bulk operation: {operationType}");

                _lastResult = await _manager.ExecuteBulkOperationAsync(
                    operation,
                    progress,
                    _cancellationTokenSource.Token);

                // Update results
                DisplayResults(_lastResult);

                // Show completion toast
                if (_lastResult.FinalStatus == BulkOperationStatus.Completed)
                {
                    ToastManager.ShowSuccess($"Bulk operation completed: {_lastResult.SuccessCount}/{_lastResult.TotalTargets} successful");
                }
                else if (_lastResult.FinalStatus == BulkOperationStatus.PartiallyCompleted)
                {
                    ToastManager.ShowWarning($"Bulk operation partially completed: {_lastResult.SuccessCount}/{_lastResult.TotalTargets} successful");
                }
                else if (_lastResult.FinalStatus == BulkOperationStatus.Cancelled)
                {
                    ToastManager.ShowInfo("Bulk operation cancelled");
                }
                else
                {
                    ToastManager.ShowError($"Bulk operation failed: {_lastResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[BulkOperationsWindow] Bulk operation execution error", ex);
                ToastManager.ShowError($"Execution error: {ex.Message}");
            }
            finally
            {
                // Restore UI state
                BtnExecute.IsEnabled = true;
                BtnCancel.IsEnabled = false;
                BtnExportCsv.IsEnabled = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Add operation-specific parameters
        /// TAG: #OPERATION_PARAMETERS
        /// </summary>
        private void AddOperationParameters(BulkOperation operation, BulkOperationType operationType)
        {
            switch (operationType)
            {
                case BulkOperationType.RestartComputers:
                    // Could add forced/graceful option from UI
                    operation.Parameters["Forced"] = false;
                    break;

                case BulkOperationType.RunPowerShellScript:
                    // Future: Add script input dialog
                    operation.Parameters["ScriptContent"] = "Get-ComputerInfo | Select-Object CsName, OsVersion";
                    break;

                case BulkOperationType.EnableService:
                case BulkOperationType.DisableService:
                    // Future: Add service name input dialog
                    operation.Parameters["ServiceName"] = "Spooler";
                    break;
            }
        }

        /// <summary>
        /// Update progress bar and status
        /// TAG: #PROGRESS_TRACKING #UI_UPDATE
        /// </summary>
        private void UpdateProgress(BulkOperationProgress progress)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressOverall.Maximum = progress.TotalTargets;
                ProgressOverall.Value = progress.CompletedTargets;
                TxtProgress.Text = $"{progress.PercentComplete:F0}%";

                TxtTotal.Text = $"Total: {progress.TotalTargets}";
                TxtSuccess.Text = $"Success: {progress.SuccessCount}";
                TxtFailed.Text = $"Failed: {progress.FailureCount}";

                if (!string.IsNullOrEmpty(progress.CurrentTarget))
                {
                    LogManager.LogDebug($"[BulkOperationsWindow] Processing: {progress.CurrentTarget}");
                }
            });
        }

        /// <summary>
        /// Display results in DataGrid
        /// TAG: #RESULTS_DISPLAY #UI_UPDATE
        /// </summary>
        private void DisplayResults(BulkOperationResult result)
        {
            _resultsCollection.Clear();

            foreach (var computerResult in result.ComputerResults)
            {
                _resultsCollection.Add(new ResultGridItem
                {
                    ComputerName = computerResult.ComputerName,
                    StatusText = computerResult.Success ? "Success" : (computerResult.Skipped ? "Skipped" : "Failed"),
                    Message = computerResult.Message,
                    ExecutionTimeMs = (int)computerResult.ExecutionTime.TotalMilliseconds,
                    RetryCount = computerResult.RetryCount
                });
            }

            // Update summary
            TxtTotal.Text = $"Total: {result.TotalTargets}";
            TxtSuccess.Text = $"Success: {result.SuccessCount}";
            TxtFailed.Text = $"Failed: {result.FailureCount}";
            TxtSkipped.Text = $"Skipped: {result.SkippedCount}";

            LogManager.LogInfo($"[BulkOperationsWindow] Results displayed - {result.ComputerResults.Count} rows");
        }

        /// <summary>
        /// Cancel running operation
        /// TAG: #CANCELLATION #ASYNC_OPERATIONS
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    ToastManager.ShowInfo("Cancelling bulk operation...");
                    LogManager.LogInfo("[BulkOperationsWindow] Bulk operation cancellation requested");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[BulkOperationsWindow] Error cancelling operation", ex);
            }
        }

        /// <summary>
        /// Export results to CSV
        /// TAG: #CSV_EXPORT #FILE_OPERATION
        /// </summary>
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastResult == null || _lastResult.ComputerResults.Count == 0)
                {
                    ToastManager.ShowWarning("No results to export");
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    Title = "Export Results",
                    FileName = $"BulkOperation_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // TAG: #SECURITY_CRITICAL #FILE_PATH_VALIDATION
                    string filename = Path.GetFileName(saveFileDialog.FileName);
                    if (!SecurityValidator.IsValidFilename(filename))
                    {
                        ToastManager.ShowError("Invalid filename");
                        return;
                    }

                    string csv = _manager.ExportResultsToCsv(_lastResult);
                    File.WriteAllText(saveFileDialog.FileName, csv);

                    ToastManager.ShowSuccess($"Results exported to {filename}");
                    LogManager.LogInfo($"[BulkOperationsWindow] Results exported to {filename}");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[BulkOperationsWindow] Failed to export CSV", ex);
                ToastManager.ShowError($"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update target count display
        /// TAG: #UI_UPDATE
        /// </summary>
        private void UpdateTargetCount()
        {
            var targets = TxtTargets.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            TxtTargetCount.Text = $"{targets.Count} target{(targets.Count != 1 ? "s" : "")}";
        }

        /// <summary>
        /// Close window
        /// TAG: #UI_EVENT_HANDLER
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Grid item for results display
    /// TAG: #DATA_MODEL #UI
    /// </summary>
    public class ResultGridItem
    {
        public string ComputerName { get; set; }
        public string StatusText { get; set; }
        public string Message { get; set; }
        public int ExecutionTimeMs { get; set; }
        public int RetryCount { get; set; }
    }
}
