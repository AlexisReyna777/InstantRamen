using Unity.Netcode;
using UnityEngine;

public class ObjetoRecolectable : NetworkBehaviour
{
    private bool isPickUp = false;

private void OnTriggerEnter(Collider other)
{
    // 1. ¿Llega el choque al servidor?
    if (!IsServer) return;

    if (isPickUp) return;

    // 2. ¿Detecta que es el jugador?
    if (other.CompareTag("Player"))
    {
        Debug.Log("¡Servidor detectó choque con un Player!");

        Transform pickUpPoint = other.transform.Find("PickUp");

        // 3. ¿Encontró el punto de carga?
        if (pickUpPoint != null)
        {
            Debug.Log("¡Servidor detectó choque con un Player!");
            isPickUp = true;

            if (TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = false; 
            }

            transform.SetParent(other.transform);

            transform.localPosition = new Vector3(0, 1.5f, 0); 
            transform.localRotation = Quaternion.identity;

            Debug.Log("¡Cubo emparentado directamente al jugador raíz con éxito!");

        }
    }
}}