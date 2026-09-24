using UnityEngine;
using System.Collections;

public class GameJuicePlayer : MonoBehaviour
{
    [Header("Squash & Stretch (Salto/Caída)")]
    public float multiplicadorEstiramiento = 0.3f;
    public float velocidadMaxima = 8f;

    [Header("Tilt (Inclinación por Inercia)")]
    [Tooltip("Cuánto se inclina al acelerar o derrapar. (Valores entre 1.5 y 3 recomendados)")]
    public float multiplicadorInclinacion = 2.5f; 
    public float inclinacionMaxima = 25f;
    public float velocidadRecuperacion = 12f;

    [Header("Damage Flash (Daño)")]
    public Color colorFlash = Color.white;
    public float duracionFlash = 0.1f;

    // Referencias internas
    private Transform modeloVisual;
    private Renderer meshRendererActivo;
    private Renderer meshRendererOriginal;
    private Material materialJugador;
    private Color colorOriginal;
    
    private Vector3 ultimaPosicion;
    private Vector3 velocidadSuavizada; // Guarda la inercia del movimiento

    void Awake()
    {
        MeshFilter originalMF = GetComponent<MeshFilter>();
        meshRendererOriginal = GetComponent<MeshRenderer>();

        if (originalMF != null && meshRendererOriginal != null)
        {
            GameObject hijoVisual = new GameObject("ModeloVisual_Animado");
            hijoVisual.transform.SetParent(transform);
            hijoVisual.transform.localPosition = Vector3.zero;
            hijoVisual.transform.localRotation = Quaternion.identity;
            hijoVisual.transform.localScale = Vector3.one;

            MeshFilter nuevoMF = hijoVisual.AddComponent<MeshFilter>();
            nuevoMF.sharedMesh = originalMF.sharedMesh;

            meshRendererActivo = hijoVisual.AddComponent<MeshRenderer>();
            meshRendererActivo.sharedMaterials = meshRendererOriginal.sharedMaterials;

            meshRendererOriginal.enabled = false;
            modeloVisual = hijoVisual.transform;
        }
    }

    void Start()
    {
        if (meshRendererActivo != null)
        {
            materialJugador = meshRendererActivo.material;
            colorOriginal = materialJugador.color;
        }
        ultimaPosicion = transform.position;
    }

    void Update()
    {
        if (modeloVisual == null) return;
        
        // 1. Calcular velocidad real en el mundo
        Vector3 velocidadCalculada = (transform.position - ultimaPosicion) / Time.deltaTime;
        ultimaPosicion = transform.position;

        // --- SQUASH & STRETCH ---
        float velY = velocidadCalculada.y;
        float factorEstiramiento = Mathf.Clamp(Mathf.Abs(velY) / velocidadMaxima, 0f, 1f) * multiplicadorEstiramiento;
        
        float escalaY = 1f + factorEstiramiento;
        float escalaXZ = 1f - (factorEstiramiento / 2f);
        
        modeloVisual.localScale = Vector3.Lerp(modeloVisual.localScale, new Vector3(escalaXZ, escalaY, escalaXZ), Time.deltaTime * 15f);

        // --- TILT (Inclinación por inercia) ---
        // Aislamos el movimiento plano (X, Z) ignorando saltos
        Vector3 velPlana = new Vector3(velocidadCalculada.x, 0f, velocidadCalculada.z);
        
        // Suavizamos esta velocidad en el mundo. Esto genera el retraso o "inercia"
        velocidadSuavizada = Vector3.Lerp(velocidadSuavizada, velPlana, Time.deltaTime * velocidadRecuperacion);

        // Convertimos la inercia del mundo según hacia dónde esté mirando el jugador ahora mismo
        Vector3 inerciaLocal = transform.InverseTransformDirection(velocidadSuavizada);

        // Eje X: Pitch (Adelante/Atrás). Depende de tu inercia hacia el frente.
        float rotacionX = Mathf.Clamp(inerciaLocal.z * multiplicadorInclinacion, -inclinacionMaxima, inclinacionMaxima);
        
        // Eje Z: Roll (A los lados). Depende de tu inercia lateral mientras haces giros bruscos.
        float rotacionZ = Mathf.Clamp(-inerciaLocal.x * multiplicadorInclinacion, -inclinacionMaxima, inclinacionMaxima);

        // Aplicamos la rotación de forma suave
        Quaternion rotacionObjetivo = Quaternion.Euler(rotacionX, 0, rotacionZ);
        modeloVisual.localRotation = Quaternion.Lerp(modeloVisual.localRotation, rotacionObjetivo, Time.deltaTime * 15f);
    }

    // --- DAMAGE FLASH ---
    public void HacerFlash()
    {
        if (meshRendererActivo != null && gameObject.activeInHierarchy)
        {
            StopAllCoroutines(); 
            StartCoroutine(RutinaFlash());
        }
    }

    private IEnumerator RutinaFlash()
    {
        materialJugador.color = colorFlash;
        yield return new WaitForSeconds(duracionFlash);
        materialJugador.color = colorOriginal;
    }

    public void ApagarVisual()
    {
        if(meshRendererActivo != null) meshRendererActivo.enabled = false;
    }
}