using NetworkConnections;
using OSCTools;
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Network
{
    public class Client : MonoBehaviour
    {
        public IPAddress ServerIP = IPAddress.Loopback;
        TcpNetworkConnection connection;
        OSCDispatcher dispatcher;

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
                connection = new TcpNetworkConnection(client);
                Debug.Log("Client: Connecting with client to server " + ServerIP);

                dispatcher = new OSCDispatcher();
                dispatcher.ShowIncomingMessages = true;
                Initialize();
                return true;
            }
            catch (Exception exp)
            {
                Debug.LogException(exp);
                return false;
            }
        }

        public bool Login(string username, string password)
        {
            return false;
        }

        public bool Register(string username, string password)
        {
            return false;
        }

        void Initialize()
        {
            // Subscribe to methods.
        }

        /// <summary>
        /// Called from NetworkConnection callback (connection.Update), when a packet arrives:
        /// </summary>
        void HandlePacket(byte[] packet, IPEndPoint remote)
        {
            OSCMessageIn mess = new OSCMessageIn(packet);
            Debug.Log("Message arrives on client: " + mess);
            dispatcher.HandlePacket(packet, remote);
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            // Check for incoming packets, and deal with them:
            while (connection.Available() > 0)
            {
                HandlePacket(connection.GetPacket(), connection.Remote);
            }
            // TODO: disconnect handling
        }

        //void Initialize()
        //{
        //    // The (optional) list of parameter types (OSCUtil.INT) lets the dispatcher filter
        //    //  messages that do not satisfy the expected signature (=parameter list):
        //    dispatcher.AddListener("/CellChange", CellChangeRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT);
        //    dispatcher.AddListener("/ActivePlayer", ActivePlayerChangeRpc, OSCUtil.INT);
        //    dispatcher.AddListener("/GameOver", GameOverRpc, OSCUtil.INT);
        //    dispatcher.AddListener("/PlayerInfo", PlayerInfoRpc, OSCUtil.INT);
        //}
    }
}