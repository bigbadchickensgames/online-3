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

    private int municionRestante;
    private bool tieneTrampaDisponible;
    private float tiempoUltimoDisparo;

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

        LimpiarModeloArmaLocal();

        puntoDeDisparo = null;
    }

    public void LimpiarArmaVisualLocal()
    {
        armaActual = null;
        municionRestante = 0;
        tieneTrampaDisponible = false;

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

        foreach (
            PickupArma pickup
            in modeloArmaActualInstancia.GetComponentsInChildren<PickupArma>()
        )
        {
            pickup.enabled = false;
            Destroy(pickup);
        }

        foreach (
            Collider col
            in modeloArmaActualInstancia.GetComponentsInChildren<Collider>()
        )
        {
            col.enabled = false;
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
            Debug.LogWarning(
                "No se puede atacar: falta 'PuntoDisparo'."
            );
            return;
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
        municionRestante--;

        Quaternion rotacionDisparo =
            puntoDeDisparo.rotation;

        if (Camera.main != null)
        {
            Vector3 puntoObjetivoMira =
                Camera.main.transform.position +
                (Camera.main.transform.forward * 100f);

            if (
                Physics.Raycast(
                    Camera.main.transform.position,
                    Camera.main.transform.forward,
                    out RaycastHit hit,
                    100f
                )
            )
            {
                puntoObjetivoMira = hit.point;
            }

            rotacionDisparo =
                Quaternion.LookRotation(
                    puntoObjetivoMira -
                    puntoDeDisparo.position
                );
        }

        GameObject bala =
            PhotonNetwork.Instantiate(
                armaActual.nombrePrefabProyectilNet,
                puntoDeDisparo.position,
                rotacionDisparo
            );

        ProyectilBasico proyectil =
            bala.GetComponent<ProyectilBasico>();

        if (proyectil != null)
        {
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

        Debug.Log($"<color=yellow>[MELEE DEBUGS] Ejecutando golpe. Arma: {armaActual.name} | Puede devolver balas: {armaActual.puedeDevolverProyectiles} | Centro del golpe: {centroGolpe} | Radio usado: {radioUsado}</color>");

        Collider[] impactados =
            Physics.OverlapSphere(
                centroGolpe,
                radioUsado,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

        Debug.Log($"<color=yellow>[MELEE DEBUGS] Objetos totales encontrados en la esfera: {impactados.Length}</color>");

        foreach (Collider col in impactados)
        {
            Debug.Log($"<color=orange>[MELEE DEBUGS] Objeto detectado: {col.gameObject.name} (Parent: {(col.transform.parent != null ? col.transform.parent.name : "Ninguno")})</color>");

            // --- INTERCEPCIÓN Y DEVOLUCIÓN DE PROYECTILES ---
            if (armaActual.puedeDevolverProyectiles)
            {
                ProyectilBasico proyectilEnemigo = col.GetComponentInParent<ProyectilBasico>();
                if (proyectilEnemigo != null)
                {
                    Debug.Log("<color=cyan>[MELEE DEBUGS] ¡Se encontró un componente ProyectilBasico en el objeto impactado!</color>");

                    PhotonView pvProyectil = proyectilEnemigo.GetComponent<PhotonView>();
                    if (pvProyectil != null)
                    {
                        Debug.Log($"<color=cyan>[MELEE DEBUGS] Dueño actual de la bala: {pvProyectil.Owner?.NickName ?? "Desconocido"} (ActorNumber: {pvProyectil.Owner?.ActorNumber}) | Mi ActorNumber: {photonView.Owner.ActorNumber}</color>");

                        // 1. Obtener el punto objetivo exacto adonde apunta la retícula/cámara
                        Vector3 puntoObjetivoMira = puntoDeDisparo.position + (puntoDeDisparo.forward * 100f);

                        if (Camera.main != null)
                        {
                            puntoObjetivoMira = Camera.main.transform.position + (Camera.main.transform.forward * 100f);

                            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, 100f))
                            {
                                puntoObjetivoMira = hit.point;
                            }
                        }

                        // 2. Vector dirección desde la posición ACTUAL de la bala hacia la retícula
                        Vector3 direccionRebote = (puntoObjetivoMira - proyectilEnemigo.transform.position).normalized;

                        Debug.Log("<color=green>¡ÉXITO! Redirigiendo bala exactamente hacia la retícula de disparo.</color>");
                        proyectilEnemigo.photonView.RPC("RedirigirProyectil", RpcTarget.All, direccionRebote, photonView.Owner.ActorNumber);
                        continue;
                    }
                    else
                    {
                        Debug.LogError("<color=red>[MELEE DEBUGS] El ProyectilBasico NO tiene PhotonView asignado.</color>");
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