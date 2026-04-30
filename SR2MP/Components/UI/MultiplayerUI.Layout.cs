namespace SR2MP.Components.UI;

public sealed partial class MultiplayerUI
{
    private Rect previousLayoutRect;
    private Rect previousLayoutChatRect;
    private int previousLayoutHorizontalIndex;
    private float layoutOriginX = 6f;
    private float layoutMaxWidth = WindowWidth - (HorizontalSpacing * 2);

    private void ResetWindowLayout(Rect windowRect)
    {
        layoutOriginX = windowRect.x;
        layoutMaxWidth = windowRect.width - (HorizontalSpacing * 2);
        previousLayoutRect = new Rect(windowRect.x, windowRect.y + WindowHeaderHeight, windowRect.width, 0);
        previousLayoutHorizontalIndex = 0;
    }

    private void DrawText(string text, int horizontalShare = 1, int horizontalIndex = 0)
    {
        GUI.Label(CalculateTextLayout(6, text, horizontalShare, horizontalIndex), text);
    }

    private Rect CalculateTextLayout(float originalX, string text, int horizontalShare = 1, int horizontalIndex = 0)
    {
        var style = GUI.skin.label;
        var height = style.CalcHeight(new GUIContent(text), layoutMaxWidth / horizontalShare);

        float x = layoutOriginX + HorizontalSpacing;
        float y = previousLayoutRect.y;
        float w = layoutMaxWidth / horizontalShare;
        float h = height;

        x += horizontalIndex * w;

        if (horizontalIndex <= previousLayoutHorizontalIndex)
            y += previousLayoutRect.height + SpacerHeight;

        var result = new Rect(x, y, w, h);

        previousLayoutHorizontalIndex = horizontalIndex;
        previousLayoutRect = result;

        return result;
    }

    private Rect CalculateInputLayout(float originalX, int horizontalShare = 1, int horizontalIndex = 0)
    {
        float x = layoutOriginX + HorizontalSpacing;
        float y = previousLayoutRect.y;
        float w = layoutMaxWidth / horizontalShare;
        const float h = InputHeight;

        x += horizontalIndex * w;

        if (horizontalIndex <= previousLayoutHorizontalIndex)
            y += previousLayoutRect.height + SpacerHeight;

        var result = new Rect(x, y, w, h);

        previousLayoutHorizontalIndex = horizontalIndex;
        previousLayoutRect = result;

        return result;
    }

    private Rect CalculateButtonLayout(float originalX, int horizontalShare = 1, int horizontalIndex = 0)
    {
        float x = layoutOriginX + HorizontalSpacing;
        float y = previousLayoutRect.y;
        float w = layoutMaxWidth / horizontalShare;
        const float h = ButtonHeight;

        x += horizontalIndex * w;

        if (horizontalIndex <= previousLayoutHorizontalIndex)
            y += previousLayoutRect.height + SpacerHeight;

        var result = new Rect(x, y, w, h);

        previousLayoutHorizontalIndex = horizontalIndex;
        previousLayoutRect = result;

        return result;
    }
}
