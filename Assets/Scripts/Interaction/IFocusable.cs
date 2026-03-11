/// <summary>
/// Интерфейс для объектов, на которые можно навести взгляд.
/// Реализуют: InteractableObject (жёлтая обводка, переключение состояний)
///            InspectableObject (голубая обводка, осмотр/сбор)
///
/// InteractionRaycaster работает с IFocusable для подсветки,
/// а PlayerController проверяет конкретный тип для выбора действия по E.
/// </summary>
public interface IFocusable
{
    void SetHighlight(bool enabled);
}
