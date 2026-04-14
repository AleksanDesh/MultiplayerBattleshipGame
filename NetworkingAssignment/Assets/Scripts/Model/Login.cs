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

        public PlayerData LoginOrCreate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Login username cannot be empty.", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Login password cannot be empty.", nameof(password));

            var players = _store.Load();

            var existing = players.FirstOrDefault(p =>
                string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (existing.Password != password)
                    return null;

                return existing;
            }

            var newPlayer = new PlayerData(username, password);
            players.Add(newPlayer);
            _store.Save(players);

            return newPlayer;
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