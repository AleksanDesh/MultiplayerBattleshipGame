using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;

namespace NetworkingAssignmentServer
{

    internal class TcpServer
    {
        private int port = 50001;
        private bool autoStart = true;

        private SeaBattleTcpServerCore? _server;
        private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();

        public void Start()
        {
            _server = new SeaBattleTcpServerCore(port, msg => _logQueue.Enqueue(msg));
            if (autoStart)
            {
                Console.WriteLine($"Starting server on port {port}");
                _server.Start();
            }
        }

        public void Update()
        {
            string? line;
            while (_logQueue.TryDequeue(out line))
            {
                Console.WriteLine(line);
            }
        }

        void OnApplicationQuit()
        {
            StopServer();
        }

        void OnDestroy()
        {
            StopServer();
        }

        public void StopServer()
        {
            if (_server != null)
            {
                Console.WriteLine($"Stopping server at port {port}");
                _server.Stop();
                _server = null;
            }
        }
    }
}
