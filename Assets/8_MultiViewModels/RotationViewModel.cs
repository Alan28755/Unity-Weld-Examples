using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityWeld.Binding;

[Binding]
public class RotationViewModel : MonoBehaviour
{
    /// <summary>
    /// The object we want to manipulate.
    /// </summary>
    public MeshRenderer obj;

    /// <summary>
    /// Property for controlling the object's rotation about the Y axis.
    /// </summary>
    [Binding]
    public float Rotation
    {
        get
        {
            return obj.transform.localRotation.eulerAngles.y;
        }
        set
        {
            obj.transform.localRotation = Quaternion.AngleAxis(value, Vector3.up);
        }
    }

    private void Awake()
    {
        Assert.IsNotNull(obj);
    }
}
