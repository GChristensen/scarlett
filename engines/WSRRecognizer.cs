using System.Speech.Recognition;
using System.Windows.Threading;

namespace Scarlett.engines;

public class WSRRecognizer : RecognizerEngine
{
    private const string WildcardGrammarName = "__wildcard__";

    private SpeechRecognitionEngine? _engine;

    public override bool IsListening => _engine?.AudioState != null && _engine?.AudioState != AudioState.Stopped;

    public WSRRecognizer(Settings settings, Dispatcher dispatcher) : base(settings, dispatcher)
    {
        Log.Print("[WSR] WSRRecognizer constructor completed");
    }

    protected override void HandleStart()
    {
        Log.Print($"[WSR] HandleStart called. IsShutDown={IsShutDown}, IsListening={IsListening}");

        if (!IsShutDown || IsListening)
        {
            Log.Print("[WSR] Skipped start due to running status.");
            return;
        }

        _userInitiatedStop = false;

        if (_engine == null)
        {
            Log.Print("[WSR] Creating new SpeechRecognitionEngine...");
            _engine = new SpeechRecognitionEngine();
            InitSpeechRecognition();
        }

        Log.Print("[WSR] Setting input to default audio device...");
        _engine.SetInputToDefaultAudioDevice();

        Log.Print("[WSR] Starting async recognition (Multiple mode)...");
        _engine.RecognizeAsync(RecognizeMode.Multiple);

        Log.Print("[WSR] Listening... Recognition started successfully.");

        IsShutDown = false;
        RaiseStateChanged();
    }

    protected override void HandleStop()
    {
        if (!IsShutDown)
        {
            _userInitiatedStop = true;
            _engine?.RecognizeAsyncCancel();
            IsShutDown = true;
            RaiseStateChanged();
        }
    }

    protected override void HandleShutdown()
    {
        _userInitiatedStop = true;
        HandleStop();

        if (_engine != null)
        {
            _engine.Dispose();
            _engine = null;
        }
    }

    protected override void CleanupEngine()
    {
        if (_engine != null)
        {
            _engine.Dispose();
            _engine = null;
        }
    }

    private void InitSpeechRecognition()
    {
        try
        {
            Log.Print("[WSR] Initializing speech recognition...");
            _engine?.SetInputToDefaultAudioDevice();

            // Add special commands grammar
            Log.Print("[WSR] Adding special commands grammar...");
            AddSpecialCommandsGrammar();

            Log.Print($"[WSR] Loading grammars for {_settings.Verbs.Count} verbs...");

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
                    Log.Print($"[WSR] Loaded grammar for '{actionVerb}' with {nouns.Count} nouns: {string.Join(", ", nouns.Keys)}");
                }
                else
                {
                    Log.Print($"[WSR] Skipped grammar for '{actionVerb}' - no nouns defined");
                }
            }

            // Add a catch-all wildcard grammar so unrelated speech competes for and wins
            // recognition instead of being coerced into the closest-matching command grammar.
            Log.Print("[WSR] Adding wildcard grammar...");
            AddWildcardGrammar();

            Log.Print("[WSR] Attaching event handlers...");
            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.RecognizeCompleted += OnRecognizeCompleted;

            Log.Print("[WSR] Speech recognition initialization complete.");
        }
        catch (Exception e)
        {
            Log.Error("[WSR] Error during initialization:");
            Log.Error(e);
        }
    }

    private void AddSpecialCommandsGrammar()
    {
        if (_engine == null)
        {
            Log.Print("[WSR] Cannot add special commands - engine is null");
            return;
        }

        string stopCommand = Constants.Commands.StopListening;
        string resumeCommand = Constants.Commands.ResumeListening;

        if (_settings is { AssistantName: not null, EnableAssistantName: true })
        {
            stopCommand = _settings.AssistantName + " " + stopCommand;
            resumeCommand = _settings.AssistantName + " " + resumeCommand;
        }

        // Create grammar for "stop listening" command
        GrammarBuilder stopListeningBuilder = new GrammarBuilder();
        stopListeningBuilder.Append(stopCommand);
        Grammar stopListeningGrammar = new Grammar(stopListeningBuilder);
        _engine.LoadGrammar(stopListeningGrammar);
        Log.Print($"[WSR] Loaded special command: '{stopCommand}'");

        // Create grammar for "resume listening" command
        GrammarBuilder resumeListeningBuilder = new GrammarBuilder();
        resumeListeningBuilder.Append(resumeCommand);
        Grammar resumeListeningGrammar = new Grammar(resumeListeningBuilder);
        _engine.LoadGrammar(resumeListeningGrammar);
        Log.Print($"[WSR] Loaded special command: '{resumeCommand}'");
    }

    private void AddWildcardGrammar()
    {
        if (_engine == null)
        {
            Log.Print("[WSR] Cannot add wildcard grammar - engine is null");
            return;
        }

        GrammarBuilder wildcardBuilder = new GrammarBuilder();
        wildcardBuilder.AppendWildcard();

        Grammar wildcardGrammar = new Grammar(wildcardBuilder) { Name = WildcardGrammarName };
        _engine.LoadGrammar(wildcardGrammar);
        Log.Print("[WSR] Loaded wildcard grammar to absorb unrelated speech.");
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        Log.Print("Recognition completed.");
        Log.Print($"Cancelled: {e.Cancelled}, Error: {e.Error?.Message}");

        if (!_userInitiatedStop && !IsShutDown)
        {
            Log.Print("Spontaneous completion detected - restarting recognition...");
            HandleStart();
        }

        _userInitiatedStop = false; // Reset flag
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => OnSpeechRecognized(sender, e));
            return;
        }

        Log.Print("Entered SpeechRecognized handler.");

        if (e.Result.Grammar?.Name == WildcardGrammarName)
        {
            Log.Print($"Ignored unrelated speech matched by wildcard grammar: '{e.Result.Text}'");
            return;
        }

        string recognizedText = e.Result.Text.ToLower();
        double confidence = e.Result.Confidence;

        // Check for special commands first
        if (CheckPauseVoiceCommand(recognizedText, confidence)) return;

        // If we're paused and it's not the resume listening command, ignore all other commands
        if (IsPaused)
        {
            Log.Print("Command ignored because recognition is paused.");
            return;
        }

        // Process normal commands
        int shiftIndex = _settings.AssistantName == null || !_settings.EnableAssistantName ? 0 : 1;

        if (e.Result.Words.Count > shiftIndex + 1)
        {
            string recognizedVerb = e.Result.Words[shiftIndex + 0].Text;
            string recognizedNoun = e.Result.Words[shiftIndex + 1].Text;
            string phrase = recognizedVerb + " " + recognizedNoun;

            Log.Print("Recognized: '" + phrase + "' with confidence: " + confidence);

            if (_settings.MinConfidence != null && confidence < _settings.MinConfidence)
            {
                Log.Print("The command is not executed because the recognition confidence is below the threshold.");
                return;
            }

            _actionManager.TryExecute(recognizedVerb, recognizedNoun);
        }

        Log.Print("Exited SpeechRecognized handler.");
    }

    private bool CheckPauseVoiceCommand(string recognizedText, double confidence)
    {
        string stopCommand = Constants.Commands.StopListening;
        string resumeCommand = Constants.Commands.ResumeListening;

        if (_settings is { AssistantName: not null, EnableAssistantName: true })
        {
            stopCommand = _settings.AssistantName.ToLower() + " " + stopCommand;
            resumeCommand = _settings.AssistantName.ToLower() + " " + resumeCommand;
        }

        if (recognizedText == stopCommand)
        {
            if (_settings.MinConfidence != null && confidence < _settings.MinConfidence)
            {
                Log.Print("Stop listening command ignored due to low confidence.");
                return true;
            }

            IsPaused = true;

            Log.Print("Pausing recognition with 'stop listening' command.");

            return true;
        }
        else if (recognizedText == resumeCommand)
        {
            if (_settings.MinConfidence != null && confidence < _settings.MinConfidence)
            {
                Log.Print("Resume listening command ignored due to low confidence.");
                return true;
            }

            if (IsPaused)
            {
                IsPaused = false;

                Log.Print("Resuming recognition with 'resume listening' command.");
            }
            return true;
        }

        return false;
    }
}
