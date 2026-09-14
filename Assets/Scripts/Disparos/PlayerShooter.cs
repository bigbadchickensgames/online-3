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
    
    // NUEVO: Controlamos la corrutina del golpe para que no se superpongan
    private Coroutine corrutinaMeleeActual;

    void Start()
    {
        controladorJugador = GetComponent<PlayerController>();

        if (armaActual != null)
        {
            EquiparArma(armaActual);
        }
    }

    public void EquiparArma(DatosArma nuevaArma)
    {
        if (nuevaArma == null) return;

        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_EquiparArmaRed), RpcTarget.AllBuffered, nuevaArma.name);
        }
    }

    [PunRPC]
    private void RPC_EquiparArmaRed(string nombreDatosArma)
    {
        DatosArma datosCargados = Resources.Load<DatosArma>(nombreDatosArma);

        if (datosCargados == null)
        {
            DatosArma[] todas = Resources.LoadAll<DatosArma>("");
            datosCargados = System.Array.Find(todas, a => a.name == nombreDatosArma);
        }

        if (datosCargados == null)
        {
            Debug.LogError($"[PlayerShooter] No se encontró el ScriptableObject '{nombreDatosArma}'.");
            return;
        }

        armaActual = datosCargados;
        municionRestante = armaActual.municionMaxima;
        tieneTrampaDisponible = (armaActual.tipoArma == TipoArma.Disparo && armaActual.efectoVacio == TipoEfectoVacio.TirarComoTrampa);

        ActualizarModeloArmaLocal(armaActual.nombrePrefabModeloArma);
    }

    public void DesequiparArma()
    {
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_DesequiparArmaRed), RpcTarget.AllBuffered);
        }
    }

    [PunRPC]
    private void RPC_DesequiparArmaRed()
    {
        armaActual = null;
        municionRestante = 0;
        tieneTrampaDisponible = false;

        if (modeloArmaActualInstancia != null)
        {
            Destroy(modeloArmaActualInstancia);
        }
        puntoDeDisparo = null;
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
        if (modeloArmaActualInstancia != null)
        {
            Destroy(modeloArmaActualInstancia);
        }
        puntoDeDisparo = null;

        // --- ARREGLO DE ANIMACIÓN Y MANO TORCIDA ---
        if (corrutinaMeleeActual != null) 
        {
            StopCoroutine(corrutinaMeleeActual);
            corrutinaMeleeActual = null;
        }
        if (socketArma != null) 
        {
            socketArma.localRotation = Quaternion.identity; // Aseguramos que la mano vuelve a su sitio
        }
        // ---------------------------------------------

        if (socketArma == null || string.IsNullOrEmpty(nombrePrefabModelo)) return;

        GameObject prefabModelo = Resources.Load<GameObject>(nombrePrefabModelo);
        if (prefabModelo == null)
        {
            Debug.LogError($"[PlayerShooter] No se encontró el prefab '{nombrePrefabModelo}'.");
            return;
        }

        modeloArmaActualInstancia = Instantiate(prefabModelo, socketArma);

        // --- ARREGLO DEL PESCAO (Respetar el Prefab) ---
        // Volvemos a coger LA ROTACIÓN DEL PREFAB EXACTA. Si el pescao necesita estar girado 90º,
        // esto lo respetará sin heredar rotaciones raras de cuando estaba en el suelo.
        modeloArmaActualInstancia.transform.localPosition = prefabModelo.transform.localPosition;
        modeloArmaActualInstancia.transform.localRotation = prefabModelo.transform.localRotation;
        modeloArmaActualInstancia.transform.localScale = prefabModelo.transform.localScale;
        // -----------------------------------------------

        foreach (PickupArma pickup in modeloArmaActualInstancia.GetComponentsInChildren<PickupArma>())
        {
            pickup.enabled = false; 
            Destroy(pickup);
        }

        foreach (Collider col in modeloArmaActualInstancia.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        puntoDeDisparo = BuscarPuntoDisparoRecursivo(modeloArmaActualInstancia.transform);
    }

    private Transform BuscarPuntoDisparoRecursivo(Transform raiz)
    {
        if (raiz.name == "PuntoDisparo") return raiz;

        foreach (Transform hijo in raiz)
        {
            Transform encontrado = BuscarPuntoDisparoRecursivo(hijo);
            if (encontrado != null) return encontrado;
        }

        return null;
    }

    void Update()
    {
        if (!photonView.IsMine || armaActual == null) return;

        if (Input.GetButtonDown("Fire1") && Time.time >= tiempoUltimoDisparo + armaActual.cadenciaDisparo)
        {
            tiempoUltimoDisparo = Time.time;
            controladorJugador.GirarHaciaCamaraYDisparar(EjecutarAccionDeAtaque);
        }
    }

    private void EjecutarAccionDeAtaque()
    {
        if (puntoDeDisparo == null)
        {
            Debug.LogWarning("No se puede atacar: falta 'PuntoDisparo'.");
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

        GameObject bala = PhotonNetwork.Instantiate(
            armaActual.nombrePrefabProyectilNet,
            puntoDeDisparo.position,
            puntoDeDisparo.rotation
        );

        ProyectilBasico proyectil = bala.GetComponent<ProyectilBasico>();
        if (proyectil != null)
        {
            proyectil.velocidad = armaActual.velocidadProyectil;
            proyectil.dano = armaActual.dano;
            proyectil.titularMuerte = armaActual.titularMuerte;
            proyectil.puntosPorBaja = armaActual.puntosPorBaja;
        }

        ComprobarAgotamientoArma();
    }

    private void LanzarTrampaExtra()
    {
        if (string.IsNullOrEmpty(armaActual.nombrePrefabTrampaNet)) return;

        tieneTrampaDisponible = false;

        Vector3 direccionLanzamiento = puntoDeDisparo.forward;
        direccionLanzamiento.y += anguloElevacionTrampa * 0.05f;
        direccionLanzamiento.Normalize();

        Vector3 velocidadImpulso = direccionLanzamiento * fuerzaLanzamientoTrampa;
        object[] datosInstanciacion = new object[] { velocidadImpulso };

        PhotonNetwork.Instantiate(
            armaActual.nombrePrefabTrampaNet,
            puntoDeDisparo.position,
            Quaternion.LookRotation(direccionLanzamiento),
            0,
            datosInstanciacion
        );

        ComprobarAgotamientoArma();
    }

    private void EjecutarAtaqueMelee()
    {
        photonView.RPC(nameof(RPC_ReproducirAnimacionMelee), RpcTarget.All);

        Vector3 centroGolpe = puntoDeDisparo.position + puntoDeDisparo.forward * armaActual.rangoMelee;
        Collider[] impactados = Physics.OverlapSphere(centroGolpe, armaActual.radioMelee);

        foreach (Collider col in impactados)
        {
            PhotonView pvImpactado = col.GetComponentInParent<PhotonView>();

            if (pvImpactado == null || pvImpactado.Owner == photonView.Owner) continue;

            PlayerHealth saludEnemigo = col.GetComponentInParent<PlayerHealth>();
            if (saludEnemigo != null)
            {
                saludEnemigo.photonView.RPC("RecibirDano", RpcTarget.All, armaActual.danoMelee, photonView.Owner.ActorNumber, armaActual.titularMuerte, armaActual.puntosPorBaja);
            }

            PlayerController controladorEnemigo = col.GetComponentInParent<PlayerController>();
            if (controladorEnemigo != null)
            {
                controladorEnemigo.AplicarRalentizacionRed(armaActual.ralentizacionMultiplicador, armaActual.ralentizacionDuracion);
            }
        }
    }

    [PunRPC]
    private void RPC_ReproducirAnimacionMelee()
    {
        if (armaActual == null || socketArma == null) return;
        
        // --- ARREGLO DE SPAM DE CLICKS ---
        // Si ya había una animación reproduciéndose, la paramos para que los ángulos no se acumulen
        if (corrutinaMeleeActual != null)
        {
            StopCoroutine(corrutinaMeleeActual);
        }
        
        corrutinaMeleeActual = StartCoroutine(RutinaAnimacionMelee(socketArma, armaActual));
    }

    private IEnumerator RutinaAnimacionMelee(Transform pivoteAnimacion, DatosArma datosDelGolpe)
    {
        // VITAL: La rotación original SIEMPRE es neutra (0,0,0). No cogemos cómo estuviese la mano
        // en este instante concreto, porque si spammeabas click, se iba torciendo poco a poco.
        Quaternion rotacionOriginal = Quaternion.identity; 
        Quaternion rotacionDelGolpe = rotacionOriginal * Quaternion.Euler(datosDelGolpe.anguloGolpeMelee);

        // Forzamos a la mano a ponerse recta justo al empezar
        pivoteAnimacion.localRotation = rotacionOriginal;

        float t = 0f;
        while (t < datosDelGolpe.duracionIdaGolpeMelee)
        {
            t += Time.deltaTime;
            float porcentaje = t / datosDelGolpe.duracionIdaGolpeMelee;
            pivoteAnimacion.localRotation = Quaternion.Slerp(rotacionOriginal, rotacionDelGolpe, porcentaje);
            yield return null;
        }

        t = 0f;
        while (t < datosDelGolpe.duracionVueltaGolpeMelee)
        {
            t += Time.deltaTime;
            float porcentaje = t / datosDelGolpe.duracionVueltaGolpeMelee;
            pivoteAnimacion.localRotation = Quaternion.Slerp(rotacionDelGolpe, rotacionOriginal, porcentaje);
            yield return null;
        }

        // Al terminar, nos aseguramos al 100% que vuelve a la posición base perfecta
        pivoteAnimacion.localRotation = rotacionOriginal;
        corrutinaMeleeActual = null; // Vaciamos la variable porque ya hemos terminado
    }

    private void OnDrawGizmos()
    {
        if (armaActual == null || armaActual.tipoArma != TipoArma.Melee || puntoDeDisparo == null) return;

        Gizmos.color = Color.red;
        Vector3 centroGolpe = puntoDeDisparo.position + puntoDeDisparo.forward * armaActual.rangoMelee;
        Gizmos.DrawWireSphere(centroGolpe, armaActual.radioMelee);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(puntoDeDisparo.position, centroGolpe);
    }
}