using Model;
using NetworkConnections;
using OSCTools;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

namespace Network
{
    public class Client : MonoBehaviour
    { // TODO: Add error displaying dictionary
        public IPAddress ServerIP = IPAddress.Loopback;
        TcpNetworkConnection _connection;
        OSCDispatcher _dispatcher;

        // Answer if the login was sucesseful
        private TaskCompletionSource<bool> loginTcs;

        private sealed class Location
        {
            public int X { get; }
            public int Y { get; }
            public bool IsMySide { get; }
            public Location(int x, int y, bool isMySide = true)
            {
                X = x;
                Y = y;
                IsMySide = isMySide;
            }
        }

        public delegate void ShipPlacementEvent(int x, int y, Ship ship);
        public event ShipPlacementEvent OnShipPlacement;
        private PendingShipPlacement _pendingShipPlacement;
        private sealed class PendingShipPlacement
        {
            public Location location { get; }
            public Ship Ship { get; }
            public TaskCompletionSource<int> Tcs { get; }

            public PendingShipPlacement(int x, int y, Ship ship)
            {
                location = new Location(x, y);
                Ship = ship;
                Tcs = new TaskCompletionSource<int>();
            }
        }

        public delegate void MinePlacementEvent(int x, int y);
        public event MinePlacementEvent OnMinePlacement;
        private PendingMinePlacement _pendingMinePlacement;
        private sealed class PendingMinePlacement
        {
            public Location location { get; }
            public TaskCompletionSource<int> Tcs { get; }
            public PendingMinePlacement(int x, int y)
            {
                location = new Location(x, y);
                Tcs = new TaskCompletionSource<int>();
            }
        }

        public delegate void BombingEvent(int x, int y);
        public event BombingEvent OnBombing;
        private PendingBombing _pendingBombing;
        private sealed class PendingBombing
        {
            public Location location { get; }
            public TaskCompletionSource<int> Tcs { get; }
            public PendingBombing(int x, int y)
            {
                location = new Location(x, y);
                Tcs = new TaskCompletionSource<int>();
            }
        }

        public delegate void MarkReadyEvent(int result);
        public event MarkReadyEvent OnMarkReady;
        public TaskCompletionSource<int> _tcsMarkReady;

        public delegate void EnqueueEvent(int result);
        public event EnqueueEvent OnEnqueue;
        public TaskCompletionSource<int> _tcsEnqueue;



        // ----- TicTacToe client things:

        // Views subscribe here, on any client:
        //public delegate void CellChangeEvent(int row, int col, int value);
        //public event CellChangeEvent OnCellChange;

        //public delegate void ActivePlayerChangeEvent(int activePlayer);
        //public event ActivePlayerChangeEvent OnActivePlayerChange;

        //public event System.Action<int> OnPlayerInfoReceived;

        //public delegate void GameOverEvent(int winner);
        //public event GameOverEvent OnGameOver;

        //void Start()
        //{
        //    TcpClient client = new TcpClient();
        //    client.Connect(new IPEndPoint(ServerIP, 50006));
        //    connection = new TcpNetworkConnection(client);
        //    // TODO: error handling

        //    Debug.Log("Starting client, connecting to " + ServerIP);

        //    // Initialize the dispatcher and callbacks for incoming OSC messages:
        //    dispatcher = new OSCDispatcher();
        //    dispatcher.ShowIncomingMessages = true;
        //    Initialize();
        //}
        void Awake()
        {
            Connect();
        }
        public bool Connect(int port = 5376)
        {// TODO: Add username loging. Also add Register.
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(new IPEndPoint(ServerIP, port));
                _connection = new TcpNetworkConnection(client);
                Debug.Log("Client: Connecting with client to server " + ServerIP);

                _dispatcher = new OSCDispatcher();
                _dispatcher.ShowIncomingMessages = true;
                Initialize();
                return true;
            }
            catch (Exception exp)
            {
                Debug.LogException(exp);
                return false;
            }
        }

        #region sendingMessages


        public Task<bool> Login(string username, string password)
        {
            loginTcs = new TaskCompletionSource<bool>();
            //OSCMessageOut message = new OSCMessageOut("/MakeMove").AddInt(row).AddInt(col);
            OSCMessageOut message = new OSCMessageOut("/Login").AddString(username).AddString(password);
            _connection.Send(message.GetBytes());
            return loginTcs.Task;
        }

        public void Register(string username, string password)
        {

        }

        public Task<int> PlaceShip(int x, int y, Ship ship)
        {
            _pendingShipPlacement = new PendingShipPlacement(x, y, ship);
            // TODO: Add different sized ship details
            OSCMessageOut message = new OSCMessageOut("/PlaceShip").AddInt(x).AddInt(y);
            _connection.Send(message.GetBytes());
            return _pendingShipPlacement.Tcs.Task;
        }

        public Task<int> PlaceMine(int x, int y)
        {
            _pendingMinePlacement = new PendingMinePlacement(x, y);
            // TODO: Add different sized ship details
            OSCMessageOut message = new OSCMessageOut("/PlaceMine").AddInt(x).AddInt(y);
            _connection.Send(message.GetBytes());
            return _pendingMinePlacement.Tcs.Task;
        }

        public Task<int> Bomb(int x, int y)
        {
            _pendingBombing = new PendingBombing(x, y);
            OSCMessageOut message = new OSCMessageOut("/Bomb").AddInt(x).AddInt(y);
            _connection.Send(message.GetBytes());
            return _pendingBombing.Tcs.Task;
        }

        public Task<int> MarkReady()
        {
            _tcsMarkReady = new TaskCompletionSource<int>();
            OSCMessageOut message = new OSCMessageOut("/MarkReady");
            _connection.Send(message.GetBytes());
            return _tcsMarkReady.Task;
        }

        public Task<int> Enqueue()
        {
            _tcsEnqueue = new TaskCompletionSource<int>();
            OSCMessageOut message = new OSCMessageOut("/Enqueue");
            _connection.Send(message.GetBytes());
            return _tcsEnqueue.Task;
        }
        #endregion


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            // Check for incoming packets, and deal with them:
            while (_connection.Available() > 0)
            {
                HandlePacket(_connection.GetPacket(), _connection.Remote);
            }
            // TODO: disconnect handling
        }

        // TODO: Make every method call the view error display, and send it the result if not 0, so it displays the error as a string
        #region receivedMessages
        void HandlePacket(byte[] packet, IPEndPoint remote)
        {
            OSCMessageIn mess = new OSCMessageIn(packet);
            Debug.Log("Message arrives on client: " + mess);
            _dispatcher.HandlePacket(packet, remote);
        }
        void Initialize()
        {
            // Subscribe to methods.
            _dispatcher.AddListener("/TryJoin", TryEnter, OSCUtil.INT);
            _dispatcher.AddListener("/PlaceShip", PlaceShip, OSCUtil.INT);
            _dispatcher.AddListener("/PlaceMine", PlaceMine, OSCUtil.INT);
            _dispatcher.AddListener("/Bomb", Bomb, OSCUtil.INT);
            _dispatcher.AddListener("/MarkReady", MarkReady, OSCUtil.INT);
            _dispatcher.AddListener("/Enqueue", Enqueue, OSCUtil.INT);
        }

        /// <summary>
        /// -1 = something went REALLY WRONG
        /// 0 = user already connected
        /// 1 = sucess
        /// 2 = wrong username
        /// 3 = wrong password
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remote"></param>
        void TryEnter(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            bool success = result == 0;
            loginTcs?.TrySetResult(success);
            loginTcs = null;
            // For now no display of errors.
            // TODO: Add error message pop-up whenever something goes wrong
            switch (result)
            {
                case 0:
                    {  
                        // Ignore
                        break;
                    }
                case 1:
                    {
                        break;
                    }
                case 2:
                    {

                        break;
                    }
                case 3:
                    {
                        break;
                    }
                default:
                    {
                        break;
                    }

            }
        }

        /// <summary>
        /// -1 = something went REALLY WRONG
        /// 0 = placed sucesefully
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remote"></param>
        void PlaceShip(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();

            var pending = _pendingShipPlacement;
            _pendingShipPlacement = null;

            if (pending == null)
                return;
            if (result == 0) // Make the actual placement
                OnShipPlacement?.Invoke(pending.location.X, pending.location.Y, pending.Ship);
            // Else display an error
            // TODO.
            pending.Tcs?.TrySetResult(result);
        }

        /// <summary>
        /// -1 = something went REALLY WRONG
        /// 0 = placed sucesefully
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remote"></param>
        void PlaceMine(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();

            var pending = _pendingMinePlacement;
            _pendingMinePlacement = null;

            if (pending == null)
                return;
            if (result == 0) // Make the actual placement
                OnMinePlacement?.Invoke(pending.location.X, pending.location.Y);
            // Else display an error
            // TODO.
            pending.Tcs?.TrySetResult(result);
        }

        void Bomb(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            var pending = _pendingBombing;
            _pendingBombing = null;

            if (pending == null)
                return;

            if (result == 0)
                OnBombing?.Invoke(pending.location.X, pending.location.Y);
            pending.Tcs?.TrySetResult(result);
        }

        void MarkReady(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            _tcsMarkReady?.TrySetResult(result);
            _tcsMarkReady = null;
            OnMarkReady?.Invoke(result);
        }

        void Enqueue(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            _tcsEnqueue?.TrySetResult(result);
            _tcsEnqueue = null;
            OnEnqueue?.Invoke(result);
        }

        #endregion
    }
}