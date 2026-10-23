using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

internal sealed class NPCInteractionController
{
    public bool IsPlayerClose { get; private set; }

    public bool UpdateProximity(
        Transform playerTransform,
        Transform interactionTarget,
        float interactionDistance,
        out float distance)
    {
        distance = Vector3.Distance(playerTransform.position, interactionTarget.position);
        bool shouldBeClose = distance < interactionDistance;
        if (shouldBeClose == IsPlayerClose)
        {
            return false;
        }

        IsPlayerClose = shouldBeClose;
        return true;
    }

    public bool IsInteractKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    public bool IsCancelKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    public void Reset()
    {
        IsPlayerClose = false;
    }
}
