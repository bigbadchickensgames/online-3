using UnityEngine;

public enum TipoEfectoVacio { Ninguno, TirarComoTrampa }

[CreateAssetMenu(fileName = "NuevoDataArma", menuName = "Armas/DatosArma")]
public class DatosArma : ScriptableObject
{
    public string nombreArma;
    public string nombrePrefabProyectilNet; // Nombre exacto del prefab en la carpeta Resources
    public int municionMaxima = 6;
    public float cadenciaDisparo = 0.3f;
    public float velocidadProyectil = 20f;
    public float dano = 25f;

    [Header("Efecto al Vaciarse")]
    public TipoEfectoVacio efectoVacio = TipoEfectoVacio.TirarComoTrampa;
    public string nombrePrefabTrampaNet; // Nombre del prefab del cáscarón en Resources
}