using System.Collections.Generic;
using UnityEngine;

namespace Ryx.Core.Tutorials
{

public class InteractWithObjectTrigger : TutorialTrigger
{
    [Header("Interaction Settings")]
    [Tooltip("Tags of objects that can trigger completion when interacted with")]
    public List<string> targetTags = new List<string>();

    [Tooltip("Whether to require all tagged objects to be interacted with")]
    public bool requireAllObjects = false;

    [Tooltip("Distance at which player can interact with objects")]
    public float interactionDistance = 3.0f;

    [Tooltip("Layer mask for interaction raycast")]
    public LayerMask interactionLayerMask = -1;

    private readonly HashSet<string> interactedObjectIds = new HashSet<string>();
    private Camera playerCamera;

    protected override void OnInitialize()
    {
        interactedObjectIds.Clear();
        playerCamera = Camera.main;
    }

    protected override void DetectTriggerCompletion()
    {
        if (!playerCamera || targetTags.Count == 0)
            return;

        if (TutorialPointerInput.TryGetPointerPressThisFrame(out var pointerState) && !IsPointerOverUI())
        {
            Ray ray = playerCamera.ScreenPointToRay(pointerState.ScreenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayerMask))
            {
                GameObject hitObject = hit.collider.gameObject;
                foreach (string tag in targetTags)
                {
                    if (hitObject.CompareTag(tag))
                    {
                        string objectId = hitObject.GetInstanceID().ToString();
                        interactedObjectIds.Add(objectId);

                        if (!requireAllObjects || interactedObjectIds.Count >= targetTags.Count)
                        {
                            TriggerComplete();
                        }
                        break;
                    }
                }
            }
        }
    }

    public override void ResetTrigger()
    {
        base.ResetTrigger();
        interactedObjectIds.Clear();
    }
}

}
