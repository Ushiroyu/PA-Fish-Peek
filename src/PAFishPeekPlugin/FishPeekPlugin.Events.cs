using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using BepInEx;

namespace PAFishPeekPlugin;

public sealed partial class FishPeekPlugin
{
    private static Channel<string>? _eventJournal;
    private static Task? _eventJournalTask;
    private static readonly string _eventSession = Guid.NewGuid().ToString("N");

    private void StartEventJournal()
    {
        if (!Config.Bind("Workbench", "WriteAnonymousEvents", true,
                "Write anonymous fishing events for the local workbench.").Value)
        {
            return;
        }

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(128)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _eventJournal = channel;
        string folder = Path.Combine(Paths.BepInExRootPath, "fishpeek");
        _eventJournalTask = Task.Run(async () =>
        {
            bool errorLogged = false;
            await foreach (string line in channel.Reader.ReadAllAsync())
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, "encounters.jsonl");
                    if (File.Exists(path) && new FileInfo(path).Length >= 4 * 1024 * 1024)
                    {
                        File.Move(path, path + ".previous", true);
                    }
                    await File.AppendAllTextAsync(path, line + Environment.NewLine, new UTF8Encoding(false));
                }
                catch (Exception)
                {
                    if (!errorLogged)
                    {
                        errorLogged = true;
                        _log?.LogWarning("[FishPeek] 工作台事件记录写入失败，本次遭遇显示继续运行。");
                    }
                }
            }
        });
    }

    private static void RecordEncounter(int playerId, float tier, float level, int bodyType)
    {
        if (_eventJournal == null)
        {
            return;
        }
        try
        {
            string snapshot = JsonSerializer.Serialize(new
            {
                event_id = _eventSession + "-" + Guid.NewGuid().ToString("N")[..12],
                observed_at = DateTimeOffset.UtcNow.ToString("O"),
                player_id = "player-" + playerId,
                level,
                tier,
                body_type = bodyType,
                scene = TryGetActiveSceneName()
            });
            _eventJournal.Writer.TryWrite(snapshot);
        }
        catch (Exception)
        {
            // Journal failures must not interrupt the encounter display.
        }
    }

    private static void StopEventJournal()
    {
        _eventJournal?.Writer.TryComplete();
        _eventJournal = null;
        try { _eventJournalTask?.Wait(1000); }
        catch (Exception) { /* Shutdown remains best effort. */ }
        _eventJournalTask = null;
    }
}
