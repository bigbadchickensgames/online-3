using System.Collections;
using UnityEngine;
using Photon.Pun;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviourPunCallbacks
{
    [Header("Movimiento")]
    public float velocidad = 5f;
    public float velocidadRotacion = 15f;
    public float alturaSalto = 2f;
    public float multiplicadorCaida = 2.5f;
    public float multiplicadorSubida = 1f;
    public float distanciaCheckSuelo = 0.3f;
    public LayerMask capaSuelo;

    [Header("Configuración de Cámara y Ratón")]
    public Transform cameraTarget;
    public float sensibilidadRaton = 2f;
    public float limiteVerticalMin = -30f;
    public float limiteVerticalMax = 70f;

    public bool EstaVivo { get; private set; } = true;

    private Rigidbody rb;
    private Collider col;
    private bool enSuelo;
    private Vector3 direccionInput;
    private bool saltoPresionado;
    private float gravedadBase;
    private Transform camaraTransform;

    private float rotacionX;
    private float rotacionY;

    private bool estaResbalando = false;
    private float velocidadGuardadaOriginal;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.freezeRotation = true;
        rb.useGravity = false;
        gravedadBase = Mathf.Abs(Physics.gravity.y);

        if (photonView.IsMine)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (Camera.main != null)
            {
                camaraTransform = Camera.main.transform;
            }

            if (cameraTarget != null)
            {
                rotacionY = cameraTarget.eulerAngles.y;
                rotacionX = cameraTarget.eulerAngles.x;
            }

            StartCoroutine(VincularCamaraConRetraso());
        }
    }

    private IEnumerator VincularCamaraConRetraso()
    {
        yield return null;

        CinemachineCamera vcam = FindFirstObjectByType<CinemachineCamera>();

        if (vcam != null)
        {
            Transform target = cameraTarget != null ? cameraTarget : transform;
            vcam.Target.TrackingTarget = target;
            vcam.Target.LookAtTarget = target;
        }
    }

    void Update()
    {
        if (!photonView.IsMine || !EstaVivo) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        direccionInput = new Vector3(h, 0, v).normalized;

        if (Input.GetButtonDown("Jump"))
            saltoPresionado = true;

        float mouseX = Input.GetAxis("Mouse X") * sensibilidadRaton;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadRaton;

        rotacionY += mouseX;
        rotacionX -= mouseY;
        rotacionX = Mathf.Clamp(rotacionX, limiteVerticalMin, limiteVerticalMax);

        if (cameraTarget != null)
        {
            cameraTarget.rotation = Quaternion.Euler(rotacionX, rotacionY, 0f);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine || !EstaVivo) return;

        enSuelo = Physics.CheckSphere(
            transform.position - new Vector3(0, col.bounds.extents.y, 0),
            distanciaCheckSuelo,
            capaSuelo
        );

        Vector3 direccionMovimiento = Vector3.zero;

        if (camaraTransform != null && direccionInput.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = camaraTransform.forward;
            Vector3 camRight = camaraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;

            if (camForward.sqrMagnitude > 0.001f) camForward.Normalize();
            if (camRight.sqrMagnitude > 0.001f) camRight.Normalize();

            direccionMovimiento = (camForward * direccionInput.z + camRight * direccionInput.x).normalized;

            if (direccionMovimiento.sqrMagnitude > 0.001f)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccionMovimiento);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, rotacionObjetivo, velocidadRotacion * Time.fixedDeltaTime));
            }
        }

        Vector3 movimiento = direccionMovimiento * velocidad;
        Vector3 velocidadActual = rb.linearVelocity;

        if (saltoPresionado && enSuelo)
        {
            float velocidadSalto = Mathf.Sqrt(2f * gravedadBase * multiplicadorSubida * alturaSalto);
            velocidadActual.y = velocidadSalto;
        }
        saltoPresionado = false;

        float multiplicador = velocidadActual.y < 0 ? multiplicadorCaida : multiplicadorSubida;
        velocidadActual.y += -gravedadBase * multiplicador * Time.fixedDeltaTime;

        rb.linearVelocity = new Vector3(movimiento.x, velocidadActual.y, movimiento.z);
    }

    public void Morir()
    {
        if (!photonView.IsMine || !EstaVivo) return;

        EstaVivo = false;
        rb.isKinematic = true;
        col.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) r.enabled = false;

        Luncher.Instancia.NotificarMuerteLocal(this);
    }

    public void EnfocarCamaraEspectador()
    {
        CinemachineCamera vcam = FindFirstObjectByType<CinemachineCamera>();
        if (vcam != null && cameraTarget != null)
        {
            vcam.Target.TrackingTarget = cameraTarget;
            vcam.Target.LookAtTarget = cameraTarget;
        }
    }

    public void AplicarResbalon(float fuerzaEmpuje, float duracion)
    {
        StartCoroutine(RutinaResbalon(fuerzaEmpuje, duracion));
    }

    private IEnumerator RutinaResbalon(float fuerza, float duracion)
    {
        if (!estaResbalando)
        {
            velocidadGuardadaOriginal = velocidad;
            estaResbalando = true;
        }

        velocidad = 1.5f;

        if (rb != null)
        {
            Vector3 direccionResbalon = transform.forward + (Vector3.up * 0.1f);
            rb.AddForce(direccionResbalon * fuerza, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(duracion);

        velocidad = velocidadGuardadaOriginal;
        estaResbalando = false;
    }
}