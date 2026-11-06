using System;
using System.Collections.Generic;
using Keystone;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("KerbalNews", 0, 1)]
[assembly: KSPAssemblyDependency("Keystone", 0, 4)]
#endif

namespace KerbalNews
{
    /// <summary>
    /// A news channel in flight.
    ///
    /// Asked for (2026-10-18, 22:44): "create a news mod that lets you turn on a news channel and see a Kerbal in a suit with a
    /// microphone, like a real news channel. The Kerbal can change focus from center screen, where he is the focus, to the
    /// corner. A screen then shows whatever is happening, such as a scene." With about a thousand lines in ElevenLabs voices,
    /// many wordings of each, "the curvle's mouth to match the voice", and "based on actions that happen in the game, it can run
    /// a voice line". The voice is Robert, the American broadcaster (the owner's pick of four, 23:15). docs/news-plan.md.
    ///
    /// Switched on on the mod's page, the channel is ready, nothing shown; Alt+N (Option+N on a Mac) then brings it up in a small window
    /// in a corner and takes it down again (the owner, 2026-10-20 13:57: "when you switch it on, it shouldn't automatically pull up the
    /// modal ... You should use the hotkey to bring it up"). It comes on with static
    /// first as the crew's portraits have, then the anchor at his desk (Studio); what happens in the flight sets off his lines
    /// (Lines), a caption under each, his jaw following his voice. Over the window, a button at its top right puts him on the whole
    /// screen; there, when something is happening, he moves aside and the flight is shown beside him (Live), as a newscast puts its
    /// pictures beside the anchor, and a button at the top right puts him back in the small window. Asked for on 2026-10-19: "If you
    /// use a hotkey to open up the news modal, then it does a TV-static turn-on, and then it'll show it ... if you hover your mouse
    /// over, I want there to be a full-screen icon in the top right that you can click ... If something eventful is happening,
    /// then he'll move to the side ... you can minimize the screen into the modal again. The hotkey gets rid of the modal and
    /// brings it back on."
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class NewsAtStart : MonoBehaviour
    {
        void Awake() { News.Setup(); Destroy(gameObject); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class News : MonoBehaviour
    {
        public const string Version = "0.1.0";

        static Mod mod;
        internal static Toggle On, Captions;
        internal static Choice Corner, Chatter;
        internal static Slider Size, Loudness;
        // (the page's fine details, in its Advanced view: each as installed does what the mod did before it had them)
        internal static Slider StaticFor, GrowFor, SpellAfter, LeastGap;
        internal static float Of(Slider slider, float installed) => slider != null ? (float)slider : installed;

        /// <summary>The settings added after Fine(true) are fine details: on the page only in Keystone's Advanced view, until Fine(false).
        /// (Keystone 0.11.0 has the two views; an older one has neither call, and shows the fine details with the rest.)</summary>
        static void Fine(bool fine) { try { if (fine) Advanced(mod); else Simple(mod); } catch (Exception) { } }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Advanced(Mod page) => page.Advanced();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Simple(Mod page) => page.Simple();

        public static void Setup()
        {
            if (mod != null && Kit.Find("Kerbal News") == mod) return;
            mod = Kit.Register("Kerbal News", Version, "A news channel in flight: a kerbal anchor at his desk talks over what your flight does, from a small window in a corner of the screen or on the whole screen with the flight beside him. Switch the channel on here, then Alt+N (Option+N on a Mac) brings its window up and takes it down.");
            On = mod.Toggle("on", "News channel", false, "On: the channel is ready, nothing shown yet. Alt+N (Option+N on a Mac) brings the anchor's window up in its corner, with static first, and takes it down again; over the window, the button at its top right puts him on the whole screen.");
            Corner = mod.Choice("corner", "His box", 0, new[] { "bottom right", "bottom left", "top right", "top left" }, "Which corner of the screen the anchor's box is in.");
            Size = mod.Slider("size", "Box size", 0.28f, 0.15f, 0.5f, "", 2, "How wide his box is, as a share of the screen's width.");
            Fine(true);
            StaticFor = mod.Slider("static", "The static as he comes on", 0.7f, 0.2f, 3f, " s", 2, "How long his box shows a television's static before his picture clears.");
            GrowFor = mod.Slider("grow", "Growing to the whole screen takes", 0.45f, 0.1f, 2f, " s", 2, "How long his box takes to grow to the whole screen, and to go back.");
            Fine(false);
            Chatter = mod.Choice("chatter", "How much he talks", 1, new[] { "only the big moments", "normal", "chatty" }, "Only the big moments: lift-off, orbit, a landing, a crash and the like. Chatty: more, and sooner after the last line.");
            Fine(true);
            SpellAfter = mod.Slider("spell", "A quiet spell filled after", 90f, 20f, 600f, " s", 0, "How long he lets the flight go by without a word before he fills the quiet with something of his own: a joke, a fact, the mail. Chatty: half of it.");
            LeastGap = mod.Slider("gap", "The least time between his lines", 3f, 0.5f, 20f, " s", 1, "However much there is to say, no line of his begins sooner than this after the last. Chatty: half of it.");
            Fine(false);
            Loudness = mod.Slider("loudness", "Voice", 0.9f, 0f, 1f, "", 2, "How loud the anchor is (the game's own voice volume applies as well).");
            Captions = mod.Toggle("captions", "Captions", true, "What he says, written at the bottom of the screen.");
        }

        enum Show { Off, Small, Whole }
        Show show = Show.Off;
        // (the times here are the window's own clock (Update): it runs alike paused, in time warp and at the pause menu)
        float clock, frameStep;
        float onAt;                        // when the window came on: the static, then his picture
        float offAt = -1f;                 // when it was put off, its picture folding away; -1 while it is on
        bool signingOff;                   // taken down by the hotkey: his sign-off being said, the window to fold when he has (Key)
        float whole;                       // 0 the small window, 1 the whole screen, between while it grows or goes back
        float aside;                       // on the whole screen: 0 he is in the middle, 1 at the side with the flight beside him
        float busyUntil, eventAt = -100f;  // the flight shown beside him until then; when something last happened in it
        float hover;                       // the pointer over the small window, its button shown (eased)
        bool hidUI, burning;               // the game's interface put away for the whole screen; an engine of the craft burning
        const float ClearTime = 0.35f, FoldTime = 0.3f, AsideTime = 0.9f, BusyHold = 7f;
        // (0.7 s and 0.45 s as installed: the page's fine details may set others)
        static float StaticTime => Of(StaticFor, 0.7f);
        static float GrowTime => Of(GrowFor, 0.45f);
        Live live;
        Texture2D noise;
        Color32[] noiseDots;
        float noiseAt = -1f;
        readonly System.Random grain = new System.Random();
        static Texture gameStatic;
        static bool windowWanted;          // the hotkey's: the window up (while the channel is on)
#if DEV
        static bool testHover;
#endif

        Studio studio;
        AudioSource voice;
        Line speaking;
        float speakingUntil;               // when a line with no sound has been shown long enough
        bool voiceStarted;
        AudioClip clipNow;
        float lastSaid = -100f, mouth, bright;     // (how open his mouth is, 0 to 1; how bright the sound is, -1 dark to +1 bright)
        readonly float[] samples = new float[512];

        sealed class Wanted { public string Event; public bool Big; public float At; }
        readonly List<Wanted> waiting = new List<Wanted>();
        readonly HashSet<string> marks = new HashSet<string>();
        Guid marksFor;
        float crashSaidAt = -100f, stageSaidAt = -100f, launchedAt = -100f, chutesLookAt, fuelLookAt;
        static News instance;

        void Awake()
        {
            instance = this;
            Setup();
            studio = new Studio();
            // (on an object of its own named so, that other mods can find: Mission Music lets its music down under it, as under the
            // AI Bridge's voice; docs/sound-rules.md, M5)
            var speaker = new GameObject("KerbalNewsVoice");
            speaker.transform.SetParent(transform, false);
            voice = speaker.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.loop = false;
            voice.priority = 10;
            live = new Live();
            Lines.Read();
        }

        void Start()
        {
            GameEvents.onLaunch.Add(Launched);
            GameEvents.onStageSeparation.Add(Staged);
            GameEvents.onVesselSituationChange.Add(Situation);
            GameEvents.onCrash.Add(Crashed);
            GameEvents.onCrashSplashdown.Add(Crashed);
            GameEvents.onCrewOnEva.Add(Outside);
            GameEvents.onPartCouple.Add(Docked);
            GameEvents.onDominantBodyChange.Add(NewWorld);
            GameEvents.onFlagPlant.Add(Flag);
            GameEvents.OnScienceRecieved.Add(Science);
            GameEvents.OnVesselRecoveryRequested.Add(Recovering);
        }

        void OnDestroy()
        {
            GameEvents.onLaunch.Remove(Launched);
            GameEvents.onStageSeparation.Remove(Staged);
            GameEvents.onVesselSituationChange.Remove(Situation);
            GameEvents.onCrash.Remove(Crashed);
            GameEvents.onCrashSplashdown.Remove(Crashed);
            GameEvents.onCrewOnEva.Remove(Outside);
            GameEvents.onPartCouple.Remove(Docked);
            GameEvents.onDominantBodyChange.Remove(NewWorld);
            GameEvents.onFlagPlant.Remove(Flag);
            GameEvents.OnScienceRecieved.Remove(Science);
            GameEvents.OnVesselRecoveryRequested.Remove(Recovering);
            RestoreUI();
            if (live != null) live.Destroy();
            if (studio != null) studio.Destroy();
            if (clipNow != null) Destroy(clipNow);
            if (noise != null) Destroy(noise);
            if (instance == this) instance = null;
        }

        // ---------------------------------------------------------------- what happens in the flight

        static bool Ours(Vessel v) => v != null && v == FlightGlobals.ActiveVessel;

        void Launched(EventReport r)
        {
            launchedAt = Time.time;
            // (a plane rolling off the runway takes off; anything else lifts off)
            Vessel v = FlightGlobals.ActiveVessel;
            bool runway = v != null && (v.landedAt ?? "").IndexOf("Runway", StringComparison.OrdinalIgnoreCase) >= 0;
            Want(runway && Lines.Has("takeoff") ? "takeoff" : "liftoff", true);
        }

        void Staged(EventReport r)
        {
            // (not the staging that lit the engines on the pad, and not every stage of a quick run of them)
            if (Time.time - launchedAt < 3f || Time.time - stageSaidAt < 12f) return;
            stageSaidAt = Time.time;
            Want("staging", false);
        }

        void Situation(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> a)
        {
            if (!Ours(a.host)) return;
            bool wasAloft = a.from == Vessel.Situations.FLYING || a.from == Vessel.Situations.SUB_ORBITAL;
            switch (a.to)
            {
                case Vessel.Situations.ORBITING: Want("orbit", true); break;
                case Vessel.Situations.LANDED:
                    if (!wasAloft) break;
                    // (a landing on another world has its own lines, where there are any)
                    bool away = a.host.mainBody != null && a.host.mainBody != FlightGlobals.GetHomeBody();
                    Want(away && Lines.Has("landedaway") ? "landedaway" : "touchdown", true);
                    break;
                case Vessel.Situations.SPLASHED: if (wasAloft) Want(Lines.Has("splashdown") ? "splashdown" : "touchdown", true); break;
            }
        }

        void Crashed(EventReport r)
        {
            if (Time.time - crashSaidAt < 10f) return;
            crashSaidAt = Time.time;
            Want("crash", true);
        }

        void Outside(GameEvents.FromToAction<Part, Part> a) => Want("eva", true);

        void Docked(GameEvents.FromToAction<Part, Part> a)
        {
            if (a.from != null && a.to != null && (Ours(a.from.vessel) || Ours(a.to.vessel))) Want("dock", true);
        }

        void NewWorld(GameEvents.FromToAction<CelestialBody, CelestialBody> a) => Want("newworld", true);

        void Flag(Vessel v) => Want("flag", true);

        float scienceSaidAt = -100f;
        void Science(float amount, ScienceSubject subject, ProtoVessel from, bool reverse)
        {
            if (amount <= 0f || Time.time - scienceSaidAt < 30f) return;
            scienceSaidAt = Time.time;
            Want("science", false);
        }

        // (Recovered: the game leaves the flight for the space centre at once, and the channel goes with the flight; so his line on
        // the recovery is said by a small player of its own that outlives the change of scene (Afterwards), while he is on the air.
        // Until 2026-10-21 nothing said these lines, nor the sign-offs, though both were voiced.)
        void Recovering(Vessel v)
        {
            if (!Ours(v) || show == Show.Off || offAt >= 0f) return;
            Line line = Lines.Pick("recovered");
            if (line != null) Afterwards.Say(line);
        }

        /// <summary>Something to talk about: kept a few seconds for when he has finished his line, the big moments first.</summary>
        void Want(string ev, bool big)
        {
            if (show == Show.Off || signingOff) return;
            // (something happened: on the whole screen the flight is shown beside him, whether or not he has a line for it)
            if (ev != "quiet" && ev != "welcome") eventAt = clock;
            if (!Lines.Has(ev)) return;
            if (!big && Chatter == 0) return;
            waiting.RemoveAll(w => w.Event == ev);
            waiting.Add(new Wanted { Event = ev, Big = big, At = Time.time });
        }

        /// <summary>The heights passed and the quiet spells, looked at each frame.</summary>
        void Watch()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            if (v.id != marksFor) { marks.Clear(); marksFor = v.id; }
            bool aloft = v.situation == Vessel.Situations.FLYING || v.situation == Vessel.Situations.SUB_ORBITAL;
            if (aloft && v.altitude > 10000.0 && marks.Add("height")) Want("height", false);
            if (aloft && v.mainBody != null && v.mainBody.atmosphere && v.altitude > v.mainBody.atmosphereDepth && marks.Add("space")) Want("space", false);
            if (v.altitude < 5000.0) marks.Remove("height");
            // (faster than sound, in the air: once until it is slower again)
            if (aloft && v.mach > 1.02 && marks.Add("supersonic")) Want("supersonic", false);
            if (v.mach < 0.8) marks.Remove("supersonic");
            // (coming in hot: in the air, falling, faster than four times the speed of sound; once a descent)
            bool inAir = v.mainBody != null && v.mainBody.atmosphere && v.altitude < v.mainBody.atmosphereDepth;
            if (inAir && v.verticalSpeed < -50.0 && v.mach > 4.0 && v.srfSpeed > 1000.0 && marks.Add("reentry")) Want("reentry", true);
            if (v.verticalSpeed > 0.0 && v.altitude > 30000.0) marks.Remove("reentry");
            // (the fuel nearly gone while an engine burns: under a tenth of what the craft set out with, once until it has more)
            if (Time.time >= fuelLookAt)
            {
                fuelLookAt = Time.time + 1f;
                double have = 0.0, most = 0.0;
                burning = false;
                foreach (Part p in v.parts)
                {
                    foreach (PartResource r in p.Resources)
                        if (r.resourceName == "LiquidFuel" || r.resourceName == "SolidFuel" || r.resourceName == "MonoPropellant") { have += r.amount; most += r.maxAmount; }
                    ModuleEngines e = p.FindModuleImplementing<ModuleEngines>();
                    if (e != null && e.EngineIgnited && e.finalThrust > 0f) burning = true;
                }
                double share = most > 0.0 ? have / most : 1.0;
                if (burning && share < 0.1 && share > 0.0 && marks.Add("lowfuel")) Want("lowfuel", false);
                if (share > 0.2) marks.Remove("lowfuel");
            }
            // (the parachutes open: any of the craft's, once until they are all packed again)
            if (Time.time >= chutesLookAt)
            {
                chutesLookAt = Time.time + 0.5f;
                bool open = false;
                foreach (Part p in v.parts)
                {
                    ModuleParachute chute = p.FindModuleImplementing<ModuleParachute>();
                    if (chute != null && (chute.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED || chute.deploymentState == ModuleParachute.deploymentStates.DEPLOYED)) { open = true; break; }
                }
                if (open && marks.Add("chutes")) Want("chutes", true);
                if (!open) marks.Remove("chutes");
            }
            // (sunrise seen from on high: the craft out of the world's shadow again, once for each night it passed through up there)
            bool lit = Lit(v);
            bool high = (v.situation == Vessel.Situations.ORBITING || v.situation == Vessel.Situations.SUB_ORBITAL || v.situation == Vessel.Situations.ESCAPING)
                        && v.altitude > 20000.0;
            if (!lit && high) marks.Add("night");
            if (lit && marks.Remove("night") && high) Want("sunrise", false);
            // (a quiet spell: something to fill it, now and then, as fits the moment: see Spell)
            float spell = Of(SpellAfter, 90f), quiet = Chatter == 2 ? spell * 0.5f : spell;
            if (Chatter > 0 && speaking == null && Time.time - lastSaid > quiet && waiting.Count == 0) Want(Spell(v, lit), false);
        }

        // What a quiet spell is filled with (the owner, 2026-10-20: "voice lines that are just him talking, like when it's quiet ...
        // some joke lines ... to make him feel even more alive"). Half the time, where the flight has something to it, that: time
        // warp, a coast in space, the night side, a rover driving, a plane cruising. Otherwise the studio's own: plain chatter, a
        // joke, a fact about the worlds, the studio itself, a viewer's letter, a word from the sponsors.
        static readonly string[] studioKinds = { "quiet", "joke", "fact", "studio", "mail", "ad" };
        static readonly float[] studioWeights = { 3f, 2.5f, 1.5f, 1.2f, 0.8f, 0.7f };
        string Spell(Vessel v, bool lit)
        {
            bool space = v.situation == Vessel.Situations.ORBITING || v.situation == Vessel.Situations.SUB_ORBITAL || v.situation == Vessel.Situations.ESCAPING;
            bool aloft = v.situation == Vessel.Situations.FLYING;
            string flight = null;
            if (TimeWarp.CurrentRate > 1.5f) flight = "warp";
            else if (!lit && (space || aloft)) flight = "night";
            else if (space && !burning) flight = "coast";
            else if (v.situation == Vessel.Situations.LANDED && v.srfSpeed > 2.0 && Wheeled(v)) flight = "rover";
            else if (aloft && Math.Abs(v.verticalSpeed) < 15.0 && v.srfSpeed > 40.0 && v.heightFromTerrain > 300f) flight = "cruise";
            if (flight != null && Lines.Has(flight) && UnityEngine.Random.value < 0.5f) return flight;
            float total = 0f;
            foreach (float w in studioWeights) total += w;
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < studioKinds.Length; i++)
            {
                if (roll < studioWeights[i] && Lines.Has(studioKinds[i])) return studioKinds[i];
                roll -= studioWeights[i];
            }
            return "quiet";
        }

        // (whether the sun is above the craft's horizon: its height above the local level, against how far the horizon dips below
        // that level at the craft's height)
        static bool Lit(Vessel v)
        {
            CelestialBody sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            if (sun == null || v.mainBody == null || v.mainBody == sun) return true;
            Vector3d at = v.GetWorldPos3D();
            Vector3d up = (at - v.mainBody.position).normalized, toSun = (sun.position - at).normalized;
            double r = v.mainBody.Radius, dip = Math.Acos(r / (r + Math.Max(0.0, v.altitude)));
            return Vector3d.Dot(up, toSun) > -Math.Sin(dip);
        }

        // (wheels that drive it: not landing legs, which are wheels to the game too)
        static bool Wheeled(Vessel v)
        {
            foreach (Part p in v.parts)
            {
                ModuleWheelBase w = p.FindModuleImplementing<ModuleWheelBase>();
                if (w != null && w.wheelType != WheelType.LEG) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the channel

        // (any frame of the channel's own work over 30 ms said in the game's log, with the collections so far: its first opening held
        // the game up, 2026-11-03)
        static readonly System.Diagnostics.Stopwatch ownWork = new System.Diagnostics.Stopwatch();

        static void Slow(string what, EventType? type = null)
        {
            double ms = ownWork.Elapsed.TotalMilliseconds;
            if (ms > 30.0) Debug.Log("[Kerbal News] " + what + (type != null ? " (" + type + ")" : "") + " took " + ms.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ms (frame " + Time.frameCount + ", " + GC.CollectionCount(0) + " collections so far)");
        }

        void Update()
        {
            ownWork.Reset(); ownWork.Start();
            Tick();
            Slow("its work this frame");
        }

        void Tick()
        {
            // (the window's own clock: a recording made frame by frame steps it by its frames, as the game's clock is stepped, so
            // that a film of the static or the fold shows what the player sees; otherwise real time, paused or not)
            frameStep = Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            clock += frameStep;
            if (On && !letteringReady) ReadyLettering();
            // (the page's switch lets the channel be; the hotkey alone brings its window up and takes it down, and only while the channel
            // is on. The window's state lasts through a scene's change in one session, never saved: a game starts with it down.)
            if (On && GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.N)) Key();
            if (!On) windowWanted = false;
            bool wanted = On && windowWanted;
            // (put off, the picture folds away first; put on again meanwhile, it comes on anew once it has)
            if (show != Show.Off && offAt >= 0f && clock - offAt > FoldTime) Close();
            if (wanted && show == Show.Off) Open();
            else if (!wanted && show != Show.Off && offAt < 0f) { offAt = clock; RestoreUI(); }
            if (show == Show.Off) return;
            // (the newsroom made a piece a frame while the static is up, and the static held until it is all made: made whole at the
            // first press, it held the game up, 2026-11-03)
            if (!studio.Built)
            {
                if (studio.BuildStep()) studio.Drawing = true;
                else if (studio.Failed) { CouldNotBuild(); return; }
                else if (clock - onAt > StaticTime - 0.05f) onAt = clock - (StaticTime - 0.05f);
            }
            studio.KeepSunOff();
            float step = frameStep;
            whole = Mathf.MoveTowards(whole, show == Show.Whole ? 1f : 0f, step / GrowTime);
            if (show == Show.Whole && Eventful()) busyUntil = clock + BusyHold;
            bool beside = show == Show.Whole && whole >= 1f && clock < busyUntil;
            aside = Mathf.MoveTowards(aside, beside ? 1f : 0f, step / AsideTime);
            studio.Aside(Mathf.SmoothStep(0f, 1f, aside));
            studio.Wider(Mathf.SmoothStep(0f, 1f, whole));
            live.Keep(aside > 0f && !GameOnTop());
            Watch();
            // (his first line once the static has cleared)
            if (clock - onAt > StaticTime) Speak();
        }

        /// <summary>
        /// The hotkey. Taken down while he is on the screen, he signs off first and the window folds as he finishes, as a newscast
        /// ends; pressed again meanwhile, it folds at once. Not while his picture is still coming through the static.
        /// </summary>
        void Key()
        {
            if (!On) return;
            if (windowWanted && !signingOff && show != Show.Off && offAt < 0f && clock - onAt > StaticTime + ClearTime && Lines.Has("signoff"))
            {
                signingOff = true;
                waiting.Clear();
                voice.Stop();
                speaking = null;
                Say("signoff");
                return;
            }
            windowWanted = !windowWanted;
            signingOff = false;
        }

        void CouldNotBuild()
        {
            ScreenMessages.PostScreenMessage("Kerbal News: the newsroom could not be built (" + studio.Said + ")", 6f);
            windowWanted = false;
            On.Value = false;
            Close();
        }

        void Open()
        {
            // (the newsroom is made under the static, in Update; one that could not be made is tried again)
            if (studio.Failed) studio.Destroy();
            studio.Drawing = true;
            show = Show.Small;
            onAt = clock;
            offAt = -1f;
            whole = aside = 0f;
            busyUntil = 0f;
            waiting.Clear();
            // (the static comes up silent: its hiss, every time the window opened, was more than the owner wanted, 2026-10-21)
            Want("welcome", true);
            Vessel v = FlightGlobals.ActiveVessel;
            if (v != null && v.situation == Vessel.Situations.PRELAUNCH)
                Want((v.landedAt ?? "").IndexOf("Runway", StringComparison.OrdinalIgnoreCase) >= 0 && Lines.Has("runway") ? "runway" : "pad", true);
        }

        void Close()
        {
            show = Show.Off;
            offAt = -1f;
            whole = aside = hover = 0f;
            studio.Drawing = false;
            studio.Aside(0f);
            studio.Wider(0f);
            live.Keep(false);
            RestoreUI();
            voice.Stop();
            speaking = null;
            signingOff = false;
            waiting.Clear();
            mouth = bright = 0f;
        }

        /// <summary>
        /// The anchor on the whole screen (the button on the small window). The game's own interface is put away meanwhile: under the
        /// news it could not be seen, and its buttons there would still take the clicks. It comes back with the small window. The
        /// craft still answers its keys, and the camera the mouse.
        /// </summary>
        void GoWhole()
        {
            if (show != Show.Small || offAt >= 0f) return;
            show = Show.Whole;
            var ui = KSP.UI.UIMasterController.Instance;
            if (ui != null && ui.IsUIShowing)
            {
                hidUI = true;
                try { GameEvents.onHideUI.Fire(); }
                catch (Exception e) { Debug.LogWarning("[Kerbal News] putting the game's interface away: " + e.Message); }
            }
        }

        /// <summary>Back into the small window (the button on the whole screen).</summary>
        void GoSmall()
        {
            if (show != Show.Whole) return;
            show = Show.Small;
            RestoreUI();
        }

        void RestoreUI()
        {
            if (!hidUI) return;
            hidUI = false;
            var ui = KSP.UI.UIMasterController.Instance;
            if (ui == null || ui.IsUIShowing) return;
            try { GameEvents.onShowUI.Fire(); }
            catch (Exception e) { Debug.LogWarning("[Kerbal News] bringing the game's interface back: " + e.Message); }
        }

        /// <summary>Whether the flight is worth showing beside him: something has just happened in it, he is talking about something
        /// in it, or its engines are burning.</summary>
        bool Eventful()
        {
            if (clock - eventAt < 1f) return true;
            if (speaking != null && speaking.Event != "welcome" && speaking.Event != "quiet" && speaking.Event != "signoff") return true;
            Vessel v = FlightGlobals.ActiveVessel;
            return burning && v != null && v.situation != Vessel.Situations.PRELAUNCH;
        }

        /// <summary>
        /// Something of the game's own up that the whole-screen news must not hide: its pause menu, a dialog, the flight's results.
        /// (The news is drawn after everything the game draws, its interface too; the small window is left, being small.)
        /// </summary>
        static bool GameOnTop()
        {
            try
            {
                if (PauseMenu.exists && PauseMenu.isOpen) return true;
                if (KSP.UI.Dialogs.FlightResultsDialog.isDisplaying) return true;
                return PopupDialog.CheckForOpenDialogs();
            }
            catch (Exception) { return false; }
        }

        /// <summary>The next line, once the last has been said: the oldest big moment waiting, else the oldest of the rest.</summary>
        void Speak()
        {
            if (speaking != null)
            {
                bool done = voiceStarted ? !voice.isPlaying : Time.time > speakingUntil;
                if (!done) return;
                speaking = null;
                lastSaid = Time.time;
                studio.Mood(0.15f);
                // (his sign-off said: the window folds)
                if (signingOff) { signingOff = false; windowWanted = false; return; }
            }
            waiting.RemoveAll(w => Time.time - w.At > (w.Big ? 20f : 8f));
            // (the craft on the pad or the runway, said only while it is still there: waiting behind the welcome, "the rocket stands
            // on the pad" came after the lift-off, 2026-10-19)
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.situation != Vessel.Situations.PRELAUNCH) waiting.RemoveAll(w => w.Event == "pad" || w.Event == "runway");
            if (waiting.Count == 0) return;
            float least = Of(LeastGap, 3f), gap = Chatter == 2 ? least * 0.5f : least;
            if (Time.time - lastSaid < gap) return;
            Wanted next = waiting.Find(w => w.Big) ?? waiting[0];
            waiting.Remove(next);
            Say(next.Event);
        }

        bool Say(string ev)
        {
            Line line = Lines.Named(ev) ?? Lines.Pick(ev);
            if (line == null) return false;
            speaking = line;
            voiceStarted = false;
            studio.Mood(MoodOf(line.Event));
            speakingUntil = Time.time + 2.5f + 0.065f * line.Caption.Length;
            string path = Lines.SoundOf(line);
            if (path != null)
                StartCoroutine(Lines.Load(path, clip =>
                {
                    if (clip == null || speaking != line) { if (clip != null) Destroy(clip); return; }
                    if (clipNow != null) Destroy(clipNow);
                    clipNow = clip;
                    voice.clip = clip;
                    voice.volume = Loudness * Mathf.Clamp01(GameSettings.VOICE_VOLUME > 0f ? GameSettings.VOICE_VOLUME : 1f);
                    voice.Play();
                    voiceStarted = true;
                }));
            return true;
        }

        /// <summary>How the anchor looks while he says a line for an event: delighted at the big good moments, alarmed at a crash.</summary>
        static float MoodOf(string ev)
        {
            switch (ev)
            {
                case "liftoff": case "orbit": case "space": case "touchdown": case "landedaway": case "splashdown": case "dock": case "flag": return 0.75f;
                case "eva": case "chutes": case "science": case "newworld": case "welcome": case "staging": case "height": return 0.4f;
                case "crash": return -0.9f;
                case "reentry": case "lowfuel": return -0.35f;
                case "joke": return 0.6f;                                                          // (amused at his own joke)
                case "sunrise": case "mail": case "ad": case "studio": return 0.35f;
                default: return 0.15f;
            }
        }

        /// <summary>
        /// His mouth with his voice: how loud the line itself is where it is playing (read from the clip, not from what is heard,
        /// which the game's and the player's volumes turn down: so read, a stand-in voice at -22 dB opened it a tenth), eased:
        /// quick to open, a little slower to close; and how bright the sound is there, for the mouth's shape (MouthRig): how often
        /// its wave crosses nought, a second. His voice crosses about 1,400 times a second at the middle of its loud stretches
        /// (860 at the darker quarter, 2,240 at the brighter: ten lines measured, 2026-10-21): the bright sounds (ee, s) cross
        /// most, the dark ones (oo, o) least.
        /// </summary>
        void LateUpdate()
        {
            if (show == Show.Off || studio == null) return;
            float loud = 0f, tone = 0f;
            AudioClip clip = voice.clip;
            if (voice.isPlaying && clip != null && clip.samples > samples.Length)
            {
                int at = Mathf.Clamp(voice.timeSamples, 0, clip.samples - samples.Length / Mathf.Max(1, clip.channels) - 1);
                if (clip.GetData(samples, at))
                {
                    double sum = 0.0;
                    int crossings = 0;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        sum += samples[i] * samples[i];
                        if (i > 0 && (samples[i] >= 0f) != (samples[i - 1] >= 0f)) crossings++;
                    }
                    loud = Mathf.Clamp01(((float)Math.Sqrt(sum / samples.Length) - 0.015f) * 7f);
                    float perSecond = crossings * (float)clip.frequency * Mathf.Max(1, clip.channels) / samples.Length;
                    tone = Mathf.Clamp((perSecond - 1400f) / 1400f, -1f, 1f);
                }
            }
            float rate = loud > mouth ? 30f : 14f;
            mouth = Mathf.MoveTowards(mouth, loud, rate * Time.unscaledDeltaTime);
            bright = Mathf.MoveTowards(bright, loud > 0.05f ? tone : 0f, 6f * Time.unscaledDeltaTime);
            studio.Mouth(mouth, bright);
        }

        // ---------------------------------------------------------------- on the screen

        GUIStyle bannerStyle, smallStyle, captionStyle, tagStyle;
        readonly GUIContent captionText = new GUIContent();

        static bool letteringReady;

        /// <summary>
        /// The window's lettering made ready once a game, the first frame the channel is on (as the flight loads, where it is on
        /// already): Unity's own font, which the window's words are drawn in, is read and its letters drawn the first time anything
        /// is written in it, and at the first press, in the window's first frame, that held the game up 0.29 s (Windows, 2026-11-03).
        /// </summary>
        static void ReadyLettering()
        {
            if (letteringReady) return;
            letteringReady = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) return;
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?'\"-()·";
            int h = Screen.height;
            foreach (int size in new[] { Mathf.Max(14, h / 30), Mathf.Max(11, h / 55) }) font.RequestCharactersInTexture(letters, size, FontStyle.Bold);
            font.RequestCharactersInTexture(letters, Mathf.Max(13, h / 42), FontStyle.Normal);
            Debug.Log("[Kerbal News] the window's lettering made ready in " + watch.Elapsed.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ms");
        }

        void Styles()
        {
            int big = Mathf.Max(14, Screen.height / 30), small = Mathf.Max(11, Screen.height / 55);
            if (bannerStyle != null && bannerStyle.fontSize == big) return;
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = big, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = small, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            tagStyle = new GUIStyle(GUI.skin.label) { fontSize = small, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            captionStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 42), alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        }

        Rect Box()
        {
            float w = Screen.width * Size, h = w * 9f / 16f;
            // (clear of the game's own: its toolbar down the right edge, the crew's portraits at the bottom right, the clock top left)
            float side = Mathf.Max(Screen.width * 0.045f, 56f), top = Screen.height * 0.11f, bottom = Screen.height * 0.24f;
            switch ((int)Corner)
            {
                case 1: return new Rect(side, Screen.height - bottom - h, w, h);
                case 2: return new Rect(Screen.width - side - w, top, w, h);
                case 3: return new Rect(side, top, w, h);
                default: return new Rect(Screen.width - side - w, Screen.height - bottom - h, w, h);
            }
        }

        static Rect Between(Rect a, Rect b, float t) => new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

        internal static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);

        void OnGUI()
        {
            EventType type = Event.current.type;
            ownWork.Reset(); ownWork.Start();
            Draw();
            Slow("its drawing", type);
        }

        void Draw()
        {
            if (show == Show.Off || studio == null) return;
            Event e = Event.current;
            Rect small = Box(), screen = new Rect(0f, 0f, Screen.width, Screen.height);
            float w = Mathf.SmoothStep(0f, 1f, whole);
            // (the game's pause menu, a dialog or the flight's results up: the whole-screen news steps out of their way)
            if (w > 0f && GameOnTop()) return;
            float since = clock - onAt;
            bool clear = offAt < 0f && since > StaticTime;          // past the static, and not going off: the buttons answer
            Rect grow = GrowButton(small), shrink = ShrinkButton();
            if (e.type == EventType.MouseDown && e.button == 0 && clear)
            {
                if (show == Show.Small && whole <= 0f && hover > 0.5f && grow.Contains(e.mousePosition)) { GoWhole(); e.Use(); }
                else if (show == Show.Whole && whole >= 1f && shrink.Contains(e.mousePosition)) { GoSmall(); e.Use(); }
                return;
            }
            if (e.type != EventType.Repaint) return;
            Styles();
            bool over = show == Show.Small && whole <= 0f && clear && small.Contains(e.mousePosition);
#if DEV
            over |= testHover && show == Show.Small && whole <= 0f && clear;
#endif
            hover = Mathf.MoveTowards(hover, over ? 1f : 0f, frameStep / 0.15f);
            Rect r = Between(small, screen, w);
            if (offAt >= 0f) { Fold(r, Mathf.Clamp01((clock - offAt) / FoldTime)); return; }
            if (w < 1f) Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.85f, 0.6f, 0.1f, 1f - w));
            // (black under the static while the newsroom is being made)
            if (studio.Picture != null) GUI.DrawTexture(r, studio.Picture, ScaleMode.ScaleAndCrop, false);
            else Fill(r, Color.black);
            if (since < StaticTime + ClearTime) Static(r, since);
            // (the craft's name as the game shows it: a stock craft's own is a localisation tag, "#autoLOC_501232" for the Kerbal X,
            // which the ticker showed as it was; the PC, 2026-10-20)
            string craft = FlightGlobals.ActiveVessel != null ? FlightGlobals.ActiveVessel.GetDisplayName() : "";
            if (aside > 0f && w >= 1f) Beside(Inset(aside), aside);
            float lower = Mathf.Clamp01((w - 0.6f) / 0.4f), bar = Mathf.Clamp01(1f - w * 3f);
            if (lower > 0f)
            {
                // (the lower third: the channel's name, LIVE, and what is being followed)
                float y = Screen.height * 0.74f, h = Screen.height * 0.09f;
                Fill(new Rect(0f, y, Screen.width, h), new Color(0.03f, 0.07f, 0.18f, 0.88f * lower));
                Fill(new Rect(0f, y, Screen.width, 3f), new Color(0.85f, 0.6f, 0.1f, lower));
                Fill(new Rect(Screen.width * 0.04f, y + h * 0.22f, h * 1.2f, h * 0.56f), new Color(0.8f, 0.1f, 0.1f, lower));
                GUI.color = new Color(1f, 1f, 1f, lower);
                GUI.Label(new Rect(Screen.width * 0.04f, y + h * 0.22f, h * 1.2f, h * 0.56f), "LIVE", tagStyle);
                GUI.Label(new Rect(Screen.width * 0.04f + h * 1.5f, y, Screen.width * 0.6f, h), "KERBAL NEWS NETWORK" + (craft.Length > 0 ? "   ·   " + craft : ""), bannerStyle);
                GUI.color = Color.white;
            }
            if (bar > 0f)
            {
                // (under the small window: the channel and LIVE)
                float h = Mathf.Max(18f, r.height * 0.13f);
                var under = new Rect(r.x - 3f, r.yMax + 3f, r.width + 6f, h);
                Fill(under, new Color(0.03f, 0.07f, 0.18f, 0.9f * bar));
                Fill(new Rect(under.x + 6f, under.y + h * 0.15f, h * 1.5f, h * 0.7f), new Color(0.8f, 0.1f, 0.1f, bar));
                GUI.color = new Color(1f, 1f, 1f, bar);
                GUI.Label(new Rect(under.x + 6f, under.y + h * 0.15f, h * 1.5f, h * 0.7f), "LIVE", tagStyle);
                GUI.Label(new Rect(under.x + 12f + h * 1.5f, under.y, under.width - h * 1.5f - 16f, h), "KNN" + (craft.Length > 0 ? " · " + craft : ""), smallStyle);
                GUI.color = Color.white;
            }
            if (Captions && speaking != null && since > StaticTime)
            {
                float cw = Screen.width * 0.56f;
                var cap = new Rect((Screen.width - cw) * 0.5f, Screen.height * (w > 0.5f ? 0.84f : 0.66f), cw, Screen.height * 0.08f);
                if (w <= 0.5f) cap = Aside(cap, small);
                // (two lines or more, where it was narrowed: the band as tall as its words)
                captionText.text = speaking.Caption;
                float need = captionStyle.CalcHeight(captionText, cap.width) + 8f;
                if (need > cap.height) cap = new Rect(cap.x, cap.y - (need - cap.height) * 0.5f, cap.width, need);
                Fill(cap, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(cap, captionText, captionStyle);
            }
            if (hover > 0f && w <= 0f) Button(grow, true, hover * (grow.Contains(e.mousePosition) ? 1f : 0.75f));
            if (w >= 1f) Button(shrink, false, shrink.Contains(e.mousePosition) ? 1f : 0.7f);
        }

        /// <summary>The caption's band kept off the small window: its near end cut back to clear the window (and the channel's name
        /// under it), the band moved outward if that left it narrower than 0.3 of the screen. Centred on the screen as it was, it ran
        /// 140 pixels into a window at the bottom right, over him (Windows, 1280 x 720, 2026-11-03).</summary>
        static Rect Aside(Rect cap, Rect box)
        {
            float gap = 12f, under = Mathf.Max(18f, box.height * 0.13f) + 6f;
            var keep = new Rect(box.x - 3f - gap, box.y - 3f - gap, box.width + 6f + gap * 2f, box.height + 6f + under + gap * 2f);
            if (!cap.Overlaps(keep)) return cap;
            float least = Screen.width * 0.3f, edge = Mathf.Max(Screen.width * 0.045f, 56f);
            if (box.center.x > Screen.width * 0.5f)
            {
                float right = keep.x, left = Mathf.Max(edge, Mathf.Min(cap.x, right - least));
                return new Rect(left, cap.y, Mathf.Max(0f, right - left), cap.height);
            }
            else
            {
                float left = keep.xMax, right = Mathf.Min(Screen.width - edge, Mathf.Max(cap.xMax, left + least));
                return new Rect(left, cap.y, Mathf.Max(0f, right - left), cap.height);
            }
        }

        /// <summary>The full-screen button: at the small window's top right, inside it.</summary>
        static Rect GrowButton(Rect box)
        {
            float s = Mathf.Clamp(box.height * 0.17f, 22f, 44f);
            return new Rect(box.xMax - s - 6f, box.y + 6f, s, s);
        }

        /// <summary>The button back to the small window: the whole screen's top right (the game's interface, whose toolbar is there,
        /// being put away meanwhile).</summary>
        static Rect ShrinkButton()
        {
            float s = Mathf.Clamp(Screen.height * 0.036f, 26f, 52f), m = Mathf.Max(10f, Screen.height * 0.025f);
            return new Rect(Screen.width - s - m, m, s, s);
        }

        /// <summary>A square button with four corners in it: pointing out (to the whole screen) or in (back to the small window).</summary>
        static void Button(Rect b, bool outward, float a)
        {
            Fill(b, new Color(0.02f, 0.04f, 0.1f, 0.72f * a));
            float pad = b.width * 0.22f, len = b.width * 0.2f, th = Mathf.Max(2f, Mathf.Round(b.width * 0.08f));
            var c = new Color(1f, 1f, 1f, a);
            float x0 = b.x + pad, y0 = b.y + pad, x1 = b.xMax - pad, y1 = b.yMax - pad;
            if (outward)
            {
                Bracket(x0, y0, 1, 1, len, th, c); Bracket(x1, y0, -1, 1, len, th, c);
                Bracket(x0, y1, 1, -1, len, th, c); Bracket(x1, y1, -1, -1, len, th, c);
            }
            else
            {
                Bracket(x0 + len, y0 + len, -1, -1, len, th, c); Bracket(x1 - len, y0 + len, 1, -1, len, th, c);
                Bracket(x0 + len, y1 - len, -1, 1, len, th, c); Bracket(x1 - len, y1 - len, 1, 1, len, th, c);
            }
        }

        /// <summary>An L with its corner at (x, y), its arms running dx across and dy down (each 1 or -1).</summary>
        static void Bracket(float x, float y, int dx, int dy, float len, float th, Color c)
        {
            Fill(new Rect(dx > 0 ? x : x - len, dy > 0 ? y : y - th, len, th), c);
            Fill(new Rect(dx > 0 ? x : x - th, dy > 0 ? y : y - len, th, len), c);
        }

        /// <summary>The flight's screen beside him on the whole screen: right of the middle and clear of the lower third, grown out of
        /// its own middle as he moves aside.</summary>
        static Rect Inset(float a)
        {
            float w = Screen.width * 0.5f, h = w * 9f / 16f, k = Mathf.SmoothStep(0f, 1f, a);
            var at = new Rect(Screen.width * 0.955f - w, Screen.height * 0.4f - h * 0.5f, w, h);
            return new Rect(at.center.x - w * k * 0.5f, at.center.y - h * k * 0.5f, w * k, h * k);
        }

        void Beside(Rect box, float a)
        {
            Fill(new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f), new Color(0.85f, 0.6f, 0.1f, Mathf.Clamp01(a * 3f)));
            Texture flight = live.Picture;
            if (flight != null) GUI.DrawTexture(box, flight, ScaleMode.ScaleAndCrop, false);
            else Fill(box, new Color(0.02f, 0.03f, 0.06f));
            if (a < 0.7f) return;
            float t = Mathf.Clamp01((a - 0.7f) / 0.3f), h = Mathf.Max(18f, box.height * 0.075f);
            var tag = new Rect(box.x + h * 0.4f, box.y + h * 0.4f, h * 1.9f, h);
            Fill(tag, new Color(0.8f, 0.1f, 0.1f, t));
            GUI.color = new Color(1f, 1f, 1f, t);
            GUI.Label(tag, "LIVE", tagStyle);
            GUI.color = Color.white;
        }

        /// <summary>
        /// The static over his picture as the window comes on: the crew portraits' own (the game's three frames of no signal, stepped
        /// ten times a second as it steps them), opening out of a bright line as an old set's picture does, then thinning as his
        /// picture comes through. Static made here if the game's is not to be had.
        /// </summary>
        void Static(Rect r, float since)
        {
            float open = Mathf.SmoothStep(0f, 1f, since / 0.12f);
            float much = 1f - Mathf.Clamp01((since - StaticTime) / ClearTime);
            float h = Mathf.Max(2f, r.height * open);
            var s = new Rect(r.x, r.center.y - h * 0.5f, r.width, h);
            if (open < 1f) Fill(r, Color.black);
            GUI.color = new Color(1f, 1f, 1f, much);
            Texture game = GameStatic();
            if (game != null) GUI.DrawTextureWithTexCoords(s, game, new Rect((int)(clock * 10f) % 3 / 3f, 2f / 3f, 1f / 3f, 1f / 3f), false);
            else GUI.DrawTexture(s, Noise(), ScaleMode.StretchToFill, false);
            GUI.color = Color.white;
            // (a brighter band rolling down it; the line bright while it opens)
            Fill(new Rect(s.x, s.y + since * 1.4f % 1f * s.height, s.width, Mathf.Max(2f, s.height * 0.07f)), new Color(1f, 1f, 1f, 0.14f * much));
            if (open < 1f) Fill(s, new Color(1f, 1f, 1f, 0.6f * (1f - open)));
        }

        /// <summary>The window going off: its picture squeezed to a bright line across, the line drawn in to a dot, and gone.</summary>
        void Fold(Rect r, float t)
        {
            if (t < 0.6f)
            {
                float k = 1f - Mathf.SmoothStep(0f, 1f, t / 0.6f), h = Mathf.Max(2f, r.height * k);
                var s = new Rect(r.x, r.center.y - h * 0.5f, r.width, h);
                if (studio.Picture != null) GUI.DrawTexture(s, studio.Picture, ScaleMode.StretchToFill, false);
                else Fill(s, Color.black);
                Fill(s, new Color(1f, 1f, 1f, 0.85f * (1f - k)));
                return;
            }
            float k2 = 1f - Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f), w = Mathf.Max(2f, r.width * k2);
            Fill(new Rect(r.center.x - w * 0.5f, r.center.y - 1f, w, 2f), new Color(1f, 1f, 1f, 0.3f + 0.7f * k2));
        }

        /// <summary>The crew portraits' static, a sheet of three frames by three states (the game's KerbalPortrait.overlayImg).</summary>
        static Texture GameStatic()
        {
            if (gameStatic == null)
                try
                {
                    var g = KSP.UI.Screens.Flight.KerbalPortraitGallery.Instance;
                    if (g != null && g.portraitPrefab != null) gameStatic = g.portraitPrefab.overlayImg;
                }
                catch (Exception) { }
            return gameStatic;
        }

        Texture2D Noise()
        {
            if (noise == null)
            {
                noise = new Texture2D(160, 90, TextureFormat.RGBA32, false) { name = "Kerbal News: static", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                noiseDots = new Color32[160 * 90];
            }
            if (clock - noiseAt >= 0.07f)
            {
                noiseAt = clock;
                for (int i = 0; i < noiseDots.Length; i++) { byte v = (byte)grain.Next(256); noiseDots[i] = new Color32(v, v, v, 255); }
                noise.SetPixels32(noiseDots);
                noise.Apply(false);
            }
            return noise;
        }

#if DEV
        // ---------------------------------------------------------------- for the development build

        /// <summary>(Development build: a line for an event said now, e.g. "liftoff", or a line by its name, "pad_01".)</summary>
        public static string Test(string ev)
        {
            if (instance == null) return "no channel (not in flight)";
            // ("spell": what two hundred quiet spells would be filled with just now, and why: the flight as Spell reads it)
            if ((ev ?? "").Trim() == "spell")
            {
                Vessel v = FlightGlobals.ActiveVessel;
                if (v == null) return "no craft";
                bool lit = Lit(v);
                var tally = new SortedDictionary<string, int>();
                for (int i = 0; i < 200; i++) { string k = instance.Spell(v, lit); tally[k] = tally.TryGetValue(k, out int c) ? c + 1 : 1; }
                var said = new System.Text.StringBuilder("lit " + lit + ", warp x" + TimeWarp.CurrentRate + ", " + v.situation + ", " + v.srfSpeed.ToString("F0") + " m/s, burning " + instance.burning + ":");
                foreach (KeyValuePair<string, int> k in tally) said.Append(" " + k.Key + " " + k.Value);
                return said.ToString();
            }
            if (!On.Value) On.Value = true;
            windowWanted = true;
            if (instance.show == Show.Off) instance.Open();
            bool ok = instance.Say((ev ?? "").Trim());
            return ok ? "saying: " + instance.speaking.Caption : "nothing to say for '" + ev + "' (" + Lines.Why + ")";
        }

        /// <summary>(Development build: where the last line said after the flight (Afterwards: the recovery's) has got to.)</summary>
        public static string Afterword() => Afterwards.Said;

        public static string Parts() => instance != null && instance.studio != null ? instance.studio.Parts() : "no studio";
        /// <summary>(Development build: the anchor's skinned parts and pictures written to a folder, for Blender: see Studio.Export.)</summary>
        public static string Export(string folder) => instance != null && instance.studio != null ? instance.studio.Export(folder) : "no studio";
        public static string View(string arg) => instance != null && instance.studio != null ? instance.studio.View(arg) : "no studio";
        public static string Rig(string arg) => instance != null && instance.studio != null ? instance.studio.Rig(arg) : "no studio";
        public static string Hold(string arg) => instance != null && instance.studio != null ? instance.studio.Hold(arg) : "no studio";
        public static string Jaw(string arg) => instance != null && instance.studio != null ? instance.studio.Jaw(arg) : "no studio";

        public static string Status() => instance == null ? "not in flight" :
            "channel " + (On ? "on" : "off") + ", window " + (windowWanted ? "wanted" : "not wanted") + "; show " + instance.show + " (whole " + instance.whole.ToString("F2") + ", aside " + instance.aside.ToString("F2") + ", hover " + instance.hover.ToString("F2") +
            ", flight copied " + (instance.live.Picture != null) + ", interface put away " + instance.hidUI + ", the game's static " + (GameStatic() != null ? GameStatic().name : "none") + "); " +
            Lines.Why + "; studio " + instance.studio.Said + "; speaking " + (instance.speaking != null ? instance.speaking.Id : "nothing") +
            "; voice " + (instance.voice.isPlaying ? "playing" : "quiet") + "; mouth " + instance.mouth.ToString("F2") + " bright " + instance.bright.ToString("F2") + "; waiting " + instance.waiting.Count;

        /// <summary>
        /// (Development build: the channel put in a state for a test: "small" (on, in its window), "whole" (on the whole screen, the
        /// static skipped), "off" (the window down), "enable" and "disable" (the page's switch alone), "key" (the hotkey as pressed),
        /// "busy" (the flight beside him for half a minute), "calm", "hover on|off" (the small window's button shown as if the pointer
        /// were over it).)
        /// </summary>
        public static string Mode(string arg)
        {
            if (instance == null) return "no channel (not in flight)";
            switch ((arg ?? "").Trim().ToLowerInvariant())
            {
                case "small":
                    if (!On.Value) On.Value = true;
                    windowWanted = true;
                    if (instance.show == Show.Off) instance.Open(); else instance.GoSmall();
                    break;
                case "whole":
                    if (!On.Value) On.Value = true;
                    windowWanted = true;
                    if (instance.show == Show.Off) instance.Open();
                    instance.onAt = Mathf.Min(instance.onAt, instance.clock - StaticTime - ClearTime);
                    instance.GoWhole();
                    break;
                case "off": windowWanted = false; break;
                // (the page's switch alone, and the hotkey as pressed: for trying that switching on shows nothing)
                case "enable": On.Value = true; break;
                case "disable": On.Value = false; break;
                case "key": instance.Key(); break;
                case "busy": instance.busyUntil = instance.clock + 30f; break;
                case "calm": instance.busyUntil = 0f; instance.eventAt = -100f; break;
                case "hover on": testHover = true; break;
                case "hover off": testHover = false; break;
            }
            return Status();
        }

        /// <summary>(Development build: the crew portraits' static sheet written to a file, to look at.)</summary>
        public static string StaticShot(string path)
        {
            Texture t = GameStatic();
            if (t == null) return "the game's static is not to be had here";
            RenderTexture rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(t, rt);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            return "written " + path + " (" + t.width + " x " + t.height + ", " + t.name + ")";
        }

        /// <summary>(Development build: the newsroom's picture written to a file, as the camera has it now.)</summary>
        public static string Shot(string path)
        {
            if (instance == null || instance.studio == null || instance.studio.Picture == null) return "no picture";
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = instance.studio.Picture;
            var tex = new Texture2D(instance.studio.Picture.width, instance.studio.Picture.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            RenderTexture.active = was;
            System.IO.File.WriteAllBytes(path, tex.EncodeToJPG(90));
            Destroy(tex);
            return "written " + path;
        }
#endif
    }
}
