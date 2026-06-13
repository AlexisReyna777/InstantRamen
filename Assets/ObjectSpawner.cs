using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ObjectSpawner : NetworkBehaviour
{
    [Header("Configuración del Spawn")]
    [SerializeField] private GameObject[] prefabsColectables; // Elemento 0: Normal | Elemento 1: Maldita | Elemento 2: Encogedora

    [Header("Distribución de Probabilidades (Debe sumar 100%)")]
    [Range(0f, 100f)] [SerializeField] private float porcientoNormal = 60f;
    [Range(0f, 100f)] [SerializeField] private float porcientoMaldita = 20f;
    [Range(0f, 100f)] [SerializeField] private float porcientoEncogedora = 20f;

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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (prefabsColectables == null || prefabsColectables.Length == 0)
            {
                Debug.LogError("[SPAWNER] No has asignado ningún prefab en el array 'Prefabs Colectables'.");
                return;
            }

            estaSpawneando = true;
            StartCoroutine(SpawnRoutine());
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (estaSpawneando)
        {
            if (GameManager.Instance == null || !GameManager.Instance.partidaIniciada.Value)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            objetosActivos.RemoveAll(item => item == null);

            if (objetosActivos.Count < maxObjetosEnMapa)
            {
                SpawnearObjeto();
            }

            yield return new WaitForSeconds(tiempoEntreSpawns);
        }
    }

    private void SpawnearObjeto()
    {
        Vector3 posicionValida = Vector3.zero;
        bool posicionEncontrada = false;

        for (int i = 0; i < intentosMaximosPorCaja; i++)
        {
            float randomX = Random.Range(-rangoX, rangoX);
            float randomZ = Random.Range(-rangoZ, rangoZ);
            Vector3 posicionTentativa = new Vector3(randomX, alturaY, randomZ);

            if (!Physics.CheckSphere(posicionTentativa, radioDeValidacion, capasProhibidas))
            {
                posicionValida = posicionTentativa;
                posicionEncontrada = true;
                break;
            }
        }

        if (!posicionEncontrada)
        {
            Debug.LogWarning("[SPAWNER] Se canceló el spawn de una caja: el área seleccionada estaba obstruida por muros.");
            return;
        }

        // Seleccionamos la caja con el nuevo equilibrio de probabilidades
        GameObject prefabAInstanciar = SeleccionarPrefabPorProbabilidad();

        if (prefabAInstanciar == null) return;

        GameObject nuevoObjeto = Instantiate(prefabAInstanciar, posicionValida, Quaternion.identity);
        objetosActivos.Add(nuevoObjeto);

        NetworkObject netObj = nuevoObjeto.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        else
        {
            Debug.LogError($"El Prefab '{prefabAInstanciar.name}' asignado al Spawner NO tiene un componente NetworkObject.");
        }
    }

    // --- NUEVA LÓGICA DE EQUILIBRIO DE 3 COLECTABLES ---
    private GameObject SeleccionarPrefabPorProbabilidad()
    {
        // Si solo hay un prefab asignado, no hay nada que calcular
        if (prefabsColectables.Length == 1) return prefabsColectables[0];

        // Tiramos un dado entre 0.0 y 100.0
        float dado = Random.Range(0f, 100f);

        // Rango 1: Caja Normal (De 0 hasta porcientoNormal)
        if (dado <= porcientoNormal)
        {
            return prefabsColectables[0]; 
        }
        
        // Rango 2: Caja Maldita (De porcientoNormal hasta porcientoNormal + porcientoMaldita)
        if (dado <= (porcientoNormal + porcientoMaldita) && prefabsColectables.Length > 1)
        {
            return prefabsColectables[1]; 
        }
        
        // Rango 3: Caja Encogedora (Cualquier valor restante arriba de los anteriores)
        if (prefabsColectables.Length > 2)
        {
            return prefabsColectables[2]; 
        }

        // Por si acaso el array no tiene los 3 elementos cargados todavía en el Inspector
        return prefabsColectables[0];
    }

    public override void OnNetworkDespawn()
    {
        estaSpawneando = false;
        StopAllCoroutines();
    }
}