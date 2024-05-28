using System.Collections;
using System.Diagnostics;

namespace Scarlett.actions;

file class Args
{
    public string? Command;
    public List<string>? Arguments;
    public bool? Maximize;
    public int? MaximizeDelay;
    
    public Args(Dictionary<string, object> args)
    {
        args.TryGetValue("cmd", out object? cmdObj);
        args.TryGetValue("args", out object? argsObj);
        args.TryGetValue("maximize", out object? maximizeObj);
        args.TryGetValue("maximize_delay", out object? maximizeDelayObj);

        Command = cmdObj as string;
        Maximize = maximizeObj as bool?;
        MaximizeDelay = (int?)(long?)maximizeDelayObj;

        if (argsObj is List<object> arguments)
        {
            Arguments = new List<string>();
            
            foreach (var value in arguments)
            {
                Arguments.Add((string)value);
            }
        }
    }
}

/// <summary>
/// Runs the specified program with the given arguments.
/// args:
///   - cmd: path to the executable file
///   - args: list of sting arguments passed to the executable, optional
///   - maximize: boolean, indicating that the program window should be maximized, optional
///   - maximize_delay: wait time in ms before the maximization attempt, 5s is the default, optional
/// </summary>
public class RunAction: IAction
{
    public string Name => "run";
    
    public Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return Task.CompletedTask;

        var args = new Args(rawArgs);
        
        if (args.Command != null)
        {
            Process process = new Process();
            process.StartInfo.FileName = args.Command;
            
            Log.Print("Executing command: " + args.Command);
            
            if (args.Arguments is { Count: > 0 })
            {
                String arguments = "";

                for (int i = 0; i < args.Arguments.Count; ++i)
                {
                    arguments += "\"" + args.Arguments[i] + "\" ";
                }

                process.StartInfo.Arguments = arguments;
                
                Log.Print("With arguments: " + arguments);
            }

            try
            {
                process.Start();
                
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
