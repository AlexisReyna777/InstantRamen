using UnityEngine;

public class CasillaInteractuable : MonoBehaviour
{
    [SerializeField] private TresEnRayaNetcode controladorPrincipal;
    [SerializeField] private int indiceCasilla; // Ponle de 0 a 8 en el inspector

    private void OnTriggerEnter(Collider other)
    {
        // Si el objeto que entra tiene el movimiento del jugador
        if (other.TryGetComponent<PlayerMovement>(out var player))
        {
            // ¡Muy importante!: Solo el dueño de ese personaje envía el comando
            if (player.IsOwner)
            {
                controladorPrincipal.IntentarMarcarCasilla(indiceCasilla);
            }
        }
    }
}