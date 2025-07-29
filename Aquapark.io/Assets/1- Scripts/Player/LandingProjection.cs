using UnityEngine;

public class LandingProjection : MonoBehaviour
{
    [SerializeField] private Transform raycaster, projection;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float rayDistance;

    private void Update()
    {
        RaycastHit hit;
        if (Physics.Raycast(raycaster.position, raycaster.forward * rayDistance, out hit))
        {
            projection.position = hit.point + offset;
        }
        else
        {
            projection.position = raycaster.position + raycaster.forward * rayDistance;
        }
    }
}
