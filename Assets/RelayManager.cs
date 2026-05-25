using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport; 
public class RelayManager : MonoBehaviour
{
   [SerializeField] Button hostButton;
   [SerializeField] Button joinButton;
   [SerializeField] TMP_InputField joinInput;
   [SerializeField] TextMeshProUGUI codeText;

   async void Start()
    {
        // Inicializa los servicios de Unity
        await UnityServices.InitializeAsync();

        // Inicia sesión de forma anónima en los servicios de Unity
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        hostButton.onClick.AddListener(CreateRelay);
        joinButton.onClick.AddListener(() => JoinRelay(joinInput.text));
    }
   
   async void CreateRelay()
    {
        // Crea la asignación en los servidores de Unity para 3 jugadores max (más el host = 4)
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
        
        // Genera el código
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        
        codeText.text = "Code: " + joinCode;

        // Configura los datos del servidor Relay usando "dtls" (encriptado seguro)
        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartHost();
    }

    async void JoinRelay(string joinCode)
    {
        // Si el campo de texto está vacío, evitamos intentar conectar
        if (string.IsNullOrEmpty(joinCode)) return;

        // Se une a la partida usando el código introducido en el InputField
        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        
        // Configura los datos del servidor Relay para el cliente
        var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
        
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartClient();
    }
}