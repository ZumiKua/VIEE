using System.IO;
using System.Windows;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace VieeSubtitleGenerator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IExtractorListener
{
    private readonly OBSWebsocket _obs;
    private readonly ExtractorClientManager _clientManager;
    private readonly SrtWriter _srtWriter = new();
    private readonly SrtWriter _choicesWriter = new();
    private readonly Timer _timer = new();

    public MainWindow()
    {
        InitializeComponent();
        _obs = new OBSWebsocket();
        _obs.RecordStateChanged += RecordStateChanged;
        _obs.Connected += Connected;
        _obs.Disconnected += Disconnected;
        OBSStatus.Text = "OBS Disconnected";
        ExtractorStatus.Text = "Extractor Disconnected";
        _clientManager = new ExtractorClientManager(this);
    }

    private void ConnectToOBS_OnClick(object sender, RoutedEventArgs e)
    {

        if (!int.TryParse(PortField.Text, out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("Invalid Port");
            return;
        }
        _obs.ConnectAsync($"ws://127.0.0.1:{port}", PasswordField.Text);
    }

    private void Disconnected(object? sender, ObsDisconnectionInfo e)
    {
        Dispatcher.Invoke(() => OnDisconnected(e));
    }

    private void OnDisconnected(ObsDisconnectionInfo e)
    {
        MessageBox.Show("OBS Disconnected");
        OBSStatus.Text = "OBS Disconnected";
        var reason = e.DisconnectReason;
        if (reason == null)
        {
            if (e.WebsocketDisconnectionInfo.Exception != null)
            {
                reason = e.WebsocketDisconnectionInfo.Exception.Message;
            }
        }
        StatusText.Text = $"OBS Disconnected, Reason: {reason}";
        
    }

    private string GetRecordingStatusString(RecordingStatus status, OutputState? outputStateState)
    {
        if (!status.IsRecording || outputStateState == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
        {
            return "OBS Not Recording";
        }
        if (status.IsRecordingPaused)
        {
            return "OBS Paused";
        }
        return "OBS Recording";
    } 

    private void Connected(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var status = UpdateRecordingStatus();
            _timer.OnConnected(status);
            StatusText.Text = $"Connected to OBS";
        });
    }

    private RecordingStatus UpdateRecordingStatus(OutputState? outputStateState = null)
    {
        var s = _obs.GetRecordStatus();
        OBSStatus.Text = GetRecordingStatusString(s, outputStateState);
        return s;
    }

    private void RecordStateChanged(object? sender, RecordStateChangedEventArgs e)
    {
        _timer.OnRecordStateChanged(e.OutputState.State);
        string? msg = null;
        if (e.OutputState.State == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED && e.OutputState.OutputPath != null)
        {
            var path = Path.ChangeExtension(e.OutputState.OutputPath, ".srt");
            if (WriteSrtWriterContent(_srtWriter, path))
            {
                msg = $"Srt File written to {path}";
            }

            var choicesPath = Path.ChangeExtension(e.OutputState.OutputPath, ".choices.srt");
            WriteSrtWriterContent(_choicesWriter, choicesPath);
        }
        Dispatcher.Invoke(() =>
        {
            if (msg != null)
            {
                StatusText.Text = msg;
            }
            UpdateRecordingStatus(e.OutputState.State);
        });
    }

    private bool WriteSrtWriterContent(SrtWriter srtWriter, string path)
    {
        SrtWriter.Writer writer;
        lock (srtWriter)
        {
            writer = srtWriter.GetWriterAndClear();
        }

        if (!writer.HasContent())
        {
            return false;
        }
        var duration = _timer.GetCurrentTime();

        using var stream = File.Open(path, FileMode.Create);
        writer.WriteTo(stream, duration, false);
        return true;
    }

    private void ConnectToExtractor_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ExtractorPortField.Text, out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("Invalid Port");
            return;
        }
        ExtractorStatus.Text = "Extractor Connected";
        StatusText.Text = "Extractor Connected";
        _clientManager.Connect(port);
    }

    public void OnText(string text)
    {
        if (_obs.IsConnected)
        {
            var status = _obs.GetRecordStatus();
            if (status.IsRecording && !status.IsRecordingPaused)
            {
                var duration = _timer.GetCurrentTime();
                lock (_srtWriter)
                {
                    _srtWriter.AddEntry(text, duration);
                }
            }
        }
        Dispatcher.Invoke(() =>
        {
            Content.Text = text;
        });
    }

    public void OnChoices(string[] choices, int index)
    {
        if (_obs.IsConnected)
        {
            var status = _obs.GetRecordStatus();
            if (status.IsRecording && !status.IsRecordingPaused)
            {
                var duration = _timer.GetCurrentTime();
                lock (_choicesWriter)
                {
                    _choicesWriter.AddEntry(string.Join("\n", choices), duration);
                }
            }
        }
    }

    public void OnError(Exception exception)
    {
        Console.WriteLine(exception);
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show("Failed to connect to the extractor.");
            ExtractorStatus.Text = "Extractor Disconnected";
            StatusText.Text = $"Extractor Disconnected: {exception.GetType().Namespace} {exception.Message}";
        });
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _clientManager.Dispose();
    }
}