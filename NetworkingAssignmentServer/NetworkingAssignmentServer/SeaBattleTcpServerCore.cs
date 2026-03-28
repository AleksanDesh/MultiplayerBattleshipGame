using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkingAssignmentServer
{
    public class SeaBattleTcpServerCore(int port, Action<string> log)
    {
        private readonly int _port = port;
        private readonly Action<string> _log = log ?? (_ => { });
        private TcpListener? _listener;
        private CancellationTokenSource? _cts; // shader across everybody

        public void Start()
        {
            if (_listener != null)
                return;

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _log("Server started on port " + _port);
            _ = Task.Run(AcceptLoopAsync);
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }

            _cts = null;
            _listener = null;
            _log("Server stopped");
        }

        private async Task AcceptLoopAsync()
        {
            while (_cts != null && !_cts.IsCancellationRequested)
            {
                TcpClient? client = null;

                try
                {
                    if (_listener != null)
                        client = await _listener.AcceptTcpClientAsync();
                    client?.NoDelay = true;

                    _log("Client connected: " + client?.Client.RemoteEndPoint);
                    if (client != null)
                        _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log("Accept error: " + ex.Message);
                    try { client?.Close(); } catch { }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    await writer.WriteLineAsync("WELCOME");
                    if (_cts == null)
                        return;
                    while (!_cts.IsCancellationRequested && client.Connected)
                    {
                        string? line = await reader.ReadLineAsync();
                        if (line == null)
                            break;

                        line = line.Trim();
                        _log("Received: " + line);

                        if (line.Equals("PING", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("PONG");
                        }
                        else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("BYE");
                            break;
                        }
                        else
                        {
                            await writer.WriteLineAsync("OK " + line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log("Client error: " + ex.Message);
            }
            finally
            {
                _log("Client disconnected");
            }
        }
    }
}
