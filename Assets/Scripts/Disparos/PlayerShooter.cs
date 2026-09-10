using UnityEngine;
using Photon.Pun;

public class PlayerShooter : MonoBehaviourPun
{
    [Header("Arma Equipada")]
    public DatosArma armaActual;
    public Transform puntoDeDisparo;

    [Header("Configuración de Lanzamiento de Trampa")]
    public float fuerzaLanzamientoTrampa = 15f;
    public float anguloElevacionTrampa = 5f;

    private int municionRestante;
    private bool tieneTrampaDisponible;
    private float tiempoUltimoDisparo;

    void Start()
    {
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
        tieneTrampaDisponible = (armaActual.efectoVacio == TipoEfectoVacio.TirarComoTrampa);
    }

    void Update()
    {
        if (!photonView.IsMine || armaActual == null) return;

        if (Input.GetButtonDown("Fire1") && Time.time >= tiempoUltimoDisparo + armaActual.cadenciaDisparo)
        {
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
    }

    private void DispararBala()
    {
        if (Camera.main == null) return;

        tiempoUltimoDisparo = Time.time;
        municionRestante--;

        Ray rayoCamara = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 puntoDestino = Physics.Raycast(rayoCamara, out RaycastHit hit, 1000f) 
            ? hit.point 
            : rayoCamara.GetPoint(1000f);

        Vector3 direccionCorregida = (puntoDestino - puntoDeDisparo.position).normalized;
        Quaternion rotacionCorregida = Quaternion.LookRotation(direccionCorregida);

        GameObject bala = PhotonNetwork.Instantiate(armaActual.nombrePrefabProyectilNet, puntoDeDisparo.position, rotacionCorregida);
        
        ProyectilBasico proyectil = bala.GetComponent<ProyectilBasico>();
        if (proyectil != null)
        {
            proyectil.velocidad = armaActual.velocidadProyectil;
            proyectil.dano = armaActual.dano;
        }
    }

    private void LanzarTrampaExtra()
    {
        if (string.IsNullOrEmpty(armaActual.nombrePrefabTrampaNet) || Camera.main == null) return;

        tiempoUltimoDisparo = Time.time;
        tieneTrampaDisponible = false; // Consumimos el tiro extra de la trampa

        Ray rayoCamara = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 puntoDestino = Physics.Raycast(rayoCamara, out RaycastHit hit, 1000f) 
            ? hit.point 
            : rayoCamara.GetPoint(1000f);

        Vector3 direccionLanzamiento = (puntoDestino - puntoDeDisparo.position).normalized;
        direccionLanzamiento.y += anguloElevacionTrampa * 0.05f;
        direccionLanzamiento.Normalize();

        GameObject trampaGO = PhotonNetwork.Instantiate(
            armaActual.nombrePrefabTrampaNet, 
            puntoDeDisparo.position, 
            Quaternion.LookRotation(direccionLanzamiento)
        );

        Rigidbody rbTrampa = trampaGO.GetComponent<Rigidbody>();
        if (rbTrampa != null)
        {
            rbTrampa.linearVelocity = direccionLanzamiento * fuerzaLanzamientoTrampa;
        }
    }
}