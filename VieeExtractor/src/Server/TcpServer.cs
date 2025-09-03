using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VieeExtractor.Server;

public class TcpServer : IDisposable
{
    private readonly ConcurrentBag<TcpClient> _incomingSockets = new();
    private readonly TcpListener _socket;
    private readonly object _startedLock = new();
    private volatile bool _started;

    public TcpServer(int port)
    {
        var localAddr = IPAddress.Parse("127.0.0.1");
        _socket = new TcpListener(localAddr, port);
        _socket.Start();
        _started = true;
        _socket.AcceptTcpClientAsync().ContinueWith(OnSocketAccepted);
    }

    public void SendMessage(string msg)
    {
        ThreadPool.QueueUserWorkItem(_ => SendToAll(msg));
    }

    private void SendToAll(string msg)
    {
        var data = Encoding.UTF8.GetBytes(msg);
        var len = BitConverter.GetBytes(data.Length);
        foreach (var socket in _incomingSockets)
        {
            var stream = socket.GetStream();
            stream.Write(len, 0, len.Length);
            stream.Write(data, 0, data.Length);
        }
    }

    private void OnSocketAccepted(Task<TcpClient> s)
    {
        if (!s.IsCompleted)
        {
            return;
        }
        var socket = s.Result;
        var dispose = false;
        lock (_startedLock)
        {
            if (_started)
            {
                _incomingSockets.Add(socket);
            }
            else
            {
                dispose = true;
            }
        }
        if (dispose)
        {
            socket.Dispose();
        }
        
        _socket.AcceptTcpClientAsync().ContinueWith(OnSocketAccepted);
    }

    public void Dispose()
    {
        lock (_startedLock)
        {
            _started = false;
        }
        _socket.Stop();
        foreach (var socket in _incomingSockets)
        {
            socket.Dispose();
        }
    }
}