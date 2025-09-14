using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VieeExtractor.VersionChecking
{
    public sealed class VersionChecker : IDisposable
    {
        private const string GithubApiUrl = "https://api.github.com/repos/ZumiKua/VIEE/releases/latest";

        private readonly string _currentVersion;
        private Action<bool>? _callback;

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly object _lock = new object();
        private bool _disposed = false;

        public VersionChecker(string currentVersion, Action<bool> callback)
        {
            if (string.IsNullOrEmpty(currentVersion))
                throw new ArgumentNullException(nameof(currentVersion));

            _currentVersion = currentVersion;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
            }

            Task.Run(CheckVersionAsync, _cancellationTokenSource.Token);
        }

        private async Task CheckVersionAsync()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(GithubApiUrl);
                request.UserAgent = "VIEE-Version-Checker";
                request.Method = "GET";
                request.Timeout = 10000;

                using var register = _cancellationTokenSource.Token.Register(() => request.Abort());
                using var response = await request.GetResponseAsync();
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                string json;
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream!))
                {
                    json = await reader.ReadToEndAsync();
                }
                
                ProcessResponse(json);
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.RequestCanceled)
            {
                //do nothing.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[VersionChecker] An error occurred: {ex.Message}");
            }
        }

        private void ProcessResponse(string json)
        {
            var match = Regex.Match(json, "\"name\":\\s*\"(.*?)\"");

            if (match.Success)
            {
                var latestVersion = match.Groups[1].Value;
                var newVersionAvailable = latestVersion != "v" + _currentVersion;

                lock (_lock)
                {
                    if (!_disposed)
                    {
                        _callback?.Invoke(newVersionAvailable);
                    }
                }
            }
            else
            {
                throw new Exception("Could not parse version name from API response.");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                _callback = null;
            }
            
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }
}
