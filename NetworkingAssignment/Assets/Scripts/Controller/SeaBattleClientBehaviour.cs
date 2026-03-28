using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Controller
{
    public class SeaBattleClientBehaviour : MonoBehaviour
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 50001;
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private int connectTimeoutMs = 5000;

        private TcpClient _client;
        private NetworkStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;

        private CancellationTokenSource _cts;
        private Task _receiveTask; 
        // only one piece of code may write to the network stream at a time
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<string> _incomingMessages = new ConcurrentQueue<string>();

        public bool IsConnected => _client != null && _client.Connected;

        private void Start()
        {
            if (connectOnStart)
            {
                _ = ConnectAsync();
            }
        }

        private void Update()
        {
            while (_incomingMessages.TryDequeue(out string message))
            {
                Debug.Log("[SERVER] " + message);
            }
        }

        private void OnApplicationQuit()
        {
            _ = DisconnectAsync();
        }

        private void OnDestroy()
        {
            _ = DisconnectAsync();
        }

        public async Task ConnectAsync()
        {
            if (IsConnected)
            {
                return;
            }

            await DisconnectAsync(); // make sure old state is gone

            _cts = new CancellationTokenSource();
            _client = new TcpClient();

            Task connectTask = _client.ConnectAsync(host, port);
            Task timeoutTask = Task.Delay(connectTimeoutMs);

            Task finished = await Task.WhenAny(connectTask, timeoutTask);
            if (finished != connectTask)
            {
                try { _client.Close(); } catch { }
                _client = null;
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
                throw new TimeoutException($"Connection to {host}:{port} timed out.");
            }

            await connectTask; // surfaces socket errors
            _client.NoDelay = true;

            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            _writer = new StreamWriter(_stream, Encoding.UTF8, 1024, leaveOpen: true)
            {
                AutoFlush = true
            };

            _incomingMessages.Enqueue($"Connected to {host}:{port}");

            _receiveTask = Task.Run(ReceiveLoopAsync);
        }

        public Task SendRawAsync(string message)
        {
            // TODO: If i'd send data, validate first here before exposing it
            return SendLineAsync(message);
        }

        public Task SendPingAsync()
        {
            return SendLineAsync("PING");
        }

        public Task SendQuitAsync()
        {
            return SendLineAsync("QUIT");
        }

        private async Task SendLineAsync(string message)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message cannot be empty.", nameof(message));
            }

            var writer = _writer;
            if (writer == null)
            {
                throw new InvalidOperationException("Writer not ready.");
            }

            await _sendLock.WaitAsync();
            try
            {
                await writer.WriteLineAsync(message.Trim());
                await writer.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var cts = _cts;
            var reader = _reader;

            if (cts == null || reader == null)
            {
                return;
            }

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        break; // server closed the connection
                    }

                    _incomingMessages.Enqueue(line);
                }
            }
            catch (IOException)
            {
                _incomingMessages.Enqueue("Connection lost.");
            }
            catch (ObjectDisposedException)
            {
                // normal during shutdown
            }
            catch (Exception ex)
            {
                _incomingMessages.Enqueue("Receive error: " + ex.Message);
            }
            finally
            {
                _incomingMessages.Enqueue("Disconnected.");
            }
        }

        public async Task DisconnectAsync()
        {
            // lock for large blocks of code and complex logic. For this Interlocked.
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
            }

            var writer = Interlocked.Exchange(ref _writer, null);
            var reader = Interlocked.Exchange(ref _reader, null);
            var stream = Interlocked.Exchange(ref _stream, null);
            var client = Interlocked.Exchange(ref _client, null);

            try { writer?.Dispose(); } catch { }
            try { reader?.Dispose(); } catch { }
            try { stream?.Dispose(); } catch { }
            try { client?.Close(); } catch { }

            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
                _receiveTask = null;
            }

            try { cts?.Dispose(); } catch { }
        }
    }
}