namespace Scarlett.actions;

public interface IAction
{
    public string Name { get; }
    
    Task Perform(Dictionary<string, object>? rawArgs);
}