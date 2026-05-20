using UnityEngine;

public class PooledAudioSource : MonoBehaviour
{
    [SerializeField] private AudioSource source;

    private void Update()
    {
        if (!source.isPlaying)
        {
            gameObject.SetActive(false);
        }
    }
}