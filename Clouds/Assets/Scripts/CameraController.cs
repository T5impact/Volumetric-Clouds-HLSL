using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] float mouseSensitivity;
    [SerializeField] float moveSpeed;
    [SerializeField] Transform camTransform;
    [SerializeField] GameObject pauseObj;

    bool paused;

    float rotationY;

    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        paused = false;
        pauseObj.SetActive(paused);
    }

    // Update is called once per frame
    void Update()
    {
        if (!paused)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            transform.rotation = Quaternion.AngleAxis(mouseX * mouseSensitivity * Time.deltaTime, Vector3.up) * transform.rotation;
            rotationY -= mouseY * mouseSensitivity * Time.deltaTime;
            rotationY = Mathf.Clamp(rotationY, -89, 89);
            camTransform.localRotation = Quaternion.AngleAxis(rotationY, Vector3.right);
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        transform.Translate((transform.forward * vertical + transform.right * horizontal) * moveSpeed * Time.deltaTime, Space.World);

        if(Input.GetKey(KeyCode.Space))
        {
            transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            transform.Translate(Vector3.up * -moveSpeed * Time.deltaTime);
        }

        if(paused && Input.GetKeyDown(KeyCode.Escape))
        {
            paused = false;
            pauseObj.SetActive(paused);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        } 
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            paused = true;
            pauseObj.SetActive(paused);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void Quit()
    {
        Application.Quit();
    }
}
