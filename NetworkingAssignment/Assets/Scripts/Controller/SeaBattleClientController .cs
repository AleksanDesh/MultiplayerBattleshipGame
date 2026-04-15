using Model;
using Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Controller
{
    internal sealed class SeaBattleClientController : MonoBehaviour
    {
        //[SerializeField] private Server _server;

        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _password;

        [Header("Inputs")]
        [SerializeField] private TMP_InputField _xInput;
        [SerializeField] private TMP_InputField _yInput;
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

            if (connected)
            {
                // TODO: make the joining logic here
                
            }
        }
        public void BtnPlaceShip()
        {
            if (!TryReadCoordinates(out var x, out var y))
            {
                Debug.LogWarning($"SeaBattleController: Invalid coodinates");
                return;
            }
            PlaceShip(x, y);
        }

        public async void PlaceShip(int x, int y)
        {// TODO: Make the controller change the view, then wait for the result,
         // and if the result is 0, do nothing, else, restore the previous position
            int result = await _networkClient.PlaceShip(x, y, null);
            
        }

        public void BtnPlaceMine()
        {
            if (!TryReadCoordinates(out var x, out var y))
            {
                Debug.LogWarning($"SeaBattleController: Invalid coodinates");
                return;
            }
            PlaceMine(x, y);
        }
        public async void PlaceMine(int x, int y)
        {// TODO: Make the controller change the view, then wait for the result,
         // and if the result is 0, do nothing, else, restore the previous position
            int result = await _networkClient.PlaceMine(x, y);

        }

        public void BtnBomb()
        {
            if (!TryReadCoordinates(out var x, out var y))
            {
                Debug.LogWarning($"SeaBattleController: Invalid coodinates");
                return;
            }
            Bomb(x, y);
        }
        public async void Bomb(int x, int y)
        {// TODO: Make the controller change the view, then wait for the result,
         // and if the result is 0, do nothing, else, restore the previous position
            int result = await _networkClient.Bomb(x, y);
        }

        public void BtnMarkReady()
        {
            MarkReady();
        }
        public async void MarkReady()
        {
            int result = await _networkClient.MarkReady();

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

        #region Helpers
        private bool TryReadCoordinates(out int x, out int y)
        {
            x = 0;
            y = 0;

            if (_xInput == null || _yInput == null)
                return false;

            return int.TryParse(_xInput.text, out x) &&
                   int.TryParse(_yInput.text, out y);
        }
        #endregion

    }
}