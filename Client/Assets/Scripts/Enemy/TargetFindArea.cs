using UnityEngine;

public class TargetFindArea : MonoBehaviour
{
    [SerializeField] private Enemy enemy;

    private void OnTriggerStay(Collider other)
    {
        enemy.OnTriggerStayInTargetFindArea(other);
    }

    private void OnTriggerExit(Collider other)
    {
        enemy.OnTriggerExitInTargetFindArea(other);
    }
}
