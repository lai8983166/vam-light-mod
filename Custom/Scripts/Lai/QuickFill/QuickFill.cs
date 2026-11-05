using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaiLighting
{
    // VaM 1.x / Unity 2018. No external dependencies or scene light mutation.
    public class QuickFill : MVRScript
    {
        private readonly Light[] lamps = new Light[4];
        private GameObject rig;
        private FreeControllerV3 head, chest, hip;
        private JSONStorableBool lighting, rotateWithBody, shadows, calibrated;
        private JSONStorableFloat exposure, fill, rim, spread, softness, azimuth, headingOffset;
        private JSONStorableStringChooser style;
        private JSONStorableString status;
        private bool ready, poseValid;
        private int warmup;
        private Vector3 smoothTarget;
        private float smoothYaw, lastBodyYaw, smoothSize = 1f;

        public override void Init()
        {
            try
            {
                status = new JSONStorableString("Info", "QuickFill | Portrait lighting\nAdd to a Person. Four lights follow the actual body.\nGenerate = enable lighting. Align to view = place the key light for your current viewpoint.");
                CreateTextField(status).height = 145f;
                if (containingAtom == null || containingAtom.type != "Person")
                {
                    status.val = "Please add QuickFill.cs to a Person atom's Plugins tab.";
                    return;
                }
                head = containingAtom.GetStorableByID("headControl") as FreeControllerV3;
                chest = containingAtom.GetStorableByID("chestControl") as FreeControllerV3;
                hip = containingAtom.GetStorableByID("hipControl") as FreeControllerV3;
                if (head == null || chest == null || hip == null)
                    throw new Exception("Person head/chest/hip controllers are unavailable.");

                lighting = new JSONStorableBool("Lighting enabled", true);
                RegisterBool(lighting); CreateToggle(lighting);
                CreateButton("Generate / rebuild lighting").button.onClick.AddListener(Generate);
                CreateButton("Align lighting to current view").button.onClick.AddListener(AlignToView);
                CreateButton("Remove lighting").button.onClick.AddListener(RemoveLighting);
                style = new JSONStorableStringChooser("Look", new List<string> { "Soft Portrait", "Clean Studio", "Cinematic" }, "Soft Portrait", "Look");
                RegisterStringChooser(style); CreatePopup(style);
                exposure = Slider("Overall brightness", 1f, 0f, 2.5f, false);
                fill = Slider("Fill amount", 1f, 0f, 2f, false);
                rim = Slider("Rim amount", 1f, 0f, 2f, false);
                spread = Slider("Rig distance", 1f, 0.65f, 1.6f, true);
                azimuth = Slider("Light direction", 0f, -180f, 180f, true);
                softness = Slider("Follow smoothing (seconds)", 0.18f, 0f, 0.8f, true);
                rotateWithBody = new JSONStorableBool("Follow body turns", true);
                RegisterBool(rotateWithBody); CreateToggle(rotateWithBody, true);
                shadows = new JSONStorableBool("Key light soft shadows", true);
                RegisterBool(shadows); CreateToggle(shadows, true);
                headingOffset = new JSONStorableFloat("Saved heading offset", 0f, -720f, 720f, false);
                RegisterFloat(headingOffset);
                calibrated = new JSONStorableBool("View calibrated", false);
                RegisterBool(calibrated);
                RegisterAction(new JSONStorableAction("Generate lighting", Generate));
                RegisterAction(new JSONStorableAction("Align to view", AlignToView));
                RegisterAction(new JSONStorableAction("Remove lighting", RemoveLighting));
                CreateTextField(new JSONStorableString("Tips", "Soft Portrait: warm key, neutral fill, cool rim.\nClean Studio: neutral, brighter fill.\nCinematic: deeper contrast, warm/cool separation.\n\nExisting scene lights remain active. If the image is too bright, reduce Overall brightness.\nOnly the key casts shadows to keep the rig lightweight.\nSettings are saved with the scene / plugin preset."), true).height = 260f;
                ready = true;
            }
            catch (Exception e)
            {
                SuperController.LogError("QuickFill Init: " + e);
                if (status != null) status.val = "QuickFill could not initialize. See VaM error log.";
                DestroyRig();
            }
        }

        private JSONStorableFloat Slider(string name, float value, float min, float max, bool right)
        {
            JSONStorableFloat s = new JSONStorableFloat(name, value, min, max);
            RegisterFloat(s); CreateSlider(s, right); return s;
        }

        // followWhenOff tracks the physical body even when controller targets are disabled.
        private Transform Body(FreeControllerV3 controller)
        {
            if (controller.followWhenOff != null) return controller.followWhenOff;
            return controller.transform;
        }

        private float BodyYaw()
        {
            Vector3 forward = Body(hip).forward;
            forward.y = 0f;
            // Keep the last valid azimuth when the pelvis points vertically (lying poses).
            if (forward.sqrMagnitude > 0.08f)
                lastBodyYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return lastBodyYaw;
        }

        public void AlignToView()
        {
            if (!ready) return;
            Vector3 target = Vector3.Lerp(Body(chest).position, Body(head).position, 0.55f);
            Transform viewer = SuperController.singleton.centerCameraTarget != null
                ? SuperController.singleton.centerCameraTarget.transform : null;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            float bodyYaw = BodyYaw();
            float yaw = bodyYaw;
            if (viewer != null)
            {
                Vector3 direction = viewer.position - target;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f)
                    yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            }
            headingOffset.val = Mathf.DeltaAngle(bodyYaw, yaw);
            azimuth.val = 0f;
            calibrated.val = true;
            poseValid = false;
        }

        public void Generate()
        {
            if (!ready) return;
            lighting.val = true;
            DestroyRig();
            poseValid = false;
        }

        public void RemoveLighting()
        {
            if (!ready) return;
            lighting.val = false;
            DestroyRig();
        }

        private void BuildRig()
        {
            try
            {
                rig = new GameObject("QuickFill - " + containingAtom.uid);
                rig.SetActive(false);
                string[] names = { "Key", "Fill", "Rim", "Eye" };
                for (int i = 0; i < lamps.Length; i++)
                {
                    GameObject child = new GameObject("QuickFill " + names[i]);
                    child.transform.SetParent(rig.transform, false);
                    Light light = child.AddComponent<Light>();
                    lamps[i] = light;
                    light.type = LightType.Spot;
                    light.renderMode = LightRenderMode.ForcePixel;
                    light.shadows = LightShadows.None;
                    light.shadowBias = 0.035f;
                    light.shadowNormalBias = 0.12f;
                    light.shadowNearPlane = 0.12f;
                    light.bounceIntensity = 0f;
                    // Exclude UI / hidden helper layers; VaM scene geometry uses Default.
                    light.cullingMask = 1;
                }
                poseValid = false;
            }
            catch
            {
                DestroyRig();
                throw;
            }
        }

        private void LateUpdate()
        {
            if (!ready) return;
            try
            {
                // Allow scene JSON restore and person initialization to finish first.
                if (warmup < 10) { warmup++; return; }
                if (!lighting.val || !containingAtom.on || head == null || chest == null || hip == null)
                {
                    if (rig != null) rig.SetActive(false);
                    poseValid = false;
                    return;
                }
                if (!calibrated.val) AlignToView();
                if (rig == null) BuildRig();
                Vector3 headPos = Body(head).position;
                Vector3 chestPos = Body(chest).position;
                Vector3 target = Vector3.Lerp(chestPos, headPos, 0.55f);
                float bodyYaw = BodyYaw();
                float desiredYaw = bodyYaw + headingOffset.val;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                float blend = softness.val <= 0.001f ? 1f : 1f - Mathf.Exp(-dt / softness.val);
                bool snap = !poseValid || (target - smoothTarget).sqrMagnitude > 4f;
                if (snap)
                {
                    smoothTarget = target;
                    smoothYaw = desiredYaw;
                }
                else
                {
                    smoothTarget = Vector3.Lerp(smoothTarget, target, blend);
                    if (rotateWithBody.val) smoothYaw = Mathf.LerpAngle(smoothYaw, desiredYaw, blend);
                }
                poseValid = true;
                rig.transform.position = smoothTarget;
                rig.transform.rotation = Quaternion.Euler(0f, smoothYaw + azimuth.val, 0f);
                // Adapt to avatar scale using physical head-to-hip distance, also valid lying down.
                float bodySize = Mathf.Clamp(Vector3.Distance(headPos, Body(hip).position) / 0.65f, 0.65f, 1.55f);
                smoothSize = snap ? bodySize : Mathf.Lerp(smoothSize, bodySize, blend);
                float distance = spread.val * smoothSize;
                bool cinema = style.val == "Cinematic";
                bool studio = style.val == "Clean Studio";
                Color keyColor = studio ? new Color(1f, 0.96f, 0.91f) : new Color(1f, 0.87f, 0.75f);
                Color rimColor = studio ? new Color(0.91f, 0.95f, 1f) : new Color(0.68f, 0.82f, 1f);
                Setup(0, new Vector3(-1.25f, 0.65f, 1.65f), new Vector3(0f, -0.10f, 0f), distance, 2.4f, keyColor, 78f);
                Setup(1, new Vector3(1.35f, 0.18f, 1.35f), new Vector3(0f, -0.20f, 0f), distance,
                    (cinema ? 0.38f : studio ? 1.15f : 0.80f) * fill.val, new Color(0.91f, 0.95f, 1f), 95f);
                Setup(2, new Vector3(0.95f, 0.75f, -1.25f), new Vector3(0f, -0.15f, 0f), distance,
                    (cinema ? 2.1f : 1.55f) * rim.val, rimColor, 72f);
                Setup(3, new Vector3(0.10f, 0.20f, 1.65f), Vector3.zero, distance,
                    cinema ? 0.12f : 0.22f, new Color(1f, 0.96f, 0.91f), 65f);
                lamps[0].shadows = shadows.val ? LightShadows.Soft : LightShadows.None;
                lamps[0].shadowStrength = 0.85f;
                rig.SetActive(true);
            }
            catch (Exception e)
            {
                SuperController.LogError("QuickFill stopped: " + e);
                lighting.val = false;
                DestroyRig();
                status.val = "Lighting stopped after an error. See VaM error log, then Generate to retry.";
            }
        }

        private void Setup(int index, Vector3 position, Vector3 aim, float scale, float intensity, Color color, float angle)
        {
            Light light = lamps[index];
            light.transform.localPosition = position * scale;
            light.transform.localRotation = Quaternion.LookRotation((aim - position).normalized, Vector3.up);
            light.range = 6f * scale;
            light.spotAngle = angle;
            light.intensity = intensity * exposure.val;
            light.color = color;
        }

        private void DestroyRig()
        {
            if (rig != null) { rig.SetActive(false); Destroy(rig); }
            rig = null;
            for (int i = 0; i < lamps.Length; i++) lamps[i] = null;
            poseValid = false;
        }

        private void OnDisable() { DestroyRig(); }
        private void OnEnable() { poseValid = false; warmup = 0; }
        private void OnDestroy() { ready = false; DestroyRig(); }
    }
}
