using UnityEngine;

public class TargetFindArea : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private AIPlayer aiPlayer;
    [SerializeField] private bool isEnemy = true;
    [SerializeField] private LayerMask targetLayerMask;

    private void OnTriggerStay(Collider other)
    {
        if((targetLayerMask.value & (1 << other.gameObject.layer)) == 0) return;
        if(other.gameObject == enemy?.gameObject || other.gameObject == aiPlayer?.gameObject) return;

        if(isEnemy)
            enemy.OnTriggerStayInTargetFindArea(other);
        else
            aiPlayer.OnTriggerStayInTargetFindArea(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if((targetLayerMask.value & (1 << other.gameObject.layer)) == 0) return;
        if(other.gameObject == enemy?.gameObject || other.gameObject == aiPlayer?.gameObject) return;

        if(isEnemy)
            enemy.OnTriggerExitInTargetFindArea(other);
        else
            aiPlayer.OnTriggerExitInTargetFindArea(other);
    }
}
