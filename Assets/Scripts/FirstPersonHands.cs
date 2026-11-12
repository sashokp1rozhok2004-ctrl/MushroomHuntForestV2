using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Animated first-person viewmodel for WRAD ARMS (wriks, CC0).
/// Uses existing FBX deformation bones, so no external animation package is needed.
/// The old model is only a fallback and will be hidden if WRAD is installed.
/// </summary>
public class FirstPersonHands : MonoBehaviour
{
    // These legacy fields are intentionally retained so serialized references in
    // the original Forest.unity file survive the replacement of this script.
    public Transform leftHand, rightHand;

    [Header("New WRAD first-person viewmodel")]
    public Transform viewmodelRoot;
    public bool showHands = false;
    [Range(0f, 1.5f)] public float walkSway = 0.035f;
    [Range(0f, 1.5f)] public float reachAmount = 0.21f;
    [Range(-100f, 100f)] public float fingerCurlDegrees = 48f;
    public Vector3 fingerCurlAxis = Vector3.right;

    public bool AreVisible => showHands && viewmodelRoot && viewmodelRoot.gameObject.activeInHierarchy;

    Transform rightShoulder, rightBicep, rightForearm, rightWrist;
    Transform leftShoulder, leftForearm, leftWrist;
    Transform[] fingers = new Transform[15];
    Quaternion[] fingerRest = new Quaternion[15];
    readonly Dictionary<Transform, Quaternion> boneRest = new Dictionary<Transform, Quaternion>();
    Transform oldHandsRoot;
    Renderer[] viewRenderers;
    CharacterController character;
    Vector3 basePosition;
    Quaternion baseRotation;
    float reachBlend, grabBlend;
    Vector3 targetOffset;
    bool busy;
    float phase;

    void Start()
    {
        character = GetComponent<CharacterController>();
        if (!viewmodelRoot)
        {
            Camera camera = GetComponentInChildren<Camera>(true);
            if (camera) viewmodelRoot = FindRecursive(camera.transform, "WRAD_Viewmodel");
        }
        oldHandsRoot = FindOldHands();
        if (oldHandsRoot)
            foreach (Renderer renderer in oldHandsRoot.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

        if (!viewmodelRoot)
        {
            showHands = false;
            return;
        }
        basePosition = viewmodelRoot.localPosition;
        baseRotation = viewmodelRoot.localRotation;
        viewRenderers = viewmodelRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in viewRenderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skin)
                skin.updateWhenOffscreen = true;
        }

        rightShoulder = FindRecursive(viewmodelRoot, "shoulder.r");
        rightBicep = FindRecursive(viewmodelRoot, "bicep.r");
        rightForearm = FindRecursive(viewmodelRoot, "forearm.r");
        rightWrist = FindRecursive(viewmodelRoot, "wrist.r");
        leftShoulder = FindRecursive(viewmodelRoot, "shoulder.l");
        leftForearm = FindRecursive(viewmodelRoot, "forearm.l");
        leftWrist = FindRecursive(viewmodelRoot, "wrist.l");

        Remember(rightShoulder); Remember(rightBicep); Remember(rightForearm); Remember(rightWrist);
        Remember(leftShoulder); Remember(leftForearm); Remember(leftWrist);

        string[] names = {"finger_thumb1.r", "finger_thumb2.r", "finger_thumb3.r",
                          "finger_index1.r", "finger_index2.r", "finger_index3.r",
                          "finger_middle1.r", "finger_middle2.r", "finger_middle3.r",
                          "finger_ring1.r", "finger_ring2.r", "finger_ring3.r",
                          "finger_pinky1.r", "finger_pinky2.r", "finger_pinky3.r"};
        for (int i = 0; i < names.Length; i++)
        {
            fingers[i] = FindRecursive(viewmodelRoot, names[i]);
            if (fingers[i]) fingerRest[i] = fingers[i].localRotation;
        }
        ApplyVisibility();
    }

    static Transform FindRecursive(Transform parent, string name)
    {
        if (!parent) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindRecursive(child, name);
            if (found) return found;
        }
        return null;
    }

    Transform FindOldHands()
    {
        Camera camera = GetComponentInChildren<Camera>(true);
        if (camera)
        {
            Transform holder = camera.transform.parent;
            if (holder) return FindRecursive(holder, "Hands");
        }
        return null;
    }

    void Remember(Transform bone)
    {
        if (bone && !boneRest.ContainsKey(bone)) boneRest.Add(bone, bone.localRotation);
    }

    public void SetVisible(bool visible)
    {
        showHands = visible && viewmodelRoot != null;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        if (viewRenderers == null) return;
        foreach (Renderer r in viewRenderers)
            if (r) r.enabled = showHands;
    }

    void Update()
    {
        // Diagnostic toggle: the viewmodel can be enabled/disabled for comparison.
        if (Input.GetKeyDown(KeyCode.H)) SetVisible(!showHands);
    }

    void LateUpdate()
    {
        if (!viewmodelRoot) return;
        float speed = character ? new Vector2(character.velocity.x, character.velocity.z).magnitude : 0f;
        float moving = Mathf.Clamp01(speed / 4f);
        phase += Time.deltaTime * (7.4f + speed * 1.7f);
        float idle = Mathf.Sin(Time.time * 1.55f) * 0.003f;
        Vector3 bob = new Vector3(Mathf.Sin(phase * 0.5f) * walkSway * moving,
                                  Mathf.Sin(phase) * walkSway * 0.65f * moving + idle, 0f);
        viewmodelRoot.localPosition = basePosition + bob + targetOffset * reachBlend;
        viewmodelRoot.localRotation = baseRotation * Quaternion.Euler(-reachBlend * 5f, 0f,
            Mathf.Sin(phase * 0.5f) * moving * 1.1f);

        // Right-arm reach followed by a separate, brief finger grasp.
        Pose(rightShoulder, new Vector3(-24f, -7f, -5f), reachBlend);
        Pose(rightBicep, new Vector3(-19f, 3f, 0f), reachBlend);
        Pose(rightForearm, new Vector3(-30f, 0f, 2f), reachBlend);
        Pose(rightWrist, new Vector3(12f, -3f, -4f), reachBlend);
        // The supporting arm stays relaxed rather than copying the reach.
        Pose(leftShoulder, new Vector3(1f, 0f, 0f), moving * 0.14f);
        Pose(leftForearm, Vector3.zero, 0f);
        Pose(leftWrist, Vector3.zero, 0f);

        Vector3 curlAxis = fingerCurlAxis.sqrMagnitude > 0.0001f ? fingerCurlAxis.normalized : Vector3.right;
        for (int i = 0; i < fingers.Length; i++)
        {
            if (!fingers[i]) continue;
            float grip = i < 3 ? 0.63f : 1f; // thumb folds a little less
            float bend = fingerCurlDegrees * grabBlend * grip;
            fingers[i].localRotation = fingerRest[i] * Quaternion.AngleAxis(bend, curlAxis);
        }
    }

    void Pose(Transform bone, Vector3 angles, float weight)
    {
        if (!bone || !boneRest.TryGetValue(bone, out Quaternion rest)) return;
        bone.localRotation = Quaternion.Slerp(rest, rest * Quaternion.Euler(angles), Mathf.Clamp01(weight));
    }

    /// <summary>
    /// Runs a two-stage reach and invokes onContact when the fingers close.
    /// The game only hides and counts the mushroom at this contact moment.
    /// </summary>
    public IEnumerator PlayPickup(Vector3 targetWorldPosition, Action onContact)
    {
        if (!AreVisible || busy)
        {
            onContact?.Invoke();
            yield break;
        }
        busy = true;
        bool contacted = false;
        Transform camera = viewmodelRoot.parent;
        Vector3 targetLocal = camera ? camera.InverseTransformPoint(targetWorldPosition) : Vector3.zero;
        // Limit camera-space travel so even a mushroom at 3 m doesn't stretch arms unnaturally.
        targetOffset = new Vector3(Mathf.Clamp(targetLocal.x, -0.2f, 0.2f) * 0.32f,
                                   Mathf.Clamp(targetLocal.y + 0.36f, -0.12f, 0.10f),
                                   Mathf.Clamp(targetLocal.z, 0.1f, 2.5f) * reachAmount);

        const float reachSeconds = 0.22f;
        const float closeSeconds = 0.085f;
        const float returnSeconds = 0.31f;
        float time = 0f;
        while (time < reachSeconds)
        {
            time += Time.deltaTime;
            reachBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / reachSeconds));
            yield return null;
        }
        reachBlend = 1f;
        time = 0f;
        while (time < closeSeconds)
        {
            time += Time.deltaTime;
            grabBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / closeSeconds));
            yield return null;
        }
        onContact?.Invoke();
        contacted = true;
        time = 0f;
        while (time < returnSeconds)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / returnSeconds));
            reachBlend = 1f - t;
            grabBlend = 1f - t;
            yield return null;
        }
        if (!contacted) onContact?.Invoke();
        reachBlend = 0f;
        grabBlend = 0f;
        targetOffset = Vector3.zero;
        busy = false;
    }

    // Backward-compatible coroutine for any earlier scripts still calling PlayPickup().
    public IEnumerator PlayPickup()
    {
        yield return PlayPickup(transform.position + transform.forward, null);
    }
}
