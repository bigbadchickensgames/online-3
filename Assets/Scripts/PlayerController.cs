using System;
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

    [Header("Disparo - Giro de Apuntado")]
    public float velocidadGiroDisparo = 720f; // grados/seg. Más alto = giro casi instantáneo, más bajo = se nota el giro

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

    private float velocidadBase; // velocidad normal del jugador, referencia para restaurar tras efectos temporales

    private bool girandoParaDisparar = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.freezeRotation = true;
        rb.useGravity = false;
        gravedadBase = Mathf.Abs(Physics.gravity.y);
        velocidadBase = velocidad;

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

            // No rotamos por movimiento mientras se está ejecutando el giro de apuntado del disparo
            if (direccionMovimiento.sqrMagnitude > 0.001f && !girandoParaDisparar)
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
        velocidad = 1.5f;

        if (rb != null)
        {
            Vector3 direccionResbalon = transform.forward + (Vector3.up * 0.1f);
            rb.AddForce(direccionResbalon * fuerza, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(duracion);

        velocidad = velocidadBase;
    }

    // NUEVO: Gira el cuerpo entero hacia el yaw actual de la cámara (rotacionY) y, al terminar,
    // ejecuta el callback que dispara/ataca. Mientras gira, se bloquea la rotación por movimiento.
    public void GirarHaciaCamaraYDisparar(Action alTerminarGiro)
    {
        if (!photonView.IsMine) return;
        StartCoroutine(RutinaGirarYDisparar(alTerminarGiro));
    }

    private IEnumerator RutinaGirarYDisparar(Action alTerminarGiro)
    {
        girandoParaDisparar = true;
        Quaternion rotacionObjetivo = Quaternion.Euler(0f, rotacionY, 0f);

        while (Quaternion.Angle(rb.rotation, rotacionObjetivo) > 1f)
        {
            Quaternion nuevaRotacion = Quaternion.RotateTowards(rb.rotation, rotacionObjetivo, velocidadGiroDisparo * Time.deltaTime);
            rb.MoveRotation(nuevaRotacion);
            yield return new WaitForFixedUpdate();
        }

        rb.MoveRotation(rotacionObjetivo);
        girandoParaDisparar = false;
        alTerminarGiro?.Invoke();
    }

    // NUEVO: Llamado por el atacante para ralentizar a esta víctima en todos los clientes (efecto "pez congelado")
    public void AplicarRalentizacionRed(float multiplicador, float duracion)
    {
        photonView.RPC(nameof(RPC_AplicarRalentizacion), RpcTarget.All, multiplicador, duracion);
    }

    [PunRPC]
    private void RPC_AplicarRalentizacion(float multiplicador, float duracion)
    {
        StartCoroutine(RutinaRalentizacion(multiplicador, duracion));
    }

    private IEnumerator RutinaRalentizacion(float multiplicador, float duracion)
    {
        velocidad = velocidadBase * multiplicador;

        yield return new WaitForSeconds(duracion);

        velocidad = velocidadBase;
    }
}