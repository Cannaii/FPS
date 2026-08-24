using UnityEngine.Events;

namespace cowsins
{
    // Implemented by InteractManager and required by PlayerDependencies
    public interface IInteractManagerProvider
    {
        float ProgressElapsed { get; }
        bool Inspecting { get; }
        Interactable HighlightedInteractable { get; }
        bool DuplicateWeaponAddsBullets { get; }
    }

    public interface IInteractEventsProvider
    {
        InteractManagerEvents Events { get; }
    }
    public class InteractManagerEvents
    {
        public UnityEvent<string> OnAllowedInteraction = new UnityEvent<string>();
        public UnityEvent<float> OnInteractionProgressChanged = new UnityEvent<float>();
        public UnityEvent OnPerformInteraction = new UnityEvent();
        public UnityEvent OnFinishInteraction = new UnityEvent();
        public UnityEvent OnDisableInteraction = new UnityEvent();
        public UnityEvent OnForbiddenInteraction = new UnityEvent();

        public UnityEvent OnBulletsPickedUp = new UnityEvent();
        public UnityEvent<Attachment> OnAttachmentPickedUp = new UnityEvent<Attachment>();
        public UnityEvent OnDrop = new UnityEvent();

        public UnityEvent<bool> OnStartRealtimeInspection = new UnityEvent<bool>();
        public UnityEvent OnStopInspect = new UnityEvent();
        public UnityEvent<bool> OnInspectionUIRefreshRequested = new UnityEvent<bool>();
    }
}