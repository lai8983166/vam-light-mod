using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaiLighting
{
    // Keep the MVRScript entry type first: VaM's single-file loader selects the main type.
    // VaM 1.x / Unity 2018. No external dependencies or scene light mutation.
    public class QuickFill : MVRScript
    {
        // Key, fill, rim, eye, plus eight optional key samples (up to a 3x3 panel).
        private readonly Light[] lamps = new Light[12];
        private GameObject rig;
        private FreeControllerV3 head, chest, hip;
        private JSONStorableBool lighting, rotateWithBody, shadows, rimShadows, calibrated;
        private JSONStorableFloat exposure, fill, rim, spread, softness, azimuth, headingOffset;
        private JSONStorableFloat elevation, keyElevation, rimElevation, shadowStrength, shadowBias, normalBias;
        private JSONStorableFloat softboxWidth, softboxHeight;
        private JSONStorableStringChooser style, layout, sampling;
        private string displayedLayout;
        private int displayedSamples;
        private JSONStorableString status;
        private bool ready, poseValid;
        private int warmup;
        private Vector3 smoothTarget;
        private float smoothYaw, lastBodyYaw, smoothSize = 1f;

        public override void Init()
        {
            try
            {
                status = new JSONStorableString("Info", "QuickFill 0.3 | Portrait lighting\nAdd to a Person. Compare Classic and Softbox in the same view.\nAlign to view places the lighting for your current viewpoint.");
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
                layout = new JSONStorableStringChooser("Lighting layout", new List<string> { "Classic (4 lights)", "Softbox (sampled key)" }, "Classic (4 lights)", "Lighting layout");
                RegisterStringChooser(layout); CreatePopup(layout);
                CreateButton("A/B: Classic / Softbox").button.onClick.AddListener(ToggleLayout);
                RegisterAction(new JSONStorableAction("Toggle Classic / Softbox", ToggleLayout));
                sampling = new JSONStorableStringChooser("Softbox quality", new List<string> { "Balanced (4 samples)", "High (9 samples)" }, "Balanced (4 samples)", "Softbox quality");
                RegisterStringChooser(sampling); CreatePopup(sampling);
                softboxWidth = Slider("Softbox width (m)", 0.8f, 0f, 2f, false);
                softboxHeight = Slider("Softbox height (m)", 1f, 0f, 2f, false);
                style = new JSONStorableStringChooser("Look", new List<string> { "Soft Portrait", "Clean Studio", "Cinematic" }, "Soft Portrait", "Look");
                RegisterStringChooser(style); CreatePopup(style);
                exposure = Slider("Overall brightness", 1f, 0f, 2.5f, false);
                fill = Slider("Fill amount", 1f, 0f, 2f, false);
                rim = Slider("Rim amount", 1f, 0f, 2f, false);
                spread = Slider("Rig distance", 1f, 0.65f, 1.6f, true);
                azimuth = Slider("Light direction", 0f, -180f, 180f, true);
                elevation = Slider("Rig elevation offset", 0f, -45f, 45f, true);
                keyElevation = Slider("Key elevation", 25f, -30f, 75f, true);
                rimElevation = Slider("Rim elevation", 30f, -30f, 75f, true);
                softness = Slider("Follow smoothing (seconds)", 0.18f, 0f, 0.8f, true);
                rotateWithBody = new JSONStorableBool("Follow body turns", true);
                RegisterBool(rotateWithBody); CreateToggle(rotateWithBody, true);
                shadows = new JSONStorableBool("Key light soft shadows", true);
                RegisterBool(shadows); CreateToggle(shadows, true);
                rimShadows = new JSONStorableBool("Rim light soft shadows", true);
                RegisterBool(rimShadows); CreateToggle(rimShadows, true);
                shadowStrength = Slider("Shadow strength", 0.95f, 0f, 1f, true);
                shadowBias = Slider("Shadow depth bias", 0.015f, 0f, 0.08f, true);
                normalBias = Slider("Shadow normal bias", 0.025f, 0f, 0.2f, true);
                headingOffset = new JSONStorableFloat("Saved heading offset", 0f, -720f, 720f, false);
                RegisterFloat(headingOffset);
                calibrated = new JSONStorableBool("View calibrated", false);
                RegisterBool(calibrated);
                RegisterAction(new JSONStorableAction("Generate lighting", Generate));
                RegisterAction(new JSONStorableAction("Align to view", AlignToView));
                RegisterAction(new JSONStorableAction("Remove lighting", RemoveLighting));
                CreateTextField(new JSONStorableString("Tips", "A/B preserves brightness, direction, style and follow position. Classic uses the previous four-light layout. Softbox replaces only the key with 4 or 9 distributed lights; total key intensity stays constant.\n\nWidth / height scale with the person. Larger panels soften the transition but can show multiple shadows. Try High or a smaller panel. Enable Key light soft shadows for shadow softening.\n\nElevation: positive = above, negative = below. Existing scene lights stay active. Softbox costs more GPU time; it is an approximation, not a real area light."), true).height = 350f;
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

        public void ToggleLayout()
        {
            if (!ready) return;
            layout.val = layout.val == "Softbox (sampled key)" ? "Classic (4 lights)" : "Softbox (sampled key)";
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
                    GameObject child = new GameObject("QuickFill " + (i < 4 ? names[i] : "Key sample " + (i - 2)));
                    child.transform.SetParent(rig.transform, false);
                    Light light = child.AddComponent<Light>();
                    lamps[i] = light;
                    light.type = LightType.Spot;
                    light.renderMode = LightRenderMode.ForcePixel;
                    light.shadows = LightShadows.None;
                    light.enabled = i < 4;
                    light.shadowBias = shadowBias.val;
                    light.shadowNormalBias = normalBias.val;
                    light.shadowCustomResolution = i == 0 ? 2048 : 1024;
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
                Setup(0, new Vector3(-1.25f, 0.65f, 1.65f), new Vector3(0f, -0.10f, 0f), keyElevation.val, distance, 2.4f, keyColor, 72f);
                Setup(1, new Vector3(1.35f, 0.18f, 1.35f), new Vector3(0f, -0.20f, 0f), distance,
                    (cinema ? 0.28f : studio ? 0.85f : 0.55f) * fill.val, new Color(0.91f, 0.95f, 1f), 95f);
                Setup(2, new Vector3(1.15f, 0.75f, -1.35f), new Vector3(0f, -0.22f, 0f), rimElevation.val, distance,
                    (cinema ? 1.8f : 1.35f) * rim.val, rimColor, 65f);
                Setup(3, new Vector3(0.10f, 0.20f, 1.65f), Vector3.zero, distance,
                    cinema ? 0.08f : 0.14f, new Color(1f, 0.96f, 0.91f), 65f);
                ConfigureShadow(lamps[0], shadows.val);
                ConfigureShadow(lamps[2], rimShadows.val);
                ApplyLayout(distance);
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
            Vector3 offset = position - aim;
            float baseElevation = Mathf.Atan2(offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg;
            Setup(index, position, aim, baseElevation, scale, intensity, color, angle);
        }

        private void Setup(int index, Vector3 position, Vector3 aim, float baseElevation, float scale, float intensity, Color color, float angle)
        {
            Light light = lamps[index];
            // Orbit each lamp around its aim point, preserving distance and horizontal bearing.
            // Pitching the whole rig would raise front lights but lower rear lights.
            Vector3 offset = position - aim;
            float radius = offset.magnitude;
            float pitch = Mathf.Clamp(baseElevation + elevation.val, -75f, 85f) * Mathf.Deg2Rad;
            Vector3 horizontal = new Vector3(offset.x, 0f, offset.z).normalized;
            position = aim + radius * (horizontal * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch));
            light.transform.localPosition = position * scale;
            light.transform.localRotation = Quaternion.LookRotation((aim - position).normalized, Vector3.up);
            light.range = 6f * scale;
            light.spotAngle = angle;
            light.intensity = intensity * exposure.val;
            light.color = color;
        }

        private void ConfigureShadow(Light light, bool active)
        {
            light.shadows = active ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = shadowStrength.val;
            light.shadowBias = shadowBias.val;
            light.shadowNormalBias = normalBias.val;
        }

        private void ApplyLayout(float distance)
        {
            bool useSoftbox = layout.val == "Softbox (sampled key)";
            int grid = sampling.val == "High (9 samples)" ? 3 : 2;
            int count = useSoftbox ? grid * grid : 1;
            // Disable unused samples before configuring the active ones, with no rebuild or pose reset.
            for (int i = 4; i < lamps.Length; i++)
            {
                lamps[i].enabled = useSoftbox && i - 3 < count;
                if (!lamps[i].enabled) lamps[i].shadows = LightShadows.None;
            }
            Light key = lamps[0];
            key.shadowCustomResolution = useSoftbox ? 1024 : 2048;
            if (useSoftbox)
            {
                // Capture the center before sample 0 moves the original key.
                Vector3 center = key.transform.localPosition;
                Quaternion orientation = key.transform.localRotation;
                Vector3 aim = new Vector3(0f, -0.10f, 0f) * distance;
                float totalIntensity = key.intensity;
                for (int sample = 0; sample < count; sample++)
                {
                    Light light = lamps[sample == 0 ? 0 : sample + 3];
                    Vector3 panelOffset = new Vector3(
                        QuickFillSampling.Offset(sample % grid, grid) * softboxWidth.val,
                        QuickFillSampling.Offset(sample / grid, grid) * softboxHeight.val, 0f);
                    Vector3 position = center + orientation * (panelOffset * smoothSize);
                    light.transform.localPosition = position;
                    light.transform.localRotation = Quaternion.LookRotation(aim - position, orientation * Vector3.up);
                    light.range = key.range;
                    light.spotAngle = key.spotAngle;
                    light.color = key.color;
                    light.intensity = totalIntensity * QuickFillSampling.Weight(grid);
                    light.shadowCustomResolution = 1024;
                    ConfigureShadow(light, shadows.val);
                }
            }
            if (displayedLayout != layout.val || displayedSamples != count)
            {
                displayedLayout = layout.val;
                displayedSamples = count;
                status.val = "QuickFill 0.3 | " + (useSoftbox ? "Softbox" : "Classic") +
                    "\n" + (count + 3) + " active lights; " + count + " key sample(s)." +
                    "\nA/B keeps your pose and settings. Total key intensity is shared; perceived brightness may differ.";
            }
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

    // Helper types must follow the MVRScript entry type for single-file VaM loading.
    public static class QuickFillSampling
    {
        public static float Offset(int coordinate, int gridSize)
        {
            return (coordinate + 0.5f) / gridSize - 0.5f;
        }

        public static float Weight(int gridSize) { return 1f / (gridSize * gridSize); }
    }
}
