namespace Scarlett.actions.user;

file class Args
{
    public readonly string? Param;
    
    public Args(Dictionary<string, object> rawArgs)
    {
        rawArgs.TryGetValue("param", out object? paramObj);
        Param = paramObj as string;
    }
}

/// <summary>
/// Copy this action template to create your own action.
/// args:
///   - param: sample string argument
/// </summary>
public class TemplateAction : IAction
{
    public string Name => "template-action";

    public /* async */ Task Perform(Dictionary<string, object>? rawArgs)
    {
        if (rawArgs == null) return Task.CompletedTask;

        var args = new Args(rawArgs);

        if (args.Param != null)
        {
            // ...
        }

        return Task.CompletedTask;
    }
}
