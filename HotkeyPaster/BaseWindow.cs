using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TalkKeys.Logging;

namespace TalkKeys
{
    /// <summary>
    /// Base window class providing common functionality for error handling and UI state management
    /// </summary>
    public abstract class BaseWindow : Window
    {
        protected readonly ILogger Logger;

        protected BaseWindow(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes an async operation with automatic UI state management and error handling
        /// </summary>
        protected async Task<T?> ExecuteWithUIFeedback<T>(
            Func<Task<T>> operation,
            string operationName,
            params Control[] controlsToDisable)
        {
            try
            {
                // Disable controls
                foreach (var control in controlsToDisable)
                {
                    control.IsEnabled = false;
                }

                // Execute operation
                return await operation();
            }
            catch (Exception ex)
            {
                HandleError(ex, operationName);
                return default;
            }
            finally
            {
                // Re-enable controls
                foreach (var control in controlsToDisable)
                {
                    control.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Executes an async operation with automatic UI state management and error handling (void return)
        /// </summary>
        protected async Task ExecuteWithUIFeedback(
            Func<Task> operation,
            string operationName,
            params Control[] controlsToDisable)
        {
            try
            {
                // Disable controls
                foreach (var control in controlsToDisable)
                {
                    control.IsEnabled = false;
                }

                // Execute operation
                await operation();
            }
            catch (Exception ex)
            {
                HandleError(ex, operationName);
            }
            finally
            {
                // Re-enable controls
                foreach (var control in controlsToDisable)
                {
                    control.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Handles errors in a consistent way across all windows
        /// </summary>
        protected virtual void HandleError(Exception ex, string operationName)
        {
            Logger.Log($"{operationName} failed: {ex.Message}");
            Logger.Log($"Stack trace: {ex.StackTrace}");
            
            MessageBox.Show(
                $"{operationName} failed:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>
        /// Disables multiple controls at once
        /// </summary>
        protected void DisableControls(params Control[] controls)
        {
            foreach (var control in controls)
            {
                control.IsEnabled = false;
            }
        }

        /// <summary>
        /// Enables multiple controls at once
        /// </summary>
        protected void EnableControls(params Control[] controls)
        {
            foreach (var control in controls)
            {
                control.IsEnabled = true;
            }
        }
    }
}
