using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System; 

public class RelayManager : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinInput;
    [SerializeField] private TextMeshProUGUI codeText;

    async void Start()
    {
        
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("Jugador no logueado. Iniciando sesión de forma anónima...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        else
        {
            Debug.Log("El jugador ya estaba autenticado de la escena anterior. Saltando login.");
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
    }

    // Funciones intermediarias obligatorias para botones tradicionales cuando usas métodos "async"
    private void OnHostButtonClicked()
    {
        CreateRelay();
    }

    private void OnJoinButtonClicked()
    {
        if (joinInput != null)
        {
            JoinRelay(joinInput.text);
        }
    }
   
    async void CreateRelay()
    {
        try
        {
            Debug.Log("[RELAY] Intentando crear asignación en la nube...");
            // Asignación para 3 invitados max
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            
            // Genera el código
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            if (codeText != null) codeText.text = "Code: " + joinCode;
            Debug.Log($"[RELAY] Código generado con éxito: {joinCode}");

            // Configura el transporte de red de Unity (UTP)
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Enciende el Host e instancia al Player automáticamente
            NetworkManager.Singleton.StartHost();
            Debug.Log("[NETCODE] ¡Host iniciado con éxito!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error al crear el Relay: {e.Message}");
        }
    }

    async void JoinRelay(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("El código de unión está vacío.");
            return;
        }

        try
        {
            Debug.Log($"[RELAY] Intentando unirse con el código: {joinCode}");
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            
            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Enciende el cliente
            NetworkManager.Singleton.StartClient();
            Debug.Log("[NETCODE] ¡Cliente iniciado y conectando al Host!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error al unirse al Relay: {e.Message}");
        }
    }
}