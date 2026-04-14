using Model;
using Network;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Controller
{
    internal sealed class SeaBattleClientController : MonoBehaviour
    {
        //[SerializeField] private Server _server;

        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _password;
        public string Username => _username.text;

        Client _networkClient;

        private void Awake()
        {
            //if (_server == null)
            //    _server = FindFirstObjectByType<Server>();
            //
            //if (_server == null)
            //    Debug.LogError("SeaBattleClientController: Server not found.");
            if (_networkClient == null)
                _networkClient = FindFirstObjectByType<Client>();
            if (_networkClient == null)
                Debug.LogError("SeaBattleClientController: _networkClient not found.");

        }

        public async void Join()
        {
            //bool connecting = _server.ConnectUser(_username.text, _password.text);
            bool connected = await _networkClient.Login(_username.text, _password.text);

            Debug.Log($"SeaBattleClientController: Loging to the server" +
                $" was {connected} with username {_username.text} and password {_password.text}");
        }
        
        public string PlaceShip(int x, int y)
        {
            return "Nothing here for now";
            //return SendCommand(() => _server.PlaceShip(_username.text, new[] { x, y }));
        }

        public string PlaceMine(int x, int y)
        {
            return "Nothing here for now";
            //return SendCommand(() => _server.PlaceMine(_username.text, new[] { x, y }));
        }

        public string Bomb(int x, int y)
        {
            return "Nothing here for now";
            //return SendCommand(() => _server.Bomb(_username.text, new[] { x, y }));
        }

        public string MarkReady()
        {
            return "Nothing here for now";
            //return SendCommand(() => _server.MarkReady(_username.text));
        }

        private string SendCommand(Func<string> command)
        {
            return "Nothing here for now";
            //if (_server == null)
            //    return "SeaBattleClientController: Server is missing.";
            //
            //if (string.IsNullOrWhiteSpace(_username.text))
            //    return "SeaBattleClientController: Username is not set.";
            //
            //try
            //{
            //    return command();
            //}
            //catch (KeyNotFoundException)
            //{
            //    return "SeaBattleClientController: User is not connected to a session.";
            //}
            //catch (InvalidOperationException ex)
            //{
            //    return ex.Message;
            //}
            //catch (ArgumentException ex)
            //{
            //    return ex.Message;
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogException(ex);
            //    return "SeaBattleClientController: Unexpected error.";
            //}
        }

    }
}