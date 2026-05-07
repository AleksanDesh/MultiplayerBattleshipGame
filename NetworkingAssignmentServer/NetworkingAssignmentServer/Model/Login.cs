using System;
using System.Linq;

namespace Model
{
    internal class Login
    {
        private readonly PlayerStore _store;

        public Login(string filePath)
        {
            _store = new PlayerStore(filePath);

        }

        public bool LoginOrCreate(string username, string password, out PlayerData? playerData)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Login username cannot be empty.", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Login password cannot be empty.", nameof(password));

            var players = _store.Load();

            var existing = players.FirstOrDefault(p =>
                string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));
            playerData = null;
            if (existing != null)
            {
                if (existing.Password != password)
                    return false;
                playerData = existing;
                return true;
            }

            var newPlayer = new PlayerData(username, password);
            players.Add(newPlayer);
            _store.Save(players);

            playerData = newPlayer;
            return true;
        }

        public bool LoginUser(string username, string password, out PlayerData? playerData)
        { // TODO: maybe add details here why didn't work? Though you can find it out from the output
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Login username cannot be empty.", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Login password cannot be empty.", nameof(password));
            var players = _store.Load();

            playerData = players.FirstOrDefault(p =>
                string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));

            if (playerData != null)
            {
                if (playerData.Password != password)
                    return false;

                return true;
            }
            return false;
        }

        /// <summary>
        /// Registers a user.
        /// Returns:
        ///  0  = registration succeeded.
        ///  1  = username is empty or whitespace.
        ///  2  = password is empty or whitespace.
        ///  3  = username already exists.
        /// -1  = something unexpected happened (for example, load/save failed).
        /// playerData = the new player on success, the existing player on duplicate username, or null on validation/unexpected failure.
        /// </summary>
        public int RegisterUser(string username, string password, out PlayerData? playerData)
        {
            playerData = null;

            if (string.IsNullOrWhiteSpace(username))
                return 1;

            if (string.IsNullOrWhiteSpace(password))
                return 2;

            try
            {
                var players = _store.Load();
                if (players == null)
                    return -1;

                playerData = players.FirstOrDefault(p =>
                    string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));

                if (playerData != null)
                    return 3;

                var newPlayer = new PlayerData(username, password);
                players.Add(newPlayer);
                _store.Save(players);

                playerData = newPlayer;
                return 0;
            }
            catch
            {
                playerData = null;
                return -1;
            }
        }

        public void SaveAccount(PlayerData player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            var players = _store.Load();

            var existing = players.FirstOrDefault(p =>
                string.Equals(p.Username, player.Username, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
                throw new InvalidOperationException("Account does not exist.");

            existing.UpdateTopScore(player.TopScore);

            _store.Save(players);
        }
    }
}