using Model;
using NetworkConnections;
using OSCTools;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Network
{
    public class Client : MonoBehaviour
    { // TODO: Add error displaying dictionary + make it Singleton
        public static Client Instance { get; private set; }

        public TMP_InputField IpInput;
        public IPAddress ServerIP = IPAddress.Loopback;// IPAddress.Parse("");//IPAddress.Loopback;
        private const int RequestTimeoutMs = 10000;

        TcpNetworkConnection _connection;
        OSCDispatcher _dispatcher;
        public delegate void ServerUnavailableEvent(string reason);
        public event ServerUnavailableEvent OnServerUnavailable;

        private TcpClient _tcpClient;
        private bool _serverUnavailableRaised;

        // Answer if the login was sucesseful
        private TaskCompletionSource<int> _tcsLogin;
        public delegate void OnLoginEvent(int result);
        public event OnLoginEvent OnLogin;

        private TaskCompletionSource<int> _tcsRegister;
        public delegate void OnRegisterEvent(int result);
        public event OnRegisterEvent OnRegister;

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

        public delegate void MinePlacementEvent(int x, int y, Mine mine);
        public event MinePlacementEvent OnMinePlacement;
        private PendingMinePlacement _pendingMinePlacement;
        private sealed class PendingMinePlacement
        {
            public Location location { get; }
            public Mine Mine { get; }
            public TaskCompletionSource<int> Tcs { get; }
            public PendingMinePlacement(int x, int y, Mine mine)
            {
                location = new Location(x, y);
                Tcs = new TaskCompletionSource<int>();
                Mine = mine;
            }
        }

        public delegate void BombingEvent(Bombpckg result);
        public event BombingEvent OnBombing;
        private Bombpckg _pendingBombing;
        public class Bombpckg
        {
            public Vector2Int location;
            public int result;
            // If true => bombing enemy location
            public bool IsForEnemy;
            public TaskCompletionSource<int> Tcs { get; }
            public Bombpckg(int x, int y, int result, bool isForEnemy)
            {
                location = new Vector2Int(x, y);
                Tcs = new TaskCompletionSource<int>();
                this.result = result;
                IsForEnemy = isForEnemy;
            }
        }

        public delegate void MarkReadyEvent(int result);
        public event MarkReadyEvent OnMarkReady;
        public TaskCompletionSource<int> _tcsMarkReady;

        public delegate void EnqueueEvent(int result);
        public event EnqueueEvent OnEnqueue;
        public TaskCompletionSource<int> _tcsEnqueue;

        public delegate void BattleStarted(BattleStartPckg package);
        public event BattleStarted OnBattleStarted;
        public struct BattleStartPckg
        {
            string enemyUsername;
            public string EnemyUsername => enemyUsername;
            int enemyVictories;
            public int EnemyVictories => enemyVictories;
            int shipPreset;
            public int ShipPreset => shipPreset;
            int minesAllowed;
            public int MinesAllowed => minesAllowed;
            int boardSize;
            public int BoardSize => boardSize;
            bool turn;
            public bool Turn => turn;

            public BattleStartPckg(string enemyUsername, int enemyVictories, int shipPreset, int minesAllowed, int boardSize, bool turn) : this()
            {
                this.enemyUsername = enemyUsername;
                this.enemyVictories = enemyVictories;
                this.shipPreset = shipPreset;
                this.minesAllowed = minesAllowed;
                this.boardSize = boardSize;
                this.turn = turn;
            }
        }

        public delegate void Vicotry(bool isWinner); // am I the winner
        public event Vicotry OnVictory;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            //Connect(); // TODO: make the connection happen in a different place?
        }

        public void ConnectBtn()
        {
            ServerIP = IPAddress.Parse(IpInput.text);

            Connect();
        }
        private void OnDisable() => ShutdownConnection();
        private void OnDestroy() => ShutdownConnection();
        private void OnApplicationQuit() => ShutdownConnection();

        private void ShutdownConnection()
        {
            try { _connection?.Close(); } catch { }
            try { _tcpClient?.Close(); } catch { }

            _connection = null;
            _tcpClient = null;
        }

        private void FailConnection(string reason, Exception ex = null)
        {
            if (_serverUnavailableRaised)
                return;

            _serverUnavailableRaised = true;

            var ioEx = ex == null ? new IOException(reason) : new IOException(reason, ex);

            _tcsLogin?.TrySetException(ioEx);
            _tcsRegister?.TrySetException(ioEx);
            _pendingShipPlacement?.Tcs?.TrySetException(ioEx);
            _pendingMinePlacement?.Tcs?.TrySetException(ioEx);
            _pendingBombing?.Tcs?.TrySetException(ioEx);
            _tcsMarkReady?.TrySetException(ioEx);
            _tcsEnqueue?.TrySetException(ioEx);

            _tcsLogin = null;
            _tcsRegister = null;
            _pendingShipPlacement = null;
            _pendingMinePlacement = null;
            _pendingBombing = null;
            _tcsMarkReady = null;
            _tcsEnqueue = null;

            if (ex != null) Debug.LogException(ex);
            else Debug.LogWarning(reason);

            ShutdownConnection();
            OnServerUnavailable?.Invoke(reason);
        }

        private async Task<int> WaitForResponse(TaskCompletionSource<int> tcs, string operation, int timeoutMs = RequestTimeoutMs)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

            if (completed == tcs.Task)
                return await tcs.Task;

            var ex = new TimeoutException($"{operation} timed out after {timeoutMs} ms.");
            tcs.TrySetException(ex);
            FailConnection(ex.Message, ex);
            throw ex;
        }

        public bool Connect(int port = 5376)
        {
            try
            {
                ShutdownConnection();
                _serverUnavailableRaised = false;

                _tcpClient = new TcpClient();
                _tcpClient.Connect(new IPEndPoint(ServerIP, port));

                _connection = new TcpNetworkConnection(_tcpClient);

                Debug.Log("Client: Connecting with client to server " + ServerIP);

                _dispatcher = new OSCDispatcher();
                _dispatcher.ShowIncomingMessages = true;
                Initialize();
                return true;
            }
            catch (Exception exp)
            {
                Debug.LogException(exp);
                ShutdownConnection();
                return false;
            }
        }

        #region sendingMessages
        private bool TrySend(OSCMessageOut message, string operation)
        {
            if (_connection == null)
            {
                FailConnection($"{operation} failed: no active connection.");
                return false;
            }

            try
            {
                _connection.Send(message.GetBytes());
                return true;
            }
            catch (Exception ex)
            {
                FailConnection($"{operation} failed: server is unavailable.", ex);
                return false;
            }
        }

        public Task<int> Login(string username, string password)
        {
            _tcsLogin = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcs = _tcsLogin;

            var message = new OSCMessageOut("/Login").AddString(username).AddString(password);
            if (!TrySend(message, "Login"))
            {
                tcs.TrySetException(new IOException("Login failed: no active connection."));
                return tcs.Task;
            }

            return WaitForResponse(tcs, "Login");
        }

        public Task<int> Register(string username, string password)
        {
            _tcsRegister = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcs = _tcsRegister;

            var message = new OSCMessageOut("/Register").AddString(username).AddString(password);
            if (!TrySend(message, "Register"))
            {
                var ex = new IOException("Register failed: no active connection.");
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return WaitForResponse(tcs, "Register");
        }

        public Task<int> PlaceShip(int x, int y, Ship ship)
        {
            _pendingShipPlacement = new PendingShipPlacement(x, y, ship);
            var tcs = _pendingShipPlacement.Tcs;

            var message = new OSCMessageOut("/PlaceShip")
                .AddInt(ship.Id)
                .AddInt(x)
                .AddInt(y)
                .AddInt(ship.Length)
                .AddBool(ship.Vertical);

            if (!TrySend(message, "PlaceShip"))
            {
                var ex = new IOException("PlaceShip failed: no active connection.");
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return WaitForResponse(tcs, "PlaceShip");
        }

        public Task<int> PlaceMine(int x, int y, Mine mine)
        {
            _pendingMinePlacement = new PendingMinePlacement(x, y, mine);
            var tcs = _pendingMinePlacement.Tcs;

            var message = new OSCMessageOut("/PlaceMine").AddInt(mine.Id).AddInt(x).AddInt(y);
            if (!TrySend(message, "PlaceMine"))
            {
                var ex = new IOException("PlaceMine failed: no active connection.");
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return WaitForResponse(tcs, "PlaceMine");
        }

        public Task<int> Bomb(int x, int y)
        {
            _pendingBombing = new Bombpckg(x, y, -2, true);
            var tcs = _pendingBombing.Tcs;

            var message = new OSCMessageOut("/Bomb").AddInt(x).AddInt(y);
            if (!TrySend(message, "Bomb"))
            {
                var ex = new IOException("Bomb failed: no active connection.");
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return WaitForResponse(tcs, "Bomb");
        }

        public Task<int> MarkReady()
        {
            _tcsMarkReady = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcs = _tcsMarkReady;

            var message = new OSCMessageOut("/MarkReady");
            if (!TrySend(message, "MarkReady"))
            {
                var ex = new IOException("MarkReady failed: no active connection.");
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return WaitForResponse(tcs, "MarkReady");
        }

        public Task<int> Enqueue(int queueId)
        {
            _tcsEnqueue = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcs = _tcsEnqueue;

            var message = new OSCMessageOut("/Enqueue").AddInt(queueId);
            if (!TrySend(message, "Enqueue"))
            {
                var ex = new IOException("Enqueue failed: no active connection.");
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return WaitForResponse(tcs, "Enqueue");
        }
        #endregion

        // Update is called once per frame
        void Update()
        {
            if (_connection == null || _tcpClient == null)
                return;

            try
            {
                var socket = _tcpClient.Client;

                // Detect a graceful remote close.
                if (socket != null && socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                {
                    FailConnection("Server closed the connection.");
                    return;
                }

                while (_connection.Available() > 0)
                {
                    HandlePacket(_connection.GetPacket(), _connection.Remote);
                }
            }
            catch (Exception ex)
            {
                FailConnection("Lost connection to server.", ex);
            }
        }

        // TODO: Make every method call the view error display,
        // and send it the result if not 0, so it displays the error as a string
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
            _dispatcher.AddListener("/TryJoin", TryJoin, OSCUtil.INT);
            _dispatcher.AddListener("/TryRegister", TryRegister, OSCUtil.INT);
            _dispatcher.AddListener("/PlaceShip", PlaceShip, OSCUtil.INT);
            _dispatcher.AddListener("/PlaceMine", PlaceMine, OSCUtil.INT);
            // result, x, y, IsForEnemy
            _dispatcher.AddListener("/Bomb", Bomb, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
            _dispatcher.AddListener("/MarkReady", MarkReady, OSCUtil.INT);
            _dispatcher.AddListener("/Enqueue", Enqueue, OSCUtil.INT);
            // We receive name, the oponent's victories coumt, ship preset (or game preset)
            // (2, 3, 4 etc. ships on the board allowed. Each number has a prefab of ships included),
            // mines to place
            // board size
            // bool if you have the first turn
            _dispatcher.AddListener("/StartBattle", StartBattle, OSCUtil.STRING, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
            _dispatcher.AddListener("/Victory", Victory, OSCUtil.BOOL); // If it's me who won
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
        void TryJoin(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            OnLogin?.Invoke(result);
            _tcsLogin?.TrySetResult(result);
            _tcsLogin = null;
        }

        void TryRegister(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            OnRegister?.Invoke(result);
            _tcsRegister?.TrySetResult(result);
            _tcsRegister = null;
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
                OnMinePlacement?.Invoke(pending.location.X, pending.location.Y, pending.Mine);
            // Else display an error
            // TODO.
            pending.Tcs?.TrySetResult(result);
        }

        void Bomb(OSCMessageIn message, IPEndPoint remote)
        {          // result, x, y, IsMyCell
            int result = message.ReadInt();
            int x = message.ReadInt();
            int y = message.ReadInt();
            bool isMy = message.ReadBool();
            var pending = _pendingBombing;
            _pendingBombing = null;

            if (pending == null) // If no pending -> enemy attacked us
            {
                pending = new Bombpckg(x, y, result, isMy);
            }
            else
                pending.result = result;

            OnBombing?.Invoke(pending);
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

        void StartBattle(OSCMessageIn message, IPEndPoint remote)
        {
            string enemyUsername = message.ReadString();
            int enemyVictories = message.ReadInt();
            int shipPreset = message.ReadInt();
            int minesAllowed = message.ReadInt();
            int boardSize = message.ReadInt();
            bool turn = message.ReadBool();
            BattleStartPckg package = new BattleStartPckg(enemyUsername, enemyVictories, shipPreset, minesAllowed, boardSize, turn);
            OnBattleStarted?.Invoke(package);
        }

        void Victory(OSCMessageIn message, IPEndPoint remote)
        {
            bool result = message.ReadBool();
            OnVictory?.Invoke(result);
        }
        #endregion
    }
}