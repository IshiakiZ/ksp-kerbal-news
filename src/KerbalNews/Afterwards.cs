using System.Collections;
using UnityEngine;

namespace KerbalNews
{
    /// <summary>
    /// A line said after the flight has gone: the recovery's. The game leaves the flight for the space centre the moment a craft
    /// is recovered, and the channel (a part of the flight) goes with it; this outlives the change of scene, waits until the
    /// space centre is up and its recovery report with it, says the line in his voice with its caption under it, and goes.
    /// </summary>
    internal sealed class Afterwards : MonoBehaviour
    {
        Line line;
        AudioSource voice;
        AudioClip clip;
        float shownFrom = -1f, giveUpAt;
        bool arrived, reported;
        float arrivedAt;
        GUIStyle style;
        /// <summary>(For the development build: where the last such line has got to.)</summary>
        internal static string Said = "nothing yet";

        public static void Say(Line line)
        {
            var holder = new GameObject("Kerbal News: afterwards");
            DontDestroyOnLoad(holder);
            Afterwards said = holder.AddComponent<Afterwards>();
            said.line = line;
            said.giveUpAt = Time.realtimeSinceStartup + 60f;
            Said = "waiting for the space centre: " + line.Id;
        }

        void Start()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.loop = false;
            voice.priority = 10;
            GameEvents.onLevelWasLoadedGUIReady.Add(Arrived);
            GameEvents.onVesselRecovered.Add(Reported);
            StartCoroutine(Run());
        }

        // (When to speak: as the space centre's recovery report comes up, which the game raises with the recovery itself. Not by
        // the loaded scene, which the game names from the start of its loading, nor by the scene's interface being ready, which it
        // says while its loading screen is still up: both times his line was said over the black loading screen. If no report
        // comes, eight seconds after the space centre's interface.)
        void Arrived(GameScenes scene) { if (scene == GameScenes.SPACECENTER && !arrived) { arrived = true; arrivedAt = Time.realtimeSinceStartup; } }
        void Reported(ProtoVessel craft, bool quick) { reported = true; }

        IEnumerator Run()
        {
            // (the space centre loaded and settled a moment: anywhere else, or a minute gone, nothing is said)
            while (!reported && !(arrived && Time.realtimeSinceStartup - arrivedAt > 8f))
            {
                if (Time.realtimeSinceStartup > giveUpAt || HighLogic.LoadedScene == GameScenes.MAINMENU) { Said = "given up: " + line.Id; Destroy(gameObject); yield break; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            string path = Lines.SoundOf(line);
            if (path != null) yield return StartCoroutine(Lines.Load(path, loaded => clip = loaded));
            float until;
            if (clip != null)
            {
                voice.clip = clip;
                voice.volume = News.Loudness * Mathf.Clamp01(GameSettings.VOICE_VOLUME > 0f ? GameSettings.VOICE_VOLUME : 1f);
                voice.Play();
                until = Time.realtimeSinceStartup + clip.length + 0.6f;
            }
            else until = Time.realtimeSinceStartup + 2.5f + 0.065f * line.Caption.Length;
            shownFrom = Time.realtimeSinceStartup;
            Said = "saying " + line.Id + (clip != null ? ", voice playing" : ", caption only") + ": " + line.Caption;
            while (Time.realtimeSinceStartup < until) yield return null;
            Said = "said " + line.Id;
            Destroy(gameObject);
        }

        // (his caption as the channel shows it on the whole screen, with the channel's name before it: there is no picture of him here)
        void OnGUI()
        {
            if (shownFrom < 0f || !News.Captions) return;
            if (style == null || style.fontSize != Mathf.Max(13, Screen.height / 42))
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 42), alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
            float width = Screen.width * 0.56f;
            var cap = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.84f, width, Screen.height * 0.08f);
            News.Fill(cap, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(cap, "KNN: " + line.Caption, style);
        }

        void OnDestroy()
        {
            GameEvents.onLevelWasLoadedGUIReady.Remove(Arrived);
            GameEvents.onVesselRecovered.Remove(Reported);
            if (clip != null) Destroy(clip);
        }
    }
}
