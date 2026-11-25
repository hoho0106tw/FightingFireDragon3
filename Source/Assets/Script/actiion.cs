using UnityEngine;
using UnityEngine.InputSystem;

public class actiion : MonoBehaviour
{
    private const float MovementPerFrame = 0.1f;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 direction = ReadArrowKeys(keyboard);
            transform.Translate((Vector3)direction * MovementPerFrame, Space.World);

            foreach (var key in keyboard.allKeys)
            {
                if (key.wasPressedThisFrame)
                    Debug.Log($"鍵盤按下 {key.displayName}");
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
            Debug.Log("滑鼠按下左鍵");

        if (mouse.rightButton.wasPressedThisFrame)
            Debug.Log("滑鼠按下右鍵");

        if (mouse.middleButton.wasPressedThisFrame)
            Debug.Log("滑鼠按下中鍵");

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            Debug.Log($"滑鼠滾輪 {scroll}");
    }

    public static Vector2 ReadArrowKeys(Keyboard keyboard)
    {
        int horizontal = (keyboard.rightArrowKey.isPressed ? 1 : 0)
            - (keyboard.leftArrowKey.isPressed ? 1 : 0);
        int vertical = (keyboard.upArrowKey.isPressed ? 1 : 0)
            - (keyboard.downArrowKey.isPressed ? 1 : 0);
        return new Vector2(horizontal, vertical).normalized;
    }
}
