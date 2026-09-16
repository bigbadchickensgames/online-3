using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ProyectilBoomerang : ProyectilBasico
{
    [Header("Configuración Búmeran (Estilo Ahri Q)")]
    [Tooltip("Distancia máxima en metros que viaja en línea recta antes de volver")]
    public float rangoMaximo = 16f;

    [Tooltip("Velocidad de giro sobre su propio eje")]
    public float velocidadRotacionVisual = 720f;

    [Tooltip("Asigna aquí el objeto 3D hijo que contiene la malla del Búmeran")]
    public Transform modeloVisual;

    // ------------------------------------------------------------------
    // El rol (pickup estático del suelo  vs  proyectil en vuelo) lo decide
    // SIEMPRE el código al nacer, leyendo los InstantiationData de Photon.
    // Por eso NO se serializa: el Inspector ya no puede dejarlo en 'true'
    // por error y provocar que los pickups salgan volando hacia el Host.
    // ------------------------------------------------------------------
    [System.NonSerialized] public bool esProyectilActivo = false;

    private Vector3 posicionInicial;
    private Transform duenoTransform;
    private PlayerShooter shooterDueno;

    private HashSet<Transform> objetivosGolpeadosEnEstaFase = new HashSet<Transform>();

    private enum EstadoBoomerang { Yendo, Volviendo }
    private EstadoBoomerang estadoActual = EstadoBoomerang.Yendo;

    protected override void Start()
    {
        // 1. Decidimos el rol ANTES de que la clase base aplique velocidad,
        //    gravedad o tiempo de vida. Este es el punto clave del arreglo.
        esProyectilActivo = LeerDatosDeLanzamiento();

        if (!esProyectilActivo)
        {
            // Es un arma tirada en el suelo (o colocada a mano en la escena):
            // se queda completamente inmóvil e inerte.
            ConfigurarComoPickupEstatico();
            return; // OJO: nunca llamamos a base.Start() en un pickup.
        }

        // 2. A partir de aquí sí es un proyectil realmente disparado.
        base.Start();

        // Un búmeran en vuelo no debe poder recogerse como arma del suelo.
        PickupArma pickupEnVuelo = GetComponent<PickupArma>();
        if (pickupEnVuelo != null)
        {
            Destroy(pickupEnVuelo);
        }

        posicionInicial = transform.position;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
        }

        BuscarTransformDueno();
    }

    /// <summary>
    /// Lee los InstantiationData que envía PlayerShooter al disparar.
    /// Devuelve true solo si el objeto fue lanzado por un jugador.
    /// De paso recoge el ActorNumber del dueño de forma determinista.
    /// </summary>
    private bool LeerDatosDeLanzamiento()
    {
        if (photonView == null)
            return false;

        object[] data = photonView.InstantiationData;

        if (data == null || data.Length == 0)
            return false;

        bool fueLanzado = false;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] is bool marcaDeLanzamiento)
            {
                if (marcaDeLanzamiento)
                    fueLanzado = true;
            }
            else if (data[i] is int actorNumber && actorNumber > 0)
            {
                duenoActorNumber = actorNumber;
            }
        }

        return fueLanzado;
    }

    /// <summary>
    /// Deja el objeto totalmente quieto. Al deshabilitar el componente,
    /// Unity deja de enviarle Update() y OnTriggerEnter(), así que este
    /// script no puede interferir con el PickupArma del mismo objeto.
    /// </summary>
    private void ConfigurarComoPickupEstatico()
    {
        // 'rb' puede no estar todavía asignado porque no ejecutamos base.Start().
        Rigidbody cuerpo = rb != null ? rb : GetComponent<Rigidbody>();

        if (cuerpo != null)
        {
            cuerpo.velocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            cuerpo.useGravity = false;
            cuerpo.isKinematic = true;
        }

        enabled = false;
    }

    private void BuscarTransformDueno()
    {
        if (duenoActorNumber == -1 && photonView != null && photonView.Owner != null)
        {
            duenoActorNumber = photonView.Owner.ActorNumber;
        }

        if (duenoActorNumber <= 0) return;

        PlayerController[] jugadores = FindObjectsOfType<PlayerController>();
        foreach (var jug in jugadores)
        {
            if (jug.photonView != null && jug.photonView.Owner != null && jug.photonView.Owner.ActorNumber == duenoActorNumber)
            {
                duenoTransform = jug.transform;
                shooterDueno = jug.GetComponent<PlayerShooter>();
                break;
            }
        }
    }

    protected override void Update()
    {
        if (!esProyectilActivo) return;

        RotarModeloVisual();

        if (estadoActual == EstadoBoomerang.Yendo)
        {
            ManejadorFaseYendo();
        }
        else if (estadoActual == EstadoBoomerang.Volviendo)
        {
            ManejadorFaseVolviendo();
        }
    }

    private void RotarModeloVisual()
    {
        if (modeloVisual != null)
        {
            modeloVisual.Rotate(Vector3.forward, velocidadRotacionVisual * Time.deltaTime, Space.Self);
        }
    }

    private void ManejadorFaseYendo()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.velocity = transform.forward * velocidad;
        }
        else
        {
            transform.position += transform.forward * velocidad * Time.deltaTime;
        }

        if (Vector3.Distance(posicionInicial, transform.position) >= rangoMaximo)
        {
            CambiarAFaseVolviendo();
        }
    }

    private void ManejadorFaseVolviendo()
    {
        if (duenoTransform == null)
        {
            BuscarTransformDueno();

            if (duenoTransform == null)
            {
                if (rb != null && !rb.isKinematic) rb.velocity = transform.forward * velocidad;
                else transform.position += transform.forward * velocidad * Time.deltaTime;
                return;
            }
        }

        Vector3 centroDueno = duenoTransform.position + Vector3.up * 1f;
        Vector3 direccionHaciaDueno = (centroDueno - transform.position).normalized;

        if (direccionHaciaDueno != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direccionHaciaDueno);
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.velocity = direccionHaciaDueno * velocidad;
        }
        else
        {
            transform.position += direccionHaciaDueno * velocidad * Time.deltaTime;
        }

        float distanciaAlJugador = Vector3.Distance(transform.position, centroDueno);
        if (distanciaAlJugador <= 1.2f)
        {
            AvisarRecuperacionMunicion();
            DestruirProyectil();
        }
    }

    private void AvisarRecuperacionMunicion()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (shooterDueno == null)
        {
            BuscarTransformDueno();
        }

        if (shooterDueno != null && shooterDueno.photonView != null)
        {
            shooterDueno.photonView.RPC(nameof(PlayerShooter.RPC_RecuperarMunicionBoomerang), RpcTarget.All);
        }
    }

    private void CambiarAFaseVolviendo()
    {
        estadoActual = EstadoBoomerang.Volviendo;
        objetivosGolpeadosEnEstaFase.Clear();
    }

    [PunRPC]
    public override void RedirigirProyectil(Vector3 nuevaDireccion, int nuevoDuenoActorNumber)
    {
        // Si alguien golpea el búmeran, sigue siendo un proyectil activo.
        if (!esProyectilActivo)
            return;

        duenoActorNumber = nuevoDuenoActorNumber;
        duenoTransform = null;
        shooterDueno = null;
        BuscarTransformDueno();

        posicionInicial = transform.position;
        estadoActual = EstadoBoomerang.Yendo;
        objetivosGolpeadosEnEstaFase.Clear();

        velocidad *= 1.25f;

        if (nuevaDireccion != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(nuevaDireccion);
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.velocity = nuevaDireccion.normalized * velocidad;
        }

        Debug.Log($"<color=cyan>[BÚMERAN] Redirigido y potenciado por el jugador: {nuevoDuenoActorNumber}</color>");
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (!esProyectilActivo) return;

        if (!PhotonNetwork.IsMasterClient && !photonView.IsMine)
            return;

        PhotonView pvImpactado = other.GetComponentInParent<PhotonView>();

        // Ignorar daño al dueño actual del proyectil
        if (pvImpactado != null && pvImpactado.Owner != null && pvImpactado.Owner.ActorNumber == duenoActorNumber)
        {
            return;
        }

        Transform raizObjetivo = other.transform.root;

        if (objetivosGolpeadosEnEstaFase.Contains(raizObjetivo))
        {
            return;
        }

        PlayerHealth salud = other.GetComponentInParent<PlayerHealth>();
        if (salud != null)
        {
            salud.photonView.RPC("RecibirDano", RpcTarget.All, Mathf.RoundToInt(dano), duenoActorNumber, titularMuerte, puntosPorBaja);
            objetivosGolpeadosEnEstaFase.Add(raizObjetivo);
        }
    }
}