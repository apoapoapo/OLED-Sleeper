using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Native;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    /// <summary>
    /// Manages blackout overlay windows for monitors asynchronously in a WPF application.
    /// Provides creation, display, and removal of overlay windows, and tracks overlay window handles.
    /// </summary>
    public class MonitorBlackoutService : IMonitorBlackoutService
    {
        private readonly Dictionary<string, Window> _overlayWindows = new();
        private readonly HashSet<nint> _overlayHandles = new();

        /// <summary>
        /// Asynchronously shows a blackout overlay on the specified monitor.
        /// </summary>
        /// <param name="hardwareId">The unique hardware ID of the monitor.</param>
        /// <param name="bounds">The bounds of the monitor in screen coordinates.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ShowBlackoutOverlayAsync(string hardwareId, Rect bounds)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_overlayWindows.ContainsKey(hardwareId)) return;

                var overlay = CreateOverlayWindow();
                overlay.Show();

                nint hwnd = new WindowInteropHelper(overlay).Handle;
                if (hwnd != nint.Zero)
                {
                    ApplyNoActivateStyle(hwnd);
                    PositionOverlayToMonitor(hwnd, bounds);
                    _overlayHandles.Add(hwnd);
                }

                _overlayWindows[hardwareId] = overlay;
            });
        }

        /// <summary>
        /// Asynchronously hides the blackout overlay for the specified monitor.
        /// </summary>
        /// <param name="hardwareId">The unique hardware ID of the monitor.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task HideBlackoutOverlayAsync(string hardwareId)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_overlayWindows.TryGetValue(hardwareId, out var overlay))
                {
                    RemoveOverlayHandle(overlay);
                    overlay.Close();
                    _overlayWindows.Remove(hardwareId);
                }
            });
        }

        /// <summary>
        /// Determines whether the specified window handle belongs to an overlay window.
        /// </summary>
        /// <param name="windowHandle">The window handle to check.</param>
        /// <returns>True if the handle is an overlay window; otherwise, false.</returns>
        public bool IsOverlayWindow(nint windowHandle) =>
            windowHandle != nint.Zero && _overlayHandles.Contains(windowHandle);

        #region Private Helpers

        /// <summary>
        /// Creates a new overlay window.
        /// </summary>
        /// <returns>A configured <see cref="Window"/> instance.</returns>
        private static Window CreateOverlayWindow() =>
            new()
            {
                Cursor = System.Windows.Input.Cursors.None,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Black,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

        /// <summary>
        /// Positions the overlay window to exactly cover the monitor using physical screen coordinates.
        /// </summary>
        /// <param name="hwnd">The window handle.</param>
        /// <param name="bounds">The monitor bounds in physical screen coordinates.</param>
        private static void PositionOverlayToMonitor(nint hwnd, Rect bounds)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                (int)bounds.Left,
                (int)bounds.Top,
                (int)bounds.Width,
                (int)bounds.Height,
                NativeMethods.SWP_NOACTIVATE);
        }

        /// <summary>
        /// Applies the WS_EX_NOACTIVATE style to prevent the overlay from stealing focus.
        /// </summary>
        /// <param name="hwnd">The window handle.</param>
        private static void ApplyNoActivateStyle(nint hwnd)
        {
            nint extendedStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE,
                new nint(extendedStyle.ToInt64() | NativeMethods.WS_EX_NOACTIVATE));
        }

        /// <summary>
        /// Removes the overlay window handle from tracking before closing.
        /// </summary>
        /// <param name="overlay">The overlay window.</param>
        private void RemoveOverlayHandle(Window overlay)
        {
            nint hwnd = new WindowInteropHelper(overlay).Handle;
            if (hwnd != nint.Zero)
            {
                _overlayHandles.Remove(hwnd);
            }
        }

        #endregion Private Helpers
    }
}