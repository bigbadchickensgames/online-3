using System.Collections;
using UnityEngine;
using Photon.Pun;

public class PlayerShooter : MonoBehaviourPun
{
    [Header("Arma Equipada")]
    public DatosArma armaActual;

    [Tooltip("Se asigna automáticamente al equipar el arma, buscando un hijo llamado 'PuntoDisparo' dentro del modelo instanciado. No hace falta asignarlo a mano.")]
    public Transform puntoDeDisparo;

    [Header("Socket del Modelo Visual del Arma")]
    public Transform socketArma; // Transform vacío donde se instancia el modelo 3D de cada arma

    [Header("Configuración de Lanzamiento de Trampa")]
    public float fuerzaLanzamientoTrampa = 15f;
    public float anguloElevacionTrampa = 5f;

    private int municionRestante;
    private bool tieneTrampaDisponible;
    private float tiempoUltimoDisparo;

    private PlayerController controladorJugador;
    private GameObject modeloArmaActualInstancia;

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
        armaActual = nuevaArma;
        municionRestante = armaActual.municionMaxima;

        // Habilitamos la trampa si el Scriptable Object tiene configurado el efecto
        tieneTrampaDisponible = (armaActual.tipoArma == TipoArma.Disparo && armaActual.efectoVacio == TipoEfectoVacio.TirarComoTrampa);

        // Actualizamos el modelo localmente YA (no esperamos a la RPC, para no tener un frame con puntoDeDisparo nulo)
        ActualizarModeloArmaLocal(armaActual.nombrePrefabModeloArma);

        // Y avisamos al resto de clientes para que también lo actualicen
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_ActualizarModeloArma), RpcTarget.Others, armaActual.nombrePrefabModeloArma);
        }
    }

    [PunRPC]
    private void RPC_ActualizarModeloArma(string nombrePrefabModelo)
    {
        ActualizarModeloArmaLocal(nombrePrefabModelo);
    }

    private void ActualizarModeloArmaLocal(string nombrePrefabModelo)
    {
        if (modeloArmaActualInstancia != null)
        {
            Destroy(modeloArmaActualInstancia);
        }
        puntoDeDisparo = null;

        if (string.IsNullOrEmpty(nombrePrefabModelo) || socketArma == null) return;

        GameObject prefabModelo = Resources.Load<GameObject>(nombrePrefabModelo);
        if (prefabModelo == null)
        {
            Debug.LogWarning($"No se encontró el prefab de modelo '{nombrePrefabModelo}' en ninguna carpeta Resources.");
            return;
        }

        modeloArmaActualInstancia = Instantiate(prefabModelo, socketArma);
        modeloArmaActualInstancia.transform.localPosition = Vector3.zero;
        modeloArmaActualInstancia.transform.localRotation = Quaternion.identity;

        puntoDeDisparo = BuscarPuntoDisparoRecursivo(modeloArmaActualInstancia.transform);
        if (puntoDeDisparo == null)
        {
            Debug.LogWarning($"El prefab '{nombrePrefabModelo}' no tiene ningún hijo llamado exactamente 'PuntoDisparo'.");
        }
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
            // Reservamos el tiempo de ataque ya, para que no se pueda spamear mientras el jugador gira
            tiempoUltimoDisparo = Time.time;
            controladorJugador.GirarHaciaCamaraYDisparar(EjecutarAccionDeAtaque);
        }
    }

    // Se llama cuando el giro hacia la cámara ha terminado
    private void EjecutarAccionDeAtaque()
    {
        if (puntoDeDisparo == null)
        {
            Debug.LogWarning("No se puede atacar: el arma actual no tiene un 'PuntoDisparo' asignado.");
            return;
        }

        if (armaActual.tipoArma == TipoArma.Melee)
        {
            EjecutarAtaqueMelee();
            return;
        }

        // 1. Si aún nos quedan balas normales
        if (municionRestante > 0)
        {
            DispararBala();
        }
        // 2. Si nos quedamos sin balas normales, pero aún nos queda el tiro extra de la trampa
        else if (tieneTrampaDisponible)
        {
            LanzarTrampaExtra();
        }
        else
        {
            Debug.Log("¡Arma totalmente vacía!");
        }
    }

    private void DispararBala()
    {
        municionRestante--;

        // La bala sale exactamente en la dirección en la que apunta el arma,
        // que ya está alineada con la cámara gracias al giro previo del cuerpo
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
        }
    }

    private void LanzarTrampaExtra()
    {
        if (string.IsNullOrEmpty(armaActual.nombrePrefabTrampaNet)) return;

        tieneTrampaDisponible = false; // Consumimos el tiro extra de la trampa

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
    }

    private void EjecutarAtaqueMelee()
    {
        // Avisamos a TODOS los clientes (incluido tú) para que reproduzcan el swing del arma
        photonView.RPC(nameof(RPC_ReproducirAnimacionMelee), RpcTarget.All);

        Vector3 centroGolpe = puntoDeDisparo.position + puntoDeDisparo.forward * armaActual.rangoMelee;
        Collider[] impactados = Physics.OverlapSphere(centroGolpe, armaActual.radioMelee);

        foreach (Collider col in impactados)
        {
            PhotonView pvImpactado = col.GetComponentInParent<PhotonView>();

            // Ignoramos nuestro propio collider
            if (pvImpactado == null || pvImpactado.Owner == photonView.Owner) continue;

            PlayerHealth saludEnemigo = col.GetComponentInParent<PlayerHealth>();
            if (saludEnemigo != null)
            {
                saludEnemigo.photonView.RPC("RecibirDano", RpcTarget.All, armaActual.danoMelee);
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
        
        // Ahora le pasamos el socketArma en lugar del modelo instanciado
        StartCoroutine(RutinaAnimacionMelee(socketArma, armaActual));
    }

    private IEnumerator RutinaAnimacionMelee(Transform pivoteAnimacion, DatosArma datosDelGolpe)
    {
        // Guardamos la rotación original del Socket
        Quaternion rotacionOriginal = pivoteAnimacion.localRotation;
        
        // Calculamos la rotación final sumándole el ángulo deseado (ej: 0, 90, 0)
        Quaternion rotacionDelGolpe = rotacionOriginal * Quaternion.Euler(datosDelGolpe.anguloGolpeMelee);

        // FASE 1: Ida (El golpe)
        float t = 0f;
        while (t < datosDelGolpe.duracionIdaGolpeMelee)
        {
            t += Time.deltaTime;
            float porcentaje = t / datosDelGolpe.duracionIdaGolpeMelee;
            pivoteAnimacion.localRotation = Quaternion.Slerp(rotacionOriginal, rotacionDelGolpe, porcentaje);
            yield return null;
        }

        // FASE 2: Vuelta (Recuperar la postura)
        t = 0f;
        while (t < datosDelGolpe.duracionVueltaGolpeMelee)
        {
            t += Time.deltaTime;
            float porcentaje = t / datosDelGolpe.duracionVueltaGolpeMelee;
            pivoteAnimacion.localRotation = Quaternion.Slerp(rotacionDelGolpe, rotacionOriginal, porcentaje);
            yield return null;
        }

        // Aseguramos que termine exactamente donde empezó para evitar desvíos
        pivoteAnimacion.localRotation = rotacionOriginal;
    }

    // Útil para depurar el rango del golpe melee en el Editor
    private void OnDrawGizmosSelected()
    {
        if (armaActual == null || armaActual.tipoArma != TipoArma.Melee || puntoDeDisparo == null) return;

        Gizmos.color = Color.red;
        Vector3 centroGolpe = puntoDeDisparo.position + puntoDeDisparo.forward * armaActual.rangoMelee;
        Gizmos.DrawWireSphere(centroGolpe, armaActual.radioMelee);
    }
}