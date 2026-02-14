using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ArtaznIT
{
    // TAG: #VERSION_7.1 #SCRIPTS #BULK_OPERATIONS
    /// <summary>
    /// PowerShell Script Executor Window
    /// Allows running custom scripts on multiple computers
    /// </summary>
    public partial class ScriptExecutorWindow : Window
    {
        private List<ScriptManager.SavedScript> _scripts;
        private ScriptManager.SavedScript _currentScript;
        private List<ScriptManager.ScriptExecutionResult> _lastResults;
        private string[] _targetComputers;
        private string _username;
        private string _password;
        private CancellationTokenSource _cancellationTokenSource;

        public ScriptExecutorWindow(string[] targetComputers, string username = null, string password = null)
        {
            InitializeComponent();

            _targetComputers = targetComputers;
            _username = username;
            _password = password;

            TxtTargetCount.Text = $"{targetComputers.Length} computer(s) selected";
        }

        /// <summary>
        /// Window loaded - initialize script library
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize ScriptManager
                ScriptManager.Initialize();

                // Load scripts
                LoadScriptLibrary();

                TxtStatus.Text = $"Ready • {_scripts.Count} scripts loaded • {_targetComputers.Length} target(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize script library:\n\n{ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Error: Failed to load script library";
            }
        }

        /// <summary>
        /// Load all scripts from library
        /// </summary>
        private void LoadScriptLibrary()
        {
            _scripts = ScriptManager.LoadAllScripts();
            ListScripts.ItemsSource = _scripts;
            TxtScriptCount.Text = $"{_scripts.Count} script(s)";

            // Select first script if available
            if (_scripts.Count > 0)
            {
                ListScripts.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Script selected from library
        /// </summary>
        private void ListScripts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListScripts.SelectedItem is ScriptManager.SavedScript script)
            {
                _currentScript = script;
                TxtScriptName.Text = script.Name;
                TxtScriptDescription.Text = script.Description;
                TxtScriptContent.Text = script.ScriptContent;

                BtnDeleteScript.IsEnabled = !script.IsBuiltIn; // Can't delete built-in scripts

                TxtStatus.Text = $"Loaded: {script.Name}";
            }
        }

        /// <summary>
        /// Create new blank script
        /// </summary>
        private void BtnNewScript_Click(object sender, RoutedEventArgs e)
        {
            _currentScript = new ScriptManager.SavedScript
            {
                Name = "New Script",
                Description = "Enter description here",
                Category = ScriptManager.ScriptCategory.Custom,
                ScriptContent = "# Enter PowerShell code here\n\n",
                CreatedDate = DateTime.Now,
                LastModified = DateTime.Now,
                IsBuiltIn = false
            };

            TxtScriptName.Text = _currentScript.Name;
            TxtScriptDescription.Text = _currentScript.Description;
            TxtScriptContent.Text = _currentScript.ScriptContent;

            ListScripts.SelectedItem = null;
            BtnDeleteScript.IsEnabled = false;

            TxtStatus.Text = "New script created";
            TxtScriptName.Focus();
            TxtScriptName.SelectAll();
        }

        /// <summary>
        /// Save current script to library
        /// </summary>
        private void BtnSaveScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtScriptName.Text))
                {
                    MessageBox.Show("Please enter a script name.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TxtScriptContent.Text))
                {
                    MessageBox.Show("Please enter script content.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create or update script
                if (_currentScript == null)
                {
                    _currentScript = new ScriptManager.SavedScript
                    {
                        CreatedDate = DateTime.Now
                    };
                }

                _currentScript.Name = TxtScriptName.Text.Trim();
                _currentScript.Description = TxtScriptDescription.Text.Trim();
                _currentScript.ScriptContent = TxtScriptContent.Text;
                _currentScript.LastModified = DateTime.Now;

                if (ScriptManager.SaveScript(_currentScript))
                {
                    TxtStatus.Text = $"✅ Saved: {_currentScript.Name}";
                    LoadScriptLibrary();

                    // Re-select the saved script
                    var savedScript = _scripts.FirstOrDefault(s => s.Name == _currentScript.Name);
                    if (savedScript != null)
                    {
                        ListScripts.SelectedItem = savedScript;
                    }
                }
                else
                {
                    MessageBox.Show("Failed to save script. Check logs for details.", "Save Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save script:\n\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Delete selected script from library
        /// </summary>
        private void BtnDeleteScript_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScript == null || _currentScript.IsBuiltIn)
                return;

            var result = MessageBox.Show($"Delete script '{_currentScript.Name}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (ScriptManager.DeleteScript(_currentScript.Name))
                {
                    TxtStatus.Text = $"🗑️ Deleted: {_currentScript.Name}";
                    LoadScriptLibrary();

                    // Clear editor
                    TxtScriptName.Text = "";
                    TxtScriptDescription.Text = "";
                    TxtScriptContent.Text = "";
                    _currentScript = null;
                }
            }
        }

        /// <summary>
        /// Import script from .ps1 file
        /// </summary>
        private void BtnImportScript_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import PowerShell Script",
                Filter = "PowerShell Scripts (*.ps1)|*.ps1|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                var script = ScriptManager.ImportScript(dialog.FileName);
                if (script != null)
                {
                    _currentScript = script;
                    TxtScriptName.Text = script.Name;
                    TxtScriptDescription.Text = script.Description;
                    TxtScriptContent.Text = script.ScriptContent;

                    TxtStatus.Text = $"📥 Imported: {dialog.FileName}";
                    MessageBox.Show($"Script imported successfully.\n\nClick 'Save' to add it to the library.",
                        "Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to import script. Check logs for details.", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Export current script to .ps1 file
        /// </summary>
        private void BtnExportScript_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScript == null)
            {
                MessageBox.Show("No script loaded to export.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export PowerShell Script",
                Filter = "PowerShell Scripts (*.ps1)|*.ps1",
                FileName = $"{_currentScript.Name}.ps1"
            };

            if (dialog.ShowDialog() == true)
            {
                if (ScriptManager.ExportScript(_currentScript, dialog.FileName))
                {
                    TxtStatus.Text = $"📤 Exported: {dialog.FileName}";
                    MessageBox.Show($"Script exported successfully to:\n{dialog.FileName}",
                        "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to export script. Check logs for details.", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Execute script on all target computers
        /// TAG: #VERSION_7.1 #BULK_OPERATIONS
        /// </summary>
        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtScriptContent.Text))
            {
                MessageBox.Show("Please enter script content before executing.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_targetComputers == null || _targetComputers.Length == 0)
            {
                MessageBox.Show("No target computers selected.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get max concurrency
            if (!int.TryParse(TxtMaxConcurrency.Text, out int maxConcurrency) || maxConcurrency < 1 || maxConcurrency > 50)
            {
                MessageBox.Show("Max parallel must be between 1 and 50.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm execution
            var confirmMsg = _targetComputers.Length == 1
                ? $"Execute script on {_targetComputers[0]}?"
                : $"Execute script on {_targetComputers.Length} computers in parallel (max {maxConcurrency} concurrent)?";

            var result = MessageBox.Show(confirmMsg, "Confirm Execution",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // Disable UI during execution
                BtnExecute.IsEnabled = false;
                BtnExecute.Content = "⏸️ Executing...";
                ProgressExecution.Visibility = Visibility.Visible;
                ProgressExecution.IsIndeterminate = true;

                _cancellationTokenSource = new CancellationTokenSource();

                // Progress reporter
                var progress = new Progress<(string hostname, int completed, int total)>(update =>
                {
                    TxtStatus.Text = $"Executing... {update.completed}/{update.total} • Last: {update.hostname}";
                    ProgressExecution.IsIndeterminate = false;
                    ProgressExecution.Maximum = update.total;
                    ProgressExecution.Value = update.completed;
                });

                TxtStatus.Text = "Executing script...";

                // Execute bulk script
                var startTime = DateTime.Now;
                _lastResults = await ScriptManager.ExecuteScriptBulkAsync(
                    TxtScriptContent.Text,
                    _targetComputers,
                    _username,
                    _password,
                    maxConcurrency,
                    _cancellationTokenSource.Token,
                    progress
                );

                var duration = DateTime.Now - startTime;

                // Display results
                GridResults.ItemsSource = _lastResults;

                // Update summary
                int successCount = _lastResults.Count(r => r.Success);
                int failCount = _lastResults.Count(r => !r.Success);

                TxtResultSummary.Text = $"{_lastResults.Count} results • ✅ {successCount} success • ❌ {failCount} failed • ⏱️ {duration.TotalSeconds:F1}s";
                TxtStatus.Text = $"✅ Execution complete • {successCount}/{_lastResults.Count} succeeded • {duration.TotalSeconds:F1}s";

                // Enable export buttons
                BtnExportCsv.IsEnabled = true;
                BtnExportTxt.IsEnabled = true;

                // Log completion
                LogManager.LogInfo($"[ScriptExecutor] Executed on {_targetComputers.Length} computers • {successCount} succeeded");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"❌ Execution failed: {ex.Message}";
                MessageBox.Show($"Script execution failed:\n\n{ex.Message}", "Execution Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.LogError("[ScriptExecutor] Execution failed", ex);
            }
            finally
            {
                // Re-enable UI
                BtnExecute.IsEnabled = true;
                BtnExecute.Content = "▶️ Execute Script";
                ProgressExecution.Visibility = Visibility.Collapsed;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Result selected - show full output
        /// </summary>
        private void GridResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridResults.SelectedItem is ScriptManager.ScriptExecutionResult result)
            {
                var details = new System.Text.StringBuilder();
                details.AppendLine($"Computer: {result.Hostname}");
                details.AppendLine($"Status: {result.StatusIcon} {result.StatusText}");
                details.AppendLine($"Duration: {result.Duration.TotalSeconds:F2}s");
                details.AppendLine($"Exit Code: {result.ExitCode}");
                details.AppendLine($"Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
                details.AppendLine();
                details.AppendLine("=== OUTPUT ===");
                details.AppendLine(string.IsNullOrEmpty(result.Output) ? "(No output)" : result.Output);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    details.AppendLine();
                    details.AppendLine("=== ERRORS ===");
                    details.AppendLine(result.Error);
                }

                TxtOutputDetails.Text = details.ToString();
            }
        }

        /// <summary>
        /// Export results to CSV
        /// </summary>
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
            {
                MessageBox.Show("No results to export.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Results to CSV",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ScriptResults_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                if (ScriptManager.ExportResultsToCsv(_lastResults, dialog.FileName))
                {
                    TxtStatus.Text = $"📋 Exported to: {dialog.FileName}";
                    MessageBox.Show($"Results exported successfully to:\n{dialog.FileName}",
                        "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to export results. Check logs for details.", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Export results to TXT
        /// </summary>
        private void BtnExportTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
            {
                MessageBox.Show("No results to export.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Results to TXT",
                Filter = "Text Files (*.txt)|*.txt",
                FileName = $"ScriptResults_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                if (ScriptManager.ExportResultsToTxt(_lastResults, dialog.FileName))
                {
                    TxtStatus.Text = $"📄 Exported to: {dialog.FileName}";
                    MessageBox.Show($"Results exported successfully to:\n{dialog.FileName}",
                        "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to export results. Check logs for details.", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
