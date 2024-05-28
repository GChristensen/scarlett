using System.Diagnostics;

namespace Scarlett.actions;

file class Args
{
    public string? URL;
    public bool? Maximize;
    public int? MaximizeDelay;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("url", out object? urlObj);
        args.TryGetValue("maximize", out object? maximizeObj);
        args.TryGetValue("maximize_delay", out object? maximizeDelayObj);

        URL = urlObj as string;
        Maximize = maximizeObj as bool?;
        MaximizeDelay = (int?)(long?)maximizeDelayObj;
    }
}

/// <summary>
/// Opens the specified URL in the default browser.
/// args:
///   - url: the URL to be opened
///   - maximize: boolean, indicating that the browser window should be maximized, optional
///   - maximize_delay: wait time in ms before the maximization attempt, 5s is the default, optional
/// </summary>
public class URLAction: IAction
{
    public string Name => "url";
    
    public Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return Task.CompletedTask;

        var args = new Args(rawArgs);
        
        if (args.URL != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(args.URL) { UseShellExecute = true, Verb = "Open" });

                if (args.Maximize != null && (bool)args.Maximize)
                {
                    int maximizeDelay = args.MaximizeDelay ?? 5000;
                    Utils.MaximizeForegroundWindow(maximizeDelay);
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