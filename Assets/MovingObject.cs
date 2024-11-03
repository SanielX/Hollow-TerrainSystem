using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingObject : MonoBehaviour
{
    public float distance;
    public float speed;
    
    Vector3 initial;

    void Start() => initial = transform.position;
    void Update()
    {
        float t = Mathf.Sin(Time.time * speed) * distance;
        transform.position = initial + new Vector3(t, 0, 0);
    }
}
