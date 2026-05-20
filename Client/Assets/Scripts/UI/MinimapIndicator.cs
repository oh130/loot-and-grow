using UnityEngine;

public class MinimapIndicator : MonoBehaviour
{
    // private void Awake()
    // {
    //     if (transform.parent != null)
    //     {
    //         Vector3 parentScale = transform.parent.localScale;
    //         transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
    //     }
    // }

    private void Update()
    {
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, 42, transform.position.z);
    }
}
