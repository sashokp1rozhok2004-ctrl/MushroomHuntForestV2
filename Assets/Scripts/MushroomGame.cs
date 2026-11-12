using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Five-mushroom hunt with animated WRAD arm reach, item collection, and victory screen.
/// </summary>
public class MushroomGame : MonoBehaviour
{
    [Header("Assignment")]
    [Min(1)] public int target = 5;
    public int collected;
    public bool Won => collected >= target;
    public bool IsPaused { get; private set; }

    [Header("Interaction")]
    public Camera view;
    public FirstPersonHands hands;
    [Range(1f, 6f)] public float collectDistance = 3f;
    [Range(0.05f, 0.35f)] public float aimAssistRadius = 0.18f;

    readonly RaycastHit[] collectHits = new RaycastHit[64];
    readonly RaycastHit[] blockingHits = new RaycastHit[64];
    MushroomPickup targetedMushroom;
    AudioSource sounds;
    AudioClip pickupTone;
    AudioClip winTone;
    float elapsedTime;
    float lastPickupAt = -100f;
    bool collecting;

    GUIStyle titleStyle, smallStyle, counterStyle, centerStyle, buttonStyle;
    static Texture2D whitePixel;
    static readonly Color cream = new Color(0.97f, 0.96f, 0.90f, 1f);
    static readonly Color green = new Color(0.62f, 0.85f, 0.54f, 1f);
    static readonly Color dark = new Color(0.08f, 0.13f, 0.11f, 0.91f);

    void Awake()
    {
        // The committed scene may still serialize target = 10. The assignment is five.
        target = 5;
        collected = 0;
        Time.timeScale = 1f;
        sounds = GetComponent<AudioSource>();
        if (!sounds) sounds = gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false;
        sounds.spatialBlend = 0f;
        sounds.volume = 0.6f;
        pickupTone = MakeTone("Mushroom pickup", 650f, 940f, 0.19f);
        winTone = MakeTone("Mushroom victory", 510f, 1000f, 0.62f);
    }

    void Start()
    {
        if (!view) view = GetComponentInChildren<Camera>();
        if (!hands) hands = GetComponent<FirstPersonHands>();
        SetCursorLocked(true);
    }

    void OnDisable()
    {
        // Leaving Play Mode while the pause menu is open must not freeze the editor.
        if (IsPaused) Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !Won)
        {
            TogglePause();
            return;
        }
        if (Input.GetKeyDown(KeyCode.R) && (Won || IsPaused))
        {
            Restart();
            return;
        }
        if (Won || IsPaused) return;

        elapsedTime += Time.deltaTime;
        CanCollect(out targetedMushroom);
        if (!collecting && Input.GetKeyDown(KeyCode.E) && targetedMushroom)
            StartCoroutine(CollectRoutine(targetedMushroom));
    }

    void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        SetCursorLocked(!IsPaused);
    }

    void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public bool CanCollect(out MushroomPickup mushroom)
    {
        mushroom = null;
        if (!view || Won || IsPaused || collecting) return false;

        Transform cameraTransform = view.transform;
        Vector3 origin = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;
        int count = Physics.SphereCastNonAlloc(origin, aimAssistRadius, direction,
            collectHits, collectDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = collectHits[i].collider;
            if (!collider || !collider.enabled) continue;
            MushroomPickup candidate = collider.GetComponentInParent<MushroomPickup>();
            if (!candidate || candidate.IsCollected || !candidate.gameObject.activeInHierarchy) continue;

            float distance = collectHits[i].distance;
            if (distance < closest && HasClearLineOfSight(origin, candidate))
            {
                mushroom = candidate;
                closest = distance;
            }
        }
        return mushroom != null;
    }

    bool HasClearLineOfSight(Vector3 cameraPosition, MushroomPickup mushroom)
    {
        // Ignore trigger volumes (including other mushrooms), but not tree/rock walls.
        Collider targetCollider = mushroom.GetComponentInChildren<Collider>();
        Vector3 destination = targetCollider ? targetCollider.bounds.center :
            mushroom.transform.position + Vector3.up * 0.4f;
        Vector3 direction = destination - cameraPosition;
        float distance = direction.magnitude;
        if (distance < 0.001f) return true;
        int hits = Physics.RaycastNonAlloc(cameraPosition, direction / distance,
            blockingHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider blocker = blockingHits[i].collider;
            if (!blocker || !blocker.enabled) continue;
            if (blocker.transform.IsChildOf(transform)) continue; // player body
            if (blocker.transform.IsChildOf(mushroom.transform)) continue;
            if (blockingHits[i].distance < distance - 0.08f) return false;
        }
        return true;
    }

    System.Collections.IEnumerator CollectRoutine(MushroomPickup mushroom)
    {
        if (!mushroom) yield break;
        // Capture the center BEFORE TryCollect disables the collider: disabled
        // colliders return empty Bounds and would make the hand reach toward 0,0,0.
        Collider collider = mushroom.GetComponentInChildren<Collider>();
        Vector3 contact = collider && collider.enabled ? collider.bounds.center :
            mushroom.transform.position + Vector3.up * 0.4f;
        if (!mushroom.TryCollect()) yield break;
        collecting = true;
        targetedMushroom = null;
        bool completed = false;
        System.Action contactCallback = () =>
        {
            if (completed) return;
            completed = true;
            FinalizeCollection(mushroom, contact);
        };

        // The hand reaches for the object while it remains visible.
        // At the grasp moment the object disappears and the score changes.
        if (hands && hands.AreVisible)
            yield return StartCoroutine(hands.PlayPickup(contact, contactCallback));
        else
            contactCallback();

        // The callback is idempotent in case the animation fails or ends early.
        if (!completed) contactCallback();
        collecting = false;
    }

    void FinalizeCollection(MushroomPickup mushroom, Vector3 contact)
    {
        if (mushroom) mushroom.gameObject.SetActive(false);
        MushroomCollectEffect.PlayAt(contact);
        if (sounds && pickupTone) sounds.PlayOneShot(pickupTone);
        collected++;
        lastPickupAt = Time.unscaledTime;

        if (Won)
        {
            SetCursorLocked(false);
            if (sounds && winTone) sounds.PlayOneShot(winTone);
        }
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    static AudioClip MakeTone(string name, float fromFrequency, float toFrequency, float duration)
    {
        const int rate = 22050;
        int length = Mathf.Max(1, Mathf.RoundToInt(duration * rate));
        var samples = new float[length];
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float frequency = Mathf.Lerp(fromFrequency, toFrequency, t);
            phase += 2.0 * System.Math.PI * frequency / rate;
            float envelope = Mathf.Pow(1f - t, 2f);
            samples[i] = (float)System.Math.Sin(phase) * envelope * 0.28f;
        }
        AudioClip clip = AudioClip.Create(name, length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold,
            normal = { textColor = green }
        };
        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, wordWrap = true,
            normal = { textColor = cream }
        };
        counterStyle = new GUIStyle(smallStyle)
        {
            fontSize = 30, fontStyle = FontStyle.Bold
        };
        centerStyle = new GUIStyle(counterStyle)
        {
            fontSize = 36, alignment = TextAnchor.MiddleCenter, wordWrap = true
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19, fontStyle = FontStyle.Bold
        };
    }

    static Texture2D WhitePixel()
    {
        if (whitePixel) return whitePixel;
        whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whitePixel.SetPixel(0, 0, Color.white);
        whitePixel.Apply();
        return whitePixel;
    }

    static void Fill(Rect area, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(area, WhitePixel());
        GUI.color = previous;
    }

    void OnGUI()
    {
        EnsureStyles();
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.6f, 2.0f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        // Compact, always visible progress HUD.
        Fill(new Rect(20, 20, 342, 142), dark);
        GUI.Label(new Rect(38, 31, 300, 26), "MUSHROOM HUNT", titleStyle);
        GUI.Label(new Rect(38, 60, 300, 44), $"Грибы: {collected} / {target}", counterStyle);
        for (int i = 0; i < target; i++)
            Fill(new Rect(40 + i * 59, 116, 48, 13), i < collected ? green :
                new Color(0.40f, 0.46f, 0.41f, 1f));

        Fill(new Rect(width - 378, 20, 358, 102), dark);
        GUI.Label(new Rect(width - 360, 30, 330, 80),
            "WASD — идти    Shift — бег    Space — прыжок\nE — собрать    Esc — пауза    H — показать руки",
            smallStyle);

        if (!Won && !IsPaused)
        {
            var crosshairStyle = new GUIStyle(centerStyle) { fontSize = 25 };
            crosshairStyle.normal.textColor = targetedMushroom ? green : cream;
            GUI.Label(new Rect(width / 2f - 24, height / 2f - 28, 48, 48), "+", crosshairStyle);
            if (targetedMushroom)
            {
                Fill(new Rect(width / 2f - 145, height / 2f + 47, 290, 42), dark);
                var prompt = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(width / 2f - 145, height / 2f + 55, 290, 26), "[E]  СОБРАТЬ ГРИБ", prompt);
            }
            if (Time.unscaledTime - lastPickupAt < 1.2f)
            {
                var notice = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(width / 2f - 150, height - 105, 300, 38), "+1 гриб!", notice);
            }
        }

        if (Won || IsPaused)
        {
            Fill(new Rect(0, 0, width, height), new Color(0.03f, 0.06f, 0.04f, 0.64f));
            Rect panel = new Rect(width / 2f - 265, height / 2f - 152, 530, 304);
            Fill(panel, new Color(0.10f, 0.16f, 0.12f, 0.98f));
            GUI.Label(new Rect(panel.x + 20, panel.y + 30, 490, 65),
                Won ? "ПОБЕДА!" : "ПАУЗА", centerStyle);
            var subtitle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
            string message = Won
                ? $"Все 5 грибов собраны за {Mathf.FloorToInt(elapsedTime / 60):00}:{Mathf.FloorToInt(elapsedTime % 60):00}!"
                : "Продолжить прогулку или начать заново?";
            GUI.Label(new Rect(panel.x + 28, panel.y + 105, 474, 48), message, subtitle);
            if (IsPaused && GUI.Button(new Rect(panel.x + 65, panel.y + 171, 190, 52), "Продолжить", buttonStyle))
                TogglePause();
            float restartX = IsPaused ? panel.x + 274 : panel.x + 145;
            if (GUI.Button(new Rect(restartX, panel.y + 171, IsPaused ? 190 : 240, 52), "Играть снова [R]", buttonStyle))
                Restart();
            GUI.Label(new Rect(panel.x + 35, panel.y + 245, 460, 36),
                Won ? "Спасибо за прогулку по лесу!" : "Esc — вернуться в игру", subtitle);
        }
        GUI.matrix = previousMatrix;
    }
}
