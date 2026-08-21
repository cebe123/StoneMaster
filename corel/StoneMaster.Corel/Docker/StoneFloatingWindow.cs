using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace StoneMaster.Corel.Docker
{
    public sealed class StoneFloatingWindow : Window
    {
        private static StoneFloatingWindow _instance;

        public StoneFloatingWindow(object app)
        {
            Initialize(app);
        }

        private void Initialize(object app)
        {
            Title = "StoneMaster";
            Width = 430;
            Height = 820;
            MinWidth = 360;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content = new StoneDocker(app, false);

            var owner = Process.GetCurrentProcess().MainWindowHandle;
            if (owner != IntPtr.Zero)
                new WindowInteropHelper(this).Owner = owner;

        }

        public static StoneFloatingWindow Open(object app)
        {
            if (_instance != null)
            {
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
                _instance.Activate();
                _instance.Topmost = true;
                _instance.Topmost = false;
                return _instance;
            }

            _instance = new StoneFloatingWindow(app);
            _instance.Closed += (_, __) => _instance = null;
            _instance.Show();
            return _instance;
        }
    }
}
