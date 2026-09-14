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

    // --- NUEVO: PUNTUACIÓN Y RISAS ---
    [Header("Humillación (Puntos)")]
    public int puntosPorBaja = 75; // 75 pistola, 150 pescado
    public string titularMuerte = "¡Acribillado!"; // Ej: "¡BOFETÓN MARINO!" o "¡Baleado!"
    // ---------------------------------

    [Header("Modelo Visual (nombre exacto del prefab en una carpeta Resources)")]
    public string nombrePrefabModeloArma;

    [Header("Ajustes de Disparo (si tipoArma = Disparo)")]
    public string nombrePrefabProyectilNet; 
    public int municionMaxima = 6;
    public float velocidadProyectil = 20f;
    public float dano = 25f;

    [Header("Efecto al Vaciarse (si tipoArma = Disparo)")]
    public TipoEfectoVacio efectoVacio = TipoEfectoVacio.TirarComoTrampa;
    public string nombrePrefabTrampaNet; 

    [Header("Ajustes de Melee (si tipoArma = Melee)")]
    public float danoMelee = 20f;
    public float rangoMelee = 2f;
    public float radioMelee = 1f;
    public float ralentizacionMultiplicador = 0.4f; 
    public float ralentizacionDuracion = 2.5f;

    [Header("Animación de Golpe Melee (si tipoArma = Melee)")]
    public Vector3 anguloGolpeMelee = new Vector3(0f, 90f, 0f); 
    public float duracionIdaGolpeMelee = 0.08f;  
    public float duracionVueltaGolpeMelee = 0.12f; 
}