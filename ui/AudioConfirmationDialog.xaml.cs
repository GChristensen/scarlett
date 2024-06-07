using System.Speech.Recognition;
using System.Windows;
using System.Windows.Threading;

namespace Scarlett.ui;

public partial class AudioConfirmationDialog : Window
{
    private const int WINDOW_LIFETIME = 10;
    
    private readonly Dispatcher? _dispatcher;
    private readonly SpeechRecognitionEngine? _engine;
    
    public bool UserResponse { get; private set; } = false;

    public AudioConfirmationDialog(string actionPhrase)
    {
        InitializeComponent();

        AlertText.Text = "Please say 'Yes' or 'No' to confirm or reject the action '" + actionPhrase + "'?";

        _dispatcher = Dispatcher.CurrentDispatcher;
        _engine = new SpeechRecognitionEngine();
        
        GrammarBuilder grammarBuilder = new GrammarBuilder();
        Choices choices = new Choices();
        
        choices.Add("yes");
        choices.Add("no");
        
        grammarBuilder.Append(choices);
        _engine.LoadGrammar(new Grammar(grammarBuilder));
        
        _engine.SpeechRecognized += SpeechRecognized;
        Closed += OnClosed;
        
        _engine.SetInputToDefaultAudioDevice();
        _engine.RecognizeAsync(RecognizeMode.Single);
        
        StartCloseTimer();
    }
    
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        Topmost = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_engine?.AudioState != AudioState.Stopped)
        {
            _engine?.RecognizeAsyncCancel();
            _engine?.Dispose();
        }
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        UserResponse = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        UserResponse = false;
        this.Close();
    }
    
    private void StartCloseTimer()
    {
        DispatcherTimer timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(WINDOW_LIFETIME);
        timer.Tick += TimerTick;
        timer.Start();
    }
    
    private void TimerTick(object? sender, EventArgs e)
    {
        DispatcherTimer? timer = (DispatcherTimer?)sender;

        if (timer != null)
        {
            timer.Stop();
            timer.Tick -= TimerTick;
        }

        Close();
    }
    
    private void SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result.Words.Count > 0)
        {
            string answer = e.Result.Words[0].Text;
     
            if (answer == "yes")
            {
                _dispatcher?.Invoke(() => YesButton_Click(this, null!));   
            }
            else if (answer == "no")
            {
                _dispatcher?.Invoke(() => NoButton_Click(this, null!));
            }
        }
    }
}
