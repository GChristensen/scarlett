using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Scarlett;

internal static class JsonHelper
{
    public static object? Deserialize(string json)
    {
        return ToObject(JToken.Parse(json));
    }

    private static object? ToObject(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                return token.Children<JProperty>()
                    .ToDictionary(prop => prop.Name,
                        prop => ToObject(prop.Value));

            case JTokenType.Array:
                return token.Select(ToObject).ToList();

            default:
                return ((JValue)token).Value;
        }
    }
}

public class Settings
{
    private const string JSON_FILE_PATH = "settings.json";

    private Dictionary<string, object>? _settings = new();
    private Dictionary<string, object>? _assistant = new();
    private Dictionary<string, object> _vars = new();
    private Dictionary<string, Dictionary<string, ActionNoun>> _actions = new();
    private List<string> _verbs = new();
    private Restrictions? _restrict;

    public bool DisplayErrors => (bool?)GetValue(_settings, "display_errors") ?? false;
    public string? AssistantName => (string?)GetValue(_assistant, "name");
    public bool EnableAssistantName => (bool?)GetValue(_assistant, "enable_name") ?? false;
    public double? MinConfidence => (double?)GetValue(_assistant, "min_confidence");

    public List<string> Verbs => _verbs;
    public Dictionary<string, Dictionary<string, ActionNoun>> NounsOf => _actions;
    public Restrictions? RestrictBy => _restrict;
    
    string? ReadTextFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }

            Log.Print($"File '{filePath}' not found.");
            return null;
        }
        catch (Exception ex)
        {
            Log.Print($"Error reading JSON file: {ex.Message}");
            return null;
        }
    }

    void SaveTextFile(string filePath, string jsonData)
    {
        try
        {
            File.WriteAllText(filePath, jsonData);
        }
        catch (Exception ex)
        {
            Log.Print($"Error saving JSON file: {ex.Message}");
        }
    }

    public void Load()
    {
        string? jsonData = ReadTextFile(JSON_FILE_PATH);

        if (jsonData != null)
        {
            try
            {
                _settings = JsonHelper.Deserialize(jsonData) as Dictionary<string, object>;
                
                if (_settings != null)
                {
                    _assistant = GetValue(_settings, "assistant") as Dictionary<string, object> ?? new();
                    _vars = GetValue(_settings, "vars") as Dictionary<string, object> ?? new();
                    
                    var actions = GetValue(_settings, "actions") as Dictionary<string, object> 
                                  ?? new();
                    
                    foreach (var (actionName, nounsObj) in actions)
                    {
                        var nouns = nounsObj as Dictionary<string, object> ?? new();
                        _actions[actionName] = CreateNouns(nouns);
                    }
                    
                    _verbs = (GetValue(_settings, "actions") as Dictionary<string, object>)?.Keys.ToList() ?? [];
                    
                    var restrictBy = GetValue(_settings, "restrict_by") as Dictionary<string, object> 
                                     ?? new();

                    _restrict = CreateRestrictions(restrictBy);
                    
                    SubstituteVars();
                }
            }
            catch (Exception e)
            {
                Log.Print(e);
                throw;
            }
        }
        else
        {
            throw new Exception("Error reading JSON file.");
        }
    }

    Dictionary<string, ActionNoun> CreateNouns(Dictionary<string, object> nouns)
    {
        Dictionary<string, ActionNoun> actionNouns = new();
                        
        foreach (var noun in nouns)
        {
            var nounName = noun.Key;
            var nounOptions = noun.Value as Dictionary<string, object>;

            nounOptions!.TryGetValue("action", out object? actionName);
            nounOptions.TryGetValue("description", out object? actionDescription);
            nounOptions.TryGetValue("confirm", out object? actionConfirm);
            nounOptions.TryGetValue("restricted", out object? actionRestrict);
            nounOptions.TryGetValue("disabled", out object? actionDisabled);
            nounOptions.TryGetValue("args", out object? actionArgs);

            if (actionName != null)
            {
                var actionNoun = new ActionNoun(
                    (actionName as string)!,
                    actionDescription as string,
                    actionConfirm as bool? ?? false,
                    actionRestrict as bool? ?? false,
                    actionDisabled as bool? ?? false,
                    actionArgs as Dictionary<string, object>);
                
                actionNouns[nounName] = actionNoun;
            }
        }

        return actionNouns;
    }

    Restrictions CreateRestrictions(Dictionary<string, object> restrictBy)
    {
        restrictBy!.TryGetValue("processes", out object? processesObj);
        
        var processes = (processesObj as List<object>)?.Select(p => p as string).ToList();

        return new Restrictions(
            processes
        );
    }

    void SubstituteVars()
    {
        foreach (var (_, nouns) in _actions)
        {
            foreach (var options in nouns.Values)
            {
                if (options.Args != null)
                {
                    SubstituteVars(options.Args);
                }
            }
        }
    }

    private object SubstituteVars(Dictionary<string, object> dic)
    {
        foreach (KeyValuePair<string, object> item in dic)
        {
            dic[item.Key] = SubstituteVar(item.Value);
        }

        return dic;
    }

    private object SubstituteVar(object val)
    {
        if (val is string && val.ToString()!.StartsWith("@@"))
        {
            var varName = val.ToString()!.Substring(2);
                                
            if (_vars.TryGetValue(varName, out var varVal))
            {
                return varVal;
            }
        }
        else if (val is List<object> valList)
        {
            return valList.Select(SubstituteVar).ToList();
        }
        else if (val is Dictionary<string, object> valDic)
        {
            return SubstituteVars(valDic);
        }

        return val;
    }

    public void Save()
    {
        SaveTextFile(JSON_FILE_PATH, JsonConvert.SerializeObject(_settings, Formatting.Indented));
    }

    private object? GetValue(Dictionary<string, object>? dic, string key)
    {
        if (dic == null || !dic.ContainsKey(key)) return null;
        
        return dic[key];
    }
}