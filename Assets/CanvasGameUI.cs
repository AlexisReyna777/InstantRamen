using UnityEngine;
using UnityEngine.UI; // Cambiar por "using TMPro;" si usas TextMeshPro

public class CanvasGameUI : MonoBehaviour
{
    [Header("Componentes de la UI")]
    [SerializeField] private Text textoTiempo; // Cambiar a TMP_Text si usas TextMeshPro
    [SerializeField] private Text textoPuntajeHost;
    [SerializeField] private Text textoPuntajeCliente;
    
    [Header("Pantalla Final")]
    [SerializeField] private GameObject panelFinDePartida;
    [SerializeField] private Text textoGanador; // Se ocultará al iniciar la partida

    private void Start()
    {
        // --- SOLUCIÓN VISUAL: Ocultamos el texto del ganador para evitar el "New Text" ---
        if (textoGanador != null)
        {
            textoGanador.gameObject.SetActive(false);
        }

        // Aseguramos que el panel final empiece apagado por si acaso
        if (panelFinDePartida != null)
        {
            panelFinDePartida.SetActive(false);
        }
    }

    private void Update()
    {
        // Primer escudo: Si el GameManager aún no nació, esperamos.
        if (GameManager.Instance == null) return;

        // Segundo escudo: Solo actualizamos si las casillas están asignadas en el Inspector
        if (textoTiempo != null)
        {
            textoTiempo.text = "Tiempo: " + Mathf.CeilToInt(GameManager.Instance.tiempoRestante.Value).ToString();
        }

        if (textoPuntajeHost != null)
        {
            textoPuntajeHost.text = "Puntos Host: " + GameManager.Instance.puntajeHost.Value;
        }

        if (textoPuntajeCliente != null)
        {
            textoPuntajeCliente.text = "Puntos Cliente: " + GameManager.Instance.puntajeCliente.Value;
        }

        // Control del panel final
        if (panelFinDePartida != null)
        {
            if (GameManager.Instance.juegoTerminado.Value)
            {
                panelFinDePartida.SetActive(true);

                if (textoGanador != null)
                {
                    // ¡MÁGIA!: Hacemos visible el texto del ganador solo cuando la partida termina
                    textoGanador.gameObject.SetActive(true);

                    int ptsHost = GameManager.Instance.puntajeHost.Value;
                    int ptsCliente = GameManager.Instance.puntajeCliente.Value;

                    if (ptsHost > ptsCliente) textoGanador.text = "¡Ganador: HOST!";
                    else if (ptsCliente > ptsHost) textoGanador.text = "¡Ganador: CLIENTE!";
                    else textoGanador.text = "¡Empate técnico!";
                }
            }
            else
            {
                panelFinDePartida.SetActive(false);

                // Si por alguna razón la partida se reinicia o des-pausa, volvemos a ocultar el texto
                if (textoGanador != null)
                {
                    textoGanador.gameObject.SetActive(false);
                }
            }
        }
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