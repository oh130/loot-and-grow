using UnityEngine;

public interface IInteractable
{
    void Interact();
    void OnTriggerEnterInteractRange(Collider other);
    void OnTriggerExitInteractRange(Collider other);
}
