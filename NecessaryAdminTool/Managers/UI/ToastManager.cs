using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NecessaryAdminTool.Models.UI;

namespace NecessaryAdminTool.Managers.UI
{
    /// <summary>
    /// Centralized toast notification manager
    /// TAG: #TOAST_MANAGER #MODULAR #UI_MANAGER #NON_BLOCKING_FEEDBACK
    /// Research: https://blog.logrocket.com/ux-design/toast-notifications/
    /// </summary>
    public static class ToastManager
    {
        private static Panel _toastContainer;
        private const int MAX_TOASTS = 5;

        /// <summary>
        /// Initialize the toast manager with a container panel
        /// TAG: #INITIALIZATION
        /// </summary>
        public static void Initialize(Panel container)
        {
            _toastContainer = container;
            LogManager.LogInfo("ToastManager initialized");
        }

        /// <summary>
        /// Show a success toast (green)
        /// TAG: #SUCCESS_FEEDBACK
        /// </summary>
        public static void ShowSuccess(string message, string actionText = null, Action actionCallback = null)
        {
            var toast = new ToastNotification(message, ToastType.Success)
            {
                ActionText = actionText,
                ActionCallback = actionCallback
            };
            ShowToast(toast);
            LogManager.LogInfo($"Toast(Success): {message}");
        }

        /// <summary>
        /// Show an info toast (blue)
        /// TAG: #INFO_FEEDBACK
        /// </summary>
        public static void ShowInfo(string message, string actionText = null, Action actionCallback = null)
        {
            var toast = new ToastNotification(message, ToastType.Info)
            {
                ActionText = actionText,
                ActionCallback = actionCallback
            };
            ShowToast(toast);
            LogManager.LogInfo($"Toast(Info): {message}");
        }

        /// <summary>
        /// Show a warning toast (amber)
        /// TAG: #WARNING_FEEDBACK
        /// </summary>
        public static void ShowWarning(string message, string actionText = null, Action actionCallback = null)
        {
            var toast = new ToastNotification(message, ToastType.Warning)
            {
                ActionText = actionText,
                ActionCallback = actionCallback
            };
            ShowToast(toast);
            LogManager.LogWarning($"Toast(Warning): {message}");
        }

        /// <summary>
        /// Show an error toast (red)
        /// TAG: #ERROR_FEEDBACK
        /// </summary>
        public static void ShowError(string message, string actionText = null, Action actionCallback = null)
        {
            var toast = new ToastNotification(message, ToastType.Error)
            {
                ActionText = actionText,
                ActionCallback = actionCallback
            };
            ShowToast(toast);
            LogManager.LogError($"Toast(Error): {message}", null);
        }

        /// <summary>
        /// Internal method to show toast with animation
        /// TAG: #ANIMATION #SLIDE_IN
        /// </summary>
        private static void ShowToast(ToastNotification toastModel)
        {
            if (_toastContainer == null)
            {
                LogManager.LogWarning("ToastManager not initialized - cannot show toast");
                return;
            }

            // Limit concurrent toasts
            if (_toastContainer.Children.Count >= MAX_TOASTS)
            {
                _toastContainer.Children.RemoveAt(0); // Remove oldest
            }

            // Create toast UI
            var toastElement = CreateToastElement(toastModel);

            // Add to container
            _toastContainer.Children.Add(toastElement);

            // Slide in animation
            AnimateSlideIn(toastElement);

            // Auto-dismiss timer
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(toastModel.Duration)
            };

            timer.Tick += (s, e) =>
            {
                DismissToast(toastElement);
                timer.Stop();
            };

            timer.Start();
        }

        /// <summary>
        /// Create toast visual element
        /// TAG: #UI_CREATION #FLUENT_DESIGN
        /// </summary>
        private static Border CreateToastElement(ToastNotification toast)
        {
            // Get semantic color based on type
            Color borderColor = toast.Type switch
            {
                ToastType.Success => Color.FromRgb(16, 185, 129),   // #10B981
                ToastType.Warning => Color.FromRgb(245, 158, 11),    // #F59E0B
                ToastType.Error => Color.FromRgb(239, 68, 68),       // #EF4444
                _ => Color.FromRgb(59, 130, 246)                     // #3B82F6 (Info)
            };

            string icon = toast.Type switch
            {
                ToastType.Success => "✓",
                ToastType.Warning => "⚠",
                ToastType.Error => "✕",
                _ => "ℹ"
            };

            // Container border
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(2, 0, 0, 0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12, 16, 12),
                MinWidth = 320,
                MaxWidth = 480,
                Margin = new Thickness(0, 0, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Grid layout
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (toast.HasAction)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon
            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 18,
                Foreground = new SolidColorBrush(borderColor),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            // Message
            var messageText = new TextBlock
            {
                Text = toast.Message,
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(messageText, 1);
            grid.Children.Add(messageText);

            // Action button (if provided)
            if (toast.HasAction)
            {
                var actionButton = new Button
                {
                    Content = toast.ActionText,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 4, 12, 4),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(12, 0, 0, 0)
                };

                actionButton.Click += (s, e) =>
                {
                    toast.ActionCallback?.Invoke();
                    DismissToast(border);
                };

                Grid.SetColumn(actionButton, 2);
                grid.Children.Add(actionButton);
            }

            border.Child = grid;
            return border;
        }

        /// <summary>
        /// Animate toast sliding in from right
        /// TAG: #ANIMATION #UX_POLISH
        /// </summary>
        private static void AnimateSlideIn(UIElement element)
        {
            var slideIn = new ThicknessAnimation
            {
                From = new Thickness(400, 0, -400, 0),
                To = new Thickness(0),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(FrameworkElement.MarginProperty, slideIn);
        }

        /// <summary>
        /// Dismiss toast with fade out animation
        /// TAG: #ANIMATION #CLEANUP
        /// </summary>
        private static void DismissToast(UIElement element)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            fadeOut.Completed += (s, e) =>
            {
                _toastContainer?.Children.Remove(element);
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
