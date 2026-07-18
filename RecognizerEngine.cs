using System.Collections.Concurrent;
using System.Windows.Threading;
using Scarlett.actions;

namespace Scarlett;

public abstract class RecognizerEngine
{
    protected Settings _settings;
    protected ActionManager _actionManager;

    protected readonly BlockingCollection<RecognizerMessage> _messageQueue;
    protected Task? _processingTask;
    protected CancellationTokenSource _cancellationTokenSource;
    protected readonly Dispatcher _dispatcher;

    protected bool _userInitiatedStop = false;
    protected bool _isPaused = false;

    public event EventHandler? StateChanged;
    public event EventHandler? RecognitionPaused;

    public bool IsShutDown { get; protected set; } = true;

    public abstract bool IsListening { get; }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;
            RaiseRecognitionPaused();
        }
    }

    protected RecognizerEngine(Settings settings, Dispatcher dispatcher)
    {
        Log.Print("[RecognizerEngine] Constructor called");
        _settings = settings;
        _actionManager = new ActionManager(settings);
        _messageQueue = new BlockingCollection<RecognizerMessage>();
        _cancellationTokenSource = new CancellationTokenSource();
        _dispatcher = dispatcher;

        _actionManager.ConfirmationBegin += OnConfirmationBegin;
        _actionManager.ConfirmationEnd += OnConfirmationEnd;

        Log.Print("[RecognizerEngine] Starting processing loop task...");
        _processingTask = Task.Run(ProcessingLoop);

        Log.Print("[RecognizerEngine] Calling HandleStart...");
        HandleStart();
    }

    protected async Task ProcessingLoop()
    {
        Log.Print("[RecognizerEngine] Processing loop started");
        try
        {
            foreach (var message in _messageQueue.GetConsumingEnumerable())
            {
                Log.Print($"[RecognizerEngine] Processing message: {message.Type}");
                try
                {
                    switch (message.Type)
                    {
                        case RecognizerMessage.MessageType.Start:
                            HandleStart();
                            break;

                        case RecognizerMessage.MessageType.Stop:
                            HandleStop();
                            break;

                        case RecognizerMessage.MessageType.Resume:
                            HandleResume();
                            break;

                        case RecognizerMessage.MessageType.Restart:
                            HandleRestart();
                            break;

                        case RecognizerMessage.MessageType.Shutdown:
                            HandleShutdown();
                            break;
                    }

                    message.CompletionSource?.SetResult(true);
                    Log.Print($"[RecognizerEngine] Completed message: {message.Type}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[RecognizerEngine] Error processing message {message.Type}:");
                    Log.Error(ex);
                    message.CompletionSource?.SetException(ex);
                }
            }
        }
        finally
        {
            Log.Print("[RecognizerEngine] Processing loop ending, cleaning up engine...");
            CleanupEngine();
        }
    }

    protected void RaiseStateChanged()
    {
        if (StateChanged != null)
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(() => StateChanged?.Invoke(this, EventArgs.Empty));
            }
            else
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    protected void RaiseRecognitionPaused()
    {
        if (RecognitionPaused != null)
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(() => RecognitionPaused?.Invoke(this, EventArgs.Empty));
            }
            else
            {
                RecognitionPaused?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    protected abstract void HandleStart();
    protected abstract void HandleStop();
    protected abstract void HandleShutdown();
    protected abstract void CleanupEngine();

    protected virtual void HandleResume()
    {
        HandleStart();
    }

    protected virtual void HandleRestart()
    {
        HandleShutdown();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 1000)
        {
            Thread.Yield();
        }

        HandleStart();
    }

    protected async Task SendMessageAsync(RecognizerMessage.MessageType messageType)
    {
        var tcs = new TaskCompletionSource<bool>();
        _messageQueue.Add(new RecognizerMessage(messageType, tcs));
        await tcs.Task;
    }

    public async Task StopAsync()
    {
        await SendMessageAsync(RecognizerMessage.MessageType.Stop);
    }

    public async Task ResumeAsync()
    {
        await SendMessageAsync(RecognizerMessage.MessageType.Resume);
    }

    public async Task RestartAsync()
    {
        await SendMessageAsync(RecognizerMessage.MessageType.Restart);
    }

    public async ValueTask DisposeAsync()
    {
        await SendMessageAsync(RecognizerMessage.MessageType.Shutdown);
        _messageQueue.CompleteAdding();

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _cancellationTokenSource.Cancel();
    }

    protected virtual void OnConfirmationBegin(object? sender, EventArgs e)
    {
        Log.Print("Pausing general recognition during confirmation...");
        StopAsync();
    }

    protected virtual void OnConfirmationEnd(object? sender, EventArgs e)
    {
        ResumeAsync();
        Log.Print("Resuming general recognition...");
    }
}
