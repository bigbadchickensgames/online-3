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

    [Tooltip("Tiempo límite de vuelo seguro antes de autodestruirse y devolver la munición")]
    public float tiempoMaximoVuelo = 8f;

    [Tooltip("Asigna aquí el objeto 3D hijo que contiene la malla del Búmeran")]
    public Transform modeloVisual;

    // ------------------------------------------------------------------
    // El rol (pickup estático del suelo vs proyectil en vuelo) lo decide
    // SIEMPRE el código al nacer, leyendo los InstantiationData de Photon.
    // ------------------------------------------------------------------
    [System.NonSerialized] public bool esProyectilActivo = false;

    private Vector3 posicionInicial;
    private Transform duenoTransform;
    private PlayerShooter shooterDueno;
    private float tiempoInicioVuelo;
    private TrailRenderer estela; // Referencia para la estela visual

    private HashSet<Transform> objetivosGolpeadosEnEstaFase = new HashSet<Transform>();

    private enum EstadoBoomerang { Yendo, Volviendo }
    private EstadoBoomerang estadoActual = EstadoBoomerang.Yendo;

    protected override void Start()
    {
        // ------------------------------------------------------------------
        // FIX ESTELA EN ZIGZAG / "ESTRELLA":
        // Este script mueve y rota el Transform manualmente cada frame en
        // TODOS los clientes (Update() no filtra por IsMine). Si además hay
        // un PhotonTransformView en el mismo objeto, ese componente intenta
        // sincronizar/interpolar el mismo Transform por red en los clientes
        // que no son el dueño, y ambos sistemas compiten por escribir la
        // misma posición/rotación cada frame. Ese "tira y afloja" es lo que
        // se ve como una estela en zigzag. Lo neutralizamos por código para
        // no depender de tocar nada en el Editor.
        // ------------------------------------------------------------------
        NeutralizarSincronizacionDeTransformPorRed();

        // Buscamos el TrailRenderer (esté en la raíz o en un objeto hijo)
        estela = GetComponentInChildren<TrailRenderer>();

        // 1. Decidimos el rol leyendo datos de Photon
        esProyectilActivo = LeerDatosDeLanzamiento();

        if (!esProyectilActivo)
        {
            // Pickup estático en el suelo: apagamos la estela para que no dibuje nada
            if (estela != null) estela.enabled = false;
            
            ConfigurarComoPickupEstatico();
            return; 
        }

        // Si es un proyectil activo, aseguramos que la estela esté encendida y la limpiamos
        // para evitar el bug visual del "tirón" desde el punto 0,0,0
        if (estela != null)
        {
            estela.enabled = true;
            estela.Clear();

            // Forzamos por código los valores correctos del emisor de la
            // estela, aunque en el Editor haya quedado algún desfase de
            // posición/rotación local o el Alignment mal puesto:
            //  - Sin desfase local: si el objeto de la estela tuviera una
            //    posición local distinta de (0,0,0), cualquier rotación del
            //    padre lo haría orbitar en un círculo y generaría el mismo
            //    efecto de zigzag.
            //  - Alignment = View: la cinta siempre mira a cámara y su forma
            //    depende solo del historial de posiciones, nunca de cómo
            //    esté rotado el objeto que la emite.
            estela.transform.localPosition = Vector3.zero;
            estela.transform.localRotation = Quaternion.identity;
            estela.alignment = LineAlignment.View;
        }

        // 2. NO llamamos a base.Start() para evitar que Destroy(gameObject, tiempoVida)
        // borre el búmeran localmente y desincronice la mano del jugador.
        if (photonView != null && photonView.Owner != null && duenoActorNumber <= 0)
        {
            duenoActorNumber = photonView.Owner.ActorNumber;
        }

        // Un búmeran en vuelo no debe poder recogerse como arma del suelo
        PickupArma pickupEnVuelo = GetComponent<PickupArma>();
        if (pickupEnVuelo != null)
        {
            Destroy(pickupEnVuelo);
        }

        posicionInicial = transform.position;
        tiempoInicioVuelo = Time.time;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
        }

        BuscarTransformDueno();
    }

    /// <summary>
    /// Si el objeto tiene un PhotonTransformView (u otro componente que
    /// sincronice el Transform por red), lo desactiva y lo quita de los
    /// Observed Components del PhotonView. Este proyectil ya calcula su
    /// movimiento de forma determinista en todos los clientes, así que
    /// sincronizarlo también por red es redundante y provoca conflictos
    /// visibles (estela en zigzag, saltos de posición/rotación).
    /// </summary>
    private void NeutralizarSincronizacionDeTransformPorRed()
    {
        if (photonView == null)
            return;

        PhotonTransformView vistaTransformRed = GetComponent<PhotonTransformView>();

        if (vistaTransformRed == null)
            return;

        vistaTransformRed.enabled = false;

        if (photonView.ObservedComponents != null)
        {
            photonView.ObservedComponents.Remove(vistaTransformRed);
        }

        Debug.LogWarning("[ProyectilBoomerang] Se detectó y neutralizó un PhotonTransformView " +
            "en este objeto: competía con el movimiento manual del script y causaba la estela " +
            "en zigzag. Puedes eliminar el componente del prefab con tranquilidad si quieres " +
            "dejarlo limpio también en el Editor.");
    }

    /// <summary>
    /// Lee los InstantiationData que envía PlayerShooter al disparar.
    /// Devuelve true solo si el objeto fue lanzado por un jugador.
    /// </summary>
    private bool LeerDatosDeLanzamiento()
    {
        if (photonView == null)
            return false;

        object[] data = photonView.InstantiationData;

        if (data == null || data.Length == 0)
            return false;

        bool fueLanzado = false;

        if (data[0] is bool marcaDeLanzamiento)
        {
            fueLanzado = marcaDeLanzamiento;
        }

        // Leemos los datos en el orden exacto que los enviamos desde PlayerShooter
        if (fueLanzado && data.Length >= 6)
        {
            duenoActorNumber = (int)data[1];
            velocidad = (float)data[2];
            dano = (int)(float)data[3];
            titularMuerte = (string)data[4];
            puntosPorBaja = (int)data[5];
        }
        else if (fueLanzado)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] is int actorNumber && actorNumber > 0)
                {
                    duenoActorNumber = actorNumber;
                }
            }
        }

        return fueLanzado;
    }

    private void ConfigurarComoPickupEstatico()
    {
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

        // Red de seguridad: si supera el tiempo máximo de vuelo sin volver,
        // se destruye sincronizadamente por red y recupera la munición.
        if (Time.time - tiempoInicioVuelo >= tiempoMaximoVuelo)
        {
            AvisarRecuperacionMunicion();
            DestruirProyectil();
            return;
        }

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
        if (PhotonNetwork.LocalPlayer.ActorNumber != duenoActorNumber)
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
        if (!esProyectilActivo)
            return;

        duenoActorNumber = nuevoDuenoActorNumber;
        duenoTransform = null;
        shooterDueno = null;
        BuscarTransformDueno();

        posicionInicial = transform.position;
        tiempoInicioVuelo = Time.time; 
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
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (!esProyectilActivo) return;

        if (PhotonNetwork.LocalPlayer.ActorNumber != duenoActorNumber)
            return;

        PhotonView pvImpactado = other.GetComponentInParent<PhotonView>();

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