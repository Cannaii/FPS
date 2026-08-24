#if UNITY_EDITOR
namespace cowsins
{
    public interface ITab
    {
        string TabName { get; }
        void OnGUI();
        void StartTab();
    }
}
#endif