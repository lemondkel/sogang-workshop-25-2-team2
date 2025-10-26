using UnityEngine;
using DoorScript;
using UnityEngine.EventSystems;
using System.Collections;

// RequireComponent는 사용하지 않거나, 2개로 지정해야 하므로 일단 삭제
// [RequireComponent(typeof(AudioSource))]
public class GameManager : MonoBehaviour
{
    [Header("문 타겟")]
    [Tooltip("필요한 경우, 문 제어를 위한 레퍼런스입니다.")]
    public Door doorTarget;

    [Header("랜덤 이벤트 시간 설정 (초)")]
    [Tooltip("이벤트가 발생할 최소 대기 시간")]
    public float minDelay = 3f;
    [Tooltip("이벤트가 발생할 최대 대기 시간")]
    public float maxDelay = 7f;

    [Header("순차 이벤트 오브젝트 리스트")]
    [Tooltip("이벤트에 사용될, 하이어라키에서 참조할 게임 오브젝트들을 등록합니다. (순서대로 실행)")]
    public GameObject[] eventObjects;

    [Header("오디오 소스 (인스펙터에서 할당)")]
    [Tooltip("배경음악(BGM)을 재생할 AudioSource")]
    public AudioSource bgmSource;
    [Tooltip("효과음(SFX, 노크 등)을 재생할 AudioSource")]
    public AudioSource sfxSource;

    [Header("랜덤 이벤트 오디오 리스트")]
    [Tooltip("이벤트 발생 시 랜덤으로 재생할 사운드 클립들을 등록합니다.")]
    public AudioClip[] randomEventSounds;

    private bool isTimerRunning = false;
    private int currentEventIndex = 0;
    private bool wasDoorOpen = false;
    private bool isLetterActive = false; // 노크(이벤트) 발생 후, 플레이어가 해결해야 할 상태
    private bool canAdvanceIndex = false; // 인덱스 증가 자격이 있는지 확인


    void Start()
    {
        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
        }
        else
        {
            Debug.LogError("[GameManager] 'Sfx Source'가 인스펙터에 할당되지 않았습니다!");
        }

        if (bgmSource == null)
        {
            Debug.LogWarning("[GameManager] 'Bgm Source'가 할당되지 않았습니다. (배경음악이 없다면 정상)");
        }

        HideAllEventObjects();

        if (doorTarget != null)
        {
            wasDoorOpen = doorTarget.open;
        }
    }

    /// <summary>
    /// 문이 열리는 '순간' 호출되는 함수.
    /// </summary>
    private void OnDoorOpened()
    {
        // 1. 이벤트 해결 자격이 있는지 검사
        if (canAdvanceIndex)
        {
            Debug.Log("[GameManager] 문이 열렸습니다. 이벤트를 '해결'했습니다. 인덱스 증가!");

            if (sfxSource != null && sfxSource.isPlaying)
            {
                sfxSource.Stop();
                Debug.Log("[GameManager] 문을 열었으므로 노크 사운드를 중지합니다.");
            }

            if (currentEventIndex == 0)
            {
                isLetterActive = true;
                Debug.Log("[GameManager] 쪽지 이벤트 설정.");
            }

            currentEventIndex++;
            if (currentEventIndex >= eventObjects.Length)
            {
                currentEventIndex = 0;
                Debug.Log("[GameManager] 이벤트 인덱스가 0으로 리셋됩니다.");
            }
            Debug.Log($"[GameManager] 다음 이벤트 인덱스는 {currentEventIndex} 입니다.");

            canAdvanceIndex = false;
            isTimerRunning = false;
        }
        else
        {
            // 문은 열 수 있지만, 인덱스는 증가하지 않습니다.
            Debug.LogWarning($"[GameManager] 문이 열렸으나, 인덱스 증가 조건({canAdvanceIndex})을 충족하지 못했습니다. 인덱스({currentEventIndex})는 유지됩니다.");
        }
    }

    void Update()
    {
        // Android 백 버튼 로직
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[GameManager] Android Back Button Pressed. Quitting application.");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        bool isDoorCurrentlyOpen = (doorTarget != null && doorTarget.open);

        // 2. 문 닫힘 '순간' 감지
        if (wasDoorOpen && !isDoorCurrentlyOpen)
        {
            OnDoorClosed();
        }

        // 3. 문 열림 '순간' 감지
        // 문이 닫혀 있다가(wasDoorOpen=false) 열리는(isDoorCurrentlyOpen=true) 순간
        if (!wasDoorOpen && isDoorCurrentlyOpen)
        {
            OnDoorOpened(); // <-- 이벤트를 해결한 것으로 처리
        }

        wasDoorOpen = isDoorCurrentlyOpen;

        // 4. 이벤트 타이머 제어
        // 문이 닫혀있고, 이벤트가 활성화되지 않은 (해결된) 상태에서만 타이머 시작
        if (!isDoorCurrentlyOpen && !isTimerRunning && !isLetterActive)
        {
            StartCoroutine(RandomEventTimerRoutine());
        }
    }

    /// <summary>
    /// UI 버튼 등 외부에서 문을 열거나 닫기 위해 사용하는 함수.
    /// 문이 열려있든 닫혀있든 상관없이 여닫기 상호작용이 가능하게 합니다.
    /// </summary>
    public void ToggleDoor()
    {
        if (doorTarget == null) return;

        // 🚨이벤트가 활성화된 상태라면 문 상호작용을 차단합니다.
        if (isLetterActive && doorTarget != null && doorTarget.open)
        {
            Debug.LogWarning($"[GameManager] 문 잠김! 현재 이벤트({currentEventIndex})가 활성화되어 해결 전까지 문을 조작할 수 없습니다.");
            return; // 문 여닫기 로직을 실행하지 않고 종료
        }

        // isLetterActive가 false일 때만 정상적으로 문을 여닫습니다.
        doorTarget.OpenDoor();
        Debug.Log($"[GameManager] Door Toggle: {doorTarget.name} - Current State: {(doorTarget.open ? "Open" : "Closed")}.");
    }

    /// <summary>
    /// 랜덤 이벤트가 발생했을 때 호출되는 핵심 함수.
    /// </summary>
    public void TriggerRandomEvent()
    {
        // 문이 열려있으면 이벤트 발동 자체를 건너뜁니다.
        if (doorTarget != null && doorTarget.open)
        {
            Debug.LogWarning("[GameManager] 이벤트 발동 순간 문이 열려있어 건너뜁니다. 타이머가 리셋됩니다.");
            isTimerRunning = false;
            return;
        }
        canAdvanceIndex = true;

        PlayRandomSound();
        StartCoroutine(TimedObjectEvent());

        Debug.Log($"[GameManager] Custom Logic: 인덱스 {currentEventIndex}에 대한 즉각적인 로직을 실행합니다.");
        switch (currentEventIndex)
        {
            case 0:
                // 일반적인 흐름 유지
                Debug.Log("Custom Logic: 0번 이벤트 - 특별한 조치 없이 일반적인 흐름 유지.");
                break;

            case 1:
                // return을 사용하여 다음 이벤트 타이머 시작을 막음 (해결만 대기)
                Debug.Log("Custom Logic: 1번 이벤트 - 강제 return;을 사용하여 다음 이벤트 타이머 시작을 막고, 플레이어의 문 열기 해결만 대기합니다.");
                return;

            case 2:
                // 인덱스 2일 때 return을 사용하여 다음 이벤트 타이머 시작을 막습니다.
                Debug.Log("Custom Logic: 2번 이벤트 - 문 닫기 (다음 타이머 시작)를 막고 플레이어의 해결만 대기합니다.");
                return;

            default:
                Debug.Log($"Custom Logic: {currentEventIndex}번 이벤트 - 별도의 커스텀 액션 없음.");
                break;
        }

        Debug.Log($"[GameManager] 순차 이벤트 발생! (인덱스: {currentEventIndex}) -> 해결 대기 상태로 전환됨.");
    }

    /// <summary>
    /// 모든 이벤트 오브젝트를 즉시 비활성화(숨기기)합니다.
    /// </summary>
    private void HideAllEventObjects()
    {
        if (eventObjects == null) return;
        foreach (GameObject obj in eventObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 문이 닫히는 '순간' 호출되는 함수.
    /// </summary>
    private void OnDoorClosed()
    {
        Debug.Log($"[GameManager] 문이 닫혔습니다. (다음 이벤트 인덱스: {currentEventIndex})");

        if (isLetterActive)
        {
            switch (currentEventIndex)
            {
                case 0:
                    Debug.Log("문 닫힘 로직: 0번 이벤트 - 문 닫힘 상태 유지 확인.");
                    break;
                case 1:
                    Debug.Log("문 닫힘 로직: 1번 이벤트 - 문 닫힘 상태에 따른 추가 연출 대기.");
                    break;
                case 2:
                    Debug.Log("문 닫힘 로직: 2번 이벤트 - 문 닫힘 상태에 따른 추가 연출 대기.");
                    break;
                default:
                    Debug.Log($"문 닫힘 로직: {currentEventIndex}번 이벤트 - 기본 로직.");
                    break;
            }
        }
    }


    /// <summary>
    /// '순차' 오브젝트를 선택하고 *다음 타이머를 위해 리셋*합니다.
    /// </summary>
    private IEnumerator TimedObjectEvent()
    {
        // 1. 이전 이벤트를 위해 켜져 있던 오브젝트를 모두 숨깁니다.
        HideAllEventObjects();

        if (eventObjects == null || eventObjects.Length == 0)
        {
            Debug.LogWarning("[GameManager] 이벤트 오브젝트가 없습니다. 비주얼 이벤트를 건너뛰고 타이머를 리셋합니다.");
            isTimerRunning = false;
            isLetterActive = false; // 이벤트가 없으므로 활성 상태도 리셋
            yield break;
        }

        // 2. '현재 인덱스'로 오브젝트 선택
        GameObject sequentialObj = eventObjects[currentEventIndex];

        // 3. 선택된 오브젝트 활성화 (노출)
        if (sequentialObj != null)
        {
            sequentialObj.SetActive(true);
            Debug.Log($"[GameManager] 오브젝트 활성화 (유지): {sequentialObj.name}");
        }
        else
        {
            Debug.LogWarning($"[GameManager] 선택된 {currentEventIndex}번 오브젝트가 Null입니다.");
        }

        // 4. 타이머 플래그를 false로 내립니다.
        // isEventActive가 true이므로, Update()에서 새 타이머를 시작하지 않고 해결을 기다립니다.
        isTimerRunning = false;

        yield return null;
    }


    /// <summary>
    /// 등록된 오디오 클립 중 하나를 랜덤으로 선택하여 재생합니다.
    /// </summary>
    private void PlayRandomSound()
    {
        if (randomEventSounds == null || randomEventSounds.Length == 0)
        {
            Debug.LogWarning("[GameManager] 재생할 오디오 클립이 리스트에 없습니다. 오디오를 건너킵니다.");
            return;
        }

        if (sfxSource == null) return;

        // 이벤트 발동 시 기존 SFX를 멈추고 새 사운드를 재생 (중첩 방지)
        if (sfxSource.isPlaying)
        {
            sfxSource.Stop();
            Debug.Log("[GameManager] 반복 이벤트를 위해 기존 사운드를 중지합니다.");
        }

        int randomIndex = Random.Range(0, randomEventSounds.Length);
        AudioClip clipToPlay = randomEventSounds[randomIndex];

        sfxSource.PlayOneShot(clipToPlay);

        Debug.Log($"[GameManager] 랜덤 사운드 재생: {clipToPlay.name}");
    }

    /// <summary>
    /// 랜덤한 시간 동안 '문이 닫혀있는지 매 프레임 검사하며' 대기한 후
    /// TriggerRandomEvent()를 호출하는 코루틴.
    /// </summary>
    IEnumerator RandomEventTimerRoutine()
    {
        isTimerRunning = true;
        float randomDelay = Random.Range(minDelay, maxDelay);
        float timer = 0f;

        Debug.Log($"[GameManager] 다음 이벤트(인덱스: {currentEventIndex})까지 {randomDelay:F2}초 대기 시작...");

        // randomDelay 시간만큼 대기하되, 1프레임마다 문 상태를 검사
        while (timer < randomDelay)
        {
            // 1. 대기 중에 문이 열렸는지 확인
            if (doorTarget != null && doorTarget.open)
            {
                Debug.Log("[GameManager] 문이 열려서 이벤트 타이머를 중단/리셋합니다.");
                isTimerRunning = false;
                yield break; // 코루틴(타이머) 즉시 종료
            }

            // 2. 문이 닫혀있다면 타이머 시간 누적
            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 1프레임 대기
        }

        // 3. while 루프를 무사히 통과했다면 (시간이 다 지났고 문도 계속 닫혀있었다면)
        Debug.Log("[GameManager] 타이머 완료. 이벤트 발동.");
        TriggerRandomEvent();
    }
}
