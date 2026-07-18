using System.Windows.Threading;
using Scarlett.engines;

namespace Scarlett;

public static class RecognizerFactory
{
    public static RecognizerEngine CreateRecognizer(Settings settings, Dispatcher dispatcher)
    {
        Log.Print($"[Factory] Creating recognizer with engine: '{settings.Engine}'");

        RecognizerEngine engine = settings.Engine.ToUpperInvariant() switch
        {
            "WSR" => new WSRRecognizer(settings, dispatcher),
           // "VOSK" => new VoskRecognizer(settings, dispatcher),
            _ => throw new NotSupportedException($"Recognition engine '{settings.Engine}' is not supported. Available engines: WSR, VOSK")
        };

        Log.Print($"[Factory] Successfully created {engine.GetType().Name}");
        return engine;
    }
}
