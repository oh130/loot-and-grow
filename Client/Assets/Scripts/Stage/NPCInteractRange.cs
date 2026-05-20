using UnityEngine;

public class NPCInteractRange : MonoBehaviour
{
    [SerializeField] private GameObject npc;

    private void OnTriggerEnter(Collider other)
    {
        if(npc.TryGetComponent<IInteractable>(out var component))
        {
            component.OnTriggerEnterInteractRange(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(npc.TryGetComponent<IInteractable>(out var component))
        {
            component.OnTriggerExitInteractRange(other);
        }
    }
}
