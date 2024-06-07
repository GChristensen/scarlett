using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Scarlett.ui;
using MessageBox = System.Windows.MessageBox;

namespace Scarlett;

public partial class App : System.Windows.Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    private const String APP_NAME = "Scarlett";
    private const Int32 WATCH_THREAD_SLEEP_MIN = 1;
    private const Int32 RECOGNIZER_PERIOD_MIN = 20;

    private Int32 _wakeCounter = 0;
    
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _playPauseMenuItem;
    private Recognizer? _recognizer;

    private EventHandler<EventArgs>? _periodicResumeEvent;
    
    public App()
    {
        if (Utils.IsAlreadyRunning())
        {
            Environment.Exit(0);
            return;
        }
            
        Startup += OnAppStartup;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        //DispatcherUnhandledException += OnUnhandledException;
        AppDomain currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += OnUnhandledException;
        //SystemEvents.PowerModeChanged += SystemEvents_OnPowerChange;
    }
    
    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        Log.Init();
        Log.UseConsole = Debugger.IsAttached;
        
        var settings = new Settings();
        try
        {
            settings.Load();

            Log.DisplayErrors = settings.DisplayErrors;
        }
        catch
        {
            MessageBox.Show("Error reading the configuration file. Please see the log file for more details.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        } 
        
        CreateTrayIcon();
        CreateRecognizer(settings);
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new NotifyIcon()
        {
            Text = APP_NAME,
            Icon = new Icon(GetResourceStream(new Uri("pack://application:,,,/icons/Tray.ico")).Stream),
            Visible = true
        };
        
        ContextMenuStrip cms = new ContextMenuStrip();

        var aboutMenuItem = new ToolStripMenuItem("About", null, About_Click, "PlayPause");
        _playPauseMenuItem = new ToolStripMenuItem("Pause", null, PlayPause_Click, "PlayPause");

        cms.Items.Add(aboutMenuItem);
        cms.Items.Add(_playPauseMenuItem);
        cms.Items.Add(new ToolStripSeparator());
        cms.Items.Add(new ToolStripMenuItem("Exit", null, Exit_Click, "Exit"));
        
        _trayIcon.ContextMenuStrip = cms;
    }

    private void CreateRecognizer(Settings settings)
    {
        Log.Print("Creating recognizer...");

        if (_recognizer == null)
        {
            _recognizer = new Recognizer(settings);
            _recognizer.StateChanged += State_Changed;
            _recognizer.Resume();
            
            StartMonitoringThread();
        }
    }

    // private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    // {
    //     Log.Error(e.Exception);
    // }
    
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error(e.ExceptionObject);
    }

    
    public void StartMonitoringThread()
    {
        _periodicResumeEvent += OnPeriodicRestart;
        Thread thread = new Thread(RunPeriodically);
        thread.Start();
    }

    private void RunPeriodically()
    {
        while (true)
        {
            Thread.Sleep(WATCH_THREAD_SLEEP_MIN * 60 * 1000);
            _wakeCounter++;
            
            bool listeningStateAnomaly = _recognizer is { IsPaused: false, IsListening: false };
            
            if (listeningStateAnomaly || _wakeCounter % RECOGNIZER_PERIOD_MIN == 0)
            {
                if (listeningStateAnomaly)
                    Log.Print("Listening state anomaly: not paused and not listening!");
                
                try
                {
                    App.Current.Dispatcher.BeginInvoke(delegate()
                    {
                        // Although this may be a voodoo, the recognizer is restarted every N minutes to reduce hallucinations
                        Log.Print("Restarting recognizer from the background thread.");
                        _periodicResumeEvent?.Invoke(this, EventArgs.Empty);
                    }, null);
                    
                }
                catch (Exception e)
                {
                    Log.Print(e);
                }
            }
        }
    }

    private void OnPeriodicRestart(object? sender, EventArgs e)
    {
        _recognizer?.Restart();
    }
    
    private void About_Click(object? sender, EventArgs e)
    {
        var thread = new Thread(() =>
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.Show();

            aboutDialog.Closed += (object? sender, EventArgs e) =>
            {
                System.Windows.Threading.Dispatcher.ExitAllFrames();
            };

            System.Windows.Threading.Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
    
    private void PlayPause_Click(object? sender, EventArgs e)
    {
        if (_recognizer?.IsPaused ?? true)
        {
            _recognizer?.Resume();
        }
        else
        {
            _recognizer?.Pause();
        }
    }
    
    private void State_Changed(object? sender, EventArgs e)
    {
        if (_recognizer?.IsPaused ?? true)
        {
            if (_playPauseMenuItem != null)
                _playPauseMenuItem.Text = "Resume";
        }
        else
        {
            if (_playPauseMenuItem != null)
                _playPauseMenuItem.Text = "Pause";
        }
        
        Log.Print("State changed to: " + (_recognizer?.IsPaused ?? true? "paused.": "resumed."));
    }
    
    private void Exit_Click(object? sender, EventArgs e)
    {
        if (_trayIcon != null)
            _trayIcon.Visible = false;

        Environment.Exit(0);
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        Log.Print("Session swtch reason: " + e.Reason);
        
        lock (this)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteConnect)
            {
                Log.Print("Pausing due to session lock.");
                _recognizer?.Pause();
            }
            else if
                (e.Reason == SessionSwitchReason.SessionUnlock /*|| e.Reason == SessionSwitchReason.RemoteDisconnect*/)
            {
                Log.Print("Resuming due to session ulock.");
                _recognizer?.Resume();
            }
        }
    }
    
    // private void SystemEvents_OnPowerChange(object s, PowerModeChangedEventArgs e) 
    // {
    //     switch ( e.Mode ) 
    //     {
    //         case PowerModes.Resume: 
    //             recognizer.Resume();
    //             break;
    //         case PowerModes.Suspend:
    //             recognizer.Pause();
    //             break;
    //     }
    // }
    
}
