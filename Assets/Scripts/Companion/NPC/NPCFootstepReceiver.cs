
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class NPCFootstepReceiver : MonoBehaviour
{
    [SerializeField] private AudioClip footstepSound;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void OnFootstep(AnimationEvent animationEvent)
    {

    }
}
