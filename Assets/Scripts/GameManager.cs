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

    [Header("쪽지 리스트")]
    public GameObject[] letterObjects;

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
    private bool isAwaitingLetterSolution = false; // 쪽지 이벤트 활성화 상태 (쪽지 클릭 대기)
    private bool canAdvanceIndex = false; // 인덱스 증가 자격 (노크 사운드 발생 시 true)

    // 쪽지 진행 상황을 추적
    private int currentLetterIndex = 0;


    void Start()
    {
        currentLetterIndex = 0;

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

        // LetterClickHandler 컴포넌트 추가 확인은 이전과 동일하게 유지
        foreach (GameObject letter in letterObjects)
        {
            if (letter != null && letter.GetComponent<LetterClickHandler>() == null)
            {
                letter.AddComponent<LetterClickHandler>();
                Debug.LogWarning($"[GameManager] {letter.name}에 LetterClickHandler가 없어 자동으로 추가했습니다.");
            }
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
            Debug.Log("[GameManager] 문이 열렸습니다. 이벤트를 '해결'했습니다. 인덱스 증가 준비!");

            if (sfxSource != null && sfxSource.isPlaying)
            {
                sfxSource.Stop();
                Debug.Log("[GameManager] 문을 열었으므로 노크 사운드를 중지합니다.");
            }

            // 쪽지 이벤트를 활성화합니다.
            Debug.Log(currentEventIndex);
            Debug.Log(isAwaitingLetterSolution);
            if (currentEventIndex == 0 && !isAwaitingLetterSolution)
            {
                ShowNextLetter(); // 첫 쪽지를 띄웁니다.
            }
            // 쪽지 이벤트가 아닌 다른 일반 이벤트라면 바로 인덱스를 진행시킵니다.
            else if (!isAwaitingLetterSolution)
            {
                // 일반적인 이벤트 해결 (문 열기)
                currentEventIndex++;
                if (currentEventIndex >= eventObjects.Length)
                {
                    currentEventIndex = 0;
                    Debug.Log("[GameManager] 이벤트 인덱스가 0으로 리셋됩니다.");
                }
                Debug.Log($"[GameManager] 다음 이벤트 인덱스는 {currentEventIndex} 입니다.");
            }
            // 쪽지 이벤트를 기다리는 중이라면, 문을 열어도 인덱스 증가 없이 상태만 유지합니다.

            canAdvanceIndex = false; // 인덱스 증가 자격 초기화
            isTimerRunning = false;  // 다음 타이머 대기 시작
        }
        else
        {
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
        if (!wasDoorOpen && isDoorCurrentlyOpen)
        {
            OnDoorOpened(); // <-- 이벤트를 해결한 것으로 처리
        }

        wasDoorOpen = isDoorCurrentlyOpen;

        // 4. 이벤트 타이머 제어
        // isAwaitingLetterSolution 상태가 아닐 때만 타이머 시작
        if (!isDoorCurrentlyOpen && !isTimerRunning && !isAwaitingLetterSolution)
        {
            StartCoroutine(RandomEventTimerRoutine());
        }
    }

    /// <summary>
    /// UI 버튼 등 외부에서 문을 열거나 닫기 위해 사용하는 함수.
    /// 쪽지 이벤트 중에는 열린 문을 닫지 못하도록 차단합니다.
    /// </summary>
    public void ToggleDoor()
    {
        if (doorTarget == null) return;

        // 🚨쪽지 이벤트가 활성화되었고 문이 열려있다면, 문을 닫는 시도를 막습니다.
        if (isAwaitingLetterSolution && doorTarget.open)
        {
            Debug.LogWarning("[GameManager] 쪽지를 해결하기 전에는 문을 닫을 수 없습니다. (쪽지 클릭 필요)");
            return;
        }

        // 문 상호작용
        doorTarget.OpenDoor();
        Debug.Log($"[GameManager] Door Toggle: {doorTarget.name} - Current State: {(doorTarget.open ? "Open" : "Closed")}.");
    }

    /// <summary>
    /// 문이 열린 후, 쪽지 이벤트가 필요한 경우 다음 쪽지를 활성화하고 대기 상태로 전환합니다.
    /// </summary>
    private void ShowNextLetter()
    {
        if (letterObjects == null || currentLetterIndex >= letterObjects.Length)
        {
            Debug.LogWarning("[GameManager] 더 이상 활성화할 쪽지가 없습니다. 다음 이벤트로 진행합니다.");

            // 모든 쪽지를 다 띄웠다면 다음 이벤트로 넘어가도록 처리
            currentEventIndex++;
            if (currentEventIndex >= eventObjects.Length) currentEventIndex = 0;
            isAwaitingLetterSolution = false; // 대기 상태 해제
            return;
        }

        // 쪽지 활성화
        GameObject letter = letterObjects[currentLetterIndex];
        if (letter != null)
        {
            Debug.Log(letter);
            letter.SetActive(true);
            isAwaitingLetterSolution = true; // 쪽지 활성화 후, 플레이어의 클릭 해결을 기다립니다.
            Debug.Log($"[GameManager] 쪽지 #{currentLetterIndex} 활성화. 플레이어의 클릭 대기.");
        }
        else
        {
            currentLetterIndex++; // null이면 다음 인덱스로 건너뜁니다.
            ShowNextLetter(); // 재귀 호출하여 다음 쪽지를 시도
        }
    }

    /// <summary>
    /// LetterClickHandler 스크립트에 의해 쪽지 클릭 후 호출됩니다.
    /// </summary>
    public void HandleLetterDeactivated()
    {
        Debug.Log($"[GameManager] 쪽지 #{currentLetterIndex}가 해결되었습니다. 다음 쪽지/이벤트 진행.");
        currentLetterIndex++; // 다음 쪽지 준비

        // 1. 모든 쪽지를 다 해결했는지 확인
        if (currentLetterIndex >= letterObjects.Length)
        {
            Debug.Log($"[GameManager] 모든 쪽지 해결. 쪽지 이벤트 종료. 다음 문 닫기 대기.");
            isAwaitingLetterSolution = false; // 쪽지 대기 상태 해제 -> 이제 문을 닫을 수 있게 됨
            currentLetterIndex = 0; // 쪽지 인덱스 리셋

            // 다음 이벤트 인덱스 증가
            currentEventIndex++;
            if (currentEventIndex >= eventObjects.Length) currentEventIndex = 0;
            Debug.Log($"[GameManager] 다음 이벤트 인덱스: {currentEventIndex}. 문을 닫아주세요.");
        }
        else
        {
            // 2. 아직 남은 쪽지가 있다면 다음 쪽지를 바로 활성화
            ShowNextLetter();
        }
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
        canAdvanceIndex = true; // 문을 열면 해결할 자격 부여

        PlayRandomSound();
        StartCoroutine(TimedObjectEvent());

        Debug.Log($"[GameManager] Custom Logic: 인덱스 {currentEventIndex}에 대한 즉각적인 로직을 실행합니다.");
        switch (currentEventIndex)
        {
            case 0:
                // 일반적인 흐름 유지 (문 열기 대기)
                Debug.Log("Custom Logic: 0번 이벤트 - 특별한 조치 없이 일반적인 흐름 유지.");
                break;
            case 1:
                // return을 사용하여 다음 이벤트 타이머 시작을 막음
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
    /// 모든 이벤트 오브젝트 및 쪽지를 즉시 비활성화(숨기기)합니다.
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
        if (letterObjects == null) return;
        foreach (GameObject obj in letterObjects)
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

        // 쪽지 이벤트 대기 상태에서는 문 닫힘 로직이 필요하면 여기에 추가
        if (isAwaitingLetterSolution)
        {
            Debug.Log("문 닫힘 로직: 쪽지 이벤트를 해결할 때까지 대기.");
        }
    }


    /// <summary>
    /// '순차' 오브젝트를 선택하고 *다음 타이머를 위해 리셋*합니다.
    /// </summary>
    private IEnumerator TimedObjectEvent()
    {
        HideAllEventObjects();

        if (eventObjects == null || eventObjects.Length == 0)
        {
            Debug.LogWarning("[GameManager] 이벤트 오브젝트가 없습니다. 비주얼 이벤트를 건너뛰고 타이머를 리셋합니다.");
            isTimerRunning = false;
            isAwaitingLetterSolution = false;
            yield break;
        }

        GameObject sequentialObj = eventObjects[currentEventIndex];

        if (sequentialObj != null)
        {
            sequentialObj.SetActive(true);
            Debug.Log($"[GameManager] 오브젝트 활성화 (유지): {sequentialObj.name}");
        }
        else
        {
            Debug.LogWarning($"[GameManager] 선택된 {currentEventIndex}번 오브젝트가 Null입니다.");
        }

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
