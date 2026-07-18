using System.Diagnostics;
using System.IO;
using YamlDotNet.Serialization;

namespace Scarlett;

internal static class YamlHelper
{
    public static object? Deserialize(string yaml)
    {
        // Use a typed deserializer to get proper type handling
        var deserializer = new DeserializerBuilder()
            .Build();

        var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yaml);
        return ConvertToNestedDictionaries(yamlObject);
    }

    private static object? ConvertToNestedDictionaries(object? obj)
    {
        if (obj == null)
            return null;

        // Handle dictionaries - convert Dictionary<object, object> to Dictionary<string, object>
        if (obj is Dictionary<object, object> dictObjObj)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dictObjObj)
            {
                var key = kvp.Key?.ToString() ?? string.Empty;
                result[key] = ConvertToNestedDictionaries(kvp.Value) ?? new object();
            }
            return result;
        }

        // Handle already-typed string dictionaries
        if (obj is Dictionary<string, object> dictStrObj)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dictStrObj)
            {
                result[kvp.Key] = ConvertToNestedDictionaries(kvp.Value) ?? new object();
            }
            return result;
        }

        // If it's a List, recursively convert its items
        if (obj is List<object> list)
        {
            return list.Select(ConvertToNestedDictionaries).ToList();
        }

        // Handle string conversions to proper types
        if (obj is string str)
        {
            // Try to parse as boolean
            if (bool.TryParse(str, out bool boolValue))
                return boolValue;

            // Try to parse as integer
            if (int.TryParse(str, out int intValue))
                return intValue;

            // Try to parse as long
            if (long.TryParse(str, out long longValue))
                return longValue;

            // Try to parse as double
            if (double.TryParse(str, out double doubleValue))
                return doubleValue;

            // Return as string if no conversion worked
            return str;
        }

        // For primitive types (including booleans, ints, doubles), return as-is
        return obj;
    }
}

public class Settings
{
    private const string YAML_FILE_PATH = "settings.yaml";

    private Dictionary<string, object>? _settings = new();
    private Dictionary<string, object>? _assistant = new();
    private Dictionary<string, object> _vars = new();
    private Dictionary<string, Dictionary<string, ActionNoun>> _actions = new();
    private List<string> _verbs = new();
    private Restrictions? _restrict;

    public bool DisplayErrors => GetBoolValue(_settings, "display_errors", false);
    public string? AssistantName => (string?)GetValue(_assistant, "name");
    public bool EnableAssistantName => GetBoolValue(_assistant, "enable_name", false);
    public double? MinConfidence => GetDoubleValue(_assistant, "min_confidence");
    public string Engine => (string?)GetValue(_assistant, "engine") ?? "WSR";
    public string VoskModelPath => (string?)GetValue(_assistant, "vosk_model_path") ?? "models/vosk-model-en";

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
            Log.Print($"Error reading YAML file: {ex.Message}");
            return null;
        }
    }

    void SaveTextFile(string filePath, string yamlData)
    {
        try
        {
            File.WriteAllText(filePath, yamlData);
        }
        catch (Exception ex)
        {
            Log.Print($"Error saving YAML file: {ex.Message}");
        }
    }

    public void Load()
    {
        Log.Print($"[Settings] Loading settings from: {YAML_FILE_PATH}");
        string? yamlData = ReadTextFile(YAML_FILE_PATH);

        if (yamlData != null)
        {
            try
            {
                Log.Print("[Settings] Deserializing YAML...");
                _settings = YamlHelper.Deserialize(yamlData) as Dictionary<string, object>;

                if (_settings != null)
                {
                    Log.Print($"[Settings] Settings loaded. Keys: {string.Join(", ", _settings.Keys)}");

                    _assistant = GetValue(_settings, "assistant") as Dictionary<string, object> ?? new();
                    Log.Print($"[Settings] Assistant settings loaded: {_assistant.Count} properties");

                    // Debug: Log the types of assistant properties
                    foreach (var kvp in _assistant)
                    {
                        Log.Print($"[Settings] Assistant['{kvp.Key}'] = {kvp.Value} (Type: {kvp.Value?.GetType().Name ?? "null"})");
                    }

                    _vars = GetValue(_settings, "vars") as Dictionary<string, object> ?? new();
                    Log.Print($"[Settings] Vars loaded: {_vars.Count} variables");

                    var actions = GetValue(_settings, "actions") as Dictionary<string, object>
                                  ?? new();
                    Log.Print($"[Settings] Actions loaded: {actions.Count} verbs found");
                    Log.Print($"[Settings] Action keys: {string.Join(", ", actions.Keys)}");

                    foreach (var (actionName, nounsObj) in actions)
                    {
                        var nouns = nounsObj as Dictionary<string, object> ?? new();
                        _actions[actionName] = CreateNouns(nouns);
                        Log.Print($"[Settings] Processed verb '{actionName}' with {_actions[actionName].Count} nouns");
                    }

                    _verbs = (GetValue(_settings, "actions") as Dictionary<string, object>)?.Keys.ToList() ?? [];
                    Log.Print($"[Settings] Total verbs: {_verbs.Count} - {string.Join(", ", _verbs)}");

                    var restrictBy = GetValue(_settings, "restrict_by") as Dictionary<string, object>
                                     ?? new();

                    _restrict = CreateRestrictions(restrictBy);

                    SubstituteVars();
                    Log.Print("[Settings] Settings loaded successfully.");
                }
                else
                {
                    Log.Print("[Settings] ERROR: Settings deserialization returned null");
                }
            }
            catch (Exception e)
            {
                Log.Print("[Settings] ERROR during load:");
                Log.Print(e);
                throw;
            }
        }
        else
        {
            throw new Exception("Error reading YAML file.");
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
                    ParseBool(actionConfirm, false),
                    ParseBool(actionRestrict, false),
                    ParseBool(actionDisabled, false),
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
        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(_settings);
        SaveTextFile(YAML_FILE_PATH, yaml);
    }

    private object? GetValue(Dictionary<string, object>? dic, string key)
    {
        if (dic == null || !dic.ContainsKey(key)) return null;

        return dic[key];
    }

    private bool GetBoolValue(Dictionary<string, object>? dic, string key, bool defaultValue)
    {
        var value = GetValue(dic, key);
        return ParseBool(value, defaultValue);
    }

    private bool ParseBool(object? value, bool defaultValue)
    {
        if (value == null) return defaultValue;

        // Handle if it's already a boolean
        if (value is bool boolValue)
            return boolValue;

        // Handle if it's a string representation of boolean
        if (value is string strValue && bool.TryParse(strValue, out bool parsedBool))
            return parsedBool;

        return defaultValue;
    }

    private double? GetDoubleValue(Dictionary<string, object>? dic, string key)
    {
        var value = GetValue(dic, key);
        if (value == null) return null;

        // Handle if it's already a double or numeric type
        if (value is double doubleValue)
            return doubleValue;

        if (value is int intValue)
            return intValue;

        if (value is float floatValue)
            return floatValue;

        // Handle if it's a string representation of a number
        if (value is string strValue && double.TryParse(strValue, out double parsedDouble))
            return parsedDouble;

        return null;
    }
}