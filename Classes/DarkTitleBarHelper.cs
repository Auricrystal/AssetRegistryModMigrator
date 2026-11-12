using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AssetRegistryModMigrator.Classes
{
    public static class DarkTitleBarHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static void ApplyDarkTitleBar(Window window)
        {
            if (window == null)
                return;

            // EnsureHandle() creates the HWND immediately before the window is ever painted on screen
            IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero)
                return;

            int useDarkMode = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
            }

            // Tint caption bar to #252526 on Windows 11
            int captionColorRef = 0x00262525;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColorRef, sizeof(int));

            // Force Windows to immediately repaint the non-client title bar frame
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static readonly DependencyProperty EnableDarkTitleBarProperty = DependencyProperty.RegisterAttached(
        "EnableDarkTitleBar",
        typeof(bool),
        typeof(DarkTitleBarHelper),
        new PropertyMetadata(false, OnEnableDarkTitleBarChanged));

        public static bool GetEnableDarkTitleBar(DependencyObject obj) =>
            (bool)obj.GetValue(EnableDarkTitleBarProperty);

        public static void SetEnableDarkTitleBar(DependencyObject obj, bool value) =>
            obj.SetValue(EnableDarkTitleBarProperty, value);

        private static void OnEnableDarkTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                ApplyDarkTitleBar(window);
            }
        }

    }
}
