using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using NAudio.Wave;
using Newtonsoft.Json;

namespace Doorbell;

public class Alert
{
    // NEU: Ein statischer Lock, um zu verhindern, dass mehrere Sounds gleichzeitig spielen
    private static bool isSoundPlaying = false;
    private static readonly object soundLock = new object();

    public bool ChatEnabled = false;
    public string ChatFormat = string.Empty;

    public bool SoundEnabled = false;
    public string SoundFile = string.Empty;
    public float SoundVolume = 1;

    public bool LalafellSoundEnabled = true;
    public string LalafellSoundFile = string.Empty;
    public float LalafellSoundVolume = 1;

    public void DoAlert(PlayerObject player)
    {
        PrintChat(player);
        PlaySound(player.IsLala);
    }

    public void PrintChat(PlayerObject player)
    {
        if (!ChatEnabled) return;

        var messageBuilder = new SeStringBuilder();
        messageBuilder.AddText($"[{Plugin.Name}] ");

        for (var i = 0; i < ChatFormat.Length; i++)
        {
            if (ChatFormat[i] == '<')
            {
                var tagEnd = ChatFormat.IndexOf('>', i + 1);
                if (tagEnd > i)
                {
                    var tag = ChatFormat.Substring(i, tagEnd - i + 1);
                    switch (tag)
                    {
                        case "<name>":
                            {
                                messageBuilder.AddText(player.Name);
                                i = tagEnd;
                                continue;
                            }
                        case "<world>":
                            {
                                messageBuilder.AddText(player.WorldName);
                                i = tagEnd;
                                continue;
                            }
                        case "<link>":
                            {
                                messageBuilder.Add(new PlayerPayload(player.Name, player.World));
                                i = tagEnd;
                                continue;
                            }
                        case "<species>":
                            {
                                messageBuilder.Add(new UIForegroundPayload(518));
                                messageBuilder.Add(new EmphasisItalicPayload(true));
                                messageBuilder.AddText(player.IsLala ? "This user is a Lalafel" : "");
                                messageBuilder.Add(new EmphasisItalicPayload(false));
                                messageBuilder.Add(new UIForegroundPayload(0));
                                i = tagEnd;
                                continue;
                            }
                    }
                }
            }

            messageBuilder.AddText($"{ChatFormat[i]}");
        }

        var entry = new XivChatEntry()
        {
            Message = messageBuilder.Build()
        };

        Plugin.Chat.Print(entry);
    }

    public void PlaySound(bool isLalaAlert)
    {
        bool useLalaSettings = isLalaAlert && LalafellSoundEnabled;

        if (useLalaSettings)
        {
        }
        else if (!SoundEnabled)
        {
            return;
        }

        // KORREKTUR: Verhindern, dass ein neuer Sound gestartet wird, wenn bereits einer läuft.
        lock (soundLock)
        {
            if (isSoundPlaying)
            {
                Plugin.Log.Debug("[Doorbell] Sound is already playing, skipping new sound request.");
                return;
            }
            isSoundPlaying = true;
        }

        Task.Run(() =>
        {
            string requiredSoundPath;
            float requiredVolume;

            if (useLalaSettings)
            {
                requiredVolume = LalafellSoundVolume;
                requiredSoundPath = LalafellSoundFile;
                if (string.IsNullOrWhiteSpace(requiredSoundPath))
                    requiredSoundPath = Path.Join(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "lalawarning.wav");
            }
            else
            {
                requiredVolume = SoundVolume;
                requiredSoundPath = SoundFile;
                if (string.IsNullOrWhiteSpace(requiredSoundPath))
                    requiredSoundPath = Path.Join(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "doorbell.wav");
            }

            if (!requiredSoundPath.IsHttpUrl() && !File.Exists(requiredSoundPath))
            {
                var errorMsg = $"[Doorbell] Sound file not found: {requiredSoundPath}";
                Plugin.Log.Warning(errorMsg);
                lock (soundLock) { isSoundPlaying = false; }
                return;
            }

            WaveStream? soundToPlay = null;
            WaveOutEvent? outputDevice = null;
            ManualResetEvent? waitHandle = null;

            try
            {
                waitHandle = new ManualResetEvent(false);

                if (requiredSoundPath.IsHttpUrl())
                {
                    soundToPlay = new MediaFoundationReader(requiredSoundPath);
                    outputDevice = new WaveOutEvent();
                    outputDevice.Volume = MathF.Max(0, MathF.Min(1, requiredVolume));
                }
                else
                {
                    soundToPlay = new AudioFileReader(requiredSoundPath);
                    (soundToPlay as AudioFileReader)!.Volume = requiredVolume;
                    outputDevice = new WaveOutEvent();
                }

                outputDevice.PlaybackStopped += (sender, args) =>
                {
                    // Sicherstellen, dass wir nicht auf ein bereits zerstörtes Handle zugreifen
                    if (waitHandle != null && !waitHandle.SafeWaitHandle.IsClosed)
                    {
                        waitHandle.Set();
                    }
                };

                outputDevice.Init(soundToPlay);
                outputDevice.Play();

                // Wartet, bis das PlaybackStopped-Event signalisiert wird
                waitHandle.WaitOne(10000);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "[Doorbell] Caught exception during sound playback.");
            }
            finally
            {
                // Garantiert die Freigabe aller Ressourcen in der korrekten Reihenfolge
                soundToPlay?.Dispose();
                outputDevice?.Dispose();
                waitHandle?.Dispose();

                // Den Lock freigeben, damit der nächste Sound gespielt werden kann.
                lock (soundLock)
                {
                    isSoundPlaying = false;
                }
            }
        });
    }
}
