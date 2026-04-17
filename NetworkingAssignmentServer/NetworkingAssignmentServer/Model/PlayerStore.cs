using Newtonsoft.Json;

namespace Model
{
    internal sealed class PlayerStore
    {
        private readonly string _filePath;

        public PlayerStore(string filePath)
        {
            _filePath = filePath;
        }

        public List<PlayerData> Load()
        {
            if (!File.Exists(_filePath))
                return new List<PlayerData>();

            string json = File.ReadAllText(_filePath);

            var players = JsonConvert.DeserializeObject<List<PlayerData>>(json)
                          ?? new List<PlayerData>();

            int maxId = players.Count == 0 ? 0 : players.Max(p => p.Id);
            PlayerData.SetNextId(maxId);

            return players;
        }

        public void Save(List<PlayerData> players)
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(players, Formatting.Indented);

            File.WriteAllText(_filePath, json);
        }

        public PlayerData? FindByName(string name)
        {
            var players = Load();
            return players.FirstOrDefault(p =>
                string.Equals(p.Username, name, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}