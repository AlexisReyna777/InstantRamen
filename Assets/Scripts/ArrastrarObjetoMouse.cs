using UnityEngine;

public class ArrastrarObjetoMouse : MonoBehaviour
{
    [Header("Configuración del Lanzamiento")]
    [SerializeField] private float fuerzaMultiplicadora = 1.5f;
    [SerializeField] private LayerMask capasInteractuables;

    private Camera camaraPrincipal;
    private PelotaInteractivaNetcode pelotaActual;
    private bool arrastrando = false;
    private float distanciaZ;
    
    // Variables para calcular la velocidad del lanzamiento
    private Vector3 ultimaPosicionMundo;
    private Vector3 velocidadLanzamiento;

    private void Start()
    {
        camaraPrincipal = Camera.main;
    }

    private void Update()
    {
        // 1. Detectar clic inicial
        if (Input.GetMouseButtonDown(0))
        {
            Ray rayo = camaraPrincipal.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(rayo, out RaycastHit hit, 100f, capasInteractuables))
            {
                if (hit.collider.TryGetComponent<PelotaInteractivaNetcode>(out var pelota))
                {
                    pelotaActual = pelota;
                    arrastrando = true;
                    
                    // Guardamos la distancia fija desde la cámara a la pelota para el arrastre
                    distanciaZ = hit.transform.position.z - camaraPrincipal.transform.position.z;
                    
                    pelotaActual.IniciarArrastreLocal();
                    ultimaPosicionMundo = ObtenerPosicionMouseEnMundo();
                }
            }
        }

        // 2. Mover el objeto mientras se sostiene el clic
        if (arrastrando && pelotaActual != null)
        {
            Vector3 posicionMundoActual = ObtenerPosicionMouseEnMundo();
            
            // Calculamos la velocidad en base al movimiento del frame actual
            velocidadLanzamiento = (posicionMundoActual - ultimaPosicionMundo) / Time.deltaTime;
            ultimaPosicionMundo = posicionMundoActual;

            // Movemos visualmente el objeto local de forma suave
            pelotaActual.transform.position = posicionMundoActual;
        }

        // 3. Soltar el objeto y aplicar el tiro
        if (Input.GetMouseButtonUp(0) && arrastrando)
        {
            arrastrando = false;
            if (pelotaActual != null)
            {
                // Enviamos la fuerza calculada multiplicada por tu ajuste de potencia
                Vector3 fuerzaFinal = velocidadLanzamiento * fuerzaMultiplicadora;
                pelotaActual.LanzarPelotaLocal(fuerzaFinal);
                pelotaActual = null;
            }
        }
    }

    private Vector3 ObtenerPosicionMouseEnMundo()
    {
        Vector3 posicionMouseScreen = Input.mousePosition;
        posicionMouseScreen.z = distanciaZ; // Mantenemos la profundidad
        return camaraPrincipal.ScreenToWorldPoint(posicionMouseScreen);
    }
}