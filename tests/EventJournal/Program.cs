using System.Text.Json;

PAFishPeekPlugin.FishPeekPlugin.RunJournalProbe();

namespace BepInEx
{
    public static class Paths
    {
        public static string BepInExRootPath { get; set; } = "";
    }
}

namespace PAFishPeekPlugin
{
    public sealed partial class FishPeekPlugin
    {
        private ConfigStub Config { get; } = new();
        private static LogStub? _log = new();
        private static string TryGetActiveSceneName() => "journal-test";

        public static void RunJournalProbe()
        {
            string root = Path.Combine(Path.GetTempPath(), "fishpeek-journal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            BepInEx.Paths.BepInExRootPath = root;
            var plugin = new FishPeekPlugin();
            plugin.StartEventJournal();
            for (int i = 0; i < 8; i++) RecordEncounter(i + 1, 1, 6, 1);
            StopEventJournal();
            string path = Path.Combine(root, "fishpeek", "encounters.jsonl");
            string[] lines = File.ReadAllLines(path);
            Check(lines.Length == 8, "queued event drain");
            foreach (string line in lines)
            {
                using JsonDocument json = JsonDocument.Parse(line);
                Check(json.RootElement.GetProperty("player_id").GetString()!.StartsWith("player-"), "anonymous identity");
                Check(json.RootElement.GetProperty("level").GetSingle() == 6, "level preserved");
                Check(!json.RootElement.TryGetProperty("nickname", out _), "no nickname");
                Check(!line.Contains("ControllerPtr"), "no native pointer");
            }
            File.WriteAllText(path, new string('x', 4 * 1024 * 1024));
            plugin.StartEventJournal();
            RecordEncounter(1, 1, 4, 1);
            StopEventJournal();
            Check(File.Exists(path + ".previous") && File.ReadAllLines(path).Length == 1, "rotation");
            BepInEx.Paths.BepInExRootPath = Path.Combine(root, "blocked");
            File.WriteAllText(BepInEx.Paths.BepInExRootPath, "not a directory");
            plugin.StartEventJournal();
            RecordEncounter(1, 1, 4, 1);
            StopEventJournal();
            Check(_log!.Warnings == 1, "I/O error contained");
            Console.WriteLine("Event journal checks passed: drain, schema, anonymity, rotation, I/O isolation.");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("Journal check failed: " + label);
        }

        private sealed class ConfigStub
        {
            public Entry Bind(string section, string name, bool value, string description) => new();
        }
        private sealed class Entry { public bool Value => true; }
        private sealed class LogStub
        {
            public int Warnings;
            public void LogWarning(string _) => Warnings++;
        }
    }
}
