using UnityEngine;
using UnityEngine.Rendering;

namespace KerbalNews
{
    /// <summary>
    /// The flight's own picture, for the screen beside the anchor while the news has the whole screen (the owner, 2026-10-19: "it'll
    /// show him plus a mini screen showing what's actually happening"). It is copied each frame, at half the screen's size, from the
    /// camera that draws the flight, after that camera's picture effects (TUFX's tone, the lens), where Cinema Camera's look reads
    /// it too. The news itself is drawn after every camera, so the copy holds the flight without the news over it. In the map
    /// view the map's camera is copied, and inside the craft (IVA) the cabin's.
    /// </summary>
    internal sealed class Live
    {
        const CameraEvent At = CameraEvent.AfterImageEffects;
        RenderTexture picture;
        CommandBuffer grab;
        Camera on;

        /// <summary>The flight as last copied, or null while nothing is being copied.</summary>
        public Texture Picture => on != null ? picture : null;

        /// <summary>Copied while it is wanted (only while the flight's screen is shown), from whichever camera draws the flight now.</summary>
        public void Keep(bool want)
        {
            Camera cam = want ? Drawing() : null;
            if (cam == on) return;
            Off();
            if (cam == null) return;
            Ready();
            cam.AddCommandBuffer(At, grab);
            on = cam;
        }

        static Camera Drawing()
        {
            if (MapView.MapIsEnabled) return PlanetariumCamera.Camera;
            CameraManager m = CameraManager.Instance;
            if (m != null && (m.currentCameraMode == CameraManager.CameraMode.IVA || m.currentCameraMode == CameraManager.CameraMode.Internal) && InternalCamera.Instance != null)
            {
                Camera inside = InternalCamera.Instance.GetComponent<Camera>();
                if (inside != null && inside.isActiveAndEnabled) return inside;
            }
            return FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
        }

        void Ready()
        {
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(36, Screen.height / 2);
            if (picture != null && (picture.width != w || picture.height != h))
            {
                picture.Release();
                Object.Destroy(picture);
                picture = null;
                if (grab != null) { grab.Release(); grab = null; }
            }
            if (picture == null)
            {
                picture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "Kerbal News: the flight", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                picture.Create();
                // (dark until the first copy: a new picture holds whatever the card had there)
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = picture;
                GL.Clear(false, true, Color.black);
                RenderTexture.active = was;
            }
            if (grab == null)
            {
                grab = new CommandBuffer { name = "Kerbal News: the flight's picture" };
                grab.Blit(BuiltinRenderTextureType.CameraTarget, picture);
            }
        }

        void Off()
        {
            if (on != null && grab != null) on.RemoveCommandBuffer(At, grab);
            on = null;
        }

        public void Destroy()
        {
            Off();
            if (grab != null) { grab.Release(); grab = null; }
            if (picture != null) { picture.Release(); Object.Destroy(picture); picture = null; }
        }
    }
}
