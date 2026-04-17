using Newtonsoft.Json;

namespace Model
{
    internal sealed class PlayerData
    {
        public enum PlayerSessionState
        {
            InMenu,
            InQueue,
            InGame
        }

        private static int _nextId;

        public static void SetNextId(int currentMaxId)
        {
            if (currentMaxId > _nextId)
                _nextId = currentMaxId;
        }

        public int Id { get; }
        public string Username { get; }
        public string Password { get; }
        public int TopScore { get; private set; }

        [JsonIgnore]
        public PlayerSessionState SessionState { get; private set; }

        [JsonConstructor]
        public PlayerData(int id, string username, string password, int topScore = 0)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            Id = id;
            Username = username;
            Password = password;
            TopScore = topScore;

            if (id > _nextId)
                _nextId = id;
        }

        public PlayerData(string username, string password, int topScore = 0)
            : this(Interlocked.Increment(ref _nextId), username, password, topScore)
        {
        }

        public bool UpdateTopScore(int newScore)
        {
            if (newScore <= TopScore)
                return false;

            TopScore = newScore;
            return true;
        }
    }
}