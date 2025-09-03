using System.Net;
using System.Net.Sockets;
using System.Windows;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using Websocket.Client;

namespace VieeSubtitleGenerator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IExtractorListener
{
    private readonly OBSWebsocket _obs;
    private ExtractorClient? _tcpClient;

    public MainWindow()
    {
        InitializeComponent();
        _obs = new OBSWebsocket();
        _obs.RecordStateChanged += RecordStateChanged;
        _obs.Connected += Connected;
        _obs.Disconnected += Disconnected;
        OBSStatus.Text = "[X]OBS";
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
            UpdateRecordingStatus();
            StatusText.Text = $"Connected to OBS";
        });
    }

    private void UpdateRecordingStatus(OutputState? outputStateState = null)
    {
        var s = _obs.GetRecordStatus();
        OBSStatus.Text = GetRecordingStatusString(s, outputStateState);
    }

    private void RecordStateChanged(object? sender, RecordStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateRecordingStatus(e.OutputState.State));
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_obs.IsConnected)
        {
            RecordStatus.Text = "Not Connected";
            return;
        }

        var status = _obs.GetRecordStatus();
        if (!status.IsRecording)
        {
            RecordStatus.Text = "Not Recording";
            return;
        }

        var duration = status.RecordingDuration;
        var s = duration / 1000L;
        var h = s / 3600;
        var m = s % 3600 / 60;
        var ss = s % 60;
        var paused = status.IsRecordingPaused ? "Paused" : "";
        RecordStatus.Text = $"{h:00}:{m:00}:{ss:00} {status.RecordTimecode} {paused}";
    }

    private void ConnectToExtractor_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ExtractorPortField.Text, out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("Invalid Port");
            return;
        }

        if (_tcpClient != null)
        {
            _tcpClient.Dispose();
        }
        _tcpClient = new ExtractorClient(port, this);
        
    }

    public void OnText(string text)
    {
        Dispatcher.Invoke(() =>
        {
            Content.Text = text;
        });
    }

    public void OnChoices(string[] choices, int index)
    {
        
    }

    public void OnError(Exception exception)
    {
        Console.WriteLine(exception);
        Dispatcher.Invoke(() =>
        {
            Content.Text = $"{exception.GetType().Namespace} {exception.Message}";
        });
    }
}