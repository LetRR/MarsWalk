using UnityEngine;

/// <summary>
/// Controle em primeira pessoa do astronauta, com gravidade marciana (3,721 m/s²).
/// WASD = andar | Shift = correr | Espaço = pular | Mouse = olhar
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class AstronautController : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 2.5f;
    public float sprintSpeed = 5f;
    public float jumpHeight = 1.2f;          // ~0,45 m na Terra com o mesmo impulso
    public float marsGravity = 3.721f;

    [Header("Câmera")]
    public Transform cameraPivot;
    public float mouseSensitivity = 2f;

    [HideInInspector] public float speedMultiplier = 1f; // reduzido pelo traje (bateria vazia)
    [HideInInspector] public bool lookEnabled = true;    // desligado quando o painel está aberto

    public bool IsSprinting { get; private set; }
    public float CurrentSpeed { get; private set; }

    CharacterController cc;
    float pitch;
    float verticalVelocity;
    Vector3 spawnPoint;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraPivot = cam.transform;
        }
    }

    void Start()
    {
        spawnPoint = transform.position;
        SetCursorLocked(true);
    }

    public void SetSpawnPoint(Vector3 p) { spawnPoint = p; }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void Respawn()
    {
        cc.enabled = false;
        transform.position = spawnPoint;
        cc.enabled = true;
        verticalVelocity = 0f;
    }

    void Update()
    {
        // Olhar
        if (lookEnabled && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = MarsInput.Look() * mouseSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            if (cameraPivot != null) cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }
        else if (lookEnabled && MarsInput.Click())
        {
            SetCursorLocked(true);
        }

        // Andar
        Vector2 mv = MarsInput.Move();
        Vector3 dir = transform.right * mv.x + transform.forward * mv.y;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        IsSprinting = MarsInput.Held(MarsKey.Sprint) && mv.y > 0.1f;
        float speed = (IsSprinting ? sprintSpeed : walkSpeed) * speedMultiplier;

        if (cc.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -1f;
            if (MarsInput.Down(MarsKey.Jump))
                verticalVelocity = Mathf.Sqrt(2f * marsGravity * jumpHeight);
        }
        verticalVelocity -= marsGravity * Time.deltaTime;

        Vector3 velocity = dir * speed + Vector3.up * verticalVelocity;
        cc.Move(velocity * Time.deltaTime);

        Vector3 v = cc.velocity; v.y = 0f;
        CurrentSpeed = v.magnitude;

        // Segurança: caiu para fora do mapa
        if (transform.position.y < -200f || MarsInput.Down(MarsKey.Respawn)) Respawn();
    }
}
