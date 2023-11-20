using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camera : MonoBehaviour
{
    public GameObject target;
    public Vector3 offset;

    void Update()
    {
        Vector3 targetPos = target.transform.position + offset.x * target.transform.right + offset.y * target.transform.up + offset.z * target.transform.forward;
        transform.position = Vector3.Lerp(transform.position, targetPos, 0.02f);
        transform.LookAt(target.transform.position + target.transform.up * 2);
    }
}
