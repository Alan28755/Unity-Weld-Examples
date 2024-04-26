using UnityEngine;
using UnityWeld.Binding;

/// <summary>
/// View model for controlling the color of a mesh.
/// </summary>
[Binding]
public class CubeColorViewModel : MonoBehaviour
{
    private Material mat;

    private void Awake()
    {
        mat = GetComponent<MeshRenderer>().material;
    }

    /// <summary>
    /// The red component of the mesh's color.
    /// </summary>
    [Binding]
    public float Red
    {
        get
        {
            if (mat != null)
                return mat.color.r;
            return 1;
        }
        set
        {
            if(mat==null)
                return;
            var color = mat.color;
            color.r = value;
            mat.color = color;
        }
    }

    /// <summary>
    /// The green component of the mesh's color.
    /// </summary>
    [Binding]
    public float Green
    {
        get
        {
            if (mat != null)
                return mat.color.g;
            return 1;
        }
        set
        {
            var color = mat.color;
            color.g = value;
            mat.color = color;
        }
    }

    /// <summary>
    /// The blue component of the mesh's color.
    /// </summary>
    [Binding]
    public float Blue
    {
        get
        {
            if (mat != null)
                return mat.color.b;
            return 1;
        }
        set
        {
            var color = mat.color;
            color.b = value;
            mat.color = color;
        }
    }
}