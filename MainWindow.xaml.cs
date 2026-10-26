using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinSnake;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int DesiredClientWidth = 1180;
    private const int DesiredClientHeight = 1010;
    private const int MinimumClientWidth = 860;
    private const int MinimumClientHeight = 700;

    private double _dpiScale = 1.0;
    private bool _isClampingSize;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        ResizeAndCenter();
        AppWindow.Changed += OnAppWindowChanged;

        // Pause a running round whenever the window is no longer active.
        Activated += OnWindowActivated;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange || _isClampingSize)
        {
            return;
        }

        int minimumWidth = (int)Math.Round(MinimumClientWidth * _dpiScale);
        int minimumHeight = (int)Math.Round(MinimumClientHeight * _dpiScale);
        SizeInt32 size = sender.ClientSize;

        if (size.Width >= minimumWidth && size.Height >= minimumHeight)
        {
            return;
        }

        _isClampingSize = true;
        try
        {
            sender.ResizeClient(new SizeInt32(
                Math.Max(size.Width, minimumWidth),
                Math.Max(size.Height, minimumHeight)));
        }
        finally
        {
            _isClampingSize = false;
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated
            && RootFrame.Content is MainPage page)
        {
            page.PauseForFocusLoss();
        }
    }

    /// <summary>Clamps the window to the work area, centering it on the primary display.</summary>
    private void ResizeAndCenter()
    {
        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;

        // AppWindow sizes are physical pixels, so scale the DIP target by the
        // window's DPI to keep the UI the same apparent size on every display.
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _dpiScale = Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);

        int width = Math.Min((int)Math.Round(DesiredClientWidth * _dpiScale), workArea.Width - 80);
        int height = Math.Min((int)Math.Round(DesiredClientHeight * _dpiScale), workArea.Height - 80);

        AppWindow.ResizeClient(new SizeInt32(width, height));

        SizeInt32 windowSize = AppWindow.Size;
        int x = workArea.X + Math.Max(0, (workArea.Width - windowSize.Width) / 2);
        int y = workArea.Y + Math.Max(0, (workArea.Height - windowSize.Height) / 2);
        AppWindow.Move(new PointInt32(x, y));
    }
}


