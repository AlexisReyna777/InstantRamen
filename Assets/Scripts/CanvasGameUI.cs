using UnityEngine;
using UnityEngine.UI;
using TMPro; // REQUERIDO: Para usar TextMeshPro y evitar la pixelación

public class CanvasGameUI : MonoBehaviour
{
    [Header("Componentes de la UI")]
    [SerializeField] private TextMeshProUGUI textoTiempo; // MODIFICADO: Ahora es TextMeshPro para máxima nitidez

    [Header("Pantalla Final (Popups)")]
    [SerializeField] private GameObject panelFinDePartida;
    
    [Header("Pantalla Final (Carteles)")]
    [SerializeField] private GameObject cartelVictoria;
    [SerializeField] private GameObject cartelDerrota;
    [SerializeField] private GameObject cartelEmpate;

    private void Start()
    {
        // Al iniciar la partida, nos aseguramos de que TODO el panel final esté apagado
        if (panelFinDePartida != null)
        {
            panelFinDePartida.SetActive(false);
        }
        
        // Apagamos los carteles por si acaso quedaron encendidos en el Inspector
        DesactivarTodosLosCarteles();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // CORRECCIÓN NITIDEZ: Actualización de texto con TextMeshPro de forma eficiente
        if (textoTiempo != null) 
        {
            textoTiempo.text = "Tiempo: " + Mathf.CeilToInt(GameManager.Instance.tiempoRestante.Value).ToString();
        }

        // --- CONTROL DEL POPUP FINAL DE PARTIDA ---
        if (panelFinDePartida != null)
        {
            if (GameManager.Instance.juegoTerminado.Value)
            {
                // Solo si el panel está apagado, calculamos los carteles (para evitar que se ejecute en bucle cada frame)
                if (!panelFinDePartida.activeSelf)
                {
                    panelFinDePartida.SetActive(true);
                    CalcularCartelFinalLocal();
                }
            }
            else
            {
                // Si la partida se reinicia, apagamos el panel y los carteles
                if (panelFinDePartida.activeSelf)
                {
                    panelFinDePartida.SetActive(false);
                    DesactivarTodosLosCarteles();
                }
            }
        }
    }

    // --- LÓGICA INDEPENDIENTE PARA HOST Y CLIENTE ---
    private void CalcularCartelFinalLocal()
    {
        int ptsHost = GameManager.Instance.puntajeHost.Value;
        int ptsCliente = GameManager.Instance.puntajeCliente.Value;

        // 1. Caso de Empate Técnico (Es igual para ambos)
        if (ptsHost == ptsCliente)
        {
            if (cartelEmpate != null) cartelEmpate.SetActive(true);
            return;
        }

        // 2. Comprobamos quién soy yo en la red (Netcode)
        bool soyElHost = Unity.Netcode.NetworkManager.Singleton.IsServer;

        if (soyElHost)
        {
            // Lógica para la pantalla del HOST
            if (ptsHost > ptsCliente)
            {
                if (cartelVictoria != null) cartelVictoria.SetActive(true);
            }
            else
            {
                if (cartelDerrota != null) cartelDerrota.SetActive(true);
            }
        }
        else
        {
            // Lógica para la pantalla del CLIENTE
            if (ptsCliente > ptsHost)
            {
                if (cartelVictoria != null) cartelVictoria.SetActive(true);
            }
            else
            {
                if (cartelDerrota != null) cartelDerrota.SetActive(true);
            }
        }
    }

    private void DesactivarTodosLosCarteles()
    {
        if (cartelVictoria != null) cartelVictoria.SetActive(false);
        if (cartelDerrota != null) cartelDerrota.SetActive(false);
        if (cartelEmpate != null) cartelEmpate.SetActive(false);
    }

    public void AlPresionarReiniciar()
    {
        if (Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            GameManager.Instance.ReiniciarPartidaServer();
        }
        else
        {
            Debug.Log("Solo el Host puede reiniciar la partida.");
        }
    }
}