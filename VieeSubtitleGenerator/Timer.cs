using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using OBSWebsocketDotNet.Types;

namespace VieeSubtitleGenerator;

/// <summary>
/// `RecordingDuration` from obs-websocket's `GetRecordStatus` is delayed by about 200ms (This delay might be due to it
/// representing the time that has already been encoded.).
/// We use a separate Timer here to track the accurate duration by monitoring OBS's status.
/// </summary>
public class Timer
{
    private long _accumulated = 0L;
    private Stopwatch _stopwatch = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void OnConnected(RecordingStatus status)
    {
        if (status.IsRecording)
        {
            MessageBox.Show("OBS is recording, timer may be inaccurate");
            if (!status.IsRecordingPaused)
            {
                _stopwatch.Reset();
                _stopwatch.Start();
            }
            _accumulated = status.RecordingDuration;
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void OnRecordStateChanged(OutputState state)
    {
        var current = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        switch (state)
        {
            case OutputState.OBS_WEBSOCKET_OUTPUT_PAUSED:
                _stopwatch.Stop();
                _accumulated += _stopwatch.ElapsedMilliseconds;
                _stopwatch.Reset();
                break;
            case OutputState.OBS_WEBSOCKET_OUTPUT_RESUMED:
                _stopwatch.Reset();
                _stopwatch.Start();
                break;
            case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                _stopwatch.Reset();
                _stopwatch.Start();
                _accumulated = 0;
                break;
            case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                _stopwatch.Stop();
                _accumulated += _stopwatch.ElapsedMilliseconds;
                _stopwatch.Reset();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public long GetCurrentTime()
    {
        return _accumulated + _stopwatch.ElapsedMilliseconds;
    }
}