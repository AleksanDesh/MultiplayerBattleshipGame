using Model;
using NetworkConnections;
using OSCTools;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using static Model.PlayerData;
using static Model.SeaBattleBehavior;

namespace Network
{
    internal class Server
    {
        public Server() => Awake();
        // ----- General server code:
        TcpListener? _listener;
        List<TcpNetworkConnection> _allConnections = new List<TcpNetworkConnection>();
        List<TcpNetworkConnection> _unloggedConnections = new List<TcpNetworkConnection>();
        OSCDispatcher _dispatcher = new OSCDispatcher();

        SeaBattleBehavior _battleBehavior = new SeaBattleBehavior();
        Login _login = new Login("Data\\players.json");
        //public Login Login => _login;
        HashSet<PlayerData> _connectedPlayersMap = new HashSet<PlayerData>();
        List<PlayerData> _connectedPlayersList = new List<PlayerData>();
        List<SessionData> _sessionDatas = new List<SessionData>();
        Dictionary<string, SessionData> _userSessionKey = new Dictionary<string, SessionData>();
        // Probably this is redundant (and therefore can cause bugs later?):
        Dictionary<string, PlayerData> _userPlayerKey = new Dictionary<string, PlayerData>();
        Dictionary<TcpNetworkConnection, PlayerData> _tcpNetPlayerKey = new Dictionary<TcpNetworkConnection, PlayerData>();
        Dictionary<string, TcpNetworkConnection> _userTcpKey = new Dictionary<string, TcpNetworkConnection>();
        Dictionary<IPEndPoint, TcpNetworkConnection> _ipEndToTcpNetKey = new Dictionary<IPEndPoint, TcpNetworkConnection>();


        readonly object _sessionCleanupLock = new object();
        readonly Dictionary<int, Timer> _sessionCleanupTimers = new Dictionary<int, Timer>();


        // Queing for players, now enques all newly connected users
        //Queue<PlayerData> _playerQueue = new Queue<PlayerData>();
        private sealed class QueuedPlayer
        {
            public PlayerData Player { get; }
            public int QueueId { get; }

            public QueuedPlayer(PlayerData player, int queueId)
            {
                Player = player;
                QueueId = queueId;
            }
        }
        private readonly List<QueuedPlayer> _matchQueue = new List<QueuedPlayer>();

        private static bool CanMatch(int a, int b)
        {
            return (a == 0 || b == 0 || a == b) && (a != 0 || b != 0);
        }


        public static string GetLocalIPv4()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            var ip = host.AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork &&
                                     !IPAddress.IsLoopback(a));

            return ip?.ToString() ?? "No IPv4 found";
        }
        public void Awake()
        {
            //string path = Path.Combine(Application.persistentDataPath, "players.json");
            OSCLog.logging = true;

            // TODO: make the port auto adjustable
            int port = 5376;
            OSCLog.WriteLine("Starting server at " + port);
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            _dispatcher.ShowIncomingMessages = true;
            Initialize();
            OSCLog.WriteLine(("Host IP: " + GetLocalIPv4()));
        }

        void AcceptNewConnection()
        {
            if (_listener != null && _listener.Pending())
            {
                TcpClient client = _listener.AcceptTcpClient();
                TcpNetworkConnection connection = new TcpNetworkConnection(client);
                IPEndPoint remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;

                _allConnections.Add(connection);
                _unloggedConnections.Add(connection);

                // Keep the latest socket for this endpoint.
                _ipEndToTcpNetKey[remoteEndPoint] = connection;

                OSCLog.WriteLine("Server: Adding new connection from " + connection.Remote);
            }
        }
        void UpdateConnections()
        {
            // Snapshot so disconnects do not invalidate enumeration.
            foreach (TcpNetworkConnection conn in _allConnections.ToArray())
            {
                try
                {
                    if (conn.IsDisconnected)
                    {
                        DisconnectConnection(conn, "Remote closed the connection");
                        continue;
                    }

                    while (conn.Available() > 0)
                        HandlePacket(conn.GetPacket(), conn.Remote);
                }
                catch (System.Exception ex) when (
                    ex is SocketException ||
                    ex is IOException ||
                    ex is ObjectDisposedException)
                {
                    DisconnectConnection(conn, ex.Message);
                }
            }
        }
        #region Helpers

        private string HandleBattleException(string username, string action, Exception ex, out int result)
        {
            result = -1;

            if (ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                OSCLog.WriteLine($"Server: {action} failed for {username}: {ex.Message}");
                return ex.Message;
            }

            OSCLog.WriteLine($"Server: Unexpected {action} error for {username}: {ex}");
            return "Server error";
        }

        void HandlePacket(byte[] packet, IPEndPoint remote)
        {
            try
            {
                OSCMessageIn mess = new OSCMessageIn(packet);
                OSCLog.WriteLine("Server: Message arrived to server: " + mess);

                _dispatcher.HandlePacket(packet, remote);
            }
            catch (Exception ex)
            {
                OSCLog.WriteLine($"Server: packet handling failed from {remote}: {ex}");

                if (TryGetConnection(remote, out var conn))
                    DisconnectConnection(conn, "Bad packet or handler failure");
            }
        }
        bool TryGetConnection(IPEndPoint remote, out TcpNetworkConnection connection)
        {
            if (_ipEndToTcpNetKey.TryGetValue(remote, out connection))
                return true;

            OSCLog.WriteLine($"Server: No connection found for {remote}");
            return false;
        }

        bool TryGetPlayer(TcpNetworkConnection connection, out PlayerData player)
        {
            if (_tcpNetPlayerKey.TryGetValue(connection, out player))
                return true;

            OSCLog.WriteLine($"Server: Connection {connection.Remote} is not logged in");
            return false;
        }

        void RemoveQueuedPlayer(PlayerData player)
        {
            if (_matchQueue.Count == 0)
                return;

             _matchQueue.RemoveAll(qp => qp.Player.Username == player.Username);
            
        }

        void DisconnectConnection(TcpNetworkConnection connection, string reason)
        {
            // Cache the player first, because the dictionaries may be cleaned below.
            PlayerData? player = null;
            if (_tcpNetPlayerKey.TryGetValue(connection, out var foundPlayer))
                player = foundPlayer;

            _allConnections.Remove(connection);
            _unloggedConnections.Remove(connection);

            if (player != null)
            {
                _tcpNetPlayerKey.Remove(connection);
                _userTcpKey.Remove(player.Username);
                _connectedPlayersMap.Remove(player);
                _connectedPlayersList.RemoveAll(p => p.Username == player.Username);

                if (player.SessionState == PlayerSessionState.InQueue)
                {
                    // Queued users are removed completely.
                    RemoveQueuedPlayer(player);
                    player.SetSessionState(PlayerSessionState.InMenu); // fully disconnected
                }
                else if (player.SessionState != PlayerSessionState.InGame)
                {
                    player.SetSessionState(PlayerSessionState.InMenu); // disconnected outside battle, back to menu state
                }
                else if (player.SessionState == PlayerSessionState.InGame)
                {   // TODO: Make this happen. Now it is cleaned
                    // Keep the session mapping only for in-battle reconnects.
                    // This deliberately does not remove _userSessionKey.
                    
                    CleanupSession(_userSessionKey[player.Username], "Player disconnected, clearing");
                }

  

                    OSCLog.WriteLine($"Server: Disconnected {player.Username}. Reason: {reason}");
            }
            else
            {
                OSCLog.WriteLine($"Server: Disconnected unlogged connection {connection.Remote}. Reason: {reason}");
            }

            // Remove any stale endpoint mapping that still points to this connection.
            if (_ipEndToTcpNetKey.TryGetValue(connection.Remote, out var mappedConnection) &&
                ReferenceEquals(mappedConnection, connection))
            {
                _ipEndToTcpNetKey.Remove(connection.Remote);
            }
        }

        void SendRejoinPayload(TcpNetworkConnection connection, PlayerData player, SessionData session)
        {
            // Intentionally empty for now.
            // TODO: Fill this with reconnect/rejoin packet payload later.
            //OSCLog.WriteLine($"User {player.Username} reconnected. Currently no logic to reconnect to the battle.");
            //if (session.TryGetEnemyParticipant(player, out var participant))
            //{// Tell the connected enemy, that he lost, so he resets the scene. This ?might? fail if the other party left as well.
            // // If both leave, and none rejoin. The session will remain cached.
            //    var enemyPlayer = participant?.Player; 
            //    OSCMessageOut kickMessage = new OSCMessageOut("/Victory").AddBool(false);
            //    if(_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
            //        enemyConnection.Send(kickMessage.GetBytes());   
            //}   
            CleanupSession(session, "Disconnected user tryed to reconnect. No logic to reconnect yet, so cleaning up");
        }

        void StartSessionCleanupTimer(SessionData session)
        {
            lock (_sessionCleanupLock)
            {
                if (_sessionCleanupTimers.ContainsKey(session.SessionID))
                    return;

                // Reconnect window for a disconnected player.
                var timer = new Timer(
                    _ => CleanupSession(session, "Reconnect timeout"),
                    null,
                    TimeSpan.FromMinutes(1),
                    Timeout.InfiniteTimeSpan);

                _sessionCleanupTimers[session.SessionID] = timer;
            }
        }

        void CancelSessionCleanupTimer(SessionData session)
        {
            lock (_sessionCleanupLock)
            {
                if (_sessionCleanupTimers.TryGetValue(session.SessionID, out var timer))
                {
                    timer.Dispose();
                    _sessionCleanupTimers.Remove(session.SessionID);
                }
            }
        }

        void CleanupSession(SessionData session, string reason)
        {
            CancelSessionCleanupTimer(session);

            var usernames = _userSessionKey
                .Where(kvp => ReferenceEquals(kvp.Value, session))
                .Select(kvp => kvp.Key)
                .ToList();

            OSCMessageOut kickMessage = new OSCMessageOut("/Victory").AddBool(false);

            foreach (var username in usernames)
            {
                _userSessionKey.Remove(username);

                if (_userPlayerKey.TryGetValue(username, out var player))
                    player.SetSessionState(PlayerSessionState.InMenu);

                // Kick all the players from the session. Sends defeat message
                if (_userTcpKey.TryGetValue(username, out var connection))
                    connection.Send(kickMessage.GetBytes());
            }

            _sessionDatas.Remove(session);

            OSCLog.WriteLine($"Server: Session {session.SessionID} cleaned. Reason: {reason}");
        }

        #endregion
        //void CleanConnections()
        //{
        //
        //}
        public void Update()
        {
            AcceptNewConnection();
            UpdateConnections();
        }


        public void FixedUpdate()
        {
            if (_matchQueue.Count > 1)
            {
                bool isSessionCreated = TryCreateSession();
                if (isSessionCreated) OSCLog.WriteLine($"Server: Session created succesefully");
            }
        }

        #region ConnectionRequests
        void Initialize()
        {
            _dispatcher.AddListener("/Login", ConnectionLoginRequest, OSCUtil.STRING, OSCUtil.STRING);
            _dispatcher.AddListener("/Register", ConnectionRegisterRequest, OSCUtil.STRING, OSCUtil.STRING);
            _dispatcher.AddListener("/PlaceShip", ConnectionPlaceShipRequest, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
            _dispatcher.AddListener("/PlaceMine", ConnectionPlaceMineRequest, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT);
            _dispatcher.AddListener("/Bomb", ConnectionBombRequest, OSCUtil.INT, OSCUtil.INT);
            _dispatcher.AddListener("/MarkReady", ConnectionMarkReadyRequest);
            _dispatcher.AddListener("/Enqueue", ConnectionEnqueueRequest, OSCUtil.INT);
        }
        void ConnectionLoginRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadString() is not string username || message.ReadString() is not string password)
                return;

            OSCLog.WriteLine($"Server: Received Login attempt with username {username} and password {password}");

            if (!TryGetConnection(remote, out TcpNetworkConnection connection))
                return;
            
            bool sucess = TryUserLogin(username, password, connection, out var result, out PlayerData? playerData);

            OSCMessageOut reply = new OSCMessageOut("/TryJoin").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionRegisterRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadString() is not string username || message.ReadString() is not string password)
                return;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection))
                return;

            OSCLog.WriteLine($"Server: Received Register attempt with username {username} and password {password}");
            bool sucess = TryUserRegister(username, password, out var result, out PlayerData? playerData);
            OSCMessageOut reply = new OSCMessageOut("/TryRegister").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionPlaceShipRequest(OSCMessageIn message, IPEndPoint remote)
        {// TODO: if this fail, maybe notify the client?
            if (message.ReadInt() is not int id
                || message.ReadInt() is not int x
                || message.ReadInt() is not int y
                || message.ReadInt() is not int len)
                return;

            bool rot = message.ReadBool();

            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            Vector2 coordinates = new Vector2(x, y);
            Ship ship = new Ship(coordinates, len, rot, id);

            int result;
            string debug;

            try
            {
                debug = PlaceShip(player.Username, ship, out result);
            }
            catch (Exception ex) when (
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "PlaceShip", ex, out result);
            }

            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/PlaceShip").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionPlaceMineRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int id ||
                message.ReadInt() is not int x ||
                message.ReadInt() is not int y)
                return;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            var mine = new Mine(new Vector2(x, y), id);

            int result;
            string debug;

            try
            {
                debug = PlaceMine(player.Username, mine, out result);
            }
            catch (Exception ex) when (
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "PlaceMine", ex, out result);
            }

            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/PlaceMine").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionBombRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int x || message.ReadInt() is not int y)
                return;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            int[] coordinates = new int[2] { x, y };

            // general helper method?
            if (!_userSessionKey.TryGetValue(player.Username, out var session))
                return;

            if (!_userTcpKey.TryGetValue(player.Username, out var activeConnection) ||
                !ReferenceEquals(activeConnection, connection))
                return;

            if (!session.TryGetEnemyParticipant(player, out var participant))
            {
                OSCLog.WriteLine($"Server: No enemy participant found for {player.Username}");
                return;
            }

            var enemyPlayer = participant?.Player;
            if (enemyPlayer == null)
            {
                OSCLog.WriteLine($"Server: {participant} does not have PlayerData.");
                return;
            }

            int result;
            List<BombTrace> extraHits = new List<BombTrace>();
            string debug;

            try
            {
                debug = Bomb(player.Username, coordinates, out result, out extraHits);
            }
            catch (Exception ex) when (
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "Bomb", ex, out result);
            }

            OSCLog.WriteLine(debug);

            OSCMessageOut playerBomb = new OSCMessageOut("/Bomb")
                .AddInt(result)
                .AddInt(coordinates[0])
                .AddInt(coordinates[1])
                .AddBool(true)
                .AddInt(extraHits.Count);

            

            if (result == 0 || result == 3 || result == 4) // enum??
            {
                if (_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
                {
                    OSCMessageOut enemyBomb = new OSCMessageOut("/Bomb")
                        .AddInt(result)
                        .AddInt(coordinates[0])
                        .AddInt(coordinates[1])
                        .AddBool(false)
                        .AddInt(extraHits.Count);


                    if (extraHits != null && extraHits.Count > 0)
                        foreach (var hit in extraHits)
                        {
                            enemyBomb.AddInt((int)hit.Result)
                                .AddInt(hit.X)
                                .AddInt(hit.Y);

                            playerBomb.AddInt((int)hit.Result)
                                .AddInt(hit.X)
                                .AddInt(hit.Y);
                        }

                    enemyConnection.Send(enemyBomb.GetBytes());
                }
            }
            else if (result == 6)
            {
                OSCMessageOut enemyPlayerMessage = new OSCMessageOut("/Victory").AddBool(false);
                OSCMessageOut playerMessage = new OSCMessageOut("/Victory").AddBool(true);

                connection.Send(playerMessage.GetBytes());

                if (_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
                    enemyConnection.Send(enemyPlayerMessage.GetBytes());
            }
            // Send the package to the sender no matter what
            connection.Send(playerBomb.GetBytes());
        }

        void ConnectionMarkReadyRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            int result;
            string debug;

            try
            {
                debug = MarkReady(player.Username, out result);
            }
            catch (Exception ex) when (
                ex is KeyNotFoundException ||
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "MarkReady", ex, out result);
            }

            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/MarkReady").AddInt(result);
            connection.Send(reply.GetBytes());

            if (result == 1)
            {
                if (_userSessionKey.TryGetValue(player.Username, out var session) &&
                    session.TryGetEnemyParticipant(player, out var enemyParticipant))
                {
                    var enemyPlayer = enemyParticipant.Player;
                    if (_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
                        enemyConnection.Send(reply.GetBytes());
                }
            }
        }
        
        /// <summary>
        /// Attempting to enqueue this user.
        /// - 1 = unexpected
        /// 0 = sucess
        /// 1 = Player not logged in
        /// 2 = already in queue
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remote"></param>
        void ConnectionEnqueueRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int queueId)
                return;

            if (queueId < 0 || queueId > 6) // bounds (not best to hardcode, but... it is not planned to change)
                return;

            int result = -1;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection))
                return;

            if (!_tcpNetPlayerKey.TryGetValue(connection, out var player))
                result = 1;
            else if (!_matchQueue.Any(q => q.Player.Username == player.Username))
            {
                player.SetSessionState(PlayerSessionState.InQueue);
                _matchQueue.Add(new QueuedPlayer(player, queueId));
                result = 0;
            }
            else
                result = 2;

            OSCMessageOut reply = new OSCMessageOut("/Enqueue").AddInt(result);
            connection.Send(reply.GetBytes());
        }
        #endregion

        #region UserLoging
        /// <summary>
        /// Attempt to login the user
        /// </summary>
        /// <param name="result"> The output of what happend. 
        /// -1 = something went REALLY WRONG
        /// 0 = sucess
        /// 1 = user already connected
        /// 2 = wrong username
        /// 3 = wrong password
        /// </param>
        /// <returns></returns>
        bool TryUserLogin(string username, string password, TcpNetworkConnection connection, out int result, out PlayerData? playerData)
        {
            result = -1;

            bool sucess = _login.LoginUser(username, password, out PlayerData? user);
            playerData = user;

            if (sucess && user != null)
            {
                if (_userTcpKey.TryGetValue(username, out var existingConnection) &&
                    !ReferenceEquals(existingConnection, connection))
                {
                    // Replace the previous socket for this user.
                    DisconnectConnection(existingConnection, "Replaced by a new login");
                }

                _userPlayerKey[username] = user;

                _connectedPlayersMap.Add(user);
                if (!_connectedPlayersList.Exists(p => p.Username == user.Username))
                    _connectedPlayersList.Add(user);

                _tcpNetPlayerKey[connection] = user;
                _userTcpKey[username] = connection;
                _unloggedConnections.Remove(connection);

                if (_userSessionKey.ContainsKey(username))
                    user.SetSessionState(PlayerSessionState.InGame);
                else
                    user.SetSessionState(PlayerSessionState.InMenu);

                OSCLog.WriteLine($"Server: {username} connected succesefully");
                result = 0;

                // Existing session means this is a reconnect/rejoin after disconnect or kick.
                if (user.SessionState == PlayerSessionState.InGame && _userSessionKey.TryGetValue(username, out var session))
                    SendRejoinPayload(connection, user, session);

                return true;
            }
            else
            {
                if (user == null)
                {
                    result = 2;
                    OSCLog.WriteLine($"Server: {username} user not found");
                }
                else
                    result = 3;

                return false;
            }
        }

        /// <summary>
        /// result values:
        ///  0  = registration succeeded.
        ///  1  = username is empty or whitespace.
        ///  2  = password is empty or whitespace.
        ///  3  = username already exists.
        /// -1  = something unexpected happened.
        /// playerData = the registered player on success, the existing player on duplicate username, or null otherwise.
        /// </summary>
        bool TryUserRegister(string username, string password, out int result, out PlayerData? playerData)
        {
            result = _login.RegisterUser(username, password, out playerData);
            return result == 0;
        }
        bool TryCreateSession()
        {
            QueuedPlayer? first = null;
            QueuedPlayer? second = null;


            for (int i = 0; i < _matchQueue.Count; i++)
            {
                for (int j = i + 1; j < _matchQueue.Count; j++)
                {
                    if (CanMatch(_matchQueue[i].QueueId, _matchQueue[j].QueueId))
                    {
                        first = _matchQueue[i];
                        second = _matchQueue[j];

                        _matchQueue.RemoveAt(j);
                        _matchQueue.RemoveAt(i);
                        break;
                    }
                }

                if (first != null)
                    break;
            }
            

            if (first == null || second == null)
                return false;

            return TryCreateSession(first.Player, second.Player, first.QueueId == 0 ? second.QueueId : first.QueueId);
        }
        bool TryCreateSession(PlayerData player1, PlayerData player2, int queueId)
        {
            // Check both if the connection is still valid.
            if (!_userTcpKey.TryGetValue(player1.Username, out var player1Connection) ||
                player1Connection == null ||
                player1Connection.Status != ConnectionStatus.Connected ||
                player1Connection.IsDisconnected)
            {
                OSCLog.WriteLine($"Server: {player1.Username} is no longer connected, skipping session creation.");
                // Put player2 back, since they were valid.
                _matchQueue.Add(new QueuedPlayer(player2, queueId));
                player1.SetSessionState(PlayerSessionState.InMenu);
                return false;
            }

            if (!_userTcpKey.TryGetValue(player2.Username, out var player2Connection) ||
                player2Connection == null ||
                player2Connection.Status != ConnectionStatus.Connected ||
                player2Connection.IsDisconnected)
            {
                OSCLog.WriteLine($"Server: {player2.Username} is no longer connected, skipping session creation.");
                player2.SetSessionState(PlayerSessionState.InMenu);

                // Put player1 back, since they were valid.
                _matchQueue.Add(new QueuedPlayer(player1, queueId));
                player1.SetSessionState(PlayerSessionState.InQueue);
                return false;
            }
            //TODO: create UI element for it and make this work, comment by NIK (alex, add the mines with queueId / 2)
            try
            {
                SessionData newSession = new SessionData(player1, player2, queueId, queueId / 2, queueId + 2);
                if (newSession == null)
                    return false;

                if (!_userSessionKey.ContainsKey(player1.Username) && !_userSessionKey.ContainsKey(player2.Username))
                {
                    _userSessionKey.Add(player1.Username, newSession);
                    _userSessionKey.Add(player2.Username, newSession);
                }
                else
                {
                    if (_userSessionKey.ContainsKey(player1.Username))
                        OSCLog.WriteLine($"Server: !!!! {player1.Username} already is in session, HOW are you creating another one?");
                    if (_userSessionKey.ContainsKey(player2.Username))
                        OSCLog.WriteLine($"Server: !!!! {player2.Username} already is in session, HOW are you creating another one?");

                    // Kick the players
                    DisconnectConnection(player1Connection, "already in session");
                    DisconnectConnection(player2Connection, "already in session");
                    return false;
                }

                _sessionDatas.Add(newSession);
                player1.SetSessionState(PlayerSessionState.InGame);
                player2.SetSessionState(PlayerSessionState.InGame);

                // This is for starting the battle. Broadcast
                OSCMessageOut player1Info =
                    new OSCMessageOut("/StartBattle").AddString(player2.Username).AddInt(player2.TopScore)
                    .AddInt(newSession.MaxShips).AddInt(newSession.MaxMines).AddInt(newSession.FirstMap.Length).AddBool(true); // bool if first turn
                OSCMessageOut player2Info =
                    new OSCMessageOut("/StartBattle").AddString(player1.Username).AddInt(player1.TopScore)
                    .AddInt(newSession.MaxShips).AddInt(newSession.MaxMines).AddInt(newSession.FirstMap.Length).AddBool(false);

                player1Connection.Send(player1Info.GetBytes());
                player2Connection.Send(player2Info.GetBytes());
                return true;
            }
            catch (Exception ex) when (ex is ArgumentNullException || ex is ArgumentOutOfRangeException)
            {
                // kick the players:
                DisconnectConnection(player1Connection, "Invalid session");
                DisconnectConnection(player2Connection, "Invalid session");
                OSCLog.WriteLine("Session creation faied due to: ", ex);
            }
            return false;
        }

        #endregion
        #region MethodsToCall
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="location"></param>
        /// <param name="result">
        /// -1 = unexpected
        ///internal enum PlaceShipResult
        //{
        ///    Success = 0,
        ///    OutOfBounds = 1,
        ///    CellOccupied = 2,
        ///    ShipNearby = 3,
        ///    ShipLimitReached = 4
        //}
        /// 5 = player not in session
        /// </param>
        /// <returns></returns>
        public string PlaceShip(string username, Ship ship, out int result)
        {
            result = -1;
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceShip(session, player, ship);

            string message = $"Server: {username} " + outcome.ToString();

            result = outcome switch
            {
                PlaceShipResult.Success => 0,
                PlaceShipResult.OutOfBounds => 1,
                PlaceShipResult.CellOccupied => 2,
                PlaceShipResult.ShipNearby => 3,
                PlaceShipResult.ShipLimitReached => 4,
                _ => -1
            };

            return message;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="location"></param>
        /// <param name="result">
        /// 0 = sucess
        /// 1 = out of bounds</param>
        /// <returns></returns>
        public string PlaceMine(string username, Mine mine, out int result)
        {
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceMine(session, player, mine);

            string message = $"Server: {username} " + outcome.ToString();

            result = outcome switch
            {
                PlaceMineResult.Success => 0,
                PlaceMineResult.OutOfBounds => 1,
                PlaceMineResult.CellOccupied => 2,
                PlaceMineResult.MineLimitReached => 3,
                _ => -1
            };

            return message;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="location"></param>
        /// <param name="result">
        /// -1 = ???
        /// 0 = success
        /// 1 = out of bounds
        /// 2 = already bombed
        /// 3 = empty 
        /// 4 = mine 
        /// 5 = not in a session
        /// 6 = victory </param>
        /// <returns></returns>
        public string Bomb(string username, int[] location, out int result, out List<SeaBattleBehavior.BombTrace> extraHits)
        {
            result = -1;
            extraHits = new List<BombTrace>();
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }

            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            BombingResult outcome;

            try
            {
                outcome = _battleBehavior.Bomb(session, player, location, out extraHits);
            }
            catch (InvalidOperationException error)
            {
                result = -1;
                var conn = _userTcpKey[username];
                DisconnectConnection(conn, "Malicious");
                return error.Message;
            }

            string message = $"Server: {username} " + outcome.ToString();

            result = outcome switch
            {
                BombingResult.Sucess => 0,
                BombingResult.OutOfBounds => 1,
                BombingResult.AlreadyBombed => 2,
                BombingResult.Empty => 3,
                BombingResult.Mine => 4,
                BombingResult.Victory => 6,
                _ => -1
            };

            if (outcome == BombingResult.Victory)
            {
                if (!session.TryGetEnemyParticipant(player, out var participant) || participant == null)
                    return message;

                var enemyPlayer = participant.Player;
                _login.SaveAccount(player);
                CleanupSession(session, $"{player.Username} won");
            }

            return message;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="result">
        /// 0 = sucess
        /// 1 = battle started
        /// 2 = ship not placed
        /// 3 = mines not placed</param>
        /// <returns></returns>
        public string MarkReady(string username, out int result)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.MarkReady(session, player);

            string message = $"Server: {username} " + outcome.ToString();

            result = outcome switch
            {
                MarkingResult.Success => 0,
                MarkingResult.BattleStarted => 1,
                MarkingResult.ShipsNotPlaced => 2,
                MarkingResult.MinesNotPlaced => 3,
                MarkingResult.AlreadyMarked => 4,
                _ => -1
            };

            if (outcome == MarkingResult.Success)
            {
                message = $"Server: sucessefuly pressed Ready. Waiting for the other party...";
            }

            return message;
        }
        #endregion


    }
}