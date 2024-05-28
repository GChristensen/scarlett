using System.Runtime.InteropServices;

namespace Scarlett.actions;

file class Args
{
    public string? Type;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("type", out object? typeObj);
        Type = (string?)typeObj;
    }
}

/// <summary>
/// Closes the foreground window.
/// args:
///   - type: the only value is "foreground", optional
/// </summary>
public class CloseWindowAction: IAction
{
    public string Name => "close-window";
    
    // Import the GetForegroundWindow function from the user32.dll
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("kernel32.dll")]
    public static extern uint GetLastError();
    
    private const uint WM_CLOSE = 0x0010;
    
    public Task Perform(Dictionary<string, object>? _)
    {
        IntPtr hWnd = GetForegroundWindow();
        
        if (hWnd == IntPtr.Zero)
        {
            Console.WriteLine("No foreground window found.");
            return Task.CompletedTask;
        }
        
        bool result = PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        
        if (result)
        {
            Console.WriteLine("Close message sent successfully.");
        }
        else
        {
            Console.WriteLine("Failed to send close message.");
        }
        
        return Task.CompletedTask;
    }

}