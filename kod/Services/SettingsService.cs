using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace StoneMaster.Corel.Services
{
    public sealed class SettingsService
    {
        private readonly string _folder;
        private readonly string _settingsFile;

        public SettingsService()
        {
            _folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StoneMaster");
            Directory.CreateDirectory(_folder);
            _settingsFile = Path.Combine(_folder, "settings.json");
        }

        public Dictionary<string, object> Load()
        {
            if (!File.Exists(_settingsFile))
                return new Dictionary<string, object>();

            var json = File.ReadAllText(_settingsFile, Encoding.UTF8);
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }

        public void Save(Dictionary<string, object> settings)
        {
            var json = new JavaScriptSerializer().Serialize(settings);
            File.WriteAllText(_settingsFile, json, Encoding.UTF8);
        }
    }
}
