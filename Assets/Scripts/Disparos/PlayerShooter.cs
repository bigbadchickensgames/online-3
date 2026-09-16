using System.Collections;
using UnityEngine;
using Photon.Pun;

public class PlayerShooter : MonoBehaviourPun
{
    [Header("Arma Equipada")]
    public DatosArma armaActual;

    [Tooltip("Se asigna automáticamente al equipar el arma buscando 'PuntoDisparo'.")]
    public Transform puntoDeDisparo;

    [Header("Socket del Modelo Visual del Arma")]
    public Transform socketArma;

    [Header("Configuración de Lanzamiento de Trampa")]
    public float fuerzaLanzamientoTrampa = 15f;
    public float anguloElevacionTrampa = 5f;

    [Header("Búmeran")]
    [Tooltip("Segundos tras los cuales se da por perdido un búmeran que no ha vuelto (evita quedarse con el arma bloqueada e invisible).")]
    public float tiempoMaximoVueloBoomerang = 10f;

    private int municionRestante;
    private bool tieneTrampaDisponible;
    private float tiempoUltimoDisparo;

    private bool boomerangEnVuelo = false;
    private float tiempoLanzamientoBoomerang = 0f;

    private PlayerController controladorJugador;

    private GameObject modeloArmaActualInstancia;

    private Coroutine corrutinaMeleeActual = null;
    private Quaternion rotacionAnimacionMelee = Quaternion.identity;

    private void Start()
    {
        controladorJugador = GetComponent<PlayerController>();

        if (armaActual != null)
        {
            EquiparArma(armaActual);
        }
    }

    public void EquiparArma(DatosArma nuevaArma)
    {
        if (nuevaArma == null)
            return;

        if (photonView.IsMine)
        {
            photonView.RPC(
                nameof(RPC_EquiparArmaRed),
                RpcTarget.AllBuffered,
                nuevaArma.name
            );
        }
    }

    [PunRPC]
    private void RPC_EquiparArmaRed(string nombreDatosArma)
    {
        DatosArma datosCargados = Resources.Load<DatosArma>(nombreDatosArma);

        if (datosCargados == null)
        {
            DatosArma[] todas = Resources.LoadAll<DatosArma>("");
            datosCargados = System.Array.Find(
                todas,
                a => a.name == nombreDatosArma
            );
        }

        if (datosCargados == null)
        {
            Debug.LogError(
                $"[PlayerShooter] No se encontró el ScriptableObject '{nombreDatosArma}'."
            );
            return;
        }

        LimpiarModeloArmaLocal();

        armaActual = datosCargados;
        municionRestante = armaActual.municionMaxima;

        boomerangEnVuelo = false;

        tieneTrampaDisponible =
            (
                armaActual.tipoArma == TipoArma.Disparo &&
                armaActual.efectoVacio == TipoEfectoVacio.TirarComoTrampa
            );

        ActualizarModeloArmaLocal(
            armaActual.nombrePrefabModeloArma
        );
    }

    public void DesequiparArma()
    {
        if (!photonView.IsMine)
            return;

        photonView.RPC(
            nameof(RPC_DesequiparArmaRed),
            RpcTarget.AllBuffered
        );
    }

    [PunRPC]
    private void RPC_DesequiparArmaRed()
    {
        armaActual = null;
        municionRestante = 0;
        tieneTrampaDisponible = false;
        boomerangEnVuelo = false;

        LimpiarModeloArmaLocal();

        puntoDeDisparo = null;
    }

    public void LimpiarArmaVisualLocal()
    {
        armaActual = null;
        municionRestante = 0;
        tieneTrampaDisponible = false;
        boomerangEnVuelo = false;

        LimpiarModeloArmaLocal();

        puntoDeDisparo = null;
    }

    private void LimpiarModeloArmaLocal()
    {
        if (corrutinaMeleeActual != null)
        {
            StopCoroutine(corrutinaMeleeActual);
            corrutinaMeleeActual = null;
        }

        rotacionAnimacionMelee = Quaternion.identity;

        if (socketArma != null)
        {
            socketArma.localRotation = Quaternion.identity;
        }

        if (modeloArmaActualInstancia != null)
        {
            Destroy(modeloArmaActualInstancia);
            modeloArmaActualInstancia = null;
        }
    }

    private void ComprobarAgotamientoArma()
    {
        // El búmeran NO se desequipa mientras está volando:
        // esperamos a que vuelva a la mano para gastar la bala
        // y, si era la última, entonces sí se pierde el arma.
        if (
            armaActual != null &&
            armaActual.esBoomerang &&
            boomerangEnVuelo
        )
        {
            return;
        }

        if (municionRestante <= 0 && !tieneTrampaDisponible)
        {
            DesequiparArma();
        }
    }

    private void ActualizarModeloArmaLocal(string nombrePrefabModelo)
    {
        LimpiarModeloArmaLocal();

        puntoDeDisparo = null;

        if (socketArma == null)
        {
            Debug.LogWarning(
                "[PlayerShooter] socketArma no está asignado."
            );
            return;
        }

        if (string.IsNullOrEmpty(nombrePrefabModelo))
        {
            Debug.LogWarning(
                "[PlayerShooter] El nombre del prefab del modelo está vacío."
            );
            return;
        }

        GameObject prefabModelo =
            Resources.Load<GameObject>(nombrePrefabModelo);

        if (prefabModelo == null)
        {
            Debug.LogError(
                $"[PlayerShooter] No se encontró el prefab '{nombrePrefabModelo}'."
            );
            return;
        }

        modeloArmaActualInstancia =
            Instantiate(prefabModelo, socketArma);

        modeloArmaActualInstancia.transform.localPosition =
            prefabModelo.transform.localPosition;

        modeloArmaActualInstancia.transform.localRotation =
            prefabModelo.transform.localRotation;

        modeloArmaActualInstancia.transform.localScale =
            prefabModelo.transform.localScale;

        // --- DESACTIVAR / DESTRUIR COMPONENTES NO DESEADOS EN LA MANO ---
        // Permite reutilizar prefabs de Proyectil/Pickup como armas en la mano sin fallos.

        foreach (
            PickupArma pickup
            in modeloArmaActualInstancia.GetComponentsInChildren<PickupArma>()
        )
        {
            Destroy(pickup);
        }

        foreach (
            Collider col
            in modeloArmaActualInstancia.GetComponentsInChildren<Collider>()
        )
        {
            col.enabled = false;
        }

        foreach (
            Rigidbody rb
            in modeloArmaActualInstancia.GetComponentsInChildren<Rigidbody>()
        )
        {
            Destroy(rb);
        }

        foreach (
            PhotonView pv
            in modeloArmaActualInstancia.GetComponentsInChildren<PhotonView>()
        )
        {
            Destroy(pv);
        }

        // Destruye cualquier script de comportamiento o proyectil adjunto al modelo de la mano
        MonoBehaviour[] scripts = modeloArmaActualInstancia.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null)
            {
                Destroy(script);
            }
        }

        puntoDeDisparo =
            BuscarPuntoDisparoRecursivo(
                modeloArmaActualInstancia.transform
            );
    }

    private Transform BuscarPuntoDisparoRecursivo(Transform raiz)
    {
        if (raiz.name == "PuntoDisparo")
            return raiz;

        foreach (Transform hijo in raiz)
        {
            Transform encontrado =
                BuscarPuntoDisparoRecursivo(hijo);

            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    private void Update()
    {
        if (!photonView.IsMine || armaActual == null)
            return;

        // --- SEGURO ANTI-BLOQUEO DEL BÚMERAN ---
        // Si el proyectil se destruyó por el camino y nunca volvió,
        // lo damos por recuperado para no quedarnos sin arma usable.
        if (
            boomerangEnVuelo &&
            Time.time >= tiempoLanzamientoBoomerang + tiempoMaximoVueloBoomerang
        )
        {
            photonView.RPC(
                nameof(RPC_RecuperarMunicionBoomerang),
                RpcTarget.All
            );
        }

        // Mientras el búmeran está fuera no se puede volver a lanzar.
        if (armaActual.esBoomerang && boomerangEnVuelo)
            return;

        if (
            Input.GetButtonDown("Fire1") &&
            Time.time >= tiempoUltimoDisparo + armaActual.cadenciaDisparo
        )
        {
            tiempoUltimoDisparo = Time.time;

            controladorJugador.GirarHaciaCamaraYDisparar(
                EjecutarAccionDeAtaque
            );
        }
    }

    private void LateUpdate()
    {
        if (socketArma == null)
            return;

        socketArma.localRotation = rotacionAnimacionMelee;
    }

    private void EjecutarAccionDeAtaque()
    {
        if (puntoDeDisparo == null)
        {
            puntoDeDisparo = socketArma;
        }

        if (armaActual.tipoArma == TipoArma.Melee)
        {
            EjecutarAtaqueMelee();
            return;
        }

        if (municionRestante > 0)
        {
            DispararBala();
        }
        else if (tieneTrampaDisponible)
        {
            LanzarTrampaExtra();
        }
    }

    private void DispararBala()
    {
        if (armaActual.esBoomerang)
        {
            // La bala NO se descuenta al lanzar.
            // Se descuenta cuando el búmeran vuelve a la mano.
            photonView.RPC(
                nameof(RPC_BoomerangLanzado),
                RpcTarget.All
            );
        }
        else
        {
            municionRestante--;
        }

        Quaternion rotacionDisparo = puntoDeDisparo.rotation;

        if (Camera.main != null)
        {
            Vector3 puntoObjetivoMira = Camera.main.transform.position + (Camera.main.transform.forward * 100f);

            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 100f))
            {
                puntoObjetivoMira = hit.point;
            }

            rotacionDisparo = Quaternion.LookRotation(puntoObjetivoMira - puntoDeDisparo.position);
        }

        // === DATOS DE LANZAMIENTO ===
        // El 'true' marca el objeto como PROYECTIL REALMENTE DISPARADO.
        // Los pickups del suelo nacen sin esta marca, así que se quedan quietos.
        object[] datosLanzamiento =
        {
            true,
            photonView.Owner.ActorNumber
        };

        GameObject bala = PhotonNetwork.Instantiate(
            armaActual.nombrePrefabProyectilNet,
            puntoDeDisparo.position,
            rotacionDisparo,
            0,
            datosLanzamiento
        );

        ProyectilBasico proyectil = bala.GetComponent<ProyectilBasico>();

        if (proyectil != null)
        {
            proyectil.duenoActorNumber = photonView.Owner.ActorNumber;

            proyectil.velocidad = armaActual.velocidadProyectil;

            // Conversión explícita float -> int
            proyectil.dano = Mathf.RoundToInt(armaActual.dano);

            proyectil.titularMuerte = armaActual.titularMuerte;
            proyectil.puntosPorBaja = armaActual.puntosPorBaja;
        }

        ComprobarAgotamientoArma();
    }

    private void LanzarTrampaExtra()
    {
        if (
            string.IsNullOrEmpty(
                armaActual.nombrePrefabTrampaNet
            )
        )
        {
            return;
        }

        tieneTrampaDisponible = false;

        Vector3 direccionLanzamiento =
            puntoDeDisparo.forward;

        direccionLanzamiento.y +=
            anguloElevacionTrampa * 0.05f;

        direccionLanzamiento.Normalize();

        Vector3 velocidadImpulso =
            direccionLanzamiento *
            fuerzaLanzamientoTrampa;

        object[] datosInstanciacion =
        {
            velocidadImpulso
        };

        PhotonNetwork.Instantiate(
            armaActual.nombrePrefabTrampaNet,
            puntoDeDisparo.position,
            Quaternion.LookRotation(
                direccionLanzamiento
            ),
            0,
            datosInstanciacion
        );

        ComprobarAgotamientoArma();
    }

    private void EjecutarAtaqueMelee()
    {
        photonView.RPC(
            nameof(RPC_ReproducirAnimacionMelee),
            RpcTarget.All
        );

        Vector3 centroGolpe =
            puntoDeDisparo.position +
            puntoDeDisparo.forward *
            armaActual.rangoMelee;

        float radioUsado = armaActual.puedeDevolverProyectiles ? armaActual.radioDesvio : armaActual.radioMelee;

        Collider[] impactados =
            Physics.OverlapSphere(
                centroGolpe,
                radioUsado,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

        foreach (Collider col in impactados)
        {
            // --- INTERCEPCIÓN Y DEVOLUCIÓN DE PROYECTILES ---
            if (armaActual.puedeDevolverProyectiles)
            {
                ProyectilBasico proyectilEnemigo = col.GetComponentInParent<ProyectilBasico>();
                if (proyectilEnemigo != null)
                {
                    PhotonView pvProyectil = proyectilEnemigo.GetComponent<PhotonView>();
                    if (pvProyectil != null)
                    {
                        // 1. Punto objetivo exacto adonde apunta la retícula/cámara
                        Vector3 puntoObjetivoMira = puntoDeDisparo.position + (puntoDeDisparo.forward * 100f);

                        if (Camera.main != null)
                        {
                            puntoObjetivoMira = Camera.main.transform.position + (Camera.main.transform.forward * 100f);

                            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 100f))
                            {
                                puntoObjetivoMira = hit.point;
                            }
                        }

                        // 2. Dirección desde la posición ACTUAL de la bala hacia la retícula
                        Vector3 direccionRebote = (puntoObjetivoMira - proyectilEnemigo.transform.position).normalized;

                        proyectilEnemigo.photonView.RPC("RedirigirProyectil", RpcTarget.All, direccionRebote, photonView.Owner.ActorNumber);
                        continue;
                    }
                }
            }

            // --- PROCESO HABITUAL DE DAÑO Y MELEE ---
            PhotonView pvImpactado =
                col.GetComponentInParent<PhotonView>();

            if (
                pvImpactado == null ||
                pvImpactado.Owner == photonView.Owner
            )
            {
                continue;
            }

            PlayerHealth saludEnemigo =
                col.GetComponentInParent<PlayerHealth>();

            if (saludEnemigo != null)
            {
                saludEnemigo.photonView.RPC(
                    "RecibirDano",
                    RpcTarget.All,
                    Mathf.RoundToInt(armaActual.danoMelee), // Conversión explícita float -> int
                    photonView.Owner.ActorNumber,
                    armaActual.titularMuerte,
                    armaActual.puntosPorBaja
                );
            }

            PlayerController controladorEnemigo =
                col.GetComponentInParent<PlayerController>();

            if (controladorEnemigo != null)
            {
                controladorEnemigo.AplicarRalentizacionRed(
                    armaActual.ralentizacionMultiplicador,
                    armaActual.ralentizacionDuracion
                );
            }
        }
    }

    [PunRPC]
    private void RPC_ReproducirAnimacionMelee()
    {
        if (armaActual == null || socketArma == null)
            return;

        if (corrutinaMeleeActual != null)
        {
            StopCoroutine(corrutinaMeleeActual);
        }

        corrutinaMeleeActual =
            StartCoroutine(
                RutinaAnimacionMelee(armaActual)
            );
    }

    private IEnumerator RutinaAnimacionMelee(
        DatosArma datosDelGolpe
    )
    {
        Quaternion rotacionOriginal =
            Quaternion.identity;

        Quaternion rotacionDelGolpe =
            Quaternion.Euler(
                datosDelGolpe.anguloGolpeMelee
            );

        rotacionAnimacionMelee =
            rotacionOriginal;

        float t = 0f;

        while (
            t <
            datosDelGolpe.duracionIdaGolpeMelee
        )
        {
            t += Time.deltaTime;

            float porcentaje =
                t /
                datosDelGolpe.duracionIdaGolpeMelee;

            rotacionAnimacionMelee =
                Quaternion.Slerp(
                    rotacionOriginal,
                    rotacionDelGolpe,
                    porcentaje
                );

            yield return null;
        }

        t = 0f;

        while (
            t <
            datosDelGolpe.duracionVueltaGolpeMelee
        )
        {
            t += Time.deltaTime;

            float porcentaje =
                t /
                datosDelGolpe.duracionVueltaGolpeMelee;

            rotacionAnimacionMelee =
                Quaternion.Slerp(
                    rotacionDelGolpe,
                    rotacionOriginal,
                    porcentaje
                );

            yield return null;
        }

        rotacionAnimacionMelee =
            rotacionOriginal;

        corrutinaMeleeActual = null;
    }

    // ------------------------------------------------------------------
    // CICLO DEL BÚMERAN
    //
    //   Lanzar  -> RPC_BoomerangLanzado
    //              (se oculta en la mano de TODOS los clientes)
    //   Volver  -> RPC_RecuperarMunicionBoomerang
    //              (reaparece, se gasta 1 bala y, si era la última,
    //               el arma se pierde igual que el resto)
    // ------------------------------------------------------------------

    [PunRPC]
    private void RPC_BoomerangLanzado()
    {
        boomerangEnVuelo = true;
        tiempoLanzamientoBoomerang = Time.time;

        if (modeloArmaActualInstancia != null)
        {
            modeloArmaActualInstancia.SetActive(false);
        }
    }

    [PunRPC]
    public void RPC_RecuperarMunicionBoomerang()
    {
        if (armaActual == null || !armaActual.esBoomerang)
            return;

        // Si ya se procesó el regreso, ignoramos avisos duplicados
        // (por ejemplo, el aviso del Master y el seguro por tiempo).
        if (!boomerangEnVuelo)
            return;

        boomerangEnVuelo = false;

        // Ahora sí se gasta la bala: al volver a la mano.
        municionRestante--;

        if (modeloArmaActualInstancia != null)
        {
            modeloArmaActualInstancia.SetActive(true);
        }

        ComprobarAgotamientoArma();
    }

    private void OnDrawGizmos()
    {
        if (
            armaActual == null ||
            armaActual.tipoArma != TipoArma.Melee ||
            puntoDeDisparo == null
        )
        {
            return;
        }

        Gizmos.color = Color.red;

        float radioGizmo = armaActual.puedeDevolverProyectiles ? armaActual.radioDesvio : armaActual.radioMelee;

        Vector3 centroGolpe =
            puntoDeDisparo.position +
            puntoDeDisparo.forward *
            armaActual.rangoMelee;

        Gizmos.DrawWireSphere(
            centroGolpe,
            radioGizmo
        );

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(
            puntoDeDisparo.position,
            centroGolpe
        );
    }
}