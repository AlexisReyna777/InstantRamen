using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class ZonaEntrega : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Solo el servidor procesa la entrega física

        if (other.CompareTag("Player"))
        {
            PlayerMovement player = other.GetComponent<PlayerMovement>();
            
            if (player != null)
            {
                int cantidadCubos = player.colectablesActuales;

                // Si no lleva nada, ignoramos
                if (cantidadCubos == 0) return;

                Debug.Log($"[SERVIDOR] ¡El jugador entregó {cantidadCubos} objetos!");

                // OBTENEMOS LAS REFERENCIAS ANTES DE BORRARLAS
                List<GameObject> cubosAEntregar = new List<GameObject>(player.ObtenerCajasCargadas());

                // 1. ASIGNAR PUNTOS AL GAMEMANAGER
                if (GameManager.Instance != null)
                {
                    ulong playerId = other.GetComponent<NetworkObject>().OwnerClientId;
                    GameManager.Instance.SumarPuntosServer(playerId, cantidadCubos);
                }

                // 2. ORDEN DE LIMPIEZA PRIMERO: Resetea el peso y la velocidad en todos los clientes
                player.EntregarColectablesDesdeServidor();

                // 3. DESTRUCCIÓN EN RED SEGUNDO: Ahora que todos limpiaron sus listas, borramos el objeto
                foreach (GameObject cuboGO in cubosAEntregar)
                {
                    if (cuboGO == null) continue;

                    NetworkObject netObj = cuboGO.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(); 
                        Destroy(cuboGO);  
                    }
                }
            }
        }
    }
}