using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI winPanel;

    public void ShowWinScreen()
    {
        winPanel.text = "Дверь открыта";
    }
}