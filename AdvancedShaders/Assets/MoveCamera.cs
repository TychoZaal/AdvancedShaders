using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    [SerializeField]
    private float speedVertical = 1f;
    [SerializeField]
    private float speedHorizontal = 1f;
    public int speed = 5;
    private CursorLockMode cursorState;
    public float yaw = 77.819f;
    public float pitch = 0.0f;

    private void Start()
    {
        cursorState = CursorLockMode.Locked;
        SetCursorState();
    }

    void FixedUpdate()
    {
        yaw += speedHorizontal * Input.GetAxis("Mouse X");
        pitch -= speedVertical * Input.GetAxis("Mouse Y");

        transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);

        Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        movement = transform.TransformDirection(movement);
        movement *= speed;

        transform.position += movement * speed;

        // Unlock camera
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            cursorState = CursorLockMode.None;
            SetCursorState();
        }

        // Lock camera
        if (Input.GetMouseButtonDown(0))
        {
            cursorState = CursorLockMode.Locked;
            SetCursorState();
        }
    }

    void SetCursorState()
    {
        Cursor.lockState = cursorState;
        Cursor.visible = (CursorLockMode.Locked != cursorState);
    }
}