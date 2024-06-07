using System.Runtime.CompilerServices;
using System.Speech.Recognition;
using Scarlett.actions;

namespace Scarlett;

public class Recognizer
{
    private Settings _settings;
    private ActionManager _actionManager;
    private SpeechRecognitionEngine? _engine;
    private bool _recognitionCompleted = true;
    public event EventHandler? StateChanged;
    
    public bool IsPaused => _engine == null;
    public bool IsListening => _engine?.AudioState != AudioState.Stopped;
    
    public Recognizer(Settings settings)
    {
        _settings = settings;
        _actionManager = new ActionManager(settings);

        _actionManager.ConfirmationBegin += OnConfirmationBegin;
        _actionManager.ConfirmationEnd += OnConfirmationEnd;
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Pause()
    {
        if (!IsPaused)
        {
            _engine?.RecognizeAsyncCancel();
            Log.Print("Cancelled recognition...");
            _engine?.Dispose();
            _engine = null;
            
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Resume()
    {
        if (!IsPaused) return;
        
        //Thread.Sleep(1500);
        
        _engine = new SpeechRecognitionEngine();
        
        foreach (var verb in _settings.Verbs)
        {
            var actionVerb = verb;
            
            if (_settings is { AssistantName: not null, EnableAssistantName: true })
                actionVerb = _settings.AssistantName + " " + verb;
            
            GrammarBuilder grammarBuilder = new GrammarBuilder();
            grammarBuilder.Append(actionVerb);
        
            Choices choices = new Choices();
            var nouns = _settings.NounsOf[verb];
            
            foreach (var action in nouns)
            {
                choices.Add(action.Key);
            }

            if (nouns.Count > 0)
            {
                grammarBuilder.Append(choices);
                _engine.LoadGrammar(new Grammar(grammarBuilder));
            }
        }
        
        _engine.SpeechRecognized += OnSpeechRecognized;
        _engine.RecognizeCompleted += OnRecognizeCompleted;
        
        try
        {
            _engine.SetInputToDefaultAudioDevice();
            _engine.RecognizeAsync(RecognizeMode.Multiple);
            
            StateChanged?.Invoke(this, EventArgs.Empty);
            Log.Print("Listening...");
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Restart()
    {
        Pause();
        Resume();
    }
    
    private void OnConfirmationBegin(object? sender, EventArgs e)
    {
        Log.Print("Pausing general recognition during confirmation...");
        //_confirmation = true;
        // _engine?.RecognizeAsyncStop();
        Pause();
    }
    
    private void OnConfirmationEnd(object? sender, EventArgs e)
    {
        //_confirmation = false;
        Resume();
        // _engine?.RecognizeAsync();
         Log.Print("Resuming general recognition...");
    }
    
    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        Log.Print("Recognition completed.");
        Log.Print(e);

        // if (!_paused)
        // {
        //     Resume(true);  
        // }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        Log.Print("Entered SpeechRecognized handler.");

        int shiftIndex = _settings.AssistantName == null || !_settings.EnableAssistantName? 0 : 1;
        
        if (e.Result.Words.Count > shiftIndex + 1)
        {
            string recognizedVerb = e.Result.Words[shiftIndex + 0].Text;
            string recognizedNoun = e.Result.Words[shiftIndex + 1].Text;
            string phrase = recognizedVerb + " " + recognizedNoun;
            double confidence = e.Result.Confidence;
            
            Log.Print("Recognized: \'" + phrase + "\' with confidence: " + confidence);
            
            if (_settings.MinConfidence != null && confidence < _settings.MinConfidence)
            {
                Log.Print("The command is not executed because the recognition confidence is below the threshold.");
                return;
            }

            _actionManager.TryExecute(recognizedVerb, recognizedNoun);
        }
        
        Log.Print("Exited SpeechRecognized handler.");
    }
}