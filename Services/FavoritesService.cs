using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FastExplorer.Services
{
    public sealed class FavoritesService
    {
        private readonly string _filePath;
        private List<string> _favorites;

        private static readonly string[] DefaultFavorites =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        public FavoritesService()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FastExplorer");
            Directory.CreateDirectory(appData);
            _filePath = Path.Combine(appData, "favorites.json");
            _favorites = LoadFromFile();
        }

        public IReadOnlyList<string> GetAll() => _favorites;

        public bool Contains(string path) =>
            _favorites.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));

        public void Add(string path)
        {
            if (!Contains(path))
            {
                _favorites.Add(path);
                SaveToFile();
            }
        }

        public void Remove(string path)
        {
            var removed = _favorites.RemoveAll(f =>
                string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                SaveToFile();
        }

        private List<string> LoadFromFile()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null && list.Count > 0)
                        return list;
                }
            }
            catch
            {
            }
            return new List<string>(DefaultFavorites);
        }

        private void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(_favorites);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
            }
        }
    }
}
