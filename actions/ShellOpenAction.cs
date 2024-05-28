using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Scarlett.actions;

file class Args
{
    public string? File;
    public int? Menu;
    public int? MenuDelay;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("file", out object? fileObj);
        args.TryGetValue("menu", out object? menuObj);
        args.TryGetValue("menu_delay", out object? menuDelayObj);
        
        File = fileObj as string;
        Menu = (int?)(long?)menuObj;
        MenuDelay = (int?)(long?)menuDelayObj;
    }
}

/// <summary>
/// Opens the file in the associated program
/// args:
///   - file: path to the file to open
/// </summary>
public class ShellOpenAction: IAction
{
    public string Name => "shell-open";
    
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const int MOUSEEVENTF_RIGHTUP = 0x10;
    private const int KEYEVENTF_KEYDOWN = 0x0;
    private const int KEYEVENTF_KEYUP = 0x2;

    public void SimulateRightClickAndSelectMenuItem(int menuItem)
    {
        // Simulate right click at the 0, 0, it is assumed that the application window is fullscreen
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        Thread.Sleep(500); // Allow time for the context menu to appear
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);

        // Send several down key events to navigate to the desired menu item
        for (int i = 0; i < menuItem; i++)
        {
            keybd_event((byte)Keys.Down, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(100); // Add a small delay between key presses
        }
        
        // Press enter to select the menu item
        keybd_event((byte)Keys.Enter, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        keybd_event((byte)Keys.Enter, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
    
    public Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return Task.CompletedTask;

        var args = new Args(rawArgs);

        if (args.File != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(args.File) { UseShellExecute = true, Verb = "Open" });
                
                if (args.MenuDelay != null)
                    Thread.Sleep((int)args.MenuDelay);

                if (args.Menu != null)
                {
                    SimulateRightClickAndSelectMenuItem((int)args.Menu);
                }
            }
            catch (Exception e)
            {
                Log.Print(e);
            }
        }

        return Task.CompletedTask;
    }
}