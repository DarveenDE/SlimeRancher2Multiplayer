namespace SR2MP.Components.UI;

public sealed partial class MultiplayerUI
{
    private const string UsernameInputName = "UsernameInput";
    private const string IpInputName = "IpInput";
    private const string PortInputName = "PortInput";
    private const string HostPortInputName = "HostPortInput";

    private string focusedTextInput = string.Empty;

    private string DrawTextInput(Rect rect, string value, int maxLength, string controlName)
    {
        GUI.Box(rect, string.Empty);

        bool focused = focusedTextInput == controlName;
        string displayValue = value;
        if (focused && Mathf.FloorToInt(global::UnityEngine.Time.realtimeSinceStartup * 2) % 2 == 0)
        {
            displayValue += "|";
        }

        GUI.Label(new Rect(rect.x + 4, rect.y + 2, rect.width - 8, rect.height - 4), displayValue);

        var currentEvent = Event.current;
        if (currentEvent == null)
            return value;

        if (currentEvent.type == EventType.MouseDown)
        {
            if (IsInside(rect, currentEvent.mousePosition))
            {
                focusedTextInput = controlName;
                currentEvent.Use();
            }

            return value;
        }

        if (!focused || currentEvent.type != EventType.KeyDown)
            return value;

        if (currentEvent.keyCode == KeyCode.Backspace)
        {
            currentEvent.Use();
            return value.Length > 0 ? value[..^1] : value;
        }

        if (currentEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape or KeyCode.Tab)
            return value;

        char character = currentEvent.character;
        if (!char.IsControl(character) && value.Length < maxLength)
        {
            currentEvent.Use();
            return value + character;
        }

        return value;
    }

    private static bool IsInside(Rect rect, Vector2 point)
    {
        return point.x >= rect.x &&
               point.x <= rect.x + rect.width &&
               point.y >= rect.y &&
               point.y <= rect.y + rect.height;
    }
}
