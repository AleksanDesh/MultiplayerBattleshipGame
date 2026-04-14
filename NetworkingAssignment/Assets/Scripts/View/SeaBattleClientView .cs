using Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Temporal semi-controller.
    /// </summary>
    internal sealed class SeaBattleClientView : MonoBehaviour
    {
        [SerializeField] private SeaBattleClientController _controller;



        [Header("Output")]
        [SerializeField] private TMP_Text _resultText;

        
        private void Awake()
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<SeaBattleClientController>();

            if (_controller == null)
                Debug.LogError("SeaBattleClientView: Controller not found.");
        }

        // Maybe add in the future
        public void OnUsernameChanged()
        {
            if (_controller == null)
                return;

        }






        // Scrap everything lower than this :D
        public void OnPlaceShipClicked()
        {
            //ShowResult(CallWithCoordinates(_controller.PlaceShip));
        }

        public void OnPlaceMineClicked()
        {
            //ShowResult(CallWithCoordinates(_controller.PlaceMine));
        }

        public void OnBombClicked()
        {
            //ShowResult(CallWithCoordinates(_controller.Bomb));
        }

        public void OnMarkReadyClicked()
        {
            if (_controller == null)
            {
                ShowResult("SeaBattleClientView: Controller missing.");
                return;
            }

            ShowResult(_controller.MarkReady());
        }

        //private string CallWithCoordinates(System.Func<int, int, string> action)
        //{
        //    if (!TryReadCoordinates(out int x, out int y))
        //        return "SeaBattleClientView: Invalid coordinates.";
        //
        //    if (_controller == null)
        //        return "SeaBattleClientView: Controller missing.";
        //
        //    return action(x, y);
        //}

        //private bool TryReadCoordinates(out int x, out int y)
        //{
        //    x = 0;
        //    y = 0;
        //
        //    if (_xInput == null || _yInput == null)
        //        return false;
        //
        //    return int.TryParse(_xInput.text, out x) &&
        //           int.TryParse(_yInput.text, out y);
        //}

        private void ShowResult(string message)
        {
            if (_resultText != null)
                _resultText.text = message;

            Debug.Log(message);
        }
    }
}