using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Scarlett;

public class Utils
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    private const int SW_MAXIMIZE = 3;
    private const int MAXIMIZE_DELAY_MS = 6000;
    
    public static void MaximizeForegroundWindow(int maximizeDelay = MAXIMIZE_DELAY_MS)
    {
        Thread.Sleep(maximizeDelay);
        IntPtr foregroundWindow = GetForegroundWindow();
        ShowWindow(foregroundWindow, SW_MAXIMIZE);
    }
    
    public static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
            new DispatcherOperationCallback(
                delegate (object f)
                {
                    ((DispatcherFrame)f).Continue = false;
                    return null;
                }),frame);
        Dispatcher.PushFrame(frame);
    }
    
    public static bool IsProcessRunning(string? processPath)
    {
        if (processPath == null) return true;
        
        string processName = Path.GetFileNameWithoutExtension(processPath);
        Process[] processes = Process.GetProcessesByName(processName);
        
        foreach (Process p in processes)
        {
            if (p.MainModule?.FileName?.Equals(processPath, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                return true;
            }
        }
        
        return false;
    }
    
    public static bool IsAlreadyRunning()
    {
        Process thisProc = Process.GetCurrentProcess();

        if (Process.GetProcessesByName(thisProc.ProcessName).Length > 1)
        {
            return true;
        }

        return false;
    }
}