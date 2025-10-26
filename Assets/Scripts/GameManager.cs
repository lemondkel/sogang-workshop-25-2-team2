using UnityEngine;
using DoorScript;
using System.Collections;
using System.Linq;

public class GameManager : MonoBehaviour
{
    [Header("문 타겟")]
    [Tooltip("문 제어를 위한 레퍼런스입니다.")]
    public Door doorTarget;

    [Header("랜덤 이벤트 시간 설정 (초)")]
    public float minDelay = 3f;
    public float maxDelay = 7f;

    [Header("순차 이벤트 오브젝트 리스트 (땅에 떨어진 쪽지 등)")]
    public GameObject[] eventObjects;

    [Header("쪽지 다이얼로그 UI 리스트 (클릭 후 뜨는 화면)")]
    public GameObject[] letterObjects;

    [Header("문 열림으로 쪽지를 띄울 이벤트 인덱스")]
    public int[] letterEventIndices = { 0, 2, 3, 6 };

    [Header("오디오 소스 (인스펙터에서 할당)")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("랜덤 이벤트 오디오 리스트")]
    public AudioClip[] randomEventSounds;

    private bool isTimerRunning = false;
    private int currentEventIndex = 0;
    private bool wasDoorOpen = false;

    private bool isAwaitingSolution = false;
    private bool canAdvanceIndex = false;

    private bool isLetterUIDisplayed = false;
    private bool isFinalEventSolved = false;


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

        HideAllObjects();

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
        if (canAdvanceIndex)
        {
            Debug.Log("[GameManager] 문이 열렸습니다. 이벤트를 '해결'했습니다. 인덱스 증가 준비!");

            if (sfxSource != null && sfxSource.isPlaying)
            {
                sfxSource.Stop();
                Debug.Log("[GameManager] 문을 열었으므로 노크 사운드를 중지합니다.");
            }

            bool isLetterEvent = letterEventIndices.Contains(currentEventIndex);

            if (isLetterEvent)
            {
                bool success = ShowEventObjectOnly();

                if (success)
                {
                    isAwaitingSolution = true;
                    Debug.Log("[GameManager] 물리 오브젝트 활성화 성공. 문을 닫으려 시도하여 UI를 켜야 합니다.");
                }
                else
                {
                    isAwaitingSolution = false;
                    HandleGameEndCheck(ref currentEventIndex);
                    Debug.Log($"[GameManager] 오브젝트 로딩 실패! 이벤트를 건너뛰고 다음 인덱스({currentEventIndex})로 진행합니다.");
                }
            }
            else
            {
                HandleGameEndCheck(ref currentEventIndex);
                Debug.Log($"[GameManager] 다음 이벤트 인덱스는 {currentEventIndex} 입니다.");
                isAwaitingSolution = false;
            }
            canAdvanceIndex = false;
            isTimerRunning = false;
        }
        else
        {
            Debug.LogWarning($"[GameManager] 문이 열렸으나, 인덱스 증가 조건({canAdvanceIndex})을 충족하지 못했습니다. 인덱스({currentEventIndex})는 유지됩니다.");
        }
    }

    // 게임 종료/리셋 체크
    private void HandleGameEndCheck(ref int index)
    {
        if (index == eventObjects.Length - 1)
        {
            Debug.Log("[GameManager] 💥 마지막 이벤트 완료 준비. 다음 동작은 리셋입니다. 💥");
            isFinalEventSolved = true;
            index = 0; // 다음 루프를 위해 인덱스를 0으로 설정 (ResetToStart에서 최종 처리)
        }
        else
        {
            index++;
            if (index >= eventObjects.Length)
            {
                index = 0;
                Debug.Log("[GameManager] 경고: 비정상적인 인덱스 리셋.");
            }
        }
    }

    // 게임 초기화 (Start()와 유사한 역할)
    private void ResetToStart()
    {
        currentEventIndex = 0;
        isTimerRunning = false;
        isAwaitingSolution = false;
        isLetterUIDisplayed = false;
        canAdvanceIndex = false;
        isFinalEventSolved = false;

        if (doorTarget != null && doorTarget.open)
        {
            doorTarget.OpenDoor();
            wasDoorOpen = false;
        }

        HideAllObjects();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[GameManager] Android Back Button Pressed. Quitting application.");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        bool isDoorCurrentlyOpen = (doorTarget != null && doorTarget.open);

        if (wasDoorOpen && !isDoorCurrentlyOpen)
        {
            OnDoorClosed();
        }
        if (!wasDoorOpen && isDoorCurrentlyOpen)
        {
            OnDoorOpened();
        }
        wasDoorOpen = isDoorCurrentlyOpen;

        if (!isDoorCurrentlyOpen && !isTimerRunning && !isAwaitingSolution)
        {
            StartCoroutine(RandomEventTimerRoutine());
        }
    }

    /// <summary>
    /// 문 클릭으로 모든 상호작용을 처리합니다.
    /// </summary>
    public void ToggleDoor()
    {
        if (doorTarget == null) return;

        if (isFinalEventSolved && !doorTarget.open)
        {
            Debug.Log("[GameManager] 최종 이벤트 완료 후, 새로운 시작을 위해 리셋합니다.");
            ResetToStart();

            // ResetToStart가 문을 닫으므로, ToggleDoor의 나머지 코드는 문을 열게 됩니다.
        }

        if (isAwaitingSolution && doorTarget.open)
        {
            if (!isLetterUIDisplayed)
            {
                ShowLetterUIOnly();
                Debug.Log("[GameManager] 문 닫기 차단! UI 다이얼로그 활성화.");
                return;
            }
            else
            {
                HandleLetterEventSolved();
                Debug.Log("[GameManager] UI 닫고 이벤트 해결 완료. 문 닫기 허용.");
            }
        }

        doorTarget.OpenDoor();
        Debug.Log($"[GameManager] Door Toggle: {doorTarget.name} - Current State: {(doorTarget.open ? "Open" : "Closed")}.");
    }

    /// <summary>
    /// 문 열림 시 현재 인덱스에 맞는 물리 오브젝트만 활성화합니다.
    /// </summary>
    private bool ShowEventObjectOnly()
    {
        if (currentEventIndex >= 0 && currentEventIndex < eventObjects.Length && eventObjects[currentEventIndex] != null)
        {
            eventObjects[currentEventIndex].SetActive(true);
            return true;
        }
        Debug.LogError($"[GameManager] Event Object 활성화 실패! 인덱스: {currentEventIndex}가 NULL이거나 배열 범위 오류.");
        return false;
    }

    /// <summary>
    /// 문 닫기 시도 시, 쪽지 UI (다이얼로그)만 활성화합니다.
    /// </summary>
    private void ShowLetterUIOnly()
    {
        int mappedIndex = System.Array.IndexOf(letterEventIndices, currentEventIndex);

        if (mappedIndex >= 0 && mappedIndex < letterObjects.Length && letterObjects[mappedIndex] != null)
        {
            letterObjects[mappedIndex].SetActive(true);
            isLetterUIDisplayed = true;

            Debug.Log($"[GameManager] Letter UI 활성화 성공! Event Index: {currentEventIndex} -> Mapped Letter Index (Key): {mappedIndex}");
        }
        else
        {
            Debug.LogError($"[GameManager] Letter UI 활성화 실패! Event Index {currentEventIndex}에 대한 Mapped Index {mappedIndex}가 NULL이거나 배열 범위({letterObjects.Length}) 오류입니다. 강제 해결 처리.");
            HandleLetterEventSolved();
        }
    }

    /// <summary>
    /// 쪽지 이벤트를 최종적으로 해결하고 문 잠금을 해제합니다. (문 클릭 시 호출됨)
    /// </summary>
    public void HandleLetterEventSolved()
    {
        Debug.Log($"[GameManager] 문 클릭으로 쪽지 이벤트 해결 완료. 인덱스 {currentEventIndex} 처리 시작.");

        int mappedIndex = System.Array.IndexOf(letterEventIndices, currentEventIndex);

        if (currentEventIndex >= 0 && currentEventIndex < eventObjects.Length && eventObjects[currentEventIndex] != null)
        {
            eventObjects[currentEventIndex].SetActive(false);
            Debug.Log($"[GameManager DEBUG] eventObjects[{currentEventIndex}] 비활성화 완료.");
        }

        if (mappedIndex >= 0 && mappedIndex < letterObjects.Length && letterObjects[mappedIndex] != null)
        {
            letterObjects[mappedIndex].SetActive(false);
            Debug.Log($"[GameManager DEBUG] letterObjects[Mapped Index: {mappedIndex}] 비활성화 완료.");
        }
        else if (mappedIndex >= 0)
        {
            Debug.LogError($"[GameManager ERROR] letterObjects[{mappedIndex}]가 NULL이어서 비활성화에 실패했습니다.");
        }

        isAwaitingSolution = false;
        isLetterUIDisplayed = false;

        HandleGameEndCheck(ref currentEventIndex);

        if (isFinalEventSolved)
        {
            // ResetToStart()는 ToggleDoor()가 호출할 때 실행되도록 설정
            Debug.Log("[GameManager] 최종 이벤트 해결 완료. 다음 문 클릭 시 재시작됩니다.");
            return;
        }

        Debug.Log($"[GameManager] 문 잠금 해제 완료. 다음 이벤트 인덱스: {currentEventIndex}.");
    }

    /// <summary>
    /// 랜덤 이벤트가 발생했을 때 호출되는 핵심 함수.
    /// </summary>
    public void TriggerRandomEvent()
    {
        if (doorTarget != null && doorTarget.open)
        {
            Debug.LogWarning("[GameManager] 이벤트 발동 순간 문이 열려있어 건너킵니다. 타이머가 리셋됩니다.");
            isTimerRunning = false;
            return;
        }
        canAdvanceIndex = true;

        PlayRandomSound();
        StartCoroutine(TimedObjectEvent());

        switch (currentEventIndex)
        {
            case 1:
                Debug.Log("Custom Logic: 1번 이벤트 - 강제 return으로 다음 타이머 시작 막음.");
                return;
            default:
                break;
        }

        Debug.Log($"[GameManager] 순차 이벤트 발생! (인덱스: {currentEventIndex}) -> 해결 대기 상태로 전환됨.");
    }

    /// <summary>
    /// 모든 이벤트 오브젝트 및 쪽지 UI를 즉시 비활성화(숨기기)합니다.
    /// </summary>
    private void HideAllObjects()
    {
        if (eventObjects != null)
        {
            foreach (GameObject obj in eventObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
        if (letterObjects != null)
        {
            foreach (GameObject obj in letterObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }

    /// <summary>
    /// '순차' 오브젝트를 선택하고 *다음 타이머를 위해 리셋*합니다.
    /// </summary>
    private IEnumerator TimedObjectEvent()
    {
        HideAllObjects();

        if (eventObjects == null || eventObjects.Length == 0)
        {
            isTimerRunning = false;
            isAwaitingSolution = false;
            yield break;
        }

        if (!letterEventIndices.Contains(currentEventIndex))
        {
            GameObject sequentialObj = eventObjects[currentEventIndex];
            if (sequentialObj != null)
            {
                sequentialObj.SetActive(true);
                Debug.Log($"[GameManager] 일반 오브젝트 활성화 (유지): {sequentialObj.name}");
            }
            else
            {
                Debug.LogWarning($"[GameManager] 선택된 {currentEventIndex}번 오브젝트가 Null입니다.");
            }
        }

        isTimerRunning = false;
        yield return null;
    }

    private void PlayRandomSound()
    {
        if (randomEventSounds == null || randomEventSounds.Length == 0) return;
        if (sfxSource == null) return;
        if (sfxSource.isPlaying) sfxSource.Stop();

        int randomIndex = Random.Range(0, randomEventSounds.Length);
        AudioClip clipToPlay = randomEventSounds[randomIndex];

        sfxSource.PlayOneShot(clipToPlay);
        Debug.Log($"[GameManager] 랜덤 사운드 재생: {clipToPlay.name}");
    }

    IEnumerator RandomEventTimerRoutine()
    {
        isTimerRunning = true;
        float randomDelay = Random.Range(minDelay, maxDelay);
        float timer = 0f;

        Debug.Log($"[GameManager] 다음 이벤트(인덱스: {currentEventIndex})까지 {randomDelay:F2}초 대기 시작...");

        while (timer < randomDelay)
        {
            if (doorTarget != null && doorTarget.open)
            {
                Debug.Log("[GameManager] 문이 열려서 이벤트 타이머를 중단/리셋합니다.");
                isTimerRunning = false;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[GameManager] 타이머 완료. 이벤트 발동.");
        TriggerRandomEvent();
    }

    private void OnDoorClosed()
    {
        Debug.Log($"[GameManager] 문이 닫혔습니다. (다음 이벤트 인덱스: {currentEventIndex})");

        if (isAwaitingSolution || isLetterUIDisplayed)
        {
            Debug.Log("문 닫힘 로직: 이벤트 해결 전까지 대기.");
        }
    }
}