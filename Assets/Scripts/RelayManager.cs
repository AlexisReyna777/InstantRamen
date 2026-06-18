using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using System; 

public class RelayManager : MonoBehaviour
{
    [Header("Componentes de UI de los Botones")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button salirButton;
    [SerializeField] private Button iniciarPartidaButton; 

    [Header("Componentes de UI de Texto")]
    [SerializeField] private TMP_InputField joinInput; 
    [SerializeField] private TextMeshProUGUI codeText; // Se ocultará al inicio
    [SerializeField] private TextMeshProUGUI estadoConexionText; // Se ocultará al inicio

    [Header("Configuración de Escena")]
    [SerializeField] private string nombreEscenaJuego = "MainScene"; 

    async void Start()
    {
        // --- SOLUCIÓN VISUAL: Ocultamos los textos y botones al arrancar el juego ---
        if (iniciarPartidaButton != null) iniciarPartidaButton.gameObject.SetActive(false);
        if (codeText != null) codeText.gameObject.SetActive(false);
        if (estadoConexionText != null) estadoConexionText.gameObject.SetActive(false);

        // Inicialización de servicios de Unity
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Configuración de listeners de botones
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners(); 
            hostButton.onClick.AddListener(OnHostButtonClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinButtonClicked);
        }

        if (salirButton != null)
        {
            salirButton.onClick.RemoveAllListeners();
            salirButton.onClick.AddListener(OnSalirButtonClicked);
        }

        if (iniciarPartidaButton != null)
        {
            iniciarPartidaButton.onClick.RemoveAllListeners();
            iniciarPartidaButton.onClick.AddListener(OnIniciarPartidaButtonClicked);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnHostButtonClicked() => CreateRelay();
    private void OnJoinButtonClicked() => JoinRelay(joinInput?.text);
    private void OnSalirButtonClicked() => Application.Quit();

    private void OnIniciarPartidaButtonClicked()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (estadoConexionText != null) estadoConexionText.text = "Cargando mundo...";
            NetworkManager.Singleton.SceneManager.LoadScene(nombreEscenaJuego, LoadSceneMode.Single);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"[SALA] ¡Cliente conectado con ID: {clientId}!");
            
            if (estadoConexionText != null)
            {
                estadoConexionText.gameObject.SetActive(true); // Aseguramos que sea visible
                estadoConexionText.text = "¡Cliente conectado! Listo para empezar.";
            }
            
            if (NetworkManager.Singleton.IsServer && iniciarPartidaButton != null)
            {
                iniciarPartidaButton.gameObject.SetActive(true);
            }
        }
    }
   
    async void CreateRelay()
    {
        try
        {
            // Hacemos visible el texto de estado para mostrar el progreso
            if (estadoConexionText != null)
            {
                estadoConexionText.gameObject.SetActive(true);
                estadoConexionText.text = "Creando sala...";
            }
            
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            // Mostramos el código generado
            if (codeText != null) 
            {
                codeText.gameObject.SetActive(true);
                codeText.text = "Code: " + joinCode;
            }

            if (estadoConexionText != null) estadoConexionText.text = "Esperando al cliente...";

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            Debug.Log("[NETCODE] Host iniciado.");

            if (hostButton != null) hostButton.interactable = false;
            if (joinButton != null) joinButton.interactable = false;
        }
        catch (Exception e)
        {
            if (estadoConexionText != null) estadoConexionText.text = "Error al crear sala.";
            Debug.LogError($"Error al crear el Relay: {e.Message}");
        }
    }

    async void JoinRelay(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode)) return;

        try
        {
            // Hacemos visible el texto de estado en el cliente
            if (estadoConexionText != null)
            {
                estadoConexionText.gameObject.SetActive(true);
                estadoConexionText.text = "Conectando...";
            }

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            
            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            
            if (estadoConexionText != null) estadoConexionText.text = "¡Conectado! Esperando al Host...";
            
            if (hostButton != null) hostButton.interactable = false;
            if (joinButton != null) joinButton.interactable = false;
        }
        catch (Exception e)
        {
            if (estadoConexionText != null) estadoConexionText.text = "Error: Código inválido.";
            Debug.LogError($"Error al unirse al Relay: {e.Message}");
        }
    }
}