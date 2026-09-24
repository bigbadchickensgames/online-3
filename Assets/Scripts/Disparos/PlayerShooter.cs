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

    [Header("Apuntado y Origen del Disparo")]
    [Tooltip("Capas que el raycast de apuntado y el de origen libre ignoran (p. ej. Pickup). El propio jugador se ignora siempre por jerarquía; los rivales siguen siendo apuntables.")]
    public LayerMask capasIgnoradasApuntado;

    [Tooltip("Distancia máxima del raycast de apuntado desde la cámara.")]
    public float distanciaMaximaApuntado = 100f;

    [Tooltip("Separación (m) respecto a la superficie cuando PuntoDisparo queda dentro de la geometría.")]
    public float margenSpawnBala = 0.2f;

    [Tooltip("Si la dirección hacia la mira se desvía más de estos grados del forward de la cámara, se dispara recto según camera.forward.")]
    public float anguloMaximoCorreccion = 45f;

    private int municionRestante;
    private bool tieneTrampaDisponible;
    private float tiempoUltimoDisparo;

    private bool boomerangEnVuelo = false;
    private float tiempoLanzamientoBoomerang = 0f;

    private PlayerController controladorJugador;
    private Collider colliderJugador;

    private GameObject modeloArmaActualInstancia;

    private Coroutine corrutinaMeleeActual = null;
    private Quaternion rotacionAnimacionMelee = Quaternion.identity;

    private int MascaraApuntado
    {
        get { return Physics.DefaultRaycastLayers & ~capasIgnoradasApuntado.value; }
    }

    private void Start()
    {
        controladorJugador = GetComponent<PlayerController>();
        colliderJugador = GetComponent<Collider>();

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

    private bool BuscarImpactoMasCercano(
        Vector3 origen,
        Vector3 direccion,
        float distancia,
        out RaycastHit mejorImpacto
    )
    {
        mejorImpacto = default;

        RaycastHit[] impactos = Physics.RaycastAll(
            origen,
            direccion,
            distancia,
            MascaraApuntado,
            QueryTriggerInteraction.Ignore
        );

        float mejorDistancia = float.MaxValue;
        bool hayImpacto = false;

        for (int i = 0; i < impactos.Length; i++)
        {
            Collider c = impactos[i].collider;

            if (c == null || c.transform.IsChildOf(transform))
                continue;

            if (impactos[i].distance < mejorDistancia)
            {
                mejorDistancia = impactos[i].distance;
                mejorImpacto = impactos[i];
                hayImpacto = true;
            }
        }

        return hayImpacto;
    }

    private Vector3 ObtenerPuntoDeMira()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            return puntoDeDisparo.position +
                   puntoDeDisparo.forward * distanciaMaximaApuntado;
        }

        Transform tCam = cam.transform;

        float profundidadJugador =
            Vector3.Dot(transform.position - tCam.position, tCam.forward);

        Vector3 origen =
            tCam.position + tCam.forward * Mathf.Max(0f, profundidadJugador);

        if (
            BuscarImpactoMasCercano(
                origen,
                tCam.forward,
                distanciaMaximaApuntado,
                out RaycastHit impacto
            )
        )
        {
            return impacto.point;
        }

        return origen + tCam.forward * distanciaMaximaApuntado;
    }

    private Vector3 ObtenerOrigenLibreDeDisparo()
    {
        Vector3 origenDeseado = puntoDeDisparo.position;

        Vector3 centroCuerpo =
            colliderJugador != null
                ? colliderJugador.bounds.center
                : transform.position + Vector3.up;

        Vector3 haciaPunto = origenDeseado - centroCuerpo;
        float distancia = haciaPunto.magnitude;

        if (distancia < 0.001f)
            return origenDeseado;

        if (
            BuscarImpactoMasCercano(
                centroCuerpo,
                haciaPunto / distancia,
                distancia,
                out RaycastHit obstaculo
            )
        )
        {
            return obstaculo.point + obstaculo.normal * margenSpawnBala;
        }

        return origenDeseado;
    }

    private void CalcularOrigenYRotacionDeDisparo(
        out Vector3 posicion,
        out Quaternion rotacion
    )
    {
        posicion = ObtenerOrigenLibreDeDisparo();
        rotacion = puntoDeDisparo.rotation;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector3 forwardCamara = cam.transform.forward;
        Vector3 haciaObjetivo = ObtenerPuntoDeMira() - posicion;

        Vector3 direccion =
            (
                haciaObjetivo.sqrMagnitude < 0.0001f ||
                Vector3.Angle(haciaObjetivo, forwardCamara) > anguloMaximoCorreccion
            )
                ? forwardCamara
                : haciaObjetivo.normalized;

        rotacion = Quaternion.LookRotation(direccion);

        Debug.DrawRay(posicion, direccion * 10f, Color.green, 2f);
    }

    private void DispararBala()
    {
        if (armaActual.esBoomerang)
        {
            photonView.RPC(
                nameof(RPC_BoomerangLanzado),
                RpcTarget.All
            );
        }
        else
        {
            municionRestante--;
        }

        CalcularOrigenYRotacionDeDisparo(
            out Vector3 posicionDisparo,
            out Quaternion rotacionDisparo
        );

        object[] datosLanzamiento =
        {
            true,
            photonView.Owner.ActorNumber,
            armaActual.velocidadProyectil,
            armaActual.dano,
            armaActual.titularMuerte,
            armaActual.puntosPorBaja
        };

        GameObject bala = PhotonNetwork.Instantiate(
            armaActual.nombrePrefabProyectilNet,
            posicionDisparo,
            rotacionDisparo,
            0,
            datosLanzamiento
        );

        ProyectilBasico proyectil = bala.GetComponent<ProyectilBasico>();

        if (proyectil != null)
        {
            proyectil.duenoActorNumber = photonView.Owner.ActorNumber;
            proyectil.velocidad = armaActual.velocidadProyectil;
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
            ObtenerOrigenLibreDeDisparo(),
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
            if (armaActual.puedeDevolverProyectiles)
            {
                ProyectilBasico proyectilEnemigo = col.GetComponentInParent<ProyectilBasico>();
                if (proyectilEnemigo != null)
                {
                    PhotonView pvProyectil = proyectilEnemigo.GetComponent<PhotonView>();
                    if (pvProyectil != null)
                    {
                        Vector3 puntoObjetivoMira = ObtenerPuntoDeMira();
                        Vector3 direccionRebote = (puntoObjetivoMira - proyectilEnemigo.transform.position).normalized;

                        pvProyectil.TransferOwnership(PhotonNetwork.LocalPlayer);
                        pvProyectil.RPC("RedirigirProyectil", RpcTarget.AllViaServer, direccionRebote, photonView.Owner.ActorNumber);
                        continue;
                    }
                }
            }

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
                    Mathf.RoundToInt(armaActual.danoMelee),
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

        if (!boomerangEnVuelo)
            return;

        boomerangEnVuelo = false;

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