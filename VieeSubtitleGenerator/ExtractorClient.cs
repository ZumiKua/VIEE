using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace VieeSubtitleGenerator;

public class ExtractorClient : IDisposable
{
    private readonly IExtractorListener _listener;
    private readonly TcpClient _tcpClient;
    private volatile bool _started;
    private readonly Thread _thread;

    public ExtractorClient(int port, IExtractorListener listener)
    {
        _listener = listener;
        _tcpClient = new TcpClient();
        _tcpClient.Connect(IPAddress.Parse("127.0.0.1"), port);
        _started = true;
        _thread = new Thread(ReadLoop);
        _thread.Start();
    }

    private void ReadLoop()
    {
        var stream = _tcpClient.GetStream();
        var lenBuf = new byte[4];
        var buf = new byte[1024];
        while (_started)
        {
            try
            {
                stream.ReadExactly(lenBuf, 0, 4);
                var len = BitConverter.ToInt32(lenBuf, 0);
                if (buf.Length < len)
                {
                    buf = new byte[len];
                }
                stream.ReadExactly(buf, 0, len);
                var data = JsonSerializer.Deserialize<ExtractorData>(new ReadOnlySpan<byte>(buf)[..len]);
                if (_started)
                {
                    switch (data.Type)
                    {
                        case ExtractorData.TypeText:
                            _listener.OnText(data.Text);
                            break;
                        case ExtractorData.TypeChoices:
                            _listener.OnChoices(data.Choices, data.Index);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                if (_started)
                {
                    _listener.OnError(e);
                }
                break;
            }
        }
    }

    public void Dispose()
    {
        _started = false;
        _tcpClient.Dispose();
        _thread.Join();
    }
}