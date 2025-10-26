using UnityEngine;
using DoorScript;
using UnityEngine.EventSystems;
using System.Collections;

// [수정] RequireComponent는 사용하지 않거나, 2개로 지정해야 하므로 일단 삭제
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

    // [!!! 핵심 수정 !!!] AudioSource를 2개로 분리
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

    void Start()
    {
        // [!!! 핵심 수정 !!!] GetComponent 대신 인스펙터에서 할당받은 컴포넌트를 사용
        // audioSource = GetComponent<AudioSource>();
        // audioSource.playOnAwake = false;

        // sfxSource는 playOnAwake를 꺼두는 것을 보장합니다.
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
    /// [!!! 핵심 수정 !!!] 문이 열리는 '순간' 호출되는 함수.
    /// 이벤트를 '해결'한 것으로 간주하고 다음 이벤트 인덱스로 넘깁니다.
    /// </summary>
    private void OnDoorOpened()
    {
        Debug.Log("[GameManager] 문이 열렸습니다. 이벤트를 '해결'했습니다.");

        // [!!! 핵심 추가 !!!]
        // 문이 열렸으므로, '노크' 효과음을 즉시 멈춥니다.
        // (이것이 "문 열려있어도" 소리가 계속 나는 문제를 해결합니다)
        if (sfxSource != null && sfxSource.isPlaying)
        {
            sfxSource.Stop();
            Debug.Log("[GameManager] 문을 열었으므로 노크 사운드를 중지합니다.");
        }

        // 1. [유지] 다음 이벤트를 위해 인덱스를 증가시킵니다.
        currentEventIndex++;
        if (currentEventIndex >= eventObjects.Length)
        {
            currentEventIndex = 0;
            Debug.Log("[GameManager] 이벤트 인덱스가 0으로 리셋됩니다.");
        }
        Debug.Log($"[GameManager] 다음 이벤트 인덱스는 {currentEventIndex} 입니다.");

        // 2. [유지] 이벤트가 해결되었으므로, 타이머 플래그를 리셋합니다.
        isTimerRunning = false;
    }

    void Update()
    {
        // 1. Android 백 버튼 로직 (기존과 동일)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[GameManager] Android Back Button Pressed. Quitting application.");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        bool isDoorCurrentlyOpen = (doorTarget != null && doorTarget.open);

        // 2. 문 닫힘 '순간' 감지 (기존과 동일)
        if (wasDoorOpen && !isDoorCurrentlyOpen)
        {
            OnDoorClosed();
        }

        // 3. [!!! 핵심 추가 !!!] 문 열림 '순간' 감지
        // 문이 닫혀 있다가(wasDoorOpen=false) 열리는(isDoorCurrentlyOpen=true) 순간
        if (!wasDoorOpen && isDoorCurrentlyOpen)
        {
            OnDoorOpened(); // <-- 이벤트를 해결한 것으로 처리
        }

        wasDoorOpen = isDoorCurrentlyOpen;

        // 4. 이벤트 타이머 제어 (기존과 동일)
        if (!isDoorCurrentlyOpen && !isTimerRunning)
        {
            StartCoroutine(RandomEventTimerRoutine());
        }
    }

    /// <summary>
    /// UI 버튼 등 외부에서 문을 열기 위해 사용하는 함수 (기존과 동일).
    /// </summary>
    public void ToggleDoor()
    {
        if (doorTarget != null)
        {
            doorTarget.OpenDoor();
            Debug.Log($"[GameManager] Door Toggle: {doorTarget.name} Open.");
        }
    }

    /// <summary>
    /// 랜덤 이벤트가 발생했을 때 호출되는 핵심 함수. (기존과 동일)
    /// </summary>
    public void TriggerRandomEvent()
    {
        if (doorTarget != null && doorTarget.open)
        {
            Debug.LogWarning("[GameManager] 이벤트 발동 순간 문이 열려있어 건너뜁니다. 타이머가 리셋됩니다.");
            isTimerRunning = false;
            return;
        }

        PlayRandomSound();
        StartCoroutine(TimedObjectEvent()); // [수정] 비주얼 이벤트 시작

        Debug.Log($"[GameManager] 순차 이벤트 발생! (인덱스: {currentEventIndex})");
    }

    /// <summary>
    /// 모든 이벤트 오브젝트를 즉시 비활성화(숨기기)합니다. (기존과 동일)
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
    /// [추가] 문이 닫히는 '순간' 호출되는 함수. (기존과 동일)
    /// </summary>
    private void OnDoorClosed()
    {
        Debug.Log($"[GameManager] 문이 닫혔습니다. (다음 이벤트 인덱스: {currentEventIndex})");

        // 💡 요청하신 로직 영역
        switch (currentEventIndex)
        {
            case 0:
                Debug.Log("문 닫힘: 0번 이벤트 대기 중...");
                break;
            case 1:
                Debug.Log("문 닫힘: 1번 이벤트 대기 중...");
                break;
            default:
                Debug.Log($"문 닫힘: {currentEventIndex}번 이벤트 대기 중...");
                break;
        }
    }


    /// <summary>
    /// [수정됨] '순차' 오브젝트를 선택하고 *다음 타이머를 위해 리셋*합니다.
    /// </summary>
    private IEnumerator TimedObjectEvent()
    {
        // 1. [유지] 이전 이벤트를 위해 켜져 있던 오브젝트를 모두 숨깁니다.
        HideAllEventObjects();

        if (eventObjects == null || eventObjects.Length == 0)
        {
            Debug.LogWarning("[GameManager] 이벤트 오브젝트가 없습니다. 비주얼 이벤트를 건너뛰고 타이머를 리셋합니다.");
            isTimerRunning = false; // [유지] 오브젝트가 없으니 예외적으로 타이머 리셋
            yield break;
        }

        // 2. [유지] '현재 인덱스'로 오브젝트 선택
        GameObject sequentialObj = eventObjects[currentEventIndex];

        // 3. [유지] 선택된 오브젝트 활성화 (노출)
        if (sequentialObj != null)
        {
            sequentialObj.SetActive(true);
            Debug.Log($"[GameManager] 오브젝트 활성화 (유지): {sequentialObj.name}");
        }
        else
        {
            Debug.LogWarning($"[GameManager] 선택된 {currentEventIndex}번 오브젝트가 Null입니다.");
        }

        // 4. [!!! 핵심 수정 !!!]
        // 타이머 플래그를 '다시' false로 내립니다.
        // 이렇게 하면 Update()에서 문이 닫혀있는 한,
        // 3~7초 후에 이벤트를 (같은 인덱스로) '반복'하게 됩니다.
        isTimerRunning = false;

        yield return null;
    }


    /// <summary>
    /// [수정됨] 등록된 오디오 클립 중 하나를 랜덤으로 선택하여 재생합니다. (중첩 방지)
    /// </summary>
    private void PlayRandomSound()
    {
        if (randomEventSounds == null || randomEventSounds.Length == 0)
        {
            Debug.LogWarning("[GameManager] 재생할 오디오 클립이 리스트에 없습니다. 오디오를 건너킵니다.");
            return;
        }

        // [!!! 핵심 수정 !!!] sfxSource가 할당되었는지 확인
        if (sfxSource == null) return;

        // [!!! 핵심 수정 !!!] sfxSource(효과음)만 멈춥니다. bgmSource는 건드리지 않습니다.
        if (sfxSource.isPlaying)
        {
            sfxSource.Stop();
            Debug.Log("[GameManager] 반복 이벤트를 위해 기존 사운드를 중지합니다.");
            return;
        }

        int randomIndex = Random.Range(0, randomEventSounds.Length);
        AudioClip clipToPlay = randomEventSounds[randomIndex];

        // [!!! 핵심 수정 !!!] sfxSource로 효과음을 재생합니다.
        sfxSource.PlayOneShot(clipToPlay);

        Debug.Log($"[GameManager] 랜덤 사운드 재생: {clipToPlay.name}");
    }

    /// <summary>
    /// [수정됨] 랜덤한 시간 동안 '문이 닫혀있는지 매 프레임 검사하며' 대기한 후
    /// TriggerRandomEvent()를 호출하는 코루틴.
    /// </summary>
    IEnumerator RandomEventTimerRoutine()
    {
        isTimerRunning = true;
        float randomDelay = Random.Range(minDelay, maxDelay);
        float timer = 0f;

        Debug.Log($"[GameManager] 다음 이벤트(인덱스: {currentEventIndex})까지 {randomDelay:F2}초 대기 시작...");

        // [핵심] randomDelay 시간만큼 대기하되, 1프레임마다 문 상태를 검사
        while (timer < randomDelay)
        {
            // 1. [중요] 대기 중에 문이 열렸는지 확인
            if (doorTarget != null && doorTarget.open)
            {
                Debug.Log("[GameManager] 문이 열려서 이벤트 타이머를 중단/리셋합니다.");
                isTimerRunning = false; // 타이머 플래그 리셋
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