namespace Scarlett.actions;

file class Args
{
    public string? Keys;
    public int? Repeat;
    public int? RepeatDelay;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("keys", out object? keysObj);
        args.TryGetValue("repeat", out object? repeatObj);
        args.TryGetValue("repeat_delay", out object? repeatDelayObj);

        Keys = keysObj as string;
        Repeat = (int?)(long?)repeatObj;
        RepeatDelay = (int?)(long?)repeatDelayObj;
    }
}

/// <summary>
/// Simulates user input with SendKeys.SendWait.
/// args:
///   - keys: keys to send, string
///   - repeat: repeat the keystrokes sent in the keys parameter, an integer larger than 1, optional
///   - repeat_delay: delay between repeats in ms, integer, optional
/// </summary>
public class SimulateInputAction: IAction
{
    public string Name => "simulate-input";
    
    public Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return Task.CompletedTask;

        var args = new Args(rawArgs);
        
        if (args.Keys != null)
        {
            SendKeys.SendWait(args.Keys);
            Log.Print("Sent keys: " + args.Keys);

            if (args.Repeat is > 1)
            {
                for (long i = 1; i < args.Repeat; ++i)
                {
                    if (args.RepeatDelay != null)
                        Thread.Sleep((int)args.RepeatDelay);
                        
                    SendKeys.SendWait(args.Keys);
                    Log.Print("Sent keys: " + args.Keys);
                }
            }
        }
        
        return Task.CompletedTask;
    }
}