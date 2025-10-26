using UnityEngine;
using UnityEngine.EventSystems; // UI 클릭 감지를 위해 필요

/// <summary>
/// 이 스크립트가 부착된 UI 요소를 클릭하면 GameManager의 ToggleDoor() 함수를 호출합니다.
/// 문 클릭과 동일한 효과를 내기 위함입니다.
/// </summary>
public class UIToggleDoorClicker : MonoBehaviour, IPointerClickHandler
{
    private GameManager gameManager;
    private bool isInitialized = false;

    void Start()
    {
        // 씬에서 GameManager 인스턴스를 찾습니다.
        gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("[UIToggleDoorClicker] GameManager를 찾을 수 없습니다! UI 클릭 로직이 작동하지 않습니다.");
        }
        else
        {
            isInitialized = true;
        }
    }

    // UI 요소를 클릭했을 때 호출됩니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInitialized) return;

        Debug.Log($"[UIToggleDoorClicker] UI 클릭 감지: 문 클릭과 동일한 ToggleDoor() 호출.");

        gameManager.ToggleDoor();
    }
}