using System.Windows.Threading;

namespace Scarlett;

using System.Reflection;
using Scarlett.actions;

public class ActionManager
{
    private readonly Settings _settings;
    private readonly Dispatcher? _dispatcher;
    
    private readonly Dictionary<string, IAction?> _registeredActions = new();
    private readonly Dictionary<string, ActionNoun> _phraseToAction = new();
    
    public Dictionary<string, IAction?> Actions => _registeredActions;

    public EventHandler<EventArgs>? ConfirmationBegin;
    public EventHandler<EventArgs>? ConfirmationEnd;
    
    public ActionManager(Settings settings)
    {
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;
        
        RegisterActions("Scarlett.actions");
        RegisterActions("Scarlett.actions.user");
        CollectPhrases(settings);
    }

    private bool RegisterAction(IAction action)
    {
        _registeredActions.Add(action.Name, action);
        Log.Print($"Registered action: '{action.Name}'.");

        return true;
    }

    private void RegisterActions(string targetNamespace)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && t.Namespace == targetNamespace);
                
        foreach (var type in types)
        {
            var IActionType = typeof(IAction);
            
            if (IActionType.IsAssignableFrom(type))
            {
                IAction? action = (IAction?)Activator.CreateInstance(type);
                
                if (action != null && action.Name != "template-action")
                    RegisterAction(action);
            }
        }
    }

    private void CollectPhrases(Settings settings)
    {
        foreach (var verb in settings.Verbs)
        {
            foreach (var (noun, options) in settings.NounsOf[verb])
            {
                string phrase = MakePhrase(verb, noun);
                _phraseToAction[phrase] = options;
            }
        }
    }

    private string MakePhrase(string verb, string noun)
    {
        return $"{verb.Trim().ToLower()} {noun.Trim().ToLower()}";
    }

    public void TryExecute(string verb, string noun)
    {
        string phrase = MakePhrase(verb, noun);
        _phraseToAction.TryGetValue(phrase, out ActionNoun? nounOptions);

        if (nounOptions != null)
        {
            try
            {
                var action = Actions.GetValueOrDefault(nounOptions.Action, null);

                if (action != null)
                {
                    Task.Factory.StartNew(async () => await PerformAction(phrase, action, nounOptions));
                }
                else
                {
                    Log.Print("Action not found: " + nounOptions.Action);
                }

            }
            catch (Exception exc)
            {
                Log.Error(exc);
            }
        }
        else
        {
            Log.Print("Don't know how to execute: " + phrase);
        }
    }
    
    private bool ConfirmAction(string phrase, ActionNoun options)
    {
        if (options.Confirm)
        {
            try
            {
                _dispatcher?.Invoke(() => ConfirmationBegin?.Invoke(this, EventArgs.Empty));
                
                return Confirmation.Confirm(phrase);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return false;
            }
            finally
            {
                _dispatcher?.Invoke(() => ConfirmationEnd?.Invoke(this, EventArgs.Empty));
            }
        }

        return true;
    }

    private async Task PerformAction(string phrase, IAction action, ActionNoun options)
    {
        try
        {
            if (ConfirmAction(phrase, options) && CanPerformAction(action, options))
            {
                await action.Perform(options.Args);
            }
        }
        catch (Exception e) {
            Log.Error(e);   
        }
    }

    private bool CanPerformAction(IAction _, ActionNoun options)
    {
        bool result = !options.Disabled;
        
        if (options.Disabled)
            Log.Print("The action is disabled.");
        
        if (options.Restrict)
        {
            if (_settings.RestrictBy?.Processes != null)
            {
                foreach (var process in _settings.RestrictBy.Processes)
                {
                    var normProcess = (process as string)?.Replace("/", "\\");

                    if (Utils.IsProcessRunning(normProcess))
                    {
                        result = false;
                        break;
                    }
                }
            }
        }

        return result;
    }
}