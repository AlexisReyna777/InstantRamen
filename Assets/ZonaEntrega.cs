using Unity.Netcode;
using UnityEngine;

public class ZonaEntrega : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // REGLA DE ORO: Solo el servidor procesa la entrega y los puntos
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            // 1. Buscamos todos los cubos que el jugador tiene apilados como hijos
            ObjetoRecolectable[] cubosEntregados = other.GetComponentsInChildren<ObjetoRecolectable>();
            int cantidadCubos = cubosEntregados.Length;

            // Si el jugador no lleva nada, ignoramos
            if (cantidadCubos == 0) return;

            Debug.Log($"[SERVIDOR] ¡El jugador entregó {cantidadCubos} objetos!");

            // 2. Destruimos los objetos en la red
            foreach (ObjetoRecolectable cubo in cubosEntregados)
            {
                // Obtenemos el NetworkObject del cubo y lo removemos de la red
                NetworkObject netObj = cubo.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(); // Despawn los borra de las pantallas de todos los clientes
                    Destroy(cubo.gameObject); // Destruye el objeto físicamente en el servidor
                }
            }

            // 3. ASIGNAR PUNTOS (Siguiente paso)
            // Aquí le avisaremos al GameManager que sume 'cantidadCubos' al jugador correspondiente
            if (GameManager.Instance != null)
            {
                ulong playerId = other.GetComponent<NetworkObject>().OwnerClientId;
                GameManager.Instance.SumarPuntosServer(playerId, cantidadCubos);
            }
        }
    }
}