using System.Collections.Generic;
using UnityEngine;

namespace KerbalNews
{
    /// <summary>
    /// His mouth (2026-10-21; the owner: "add like mouth rigging so the mouth can actually move and not rotate weird").
    ///
    /// A kerbal's head has a mouth of bones: the lower jaw and the upper, three bones along each half of each lip with one in each
    /// lip's middle, and a corner on each side; the lower lip and the corners hang off the lower jaw. Until then only the jaw was
    /// turned, about its own x; but its hinge is its z (the corners lie at z -0.098 and +0.099 in its frame), so the jaw rolled and
    /// the mouth opened at one side only. Now the jaw turns about the line from corner to corner, the lower lip drops a little more
    /// at its middle and the upper lifts, so that the mouth opens round; and the corners go out for the bright sounds (ee, s) and
    /// in, with the lips pushed forward, for the dark ones (oo, o).
    ///
    /// Everything is laid on the pose his expression's animation gave the bones this frame (a bone it does not move keeps its own
    /// rest), and is found from the head's own bones and where they lie, so that it suits any kerbal head the game makes.
    /// </summary>
    internal sealed class MouthRig
    {
        // (how far each part goes at the most: the jaw in degrees, the rest in shares of the mouth's width, corner to corner. Tuned in
        // the game, 2026-10-21, by close pictures from the front and three quarters: with the corners drawn in by 0.09 (and 0.03 as
        // the mouth opened), his upper teeth, which do not move with the lips, showed through the near cheek in white shards)
        public float JawMost = 15f, Drop = 0.06f, Lift = 0.03f, Out = 0.07f, In = 0.04f, Forward = 0.05f, OpenIn = 0.01f;

        readonly Transform jaw, upper, left, right, lowMiddle;
        readonly List<Transform> upperLip = new List<Transform>(), lowerLip = new List<Transform>();
        readonly Dictionary<Transform, float> share = new Dictionary<Transform, float>();
        readonly Dictionary<Transform, Vector3> basePlace = new Dictionary<Transform, Vector3>(), placed = new Dictionary<Transform, Vector3>();
        Quaternion jawBase, jawTurned;
        bool jawTurnedOnce;
        readonly Vector3 hinge;                 // in the jaw's own frame: corner to corner, signed so that turning by a positive angle drops the chin
        readonly Vector3 across, up, front;     // in the upper jaw's frame: from his right corner to his left, up his face, out of it
        readonly float width;                   // corner to corner, in the scene's units
        public bool Ready { get; }
        public string Said { get; }

        public MouthRig(Transform head)
        {
            foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "bn_lowerJaw01") jaw = t;
                else if (n == "bn_upperJaw01") upper = t;
                else if (n == "bn_l_mouthCorner01") left = t;
                else if (n == "bn_r_mouthCorner01") right = t;
                else if (n.StartsWith("bn_l_mouthUp") || n.StartsWith("bn_r_mouthUp")) upperLip.Add(t);
                else if (n.StartsWith("bn_l_mouthLow") || n.StartsWith("bn_r_mouthLow")) lowerLip.Add(t);
                if (n == "bn_l_mouthLowMid_a01") lowMiddle = t;
            }
            Ready = jaw != null && upper != null && left != null && right != null && lowerLip.Count > 0;
            if (!Ready) { Said = "no mouth rig (jaw " + (jaw != null) + ", upper jaw " + (upper != null) + ", corners " + (left != null) + " " + (right != null) + ")"; return; }
            Vector3 centre = (left.position + right.position) * 0.5f;
            Vector3 acrossNow = left.position - right.position;
            width = acrossNow.magnitude;
            acrossNow /= Mathf.Max(width, 1e-6f);
            Vector3 upNow = head.up - acrossNow * Vector3.Dot(head.up, acrossNow);
            upNow = upNow.sqrMagnitude > 1e-8f ? upNow.normalized : Vector3.up;
            Vector3 frontNow = Vector3.Cross(acrossNow, upNow);
            Vector3 lip = lowMiddle != null ? lowMiddle.position : centre;
            if (Vector3.Dot(frontNow, lip - jaw.position) < 0f) frontNow = -frontNow;
            across = upper.InverseTransformDirection(acrossNow);
            up = upper.InverseTransformDirection(upNow);
            front = upper.InverseTransformDirection(frontNow);
            // (the hinge's sign: turned a little each way, the way that takes the lower lip down)
            Quaternion rest = jaw.localRotation;
            Vector3 h = jaw.InverseTransformDirection(acrossNow);
            Vector3 before = lip;
            jaw.localRotation = rest * Quaternion.AngleAxis(10f, h);
            Vector3 after = lowMiddle != null ? lowMiddle.position : (left.position + right.position) * 0.5f;
            jaw.localRotation = rest;
            hinge = Vector3.Dot(after - before, upNow) <= 0f ? h : -h;
            // (each lip bone's share of the lips' moves: all of it at the middle, less towards the corners)
            foreach (Transform b in upperLip) share[b] = Share(b.position, centre, acrossNow);
            foreach (Transform b in lowerLip) share[b] = Share(b.position, centre, acrossNow);
            Said = "mouth rig: " + upperLip.Count + " upper lip bones, " + lowerLip.Count + " lower, the jaw's hinge " + hinge.ToString("F2") + ", " + (width * 100f).ToString("F1") + " cm wide";
        }

        float Share(Vector3 at, Vector3 centre, Vector3 acrossNow)
        {
            float off = Mathf.Abs(Vector3.Dot(at - centre, acrossNow)) / Mathf.Max(width * 0.5f, 1e-6f);
            return Mathf.Clamp(1f - Mathf.Pow(Mathf.Clamp01(off), 1.5f), 0.15f, 1f);
        }

        /// <summary>The mouth as open as `open` (0 shut, 1 the widest) and as `bright` (-1 rounded for the dark sounds, +1 drawn wide for
        /// the bright ones), laid on this frame's pose. Called after his animation each frame.</summary>
        public void Pose(float open, float bright)
        {
            if (!Ready) return;
            open = Mathf.Clamp01(open);
            float round = Mathf.Clamp01(-bright), wide = Mathf.Clamp01(bright);
            Vector3 a = upper.TransformDirection(across), u = upper.TransformDirection(up), f = upper.TransformDirection(front);
            // (the jaw first: the lower lip and the corners ride on it)
            if (!jawTurnedOnce || jaw.localRotation != jawTurned) jawBase = jaw.localRotation;
            jaw.localRotation = jawBase * Quaternion.AngleAxis(JawMost * open, hinge);
            jawTurned = jaw.localRotation;
            jawTurnedOnce = true;
            foreach (Transform b in lowerLip) Place(b, (-u * (Drop * open) + f * (Forward * round)) * (share[b] * width));
            foreach (Transform b in upperLip) Place(b, (u * (Lift * open) + f * (Forward * round)) * (share[b] * width));
            Vector3 centre = (left.position + right.position) * 0.5f;
            float spread = Out * wide - In * round - OpenIn * open;
            Place(left, (Vector3.Dot(left.position - centre, a) >= 0f ? a : -a) * (spread * width));
            Place(right, (Vector3.Dot(right.position - centre, a) >= 0f ? a : -a) * (spread * width));
        }

        // (a bone moved by so much (scene units) from where its animation put it this frame, or from its rest if its animation does
        // not move it: told apart by whether it is still where it was put last frame)
        void Place(Transform b, Vector3 by)
        {
            if (!placed.TryGetValue(b, out Vector3 last) || b.localPosition != last) basePlace[b] = b.localPosition;
            b.localPosition = basePlace[b] + b.parent.InverseTransformVector(by);
            placed[b] = b.localPosition;
        }
    }
}
