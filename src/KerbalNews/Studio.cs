using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalNews
{
    /// <summary>
    /// The newsroom: the anchor at his desk with a microphone, the channel's name behind him, lit by lamps of its own, drawn
    /// by a camera of its own into a picture (Picture) that the channel puts on the screen, whole or in its box.
    ///
    /// Everything in it is on the game's layer 22, "KerbalInstructors": the game draws its instructors' portraits at the space
    /// centre so, a kerbal with a camera and lamps of its own on a layer of their own, and nothing in flight uses that layer: the
    /// flight's cameras do not draw it and its lamps light nothing else. The game's sun is kept off it (its culling mask), and
    /// while this camera draws, the air's own ambient light and fog are put by for the studio's (the game sets them for the
    /// scene, and a newsroom at night would be dark).
    ///
    /// The anchor is the game's own seated kerbal, the one it puts in cockpits (AssetBase's "kerbalMale"), made with its
    /// scripts held back and then taken off (they look for a crew member and a seat), his animation left to hold him seated.
    /// The game moves a kerbal's face only by whole expressions; his jaw is turned here, after the animation each frame, by
    /// how loud the voice is (Mouth).
    /// </summary>
    internal sealed class Studio
    {
        public const int Layer = 22;
        public RenderTexture Picture { get; private set; }
        public string Said { get; private set; } = "not built";
        public bool Built => root != null;

        GameObject root;
        Camera cam;
        Transform jaw;
        Quaternion jawRest;
        Vector3 jawAxis = Vector3.right;
        float jawMost = 18f;                      // how far the jaw opens at the loudest, degrees (where a head has no mouth rig)
        // (his mouth: the head's own mouth bones (MouthRig); the jaw alone, as above, only where a head has none)
        MouthRig rig;
        float heldOpen = -1f, heldBright;         // (development build: a shape held for a picture)
        readonly List<Light> lamps = new List<Light>();
        Color ambientWas; UnityEngine.Rendering.AmbientMode ambientModeWas; bool fogWas;
        float viewDistance = 2.3f, viewHeight = 0.02f, viewYaw = 0f;   // the camera: anchor's heights off, a share of his height down, degrees round
        Bounds anchorBounds;
        Transform anchor;
        // (his face: the game's own expressions, one number from panic (-1) to joy (+1), as its expression system sets them for a
        // kerbal in a cockpit; here by what he is saying, eased; only the animator's values that his animation has)
        Animator face;
        float moodNow, moodWant = 0.15f;
        static readonly int idExpression = Animator.StringToHash("Expression"), idHelmet = Animator.StringToHash("hasHelmet");
        bool hasExpression, hasHelmet;
        // (the way he faces: along his model's forward. The first picture seemed to show the back of his helmet; it was his visor
        // from the front, which hid his face; the second, turned round, showed the back of his head.)
        Vector3 Front => anchor != null ? anchor.forward : Vector3.forward;

        public bool Build()
        {
            if (root != null) return true;
            try
            {
                // (far from where the flight's things are: nothing else is on its layer, but the game's terrain colliders and the
                // like are, at the origin, and a newsroom inside them would do no harm and no good)
                root = new GameObject("Kerbal News: newsroom");
                UnityEngine.Object.DontDestroyOnLoad(root);
                root.transform.position = new Vector3(0f, -4000f, 0f);
                MakeAnchor();
                MakeSet();
                MakeLamps();
                MakeCamera();
                Camera.onPreCull += BeforeDrawing;
                Camera.onPostRender += AfterDrawing;
                KeepSunOff();
                Said = "built" + (anchor == null ? ", no anchor (the game's seated kerbal was not found)" : "") + (jaw == null ? ", no jaw found" : ", jaw " + jaw.name);
                return true;
            }
            catch (Exception ex)
            {
                Said = "could not be built: " + ex.Message;
                Debug.LogError("[Kerbal News] the newsroom could not be built: " + ex);
                Destroy();
                return false;
            }
        }

        public void Destroy()
        {
            Camera.onPreCull -= BeforeDrawing;
            Camera.onPostRender -= AfterDrawing;
            if (Picture != null) { Picture.Release(); UnityEngine.Object.Destroy(Picture); Picture = null; }
            foreach (Texture2D t in made2) if (t != null) UnityEngine.Object.Destroy(t);
            made2.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (Mesh m in madeMeshes) if (m != null) UnityEngine.Object.Destroy(m);
            foreach (Material m in madeMaterials) if (m != null) UnityEngine.Object.Destroy(m);
            madeMeshes.Clear(); madeMaterials.Clear();
            root = null; cam = null; jaw = null; anchor = null; lamps.Clear();
            shift = -1f;
            wide = 0f;
        }

        /// <summary>Whether the camera draws (only while the picture is shown).</summary>
        public bool Drawing { set { if (cam != null) cam.enabled = value; } }

        void MakeAnchor()
        {
            GameObject prefab = AssetBase.GetPrefab("kerbalMale");
            if (prefab == null) return;
            // (made inside a holder that is switched off, so that the kerbal's own scripts never wake; then taken off)
            var holder = new GameObject("Kerbal News: anchor");
            holder.SetActive(false);
            holder.transform.SetParent(root.transform, false);
            GameObject made = UnityEngine.Object.Instantiate(prefab, holder.transform);
            made.transform.localPosition = Vector3.zero;
            made.transform.localRotation = Quaternion.identity;
            foreach (MonoBehaviour b in made.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(b);
            foreach (Collider c in made.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Rigidbody r in made.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
            foreach (AudioSource a in made.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(a);
            foreach (Camera c in made.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Light l in made.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
            // (the cockpit kerbal carries his helmet and visor, shown only when he wears them: a newsreader does not)
            foreach (Renderer r in made.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("helmet") || n.Contains("visor") || n.Contains("flare")) r.enabled = false;
            }
            SetLayer(holder.transform);
            // (his suit dark, as a newsreader's: the suit's own picture made grey and coloured a dark navy, so that its seams and
            // folds stay (multiplied down, the orange suit came out brown); his face and hands as they are)
            foreach (Renderer r in made.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                foreach (Material m in r.materials)
                {
                    string n = (m.name + " " + (m.mainTexture != null ? m.mainTexture.name : "")).ToLowerInvariant();
                    if (n.Contains("head") || n.Contains("eye") || n.Contains("teeth") || n.Contains("tongue") || n.Contains("hand")) continue;
                    Texture2D suit = Suited(m.mainTexture);
                    if (suit != null) { m.mainTexture = suit; made2.Add(suit); if (m.HasProperty("_Color")) m.color = Color.white; }
                    else if (m.HasProperty("_Color")) m.color = new Color(0.22f, 0.25f, 0.36f);
                }
            }
            Dress(made);
            holder.SetActive(true);
            anchor = made.transform;
            face = made.GetComponentInChildren<Animator>(true);
            if (face != null)
                foreach (AnimatorControllerParameter v in face.parameters)
                {
                    if (v.nameHash == idExpression) hasExpression = true;
                    if (v.nameHash == idHelmet) hasHelmet = true;
                }
            // (his jaw: the bone named for it, the lower one where there are two)
            Transform best = null;
            foreach (Transform t in made.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("jaw")) continue;
                if (best == null || (n.Contains("low") && !best.name.ToLowerInvariant().Contains("low"))) best = t;
            }
            jaw = best;
            if (jaw != null) jawRest = jaw.localRotation;
            rig = new MouthRig(made.transform);
            Debug.Log("[Kerbal News] " + rig.Said);
            anchorBounds = BoundsOf(made);
        }

        readonly List<Texture2D> made2 = new List<Texture2D>();     // (the suit's pictures made here, destroyed with the newsroom)

        // ---- his suit (2026-10-19; the owner: "can you actually make a suit model for him"): made in Blender from the game's own
        // kerbal body (tools/news/blender/make_suit.py, from News.Export's copy of it), so that it rides on the body's own bones with
        // the body's own weights: the spacesuit's ring off and its ridges smoothed away, a dark navy jacket and trousers open in a V
        // over a white shirt and a red tie, a collar, two buttons, black shoes, his hands bare. PluginData/Suit/suit.mesh, a text the
        // game reads: its vertices (position, normal, UV, four bone weights by the body's bones), its triangles by material. Worn in
        // place of the spacesuit; where the file is missing, the spacesuit, coloured (Suited).
        static readonly Dictionary<string, Color> suitColours = new Dictionary<string, Color>
        {
            { "jacket", new Color(0.075f, 0.095f, 0.19f) }, { "shirt", new Color(0.92f, 0.93f, 0.95f) }, { "tie", new Color(0.55f, 0.05f, 0.07f) },
            { "lapel", new Color(0.05f, 0.06f, 0.12f) }, { "shoe", new Color(0.03f, 0.03f, 0.035f) }, { "skin", new Color(0.52f, 0.72f, 0.22f) },
            { "button", new Color(0.02f, 0.02f, 0.03f) },
        };
        readonly List<Mesh> madeMeshes = new List<Mesh>();
        readonly List<Material> madeMaterials = new List<Material>();

        void Dress(GameObject made)
        {
            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer r in made.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "body01") body = r;
            string path = Lines.Folder + "Suit/suit.mesh";
            if (body == null || body.sharedMesh == null || !System.IO.File.Exists(path)) return;
            Mesh m = ReadSuit(path, body.sharedMesh.bindposes, out List<string> materials);
            if (m == null) return;
            madeMeshes.Add(m);
            var g = new GameObject("Kerbal News: the anchor's suit");
            g.layer = body.gameObject.layer;
            g.transform.SetParent(body.transform.parent, false);
            g.transform.localPosition = body.transform.localPosition;
            g.transform.localRotation = body.transform.localRotation;
            g.transform.localScale = body.transform.localScale;
            var suit = g.AddComponent<SkinnedMeshRenderer>();
            suit.sharedMesh = m;
            suit.bones = body.bones;
            suit.rootBone = body.rootBone;
            suit.updateWhenOffscreen = true;
            suit.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            suit.receiveShadows = true;
            Shader lit = Shader.Find("KSP/Diffuse") ?? Shader.Find("Diffuse");
            var mats = new Material[materials.Count];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(lit) { name = "Kerbal News: suit, " + materials[i], color = suitColours.TryGetValue(materials[i], out Color c) ? c : Color.grey };
                madeMaterials.Add(mats[i]);
            }
            suit.sharedMaterials = mats;
            body.enabled = false;
        }

        // ---- the desk microphone (2026-10-19; the owner: "improve the mic model"): made in Blender (tools/news/blender/make_mic.py), a
        // broadcast microphone on a heavy base, a gooseneck curving towards him, a capsule with a foam windscreen, and a cube flag on its
        // stand with the channel's name on its four faces. PluginData/Suit/microphone.mesh; where it is missing, a rod and a ball.
        static readonly Dictionary<string, Color> micColours = new Dictionary<string, Color>
        {
            { "metal", new Color(0.55f, 0.56f, 0.58f) }, { "dark", new Color(0.05f, 0.05f, 0.055f) }, { "foam", new Color(0.09f, 0.09f, 0.1f) },
            { "grille", new Color(0.25f, 0.26f, 0.28f) }, { "flag", new Color(0.06f, 0.09f, 0.22f) },
        };

        bool Microphone(Vector3 foot, Vector3 mouth, float h)
        {
            string path = Lines.Folder + "Suit/microphone.mesh";
            if (!System.IO.File.Exists(path)) return false;
            Mesh m = ReadSuit(path, null, out List<string> materials);
            if (m == null) return false;
            madeMeshes.Add(m);
            var g = new GameObject("Kerbal News: microphone");
            g.transform.SetParent(root.transform, true);
            g.layer = Layer;
            // (the model reaches towards its own -z: turned to reach towards his mouth, level)
            Vector3 toMouth = mouth - foot; toMouth.y = 0f;
            if (toMouth.sqrMagnitude < 1e-6f) toMouth = -Front;
            float scale = 0.8f * h;
            g.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(-toMouth.normalized, Vector3.up));
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            Shader lit = Shader.Find("KSP/Specular") ?? Shader.Find("KSP/Diffuse") ?? Shader.Find("Diffuse");
            var mats = new Material[materials.Count];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(lit) { name = "Kerbal News: microphone, " + materials[i], color = micColours.TryGetValue(materials[i], out Color c) ? c : Color.grey };
                if (mats[i].HasProperty("_Shininess")) mats[i].SetFloat("_Shininess", materials[i] == "metal" ? 0.35f : 0.08f);
                madeMaterials.Add(mats[i]);
            }
            r.sharedMaterials = mats;
            // (the channel's name on the flag's four faces: the cube stands 0.074 up the stand, 0.04 across)
            for (int k = 0; k < 4; k++)
            {
                Quaternion turn = Quaternion.Euler(0f, 90f * k, 0f);
                Vector3 outward = g.transform.rotation * (turn * Vector3.forward);
                Words("KNN", g.transform.TransformPoint(turn * new Vector3(0f, 0.074f, 0.0205f)), 0.014f * scale, outward, new Color(0.95f, 0.75f, 0.2f));
            }
            return true;
        }

        /// <summary>The suit's mesh from its text (tools/news/blender/anchor_io.py's write), with the body's bind poses (none: a mesh that does not bend).</summary>
        static Mesh ReadSuit(string path, Matrix4x4[] bindposes, out List<string> materials)
        {
            materials = new List<string>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var uv = new List<Vector2>(); var bw = new List<BoneWeight>();
            var subs = new List<List<int>>();
            List<int> sub = null;
            try
            {
                foreach (string raw in System.IO.File.ReadAllLines(path))
                {
                    string[] w = raw.Split(' ');
                    if (w.Length == 0) continue;
                    switch (w[0])
                    {
                        case "v":
                            // v x y z n x y z t u v w i w i w i w i w
                            pos.Add(new Vector3(float.Parse(w[1], inv), float.Parse(w[2], inv), float.Parse(w[3], inv)));
                            nor.Add(new Vector3(float.Parse(w[5], inv), float.Parse(w[6], inv), float.Parse(w[7], inv)));
                            uv.Add(new Vector2(float.Parse(w[9], inv), float.Parse(w[10], inv)));
                            bw.Add(new BoneWeight
                            {
                                boneIndex0 = int.Parse(w[12]), weight0 = float.Parse(w[13], inv), boneIndex1 = int.Parse(w[14]), weight1 = float.Parse(w[15], inv),
                                boneIndex2 = int.Parse(w[16]), weight2 = float.Parse(w[17], inv), boneIndex3 = int.Parse(w[18]), weight3 = float.Parse(w[19], inv),
                            });
                            break;
                        case "submesh":
                            sub = new List<int>(); subs.Add(sub); materials.Add(w.Length > 3 ? w[3] : "-");
                            break;
                        case "f":
                            if (sub != null) { sub.Add(int.Parse(w[1])); sub.Add(int.Parse(w[2])); sub.Add(int.Parse(w[3])); }
                            break;
                    }
                }
            }
            catch (Exception e) { Debug.Log("[Kerbal News] the suit could not be read: " + e.Message); return null; }
            var m = new Mesh { name = "Kerbal News: the anchor's suit" };
            if (pos.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(pos); m.SetNormals(nor); m.SetUVs(0, uv);
            if (bindposes != null) { m.boneWeights = bw.ToArray(); m.bindposes = bindposes; }
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// The suit's picture as a dark navy suit: read from the card at no more than 1024 a side (the game's pictures cannot be
        /// read on the game's side), each dot made grey and coloured, its light and dark kept; its alpha (the suit's shine) as it was.
        /// </summary>
        static Texture2D Suited(Texture source)
        {
            if (source == null) return null;
            int w = Mathf.Min(source.width, 1024), h = Mathf.Min(source.height, 1024);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture was = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Kerbal News: the anchor's suit" };
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                Color32[] dots = t.GetPixels32();
                for (int i = 0; i < dots.Length; i++)
                {
                    float grey = (0.3f * dots[i].r + 0.59f * dots[i].g + 0.11f * dots[i].b) / 255f;
                    float k = 0.16f + 0.6f * grey;
                    dots[i] = new Color32((byte)(255f * k * 0.52f), (byte)(255f * k * 0.6f), (byte)(255f * k * 0.84f), dots[i].a);
                }
                t.SetPixels32(dots);
                t.Apply(true, false);
                return t;
            }
            catch (Exception) { return null; }
            finally { RenderTexture.active = was; RenderTexture.ReleaseTemporary(rt); }
        }

        static Bounds BoundsOf(GameObject g)
        {
            bool any = false;
            var b = new Bounds(g.transform.position, Vector3.one * 0.5f);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void MakeSet()
        {
            Vector3 c = anchor != null ? anchorBounds.center : root.transform.position + Vector3.up * 0.5f;
            float h = anchor != null ? Mathf.Max(anchorBounds.size.y, 0.4f) : 1f;
            Vector3 front = Front;
            Vector3 side = Vector3.Cross(Vector3.up, front).normalized;
            Shader lit = Shader.Find("KSP/Diffuse") ?? Shader.Find("Diffuse");
            Shader flat = Shader.Find("KSP/Unlit") ?? Shader.Find("Unlit/Color") ?? lit;
            // the desk: in front of him, low enough that his suit shows above it (at his elbows it hid all but his head: a head on
            // a desk, the second picture)
            float deskTop = c.y - 0.32f * h;
            Box("desk", c + front * (0.42f * h) + Vector3.up * (deskTop - 0.3f * h - c.y), new Vector3(1.7f * h, 0.6f * h, 0.45f * h), front, lit, new Color(0.12f, 0.13f, 0.17f));
            Box("desk's front", c + front * (0.65f * h) + Vector3.up * (deskTop - 0.3f * h - c.y), new Vector3(1.72f * h, 0.5f * h, 0.02f * h), front, flat, new Color(0.75f, 0.12f, 0.12f));
            // the microphone: a short stand on the desk to one side, its head below his chin and turned to him (in front of his
            // face it hid an eye, the third picture)
            Vector3 micFoot = c + front * (0.36f * h) + side * (0.24f * h) + Vector3.up * (deskTop - c.y);
            Vector3 micHead = micFoot + Vector3.up * (0.2f * h) - front * (0.1f * h) - side * (0.06f * h);
            if (!Microphone(micFoot, jaw != null ? jaw.position : c + Vector3.up * (0.3f * h), h))
            {
                Rod("microphone's stand", micFoot, micHead, 0.01f * h, lit, new Color(0.08f, 0.08f, 0.08f));
                Ball("microphone", micHead, 0.035f * h, lit, new Color(0.2f, 0.2f, 0.22f));
            }
            // behind him: the wall, deep blue, with the channel's name and a bright band
            Box("wall", c - front * (0.9f * h) + Vector3.up * (0.4f * h), new Vector3(6f * h, 4f * h, 0.05f * h), front, flat, new Color(0.04f, 0.09f, 0.2f));
            Box("band", c - front * (0.86f * h) + Vector3.up * (0.05f * h), new Vector3(6f * h, 0.06f * h, 0.02f * h), front, flat, new Color(0.85f, 0.6f, 0.1f));
            Words("KNN", c - front * (0.84f * h) + side * (0.62f * h) + Vector3.up * (0.42f * h), 0.15f * h, front, new Color(0.95f, 0.75f, 0.2f));
            Words("KERBAL NEWS NETWORK", c - front * (0.84f * h) + side * (0.62f * h) + Vector3.up * (0.26f * h), 0.035f * h, front, new Color(0.95f, 0.95f, 0.98f));
        }

        GameObject Box(string name, Vector3 at, Vector3 size, Vector3 front, Shader shader, Color colour)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Fit(g, name, shader, colour);
            g.transform.position = at;
            g.transform.rotation = Quaternion.LookRotation(front, Vector3.up);
            g.transform.localScale = new Vector3(size.x, size.y, size.z);
            return g;
        }

        void Rod(string name, Vector3 from, Vector3 to, float radius, Shader shader, Color colour)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Fit(g, name, shader, colour);
            g.transform.position = (from + to) * 0.5f;
            g.transform.up = (to - from).normalized;
            g.transform.localScale = new Vector3(radius * 2f, (to - from).magnitude * 0.5f, radius * 2f);
        }

        void Ball(string name, Vector3 at, float radius, Shader shader, Color colour)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Fit(g, name, shader, colour);
            g.transform.position = at;
            g.transform.localScale = Vector3.one * radius * 2f;
        }

        void Fit(GameObject g, string name, Shader shader, Color colour)
        {
            g.name = "Kerbal News: " + name;
            Collider c = g.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            g.transform.SetParent(root.transform, true);
            g.layer = Layer;
            var r = g.GetComponent<Renderer>();
            if (shader != null) r.material = new Material(shader) { color = colour };
            else r.material.color = colour;
        }

        void Words(string text, Vector3 at, float size, Vector3 front, Color colour)
        {
            var g = new GameObject("Kerbal News: " + text);
            g.transform.SetParent(root.transform, true);
            g.layer = Layer;
            g.transform.position = at;
            // (facing the camera, which looks at the wall from the front)
            g.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            var t = g.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.font = font;
            t.text = text;
            t.fontSize = 64;
            t.characterSize = size / 6.4f;
            t.anchor = TextAnchor.MiddleCenter;
            t.color = colour;
            var r = g.GetComponent<MeshRenderer>();
            if (font != null) r.material = font.material;
        }

        void MakeLamps()
        {
            Vector3 front = Front;
            Vector3 side = Vector3.Cross(Vector3.up, front).normalized;
            Lamp("key light", Quaternion.LookRotation(-(front + side * 0.6f + Vector3.up * 0.7f), Vector3.up), new Color(1f, 0.95f, 0.88f), 1.05f, true);
            Lamp("fill light", Quaternion.LookRotation(-(front - side * 0.8f + Vector3.up * 0.2f), Vector3.up), new Color(0.75f, 0.82f, 1f), 0.45f, false);
            Lamp("back light", Quaternion.LookRotation(-(-front + Vector3.up * 1.2f), Vector3.up), new Color(1f, 0.9f, 0.8f), 0.6f, false);
        }

        void Lamp(string name, Quaternion facing, Color colour, float strength, bool shadows)
        {
            var g = new GameObject("Kerbal News: " + name);
            g.transform.SetParent(root.transform, false);
            g.transform.rotation = facing;
            var l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = colour;
            l.intensity = strength;
            l.cullingMask = 1 << Layer;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            lamps.Add(l);
        }

        void MakeCamera()
        {
            Picture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "Kerbal News: the newsroom", antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
            Picture.Create();
            var g = new GameObject("Kerbal News: studio camera");
            g.transform.SetParent(root.transform, false);
            cam = g.AddComponent<Camera>();
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.06f, 0.14f);
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.depth = -50f;
            cam.renderingPath = RenderingPath.Forward;
            cam.allowHDR = false;
            cam.targetTexture = Picture;
            cam.enabled = false;
            Aim();
        }

        float shift = -1f, wide;
        // (on the whole screen the camera stands this much further off than for the small window: there he was a head filling a
        // third of the screen, and moved aside the channel's name on the wall was cut by the screen's edge; tried in the game at
        // 2.3, 2.8 and 3.2 of his height, 2026-10-19; 3.2 showed too much desk)
        const float WholeFurther = 2.8f / 2.3f;

        /// <summary>The camera eased back for the whole screen (0: framed for the small window, 1: for the whole screen).</summary>
        public void Wider(float share)
        {
            share = Mathf.Clamp01(share);
            if (Mathf.Abs(share - wide) < 1e-4f) return;
            wide = share;
            Aim();
        }

        /// <summary>
        /// The picture moved so that he sits left of the middle (0: in the middle; 1: a fifth of its width to the left), the newsroom
        /// running on at the right, where the flight's screen goes on the whole screen. Moved within the picture (the projection's
        /// centre shifted), not by moving the camera: he keeps the same look from the front.
        /// </summary>
        public void Aside(float share)
        {
            if (cam == null) return;
            float s = 0.42f * Mathf.Clamp01(share);
            if (Mathf.Abs(s - shift) < 1e-4f) return;
            shift = s;
            cam.ResetProjectionMatrix();
            if (s <= 0f) return;
            Matrix4x4 m = cam.projectionMatrix;
            m[0, 2] = s;
            cam.projectionMatrix = m;
        }

        /// <summary>The camera put in front of the anchor, his head and shoulders and the desk in view.</summary>
        void Aim()
        {
            if (cam == null) return;
            Vector3 c = anchor != null ? anchorBounds.center : root.transform.position + Vector3.up * 0.5f;
            float h = anchor != null ? Mathf.Max(anchorBounds.size.y, 0.4f) : 1f;
            Vector3 front = Quaternion.AngleAxis(viewYaw, Vector3.up) * Front;
            Vector3 head = c + Vector3.up * (0.32f * h);
            cam.transform.position = head + front * (viewDistance * Mathf.Lerp(1f, WholeFurther, wide) * h) + Vector3.up * (viewHeight * h);
            // (looking a little under his face, so that his head sits in the frame's upper third and his suit shows below it)
            cam.transform.rotation = Quaternion.LookRotation(head - Vector3.up * (0.22f * h) - cam.transform.position, Vector3.up);
        }

#if DEV
        /// <summary>(Development build: the camera moved, "distance height yaw" as shares of his height and degrees.)</summary>
        public string View(string arg)
        {
            string[] t = (arg ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            float V(int i, float otherwise) => i < t.Length && float.TryParse(t[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : otherwise;
            viewDistance = V(0, viewDistance); viewHeight = V(1, viewHeight); viewYaw = V(2, viewYaw);
            Aim();
            return "camera " + viewDistance + " " + viewHeight + " " + viewYaw;
        }

        /// <summary>(Development build: the mouth rig's reach, as pairs "jaw 13 drop 0.05 lift 0.03 out 0.07 in 0.09 forward 0.05
        /// openin 0.03" (the jaw in degrees, the rest in shares of the mouth's width); empty: as it is.)</summary>
        public string Rig(string arg)
        {
            if (rig == null) return "no newsroom";
            string[] t = (arg ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < t.Length; i += 2)
            {
                float v = float.Parse(t[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                switch (t[i].ToLowerInvariant())
                {
                    case "jaw": rig.JawMost = v; break;
                    case "drop": rig.Drop = v; break;
                    case "lift": rig.Lift = v; break;
                    case "out": rig.Out = v; break;
                    case "in": rig.In = v; break;
                    case "forward": rig.Forward = v; break;
                    case "openin": rig.OpenIn = v; break;
                }
            }
            return rig.Said + "; jaw " + rig.JawMost + " drop " + rig.Drop + " lift " + rig.Lift + " out " + rig.Out + " in " + rig.In + " forward " + rig.Forward + " openin " + rig.OpenIn;
        }

        /// <summary>(Development build: the mouth held at "open bright" (0 to 1, -1 to 1) for a picture, whatever he says; "off" lets go.)</summary>
        public string Hold(string arg)
        {
            string[] t = (arg ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 2) { heldOpen = -1f; return "the mouth let go"; }
            heldOpen = float.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture);
            heldBright = float.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture);
            return "the mouth held at " + heldOpen + " open, " + heldBright + " bright";
        }

        /// <summary>(Development build: the jaw's turning, "axis-x axis-y axis-z most-degrees", where a head has no mouth rig.)</summary>
        public string Jaw(string arg)
        {
            string[] t = (arg ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length >= 3) jawAxis = new Vector3(float.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(t[2], System.Globalization.CultureInfo.InvariantCulture));
            if (t.Length >= 4) jawMost = float.Parse(t[3], System.Globalization.CultureInfo.InvariantCulture);
            return "jaw " + (jaw != null ? jaw.name : "none") + " about " + jawAxis + " up to " + jawMost + " degrees";
        }

        /// <summary>(Development build: the anchor's bones and materials, for finding his jaw and suit.)</summary>
        /// <summary>
        /// (Development build: the anchor's skinned parts written to a folder for Blender, where his suit is made (the owner, 2026-10-19:
        /// "can you actually make a suit model for him"): for each, its mesh as stored (bind pose: positions, normals, UVs, triangles by
        /// submesh, four bone weights a vertex), its bones (name, parent, bind pose, local place), the mesh as he sits (baked), and its
        /// materials' pictures as PNGs. Text, one file a part: name.mesh.)
        /// </summary>
        public string Export(string folder)
        {
            if (anchor == null) return "no anchor";
            System.IO.Directory.CreateDirectory(folder);
            var said = new System.Text.StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (SkinnedMeshRenderer r in anchor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh m = r.sharedMesh;
                if (m == null) continue;
                var o = new System.Text.StringBuilder();
                o.Append("part ").Append(r.name).Append('\n');
                o.Append("enabled ").Append(r.enabled ? 1 : 0).Append('\n');
                o.Append("root ").Append(r.rootBone != null ? r.rootBone.name : "-").Append('\n');
                Transform[] bones = r.bones;
                Matrix4x4[] binds = m.bindposes;
                o.Append("bones ").Append(bones.Length).Append('\n');
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform t = bones[i];
                    o.Append("bone ").Append(i).Append(' ').Append(t != null ? t.name : "-").Append(' ').Append(t != null && t.parent != null ? t.parent.name : "-");
                    if (t != null)
                    {
                        o.Append(" local ").Append(t.localPosition.x.ToString("R", inv)).Append(' ').Append(t.localPosition.y.ToString("R", inv)).Append(' ').Append(t.localPosition.z.ToString("R", inv));
                        o.Append(' ').Append(t.localRotation.x.ToString("R", inv)).Append(' ').Append(t.localRotation.y.ToString("R", inv)).Append(' ').Append(t.localRotation.z.ToString("R", inv)).Append(' ').Append(t.localRotation.w.ToString("R", inv));
                        o.Append(' ').Append(t.localScale.x.ToString("R", inv)).Append(' ').Append(t.localScale.y.ToString("R", inv)).Append(' ').Append(t.localScale.z.ToString("R", inv));
                    }
                    if (i < binds.Length) { o.Append(" bind"); for (int k = 0; k < 16; k++) o.Append(' ').Append(binds[i][k].ToString("R", inv)); }
                    o.Append('\n');
                }
                Vector3[] v = m.vertices, n = m.normals; Vector2[] uv = m.uv; BoneWeight[] bw = m.boneWeights;
                o.Append("vertices ").Append(v.Length).Append('\n');
                for (int i = 0; i < v.Length; i++)
                {
                    o.Append("v ").Append(v[i].x.ToString("R", inv)).Append(' ').Append(v[i].y.ToString("R", inv)).Append(' ').Append(v[i].z.ToString("R", inv));
                    if (i < n.Length) o.Append(" n ").Append(n[i].x.ToString("R", inv)).Append(' ').Append(n[i].y.ToString("R", inv)).Append(' ').Append(n[i].z.ToString("R", inv));
                    if (i < uv.Length) o.Append(" t ").Append(uv[i].x.ToString("R", inv)).Append(' ').Append(uv[i].y.ToString("R", inv));
                    if (i < bw.Length) o.Append(" w ").Append(bw[i].boneIndex0).Append(' ').Append(bw[i].weight0.ToString("R", inv)).Append(' ').Append(bw[i].boneIndex1).Append(' ').Append(bw[i].weight1.ToString("R", inv))
                                        .Append(' ').Append(bw[i].boneIndex2).Append(' ').Append(bw[i].weight2.ToString("R", inv)).Append(' ').Append(bw[i].boneIndex3).Append(' ').Append(bw[i].weight3.ToString("R", inv));
                    o.Append('\n');
                }
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    int[] tri = m.GetTriangles(sm);
                    Material mat = sm < r.sharedMaterials.Length ? r.sharedMaterials[sm] : null;
                    o.Append("submesh ").Append(sm).Append(' ').Append(tri.Length / 3).Append(' ').Append(mat != null ? mat.name.Replace(' ', '_') : "-").Append('\n');
                    for (int i = 0; i + 2 < tri.Length; i += 3) o.Append("f ").Append(tri[i]).Append(' ').Append(tri[i + 1]).Append(' ').Append(tri[i + 2]).Append('\n');
                }
                // (as he sits: the skinned mesh baked, in the part's own space)
                var baked = new Mesh();
                r.BakeMesh(baked);
                Vector3[] bv = baked.vertices;
                o.Append("seated ").Append(bv.Length).Append('\n');
                for (int i = 0; i < bv.Length; i++) o.Append("s ").Append(bv[i].x.ToString("R", inv)).Append(' ').Append(bv[i].y.ToString("R", inv)).Append(' ').Append(bv[i].z.ToString("R", inv)).Append('\n');
                UnityEngine.Object.Destroy(baked);
                Matrix4x4 l2w = r.transform.localToWorldMatrix, w2a = anchor.worldToLocalMatrix * l2w;
                o.Append("toanchor"); for (int k = 0; k < 16; k++) o.Append(' ').Append(w2a[k].ToString("R", inv)); o.Append('\n');
                string file = System.IO.Path.Combine(folder, r.name.Replace(' ', '_').Replace('/', '_') + ".mesh");
                System.IO.File.WriteAllText(file, o.ToString());
                // (and its pictures, read from the card)
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null || mat.mainTexture == null) continue;
                    Texture src = mat.mainTexture;
                    RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                    RenderTexture was = RenderTexture.active;
                    Graphics.Blit(src, rt);
                    RenderTexture.active = rt;
                    var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                    t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                    t.Apply(false);
                    RenderTexture.active = was;
                    RenderTexture.ReleaseTemporary(rt);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, src.name.Replace(' ', '_').Replace('/', '_') + ".png"), t.EncodeToPNG());
                    UnityEngine.Object.Destroy(t);
                }
                said.Append(r.name).Append(": ").Append(v.Length).Append(" vertices, ").Append(bones.Length).Append(" bones, ").Append(m.subMeshCount).Append(" submeshes; ");
            }
            return said.Length > 0 ? said.ToString() : "no skinned parts";
        }

        public string Parts()
        {
            if (anchor == null) return "no anchor";
            var b = new System.Text.StringBuilder("bones:");
            foreach (Transform t in anchor.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("jaw") || n.Contains("mouth") || n.Contains("lip") || n.Contains("head") || n.Contains("neck") || n.Contains("eye") || n.Contains("tongue")) b.Append(' ').Append(t.name);
            }
            b.Append("\nmaterials:");
            foreach (Renderer r in anchor.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials) if (m != null) b.Append(' ').Append(r.name).Append('/').Append(m.name).Append('(').Append(m.shader != null ? m.shader.name : "?").Append(')');
            b.Append("\nbounds ").Append(anchorBounds.size.ToString("F2")).Append(", forward ").Append(anchor.forward.ToString("F2"));
            return b.ToString();
        }
#endif

        /// <summary>How he should look: -1 alarmed, 0 calm, +1 delighted (eased to over a second or so).</summary>
        public void Mood(float want) => moodWant = Mathf.Clamp(want, -1f, 1f);

        /// <summary>His jaw opened by so much (0 shut, 1 the widest), after his animation this frame; and his expression eased.</summary>
        public void Mouth(float open, float bright)
        {
            moodNow = Mathf.MoveTowards(moodNow, moodWant, 1.5f * Time.unscaledDeltaTime);
            if (face != null)
            {
                if (hasExpression) face.SetFloat(idExpression, moodNow);
                if (hasHelmet) face.SetBool(idHelmet, false);
            }
            if (rig != null && rig.Ready)
            {
                if (heldOpen >= 0f) rig.Pose(heldOpen, heldBright); else rig.Pose(open, bright);
                return;
            }
            if (jaw == null) return;
            jaw.localRotation = jawRest * Quaternion.AngleAxis(jawMost * Mathf.Clamp01(open), jawAxis);
        }

        /// <summary>The game's sun kept off the newsroom's layer (it lights every layer as the game makes it).</summary>
        public void KeepSunOff()
        {
            Light sun = Sun.Instance != null ? Sun.Instance.sunLight : null;
            if (sun != null && (sun.cullingMask & (1 << Layer)) != 0) sun.cullingMask &= ~(1 << Layer);
        }

        void BeforeDrawing(Camera c)
        {
            if (c == null || c != cam) return;
            ambientWas = RenderSettings.ambientLight; ambientModeWas = RenderSettings.ambientMode; fogWas = RenderSettings.fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.26f, 0.32f);
            RenderSettings.fog = false;
        }

        void AfterDrawing(Camera c)
        {
            if (c == null || c != cam) return;
            RenderSettings.ambientMode = ambientModeWas;
            RenderSettings.ambientLight = ambientWas;
            RenderSettings.fog = fogWas;
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
        }
    }
}
