using Unity.Netcode;
using UnityEngine;

public class ObjetoRecolectable : NetworkBehaviour
{
    private bool isPickUp = false;

    [Header("Configuración de la Pila")]
    [SerializeField] private float alturaBase = 1.5f; // Altura del primer cubo (arriba de la cabeza)
    [SerializeField] private float tamañoCuboY = 1.0f; // Cuánto mide el cubo de alto (espesor de cada piso)

private void OnTriggerEnter(Collider other)
{
    // 1. ¿Llega el choque al servidor?
    if (!IsServer) return;

    if (isPickUp) return;

    // 2. ¿Detecta que es el jugador?
    if (other.CompareTag("Player"))
    {

        Transform pickUpPoint = other.transform.Find("PickUp");

        if (pickUpPoint != null)
        {
            isPickUp = true;

            if (TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = false; 
            }

            ObjetoRecolectable[] cubosEnLaPila = other.GetComponentsInChildren<ObjetoRecolectable>();
            int cantidadPrevia = cubosEnLaPila.Length;

            transform.SetParent(other.transform);

            float alturaCalculada = alturaBase + (cantidadPrevia * tamañoCuboY);

            transform.localPosition = new Vector3(0, alturaCalculada, 0);
            transform.localRotation = Quaternion.identity;

            Debug.Log($"[SERVIDOR] Cubo apilado con éxito. Posición en la torre: {cantidadPrevia + 1}");

        }
    }
}}