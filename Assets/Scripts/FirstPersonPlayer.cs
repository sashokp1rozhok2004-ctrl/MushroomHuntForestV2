using UnityEngine;

/// <summary>
/// First-person WASD character. Preserves references from the original Forest scene.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayer : MonoBehaviour
{
    public Transform head;
    public MushroomGame game;
    public float speed = 4f;
    public float runMultiplier = 1.8f;
    public float jumpHeight = 1.2f;
    public float gravity = -22f;
    public float playableHalfSize = 72f;
    public float sensitivity = 2f;

    float pitch;
    float vertical;
    CharacterController controller;
    Vector3 spawnPoint;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = Vector3.up * 0.9f;
        spawnPoint = new Vector3(transform.position.x, 0f, transform.position.z);
        transform.position = spawnPoint;

        // The scene also has GroundPhysics, so disable the duplicate plane collider.
        GameObject ground = GameObject.Find("Ground");
        if (ground)
        {
            MeshCollider redundantCollider = ground.GetComponent<MeshCollider>();
            if (redundantCollider) redundantCollider.enabled = false;
        }

        if (!game) game = GetComponent<MushroomGame>();
        if (!head)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera)
                head = playerCamera.transform.parent ? playerCamera.transform.parent : playerCamera.transform;
        }
    }

    void Update()
    {
        if (game && (game.Won || game.IsPaused)) return;
        if (!controller || !controller.enabled) return;

        if (transform.position.y < -10f)
        {
            controller.enabled = false;
            transform.position = spawnPoint;
            vertical = 0f;
            controller.enabled = true;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, Input.GetAxis("Mouse X") * sensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -85f, 85f);
            if (head) head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        bool grounded = controller.isGrounded;
        if (grounded && vertical < 0f) vertical = -2f;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;
        if (Input.GetKey(KeyCode.LeftShift)) move *= runMultiplier;

        if (Input.GetKeyDown(KeyCode.Space) && grounded)
            vertical = Mathf.Sqrt(jumpHeight * -2f * gravity);
        vertical += gravity * Time.deltaTime;
        controller.Move((move + Vector3.up * vertical) * Time.deltaTime);

        // Preserve the original circular bounds to prevent leaving the forest.
        Vector3 position = transform.position;
        Vector2 horizontal = Vector2.ClampMagnitude(new Vector2(position.x, position.z), playableHalfSize);
        if (!Mathf.Approximately(position.x, horizontal.x) || !Mathf.Approximately(position.z, horizontal.y))
        {
            controller.enabled = false;
            transform.position = new Vector3(horizontal.x, position.y, horizontal.y);
            controller.enabled = true;
        }
    }
}
