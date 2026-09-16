using UnityEngine;
using Photon.Pun;

public class TestDummyShooter : MonoBehaviourPun
{
    [Header("Configuración del Bot de Pruebas")]
    [Tooltip("Nombre del prefab del proyectil en la carpeta Resources (ej. ProyectilBasico)")]
    public string nombrePrefabProyectil = "ProyectilBasico";
    
    [Tooltip("Tiempo en segundos entre cada disparo")]
    public float intervaloDisparo = 2f;

    [Tooltip("Velocidad de la bala de prueba")]
    public float velocidadBala = 20f;

    [Tooltip("Daño causado por la bala de prueba")]
    public int danoBala = 10;

    private void Start()
    {
        // Solo el MasterClient o el dueño del objeto inicia la rutina para no duplicar balas en red
        if (photonView.IsMine || PhotonNetwork.IsMasterClient)
        {
            InvokeRepeating(nameof(DispararBalaPrueba), 1f, intervaloDisparo);
        }
    }

    private void DispararBalaPrueba()
    {
        if (string.IsNullOrEmpty(nombrePrefabProyectil)) return;

        // Instancia la bala en red utilizando PhotonNetwork
        GameObject bala = PhotonNetwork.Instantiate(
            nombrePrefabProyectil,
            transform.position + transform.forward * 1.5f,
            transform.rotation
        );

        ProyectilBasico proyectil = bala.GetComponent<ProyectilBasico>();
        if (proyectil != null)
        {
            proyectil.velocidad = velocidadBala;
            proyectil.dano = danoBala; // Asignación correcta como número entero (int)
        }

        Debug.Log($"[TestDummy] ¡Disparando proyectil de prueba hacia {transform.forward}!");
    }
}