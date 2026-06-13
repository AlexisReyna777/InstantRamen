using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ObjectSpawner : NetworkBehaviour
{
    [Header("Configuración del Spawn")]
    [SerializeField] private GameObject objetoPrefab; // El prefab que tiene el componente NetworkObject
    [SerializeField] private float tiempoEntreSpawns = 3.0f; // Cada cuántos segundos aparece uno nuevo
    [SerializeField] private int maxObjetosEnMapa = 10; // Límite para no saturar el juego de cubos

    [Header("Filtro de Colisión Inmueble")]
    [SerializeField] private LayerMask capasProhibidas; // Selecciona aquí la capa de tus muros/obstáculos
    [SerializeField] private float radioDeValidacion = 0.6f; // El tamaño del chequeo (debe cubrir el ancho de tu caja)
    [SerializeField] private int intentosMaximosPorCaja = 10; // Para evitar bucles infinitos si el mapa está lleno

    [Header("Límites del Mapa (Área de Spawn)")]
    [SerializeField] private float rangoX = 8.5f;
    [SerializeField] private float rangoZ = 8.5f;
    [SerializeField] private float alturaY = 0.5f; // Altura fija para que no aparezcan flotando o enterrados

    private List<GameObject> objetosActivos = new List<GameObject>();
    private bool estaSpawneando = false;

    // OnNetworkSpawn se ejecuta automáticamente cuando el sistema de red se inicia en la escena
    public override void OnNetworkSpawn()
    {
        // IMPORTANTE: Solo el servidor/host debe ejecutar la lógica del Spawn
        if (IsServer)
        {
            estaSpawneando = true;
            StartCoroutine(SpawnRoutine());
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (estaSpawneando)
        {
            // ¡EL ESCUDO CRUCIAL!: Si el GameManager dice que la partida NO empezó, detenemos el flujo acá
            if (GameManager.Instance == null || !GameManager.Instance.partidaIniciada.Value)
            {
                // Esperamos un fotograma (o medio segundo) y volvemos a chequear, sin spawnear nada
                yield return new WaitForSeconds(0.5f);
                continue; // Vuelve al inicio del 'while' saltándose todo lo de abajo
            }

            // Limpiamos la lista de objetos destruidos antes de verificar el límite
            objetosActivos.RemoveAll(item => item == null);

            if (objetosActivos.Count < maxObjetosEnMapa)
            {
                SpawnearObjeto();
            }

            // Espera el tiempo configurado antes de la siguiente iteración
            yield return new WaitForSeconds(tiempoEntreSpawns);
        }
    }

    private void SpawnearObjeto()
    {
        Vector3 posicionValida = Vector3.zero;
        bool posicionEncontrada = false;

        // Intentamos buscar un punto limpio en el mapa varias veces en el mismo frame
        for (int i = 0; i < intentosMaximosPorCaja; i++)
        {
            // 1. Calcular una posición aleatoria tentativa
            float randomX = Random.Range(-rangoX, rangoX);
            float randomZ = Random.Range(-rangoZ, rangoZ);
            Vector3 posicionTentativa = new Vector3(randomX, alturaY, randomZ);

            // 2. Comprobamos si esa esfera matemática choca con las capas prohibidas (muros, colisionadores invisibles, etc.)
            if (!Physics.CheckSphere(posicionTentativa, radioDeValidacion, capasProhibidas))
            {
                posicionValida = posicionTentativa;
                posicionEncontrada = true;
                break; // Rompemos el ciclo porque encontramos un lugar perfecto
            }
        }

        // Si después de los intentos no encontramos un punto libre, saltamos este spawn para no congelar el servidor
        if (!posicionEncontrada)
        {
            Debug.LogWarning("[SPAWNER] Se canceló el spawn de una caja: el área seleccionada estaba obstruida por muros.");
            return;
        }

        // 3. Instanciar el prefab localmente en el Servidor en la posición segura
        GameObject nuevoObjeto = Instantiate(objetoPrefab, posicionValida, Quaternion.identity);

        // 4. Añadirlo a la lista para controlar el límite máximo
        objetosActivos.Add(nuevoObjeto);

        // 5. ¡LA CLAVE DE NETCODE!: Obtener su NetworkObject y spawnearlo en la red
        NetworkObject netObj = nuevoObjeto.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(); // Esto le avisa a todos los clientes que deben crear este objeto en sus pantallas
        }
        else
        {
            Debug.LogError("El Prefab asignado al Spawner NO tiene un componente NetworkObject.");
        }
    }

    // Detener la rutina si el script se destruye o termina la partida
    public override void OnNetworkDespawn()
    {
        estaSpawneando = false;
        StopAllCoroutines();
    }
}