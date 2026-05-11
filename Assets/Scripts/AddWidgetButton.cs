using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AddWidgetButton : MonoBehaviour
{
    Button button;

    void Awake()
    {
        #if UNITY_IOS
        Destroy(gameObject);
        return;
        #endif

        button = GetComponent<Button>();
        button.onClick.AddListener(Button_OnClick);
    }

    void Button_OnClick()
    {
        WidgetUtility.RequestPinWidget();
    }
}
