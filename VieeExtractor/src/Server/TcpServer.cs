using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VieeExtractor.Server;

public class TcpServer : IDisposable
{
    private readonly ConcurrentDictionary<TcpClient, int> _incomingSockets = new();
    private readonly TcpListener _socket;
    private readonly object _startedLock = new();
    private bool _started;

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
        var remove = new List<TcpClient>(); 
        foreach (var socket in _incomingSockets.Keys)
        {
            var stream = socket.GetStream();
            try
            {
                stream.Write(len, 0, len.Length);
                stream.Write(data, 0, data.Length);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e); 
                remove.Add(socket);
            }
        }
        foreach (var tcpClient in remove)
        {
            _incomingSockets.TryRemove(tcpClient, out _);
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
                _incomingSockets.TryAdd(socket, 0);
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
        foreach (var socket in _incomingSockets.Keys)
        {
            socket.Dispose();
        }
    }
}