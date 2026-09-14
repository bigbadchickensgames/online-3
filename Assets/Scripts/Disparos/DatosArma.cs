using UnityEngine;

public enum TipoEfectoVacio { Ninguno, TirarComoTrampa }
public enum TipoArma { Disparo, Melee }

[CreateAssetMenu(fileName = "NuevoDataArma", menuName = "Armas/DatosArma")]
public class DatosArma : ScriptableObject
{
    [Header("Datos Generales")]
    public string nombreArma;
    public TipoArma tipoArma = TipoArma.Disparo;
    public float cadenciaDisparo = 0.3f;

    [Header("Modelo Visual (nombre exacto del prefab en una carpeta Resources)")]
    public string nombrePrefabModeloArma;

    [Header("Ajustes de Disparo (si tipoArma = Disparo)")]
    public string nombrePrefabProyectilNet; // Nombre exacto del prefab en la carpeta Resources
    public int municionMaxima = 6;
    public float velocidadProyectil = 20f;
    public float dano = 25f;

    [Header("Efecto al Vaciarse (si tipoArma = Disparo)")]
    public TipoEfectoVacio efectoVacio = TipoEfectoVacio.TirarComoTrampa;
    public string nombrePrefabTrampaNet; // Nombre del prefab del cáscarón en Resources

    [Header("Ajustes de Melee (si tipoArma = Melee)")]
    public float danoMelee = 20f;
    public float rangoMelee = 2f;
    public float radioMelee = 1f;
    public float ralentizacionMultiplicador = 0.4f; // 0.4 = se mueve al 40% de su velocidad normal
    public float ralentizacionDuracion = 2.5f;

    [Header("Animación de Golpe Melee (si tipoArma = Melee)")]
    public Vector3 anguloGolpeMelee = new Vector3(0f, 90f, 0f); // Swing lateral (como un bate)
    public float duracionIdaGolpeMelee = 0.08f;   // tiempo en llegar al ángulo de golpe
    public float duracionVueltaGolpeMelee = 0.12f; // tiempo en volver a su posición de reposo
}