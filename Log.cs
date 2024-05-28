using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Scarlett;

public class Log
{
    private const string LEVEL_INFO = "INFO";
    private const string LEVEL_ERROR = "ERROR";
    private const string DEFAULT_LEVEL = LEVEL_INFO;
    
    private static readonly string _logFileName = "scarlett.log";
    private static readonly string _logFilePath = Path.Combine(Directory.GetCurrentDirectory(), _logFileName);
    
    public static bool UseConsole { get; set; } = false;
    public static bool DisplayErrors { get; set; } = false;

    
    public static void Init()
    {
        if (File.Exists(_logFilePath))
        {
            try
            {
                File.WriteAllText(_logFilePath, "");
            }
            catch (Exception e)
            {
                if (DisplayErrors)
                    DisplayError(e.ToString());
            }
        }
    }
    
    static void LogToFile(string filePath, string level, string? message)
    {
        string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} - {message}";

        try
        {
            File.AppendAllText(filePath, logMessage + Environment.NewLine);
        }
        catch (Exception e)
        {
            if (DisplayErrors)
                DisplayError(e.ToString());
        }
    }

    static void Write(string? msg, string level = DEFAULT_LEVEL)
    {
        if (UseConsole)
            Console.WriteLine(msg);
        else
            LogToFile(_logFilePath, level, msg);
    }
    
    static void Write(object? o, string level = DEFAULT_LEVEL)
    {
        Write(o?.ToString(), level);
    }
    
    public static void Print(string? s)
    {
        Write(s);
    }
    
    public static void Print(object? o)
    {
        if (o is Dictionary<string, object> dic)
        {
            Write("{");
            dic.Select(i => $"  {i.Key}: {i.Value}").ToList().ForEach(s => Write(s));
            Write("}");
        }
        else if (o is List<object> lst)
        {
            Write("[");
            lst.Select(i => $"  {i}").ToList().ForEach(Console.WriteLine);
            Write("]");
        }
        else
        {
            Write(o);
        }
    }
    
    public static void Error(string? s)
    {
        Write(s, LEVEL_ERROR);

        if (DisplayErrors && s != null)
            DisplayError(s);
    }

    public static void Error(object? o)
    {
        Write(o, LEVEL_ERROR);
        
        if (DisplayErrors && o != null)
            DisplayError(o.ToString()!);
    }

    private static void DisplayError(string e)
    {
        MessageBox.Show(e, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

