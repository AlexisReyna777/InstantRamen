using UnityEngine;

public class LluviaMenuManager : MonoBehaviour
{
    [Header("Configuración de Objetos")]
    [SerializeField] private GameObject[] prefabsObjetos;
    [SerializeField] private int cantidadObjetos = 30;
    [Tooltip("Multiplicador de tamaño si tus assets vienen muy chicos.")]
    [SerializeField] private float escalaMultiplicadora = 10f; 

    [Header("Configuración de los Pilares")]
    [Tooltip("Qué tan lejos del centro (X=0) se generarán los pilares. Aumenta este valor para alejarlos más hacia los bordes.")]
    [SerializeField] private float distanciaDelCentroX = 12f;
    [Tooltip("El ancho o grosor de cada pilar individual.")]
    [SerializeField] private float anchoPilarX = 3f;
    [Tooltip("El grosor en profundidad (Z) del pilar.")]
    [SerializeField] private float rangoZ = 2f;

    [Header("Límites de Caída (Y)")]
    [SerializeField] private float limiteSuperiorY = 10f;
    [SerializeField] private float limiteInferiorY = -10f;

    [Header("Velocidad")]
    [SerializeField] private float velocidadMinima = 2f;
    [SerializeField] private float velocidadMaxima = 6f;

    private void Start()
    {
        if (prefabsObjetos == null || prefabsObjetos.Length == 0) return;

        Vector3 posCamara = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        for (int i = 0; i < cantidadObjetos; i++)
        {
            GameObject prefabElegido = prefabsObjetos[Random.Range(0, prefabsObjetos.Length)];
            
            // Decidimos si este objeto va al pilar izquierdo (true) o al derecho (false)
            bool esPilarIzquierdo = (i % 2 == 0);
            
            // Calculamos la posición X base según el lado elegido
            float xBase = esPilarIzquierdo ? -distanciaDelCentroX : distanciaDelCentroX;
            // Le sumamos una pequeña variación aleatoria dentro del grosor de su propio pilar
            float coordenadaX = posCamara.x + xBase + Random.Range(-anchoPilarX * 0.5f, anchoPilarX * 0.5f);

            Vector3 posicionAleatoria = new Vector3(
                coordenadaX,
                posCamara.y + Random.Range(limiteInferiorY, limiteSuperiorY),
                posCamara.z + 5f // Caen a 5 metros frente a la cámara
            );

            GameObject nuevoObjeto = Instantiate(prefabElegido, posicionAleatoria, Random.rotation);
            
            nuevoObjeto.transform.localScale = prefabElegido.transform.localScale * escalaMultiplicadora;
            
            ObjetoCaidaMenu componenteCaida = nuevoObjeto.AddComponent<ObjetoCaidaMenu>();
            
            // Configuramos el comportamiento pasándole los datos necesarios
            componenteCaida.Configurar(
                posCamara.y + limiteSuperiorY, 
                posCamara.y + limiteInferiorY, 
                anchoPilarX, 
                rangoZ, 
                Random.Range(velocidadMinima, velocidadMaxima)
            );

            // Modificación interna para que cuando el objeto buclee (respawnee arriba), mantenga su lógica de pilar fijo
            componenteCaida.FijarLadoPilar(coordenadaX);
        }
    }
}