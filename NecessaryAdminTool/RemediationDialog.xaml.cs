using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool
{
    // TAG: #VERSION_7.1 #REMEDIATION #AUTOMATION #UI
    /// <summary>
    /// Progress dialog for automated remediation operations
    /// Shows real-time progress, results, and statistics
    /// </summary>
    public partial class RemediationDialog : Window
    {
        private ObservableCollection<RemediationManager.RemediationResult> _results = new ObservableCollection<RemediationManager.RemediationResult>();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isRunning = false;
        private DateTime _startTime;

        public RemediationDialog()
        {
            InitializeComponent();
            GridResults.ItemsSource = _results;
        }

        /// <summary>
        /// Execute remediation action on multiple computers
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        public async Task ExecuteRemediationAsync(
            RemediationManager.RemediationAction action,
            string[] hostnames,
            string username = null,
            string password = null)
        {
            _isRunning = true;
            _startTime = DateTime.Now;
            _results.Clear();

            // Update header
            TxtActionName.Text = $"Action: {RemediationManager.GetActionIcon(action)} {RemediationManager.GetActionName(action)}";
            TxtProgress.Text = $"0 / {hostnames.Length}";
            TxtPercentage.Text = "0%";
            TxtCurrentStatus.Text = "Starting remediation...";
            TxtTotalCount.Text = hostnames.Length.ToString();

            // Show cancel button, hide close button
            BtnCancel.Visibility = Visibility.Visible;
            BtnClose.Visibility = Visibility.Collapsed;

            // Start pulse animation on progress bar
            var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
            pulseAnimation.Begin(ProgressBarOverall);

            try
            {
                // Execute remediation on all computers in parallel (max 10 concurrent)
                var semaphore = new SemaphoreSlim(10, 10);
                var tasks = hostnames.Select(async hostname =>
                {
                    await semaphore.WaitAsync(_cts.Token);
                    try
                    {
                        // Update status
                        Dispatcher.Invoke(() =>
                        {
                            TxtCurrentStatus.Text = $"Processing {hostname}...";
                        });

                        // Execute remediation
                        var result = await RemediationManager.ExecuteRemediationAsync(
                            hostname,
                            action,
                            username,
                            password,
                            _cts.Token);

                        // Add result to UI
                        Dispatcher.Invoke(() =>
                        {
                            _results.Add(result);
                            UpdateProgress();
                        });
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                // Completion
                Dispatcher.Invoke(() =>
                {
                    pulseAnimation.Stop(ProgressBarOverall);
                    ProgressBarOverall.Opacity = 1.0;
                    TxtCurrentStatus.Text = "Remediation complete!";
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnClose.Visibility = Visibility.Visible;
                });

                LogManager.LogInfo($"[Remediation] Completed {RemediationManager.GetActionName(action)} on {hostnames.Length} computers");
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    pulseAnimation.Stop(ProgressBarOverall);
                    ProgressBarOverall.Opacity = 1.0;
                    TxtCurrentStatus.Text = "Remediation cancelled by user";
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnClose.Visibility = Visibility.Visible;
                });

                LogManager.LogWarning($"[Remediation] Cancelled {RemediationManager.GetActionName(action)}");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    pulseAnimation.Stop(ProgressBarOverall);
                    ProgressBarOverall.Opacity = 1.0;
                    TxtCurrentStatus.Text = $"Error: {ex.Message}";
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnClose.Visibility = Visibility.Visible;
                });

                LogManager.LogError($"[Remediation] Failed to execute {RemediationManager.GetActionName(action)}", ex);
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Update progress indicators and statistics
        /// TAG: #VERSION_7.1 #REMEDIATION #UI
        /// </summary>
        private void UpdateProgress()
        {
            int total = int.Parse(TxtTotalCount.Text);
            int completed = _results.Count;
            int success = _results.Count(r => r.Success);
            int failed = _results.Count(r => !r.Success);

            // Update progress
            double percentage = total > 0 ? (double)completed / total * 100 : 0;
            TxtProgress.Text = $"{completed} / {total}";
            TxtPercentage.Text = $"{percentage:F0}%";
            ProgressBarOverall.Value = percentage;

            // Update statistics
            TxtSuccessCount.Text = success.ToString();
            TxtFailedCount.Text = failed.ToString();

            // Calculate average duration
            if (_results.Count > 0)
            {
                var avgSeconds = _results.Average(r => r.Duration.TotalSeconds);
                TxtAvgDuration.Text = $"{avgSeconds:F1}s";
            }
        }

        /// <summary>
        /// Cancel button click handler
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                ToastManager.ShowWarning("Are you sure you want to cancel the remediation?", "Yes, Cancel", () =>
                {
                    _cts.Cancel();
                    BtnCancel.IsEnabled = false;
                    BtnCancel.Content = "⏸️ CANCELLING...";
                });
            }
        }

        /// <summary>
        /// Close button click handler
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// Prevent window close while running
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isRunning)
            {
                e.Cancel = true;
                ToastManager.ShowWarning("Remediation is still running.", "Close Anyway", () =>
                {
                    _cts.Cancel();
                    Close();
                });
                return;
            }

            base.OnClosing(e);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // VALUE CONVERTERS
    // TAG: #VERSION_7.1 #REMEDIATION #UI
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts boolean Success value to icon (✅ or ❌)
    /// </summary>
    public class BoolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool success)
            {
                return success ? "✅" : "❌";
            }
            return "⏳";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
