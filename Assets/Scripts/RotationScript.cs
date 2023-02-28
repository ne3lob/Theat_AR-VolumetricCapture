using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationScript : MonoBehaviour
{
    public float rotationSpeed = 10f; // The speed at which to rotate the object

    private void Update()
    {
        // Rotate the object around itself
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}