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
    { // TODO: Add error displaying dictionary + make it Singleton
        public static Client Instance { get; private set; }


        public IPAddress ServerIP = IPAddress.Loopback;// IPAddress.Parse("");//IPAddress.Loopback;
        TcpNetworkConnection _connection;
        OSCDispatcher _dispatcher;

        // Answer if the login was sucesseful
        private TaskCompletionSource<int> _tcsLogin;
        private TaskCompletionSource<int> _tcsRegister;

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
            Connect(); // TODO: make the connection happen in a different place?
        }
        public bool Connect(int port = 5376)
        {
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


        public Task<int> Login(string username, string password)
        {
            _tcsLogin = new TaskCompletionSource<int>();
            //OSCMessageOut message = new OSCMessageOut("/MakeMove").AddInt(row).AddInt(col);
            OSCMessageOut message = new OSCMessageOut("/Login").AddString(username).AddString(password);
            _connection?.Send(message.GetBytes());
            return _tcsLogin.Task;
        }

        public Task<int> Register(string username, string password)
        {
            _tcsRegister = new TaskCompletionSource<int>();

            OSCMessageOut message = new OSCMessageOut("/Register").AddString(username).AddString(password);
            _connection?.Send(message.GetBytes());
            return _tcsRegister.Task;
        }

        public Task<int> PlaceShip(int x, int y, Ship ship)
        {
            _pendingShipPlacement = new PendingShipPlacement(x, y, ship);
            // TODO: Add different sized ship details
            OSCMessageOut message = new OSCMessageOut("/PlaceShip").AddInt(ship.Id).AddInt(x).AddInt(y).AddInt(ship.Length).AddBool(ship.Vertical);
            _connection?.Send(message.GetBytes());
            return _pendingShipPlacement.Tcs.Task;
        }

        public Task<int> PlaceMine(int x, int y)
        {
            _pendingMinePlacement = new PendingMinePlacement(x, y);
            // TODO: Add different sized ship details
            OSCMessageOut message = new OSCMessageOut("/PlaceMine").AddInt(x).AddInt(y);
            _connection?.Send(message.GetBytes());
            return _pendingMinePlacement.Tcs.Task;
        }

        public Task<int> Bomb(int x, int y)
        {
            _pendingBombing = new Bombpckg(x, y, -2, true);
            OSCMessageOut message = new OSCMessageOut("/Bomb").AddInt(x).AddInt(y);
            _connection?.Send(message.GetBytes());
            return _pendingBombing.Tcs.Task;
        }

        public Task<int> MarkReady()
        {
            _tcsMarkReady = new TaskCompletionSource<int>();
            OSCMessageOut message = new OSCMessageOut("/MarkReady");
            _connection?.Send(message.GetBytes());
            return _tcsMarkReady.Task;
        }

        public Task<int> Enqueue()
        {
            _tcsEnqueue = new TaskCompletionSource<int>();
            OSCMessageOut message = new OSCMessageOut("/Enqueue");
            _connection?.Send(message.GetBytes());
            return _tcsEnqueue.Task;
        }
        #endregion

        // Update is called once per frame
        void Update()
        {
            // Check for incoming packets, and deal with them:
            while (_connection != null && _connection.Available() > 0)
            {
                HandlePacket(_connection.GetPacket(), _connection.Remote);
            }
            // TODO: disconnect handling
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
            _tcsLogin?.TrySetResult(result);
            _tcsLogin = null;
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

        void TryRegister(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
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
                OnMinePlacement?.Invoke(pending.location.X, pending.location.Y);
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