using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;

    void Start()
    {

        if (IsOwner)
        {
            
            if (TryGetComponent<Renderer>(out Renderer render))
            {
                render.material.color = Color.red;
            }

            StartCoroutine(ForzarSpawnCliente());
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        float x = 0f;
        float z = 0f;

        // Control con WASD
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        // Control con Flechas
        if (Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.UpArrow))    z += 1f;
        if (Input.GetKey(KeyCode.DownArrow))  z -= 1f;

        
        Vector3 move = new Vector3(x, 0f, z);

        
        if (move.magnitude > 0.1f)
        {
            move = move.normalized;
            transform.position += move * speed * Time.deltaTime;
        }
    }
    public override void OnNetworkSpawn()
    {
        if (IsOwner && !IsServer)
        {
            transform.position = NetworkObject.transform.position;
        }
    }

    private System.Collections.IEnumerator ForzarSpawnCliente()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        Vector3 posicionServidor = NetworkObject.transform.position;
        Quaternion rotacionServidor = NetworkObject.transform.rotation;

        transform.position = posicionServidor;
        transform.rotation = rotacionServidor;

        Debug.Log($"[CLIENTE] Teletransportación forzada exitosa al spawn del servidor: {posicionServidor}");
    }
}