public interface IUIBase
{
    void SetRoot(UI_Root parent);
    void Open();
    void Close();
    bool BIsOpened { get; }
    bool BIsSearchedByTypeHash { get; }
}