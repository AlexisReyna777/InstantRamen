using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    //Initial variable to speed
    [SerializeField] private float speed = 5f;
    void Start()
    {
        //Color start player Owner sesion
        if (IsOwner)
        {
            GetComponent<Renderer>().material.color = Color.red;
        }
    }

    
    void Update()
    {
        if (!IsOwner) return;

        float x = 0f;
        float z = 0f;

        // To controller WASD keys
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        // To controller Arrow keys
        if (Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.UpArrow))    z += 1f;
        if (Input.GetKey(KeyCode.DownArrow))  z -= 1f;

        Vector3 move = new Vector3(x, 0f, z).normalized;
        transform.position += move * speed * Time.deltaTime;
    }
}
