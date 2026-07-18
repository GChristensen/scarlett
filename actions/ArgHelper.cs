namespace Scarlett.actions;

internal static class ArgHelper
{
    public static int? ToInt(object? value)
    {
        if (value == null) return null;
        if (value is int intValue) return intValue;
        if (value is long longValue) return (int)longValue;
        if (value is double doubleValue) return (int)doubleValue;
        if (value is string strValue && int.TryParse(strValue, out int parsed))
            return parsed;
        return null;
    }

    public static bool? ToBool(object? value)
    {
        if (value == null) return null;
        if (value is bool boolValue) return boolValue;
        if (value is string strValue && bool.TryParse(strValue, out bool parsed))
            return parsed;
        return null;
    }
}
