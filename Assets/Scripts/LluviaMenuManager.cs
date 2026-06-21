using UnityEngine;

public class LluviaMenuManager : MonoBehaviour
{
    [Header("Configuración de Objetos")]
    [Tooltip("El o los prefabs 3D que van a caer (pueden ser cubos, esferas, cápsulas, etc.)")]
    [SerializeField] private GameObject[] prefabsObjetos;
    [Tooltip("Cantidad de objetos cayendo al mismo tiempo en pantalla")]
    [SerializeField] private int cantidadObjetos = 30;
    
    [Header("Variedad de Tamaños")]
    [Tooltip("Multiplicador MÍNIMO de tamaño para los objetos.")]
    [SerializeField] private float escalaMinima = 5f; 
    [Tooltip("Multiplicador MÁXIMO de tamaño para los objetos.")]
    [SerializeField] private float escalaMaxima = 15f; 

    [Header("Configuración de los Pilares")]
    [Tooltip("Qué tan lejos del centro (X=0) se generarán los pilares. Sube este valor para alejarlos a los bordes.")]
    [SerializeField] private float distanciaDelCentroX = 12f;
    [Tooltip("El ancho o grosor horizontal de cada pilar individual.")]
    [SerializeField] private float anchoPilarX = 3f;
    [Tooltip("El grosor en profundidad (Z) del pilar para dar efecto 3D.")]
    [SerializeField] private float rangoZ = 2f;

    [Header("Límites de Caída (Y)")]
    [SerializeField] private float limiteSuperiorY = 10f;
    [SerializeField] private float limiteInferiorY = -10f;

    [Header("Velocidad")]
    [SerializeField] private float velocidadMinima = 2f;
    [SerializeField] private float velocidadMaxima = 6f;

    private void Start()
    {
        if (prefabsObjetos == null || prefabsObjetos.Length == 0)
        {
            Debug.LogError("¡Falta asignar los prefabs de los objetos en el LluviaMenuManager!");
            return;
        }

        // Usamos la posición del propio objeto GestorLluvia como centro fijo independiente de la cámara
        Vector3 centroFijo = transform.position;

        for (int i = 0; i < cantidadObjetos; i++)
        {
            // Elegimos un prefab aleatorio
            GameObject prefabElegido = prefabsObjetos[Random.Range(0, prefabsObjetos.Length)];
            
            // Alternamos: un objeto va al pilar izquierdo y el siguiente al derecho
            bool esPilarIzquierdo = (i % 2 == 0);
            
            // Calculamos la coordenada X según el pilar correspondiente
            float xBase = esPilarIzquierdo ? -distanciaDelCentroX : distanciaDelCentroX;
            float coordenadaX = centroFijo.x + xBase + Random.Range(-anchoPilarX * 0.5f, anchoPilarX * 0.5f);

            // Calculamos la profundidad Z relativa al centro del Gestor
            float coordenadaZ = centroFijo.z + Random.Range(-rangoZ * 0.5f, rangoZ * 0.5f);

            // Posición final combinada
            Vector3 posicionAleatoria = new Vector3(
                coordenadaX,
                centroFijo.y + Random.Range(limiteInferiorY, limiteSuperiorY),
                coordenadaZ
            );

            // Creamos el clon
            GameObject nuevoObjeto = Instantiate(prefabElegido, posicionAleatoria, Random.rotation);
            
            // Aplicamos la variación de tamaño aleatorio entre el mínimo y máximo configurados
            float multiplicadorAleatorio = Random.Range(escalaMinima, escalaMaxima);
            nuevoObjeto.transform.localScale = prefabElegido.transform.localScale * multiplicadorAleatorio;
            
            // Le inyectamos el script de caída individual
            ObjetoCaidaMenu componenteCaida = nuevoObjeto.AddComponent<ObjetoCaidaMenu>();
            
            // Lo configuramos pasándole las dimensiones del pilar
            componenteCaida.Configurar(
                centroFijo.y + limiteSuperiorY, 
                centroFijo.y + limiteInferiorY, 
                anchoPilarX, 
                rangoZ, 
                Random.Range(velocidadMinima, velocidadMaxima)
            );

            // Bloqueamos su coordenada X para que al reaparecer arriba no se cruce de pilar
            componenteCaida.FijarLadoPilar(coordenadaX);
        }
    }
}