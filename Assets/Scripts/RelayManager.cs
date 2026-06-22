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
    [SerializeField] private TextMeshProUGUI codeText; 
    [SerializeField] private TextMeshProUGUI estadoConexionText; 

    [Header("Configuración de Escena")]
    [SerializeField] private string nombreEscenaJuego = "MainScene"; 

    [Header("Limpieza Anti-Bugs (Menú 3D)")]
    [Tooltip("Arrastra aquí la cámara que usa tu menú principal")]
    [SerializeField] private Camera camaraMenu;
    [Tooltip("Arrastra aquí el objeto vacío que tiene el script LluviaMenuManager")]
    [SerializeField] private GameObject gestorLluvia3D;

    async void Start()
    {
        if (iniciarPartidaButton != null) iniciarPartidaButton.gameObject.SetActive(false);
        if (codeText != null) codeText.gameObject.SetActive(false);
        if (estadoConexionText != null) estadoConexionText.gameObject.SetActive(false);

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

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

            // Ordenamos a todos los clientes limpiar el menú inmediatamente para que no haya desincronización física
            LimpiarElementosMenuClientRpc();

            NetworkManager.Singleton.SceneManager.LoadScene(nombreEscenaJuego, LoadSceneMode.Single);
        }
    }

    [ClientRpc]
    private void LimpiarElementosMenuClientRpc()
    {
        // 1. Apagamos el gestor de la lluvia para frenar los bucles de instanciación
        if (gestorLluvia3D != null)
        {
            gestorLluvia3D.SetActive(false);
        }

        // 2. Desactivamos la cámara del menú para que no compita con la cámara de juego real
        if (camaraMenu != null)
        {
            camaraMenu.enabled = false;
            camaraMenu.gameObject.SetActive(false);
        }

        // MODIFICACIÓN DE SEGURIDAD: Fulminamos el EventSystem desalineado del menú antes del cambio de escena
        UnityEngine.EventSystems.EventSystem sistemaEventos = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (sistemaEventos != null)
        {
            sistemaEventos.enabled = false;
            sistemaEventos.gameObject.SetActive(false);
        }

        // 3. Ocultamos el canvas completo por si acaso tarda un frame en destruirse
        gameObject.SetActive(false);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"[SALA] ¡Cliente conectado con ID: {clientId}!");
            
            if (estadoConexionText != null)
            {
                estadoEventosTextVisibilidad(true, "¡Cliente conectado! Listo para empezar.");
            }
            
            if (NetworkManager.Singleton.IsServer && iniciarPartidaButton != null)
            {
                iniciarPartidaButton.gameObject.SetActive(true);
            }
        }
    }

    private void estadoEventosTextVisibilidad(bool activar, string mensaje)
    {
        if (estadoConexionText != null)
        {
            estadoConexionText.gameObject.SetActive(activar);
            estadoConexionText.text = mensaje;
        }
    }
   
    async void CreateRelay()
    {
        try
        {
            estadoEventosTextVisibilidad(true, "Creando sala...");
            
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            if (codeText != null) 
            {
                codeText.gameObject.SetActive(true);
                codeText.text = "Code: " + joinCode;
            }

            estadoEventosTextVisibilidad(true, "Esperando al cliente...");

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            Debug.Log("[NETCODE] Host iniciado.");

            if (hostButton != null) hostButton.interactable = false;
            if (joinButton != null) joinButton.interactable = false;
        }
        catch (Exception e)
        {
            estadoEventosTextVisibilidad(true, "Error al crear sala.");
            Debug.LogError($"Error al crear el Relay: {e.Message}");
        }
    }

    async void JoinRelay(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode)) return;

        try
        {
            estadoEventosTextVisibilidad(true, "Conectando...");

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            
            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            
            estadoEventosTextVisibilidad(true, "¡Conectado! Esperando al Host...");
            
            if (hostButton != null) hostButton.interactable = false;
            if (joinButton != null) joinButton.interactable = false;
        }
        catch (Exception e)
        {
            estadoEventosTextVisibilidad(true, "Error: Código inválido.");
            Debug.LogError($"Error al unirse al Relay: {e.Message}");
        }
    }
}