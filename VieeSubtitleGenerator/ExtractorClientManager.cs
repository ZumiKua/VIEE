namespace VieeSubtitleGenerator;

public class ExtractorClientManager : IDisposable
{
    private readonly IExtractorListener _listener;
    private ExtractorClient? _client;

    public ExtractorClientManager(IExtractorListener listener)
    {
        _listener = listener;
    }

    public void Connect(int port)
    {
        _client?.Dispose();
        _client = null;
        try
        {
            _client = new ExtractorClient(port, _listener);
        }
        catch (Exception e)
        {
            _listener.OnError(e);
        }
    }


    public void Dispose()
    {
        _client?.Dispose();
    }
}