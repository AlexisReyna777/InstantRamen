using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class ControladorBarraCompartida : MonoBehaviour
{
    [Header("Componentes de UI")]
    [SerializeField] private Image barraHostImage; // Arrastra aquí la imagen 'BarraHost' (la que está en modo Filled)

    private void Start()
    {
        // Inicializamos la barra justo a la mitad en todas las pantallas
        if (barraHostImage != null)
        {
            barraHostImage.fillAmount = 0.5f;
        }
    }

    private void Update()
    {
        // Esperamos a que el GameManager esté listo y la partida haya iniciado
        if (GameManager.Instance == null) return;

        ActualizarBarraVisual();
    }

    private void ActualizarBarraVisual()
    {
        // Obtenemos los puntajes de red actuales de ambos jugadores
        float puntosHost = GameManager.Instance.puntajeHost.Value;
        float puntosCliente = GameManager.Instance.puntajeCliente.Value;

        // Caso base: Si nadie ha anotado puntos, la barra se mantiene al centro (50/50)
        if (puntosHost == 0 && puntosCliente == 0)
        {
            barraHostImage.fillAmount = 0.5f;
            return;
        }

        // Calculamos el porcentaje total que le corresponde al Host usando una regla de tres simple:
        // Porcentaje = PuntosHost / (PuntosHost + PuntosCliente)
        float totalPuntos = puntosHost + puntosCliente;
        float porcentajeHost = puntosHost / totalPuntos;

        // Sincronizamos el llenado visual de la imagen de manera suave (Lerp) para evitar saltos bruscos
        if (barraHostImage != null)
        {
            barraHostImage.fillAmount = Mathf.Lerp(barraHostImage.fillAmount, porcentajeHost, Time.deltaTime * 5f);
        }
    }
}
