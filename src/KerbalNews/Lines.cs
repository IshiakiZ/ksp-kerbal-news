using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace KerbalNews
{
    /// <summary>
    /// One thing the anchor says: what it is said for (the event), its name (its sound file's), its words as the voice is given
    /// them (with ElevenLabs' delivery in brackets, "[excited]"), and as the caption shows them (without).
    /// </summary>
    internal sealed class Line
    {
        public string Event, Id, Text, Caption;
    }

    /// <summary>
    /// What the anchor can say, read from PluginData/lines.txt: a line each, "event, a tab, the line's name, a tab, its words"
    /// (a # begins a note). Its sound is PluginData/Lines/name.ogg (or .wav), read when it is to be said, never at the game's
    /// start: a thousand of them would add to every loading. Many wordings of one event are picked among at random, not the one
    /// said last for it nor the two before.
    /// </summary>
    internal static class Lines
    {
        static readonly List<Line> all = new List<Line>();
        static readonly Dictionary<string, List<string>> recent = new Dictionary<string, List<string>>();
        static readonly System.Random dice = new System.Random();
        static bool read;
        public static string Why = "";

        public static string Folder => KSPUtil.ApplicationRootPath + "GameData/KerbalNews/PluginData/";

        public static int Count { get { Read(); return all.Count; } }

        public static void Read()
        {
            if (read) return;
            read = true;
            all.Clear();
            string path = Folder + "lines.txt";
            try
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    string text = raw.Trim();
                    if (text.Length == 0 || text.StartsWith("#")) continue;
                    string[] parts = text.Split('\t');
                    if (parts.Length < 3) continue;
                    string words = parts[2].Trim();
                    string caption = System.Text.RegularExpressions.Regex.Replace(words, @"\[[^\]]*\]", "");
                    caption = System.Text.RegularExpressions.Regex.Replace(caption, @"\s+", " ").Trim();
                    all.Add(new Line { Event = parts[0].Trim(), Id = parts[1].Trim(), Text = words, Caption = caption });
                }
                Why = all.Count + " lines";
            }
            catch (Exception ex) { Why = "the lines could not be read (" + ex.Message + ")"; }
        }

        /// <summary>Whether there is anything to say for an event.</summary>
        public static bool Has(string ev)
        {
            Read();
            foreach (Line l in all) if (l.Event == ev) return true;
            return false;
        }

        /// <summary>A line by its name, or null.</summary>
        public static Line Named(string id)
        {
            Read();
            return all.Find(l => l.Id == id);
        }

        /// <summary>A line for an event, not one of the last three said for it where there are more; null if there is none.</summary>
        public static Line Pick(string ev)
        {
            Read();
            var those = new List<Line>();
            foreach (Line l in all) if (l.Event == ev) those.Add(l);
            if (those.Count == 0) return null;
            if (!recent.TryGetValue(ev, out List<string> said)) recent[ev] = said = new List<string>();
            var fresh = those.FindAll(l => !said.Contains(l.Id));
            if (fresh.Count == 0) { said.Clear(); fresh = those; }
            Line pick = fresh[dice.Next(fresh.Count)];
            said.Add(pick.Id);
            while (said.Count > Math.Min(3, those.Count - 1)) said.RemoveAt(0);
            return pick;
        }

        /// <summary>The line's sound file, or null if it has none (then only its caption is shown).</summary>
        public static string SoundOf(Line line)
        {
            foreach (string ending in new[] { ".ogg", ".wav" })
            {
                string path = Folder + "Lines/" + line.Id + ending;
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>
        /// A line's sound read from its file, whole (not streamed: the mouth follows the voice by what is playing, which a
        /// stream does not let be read), handed to done (null where it could not be read).
        /// </summary>
        public static IEnumerator Load(string path, Action<AudioClip> done)
        {
            AudioType type = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV : AudioType.OGGVORBIS;
            string address;
            try { address = new Uri(path).AbsoluteUri; }
            catch (Exception) { address = KSPUtil.ApplicationFileProtocol + path; }
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(address, type))
            {
                if (request.downloadHandler is DownloadHandlerAudioClip handler) { handler.compressed = false; handler.streamAudio = false; }
                yield return request.SendWebRequest();
                AudioClip clip = null;
                if (!request.isNetworkError && !request.isHttpError)
                {
                    try { clip = DownloadHandlerAudioClip.GetContent(request); }
                    catch (Exception) { clip = null; }
                }
                if (clip != null)
                {
                    float until = Time.realtimeSinceStartup + 5f;
                    while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < until) yield return null;
                    if (clip.loadState == AudioDataLoadState.Failed || clip.samples <= 0) { UnityEngine.Object.Destroy(clip); clip = null; }
                }
                done(clip);
            }
        }
    }
}
