using UnityEngine;
using DoorScript;
using UnityEngine.EventSystems; // UI 블로킹 확인을 위해 추가
using System.Collections; // 코루틴(IEnumerator) 사용을 위해 필수 추가됨

[RequireComponent(typeof(AudioSource))] // AudioSource 컴포넌트가 필수적으로 요구됨을 명시
public class GameManager : MonoBehaviour
{
    [Header("문 타겟")]
    [Tooltip("필요한 경우, 문 제어를 위한 레퍼런스입니다.")]
    public Door doorTarget;

    // [랜덤 이벤트 추가] 이벤트 발생 시간 설정
    [Header("랜덤 이벤트 시간 설정 (초)")]
    [Tooltip("이벤트가 발생할 최소 대기 시간")]
    public float minDelay = 5f;
    [Tooltip("이벤트가 발생할 최대 대기 시간")]
    public float maxDelay = 15f;

    // [오브젝트 리스트 추가]
    [Header("랜덤 이벤트 오브젝트 리스트")]
    [Tooltip("이벤트에 사용될, 하이어라키에서 참조할 게임 오브젝트들을 등록합니다.")]
    public GameObject[] eventObjects;

    // [오디오 재생용 변수]
    private AudioSource audioSource;
    [Header("랜덤 이벤트 오디오 리스트")]
    [Tooltip("이벤트 발생 시 랜덤으로 재생할 사운드 클립들을 등록합니다.")]
    public AudioClip[] randomEventSounds;

    void Start()
    {
        // AudioSource 컴포넌트 레퍼런스 가져오기
        audioSource = GetComponent<AudioSource>();
        // 시작 시 자동 재생을 방지합니다.
        audioSource.playOnAwake = false;

        // 씬 시작 시 바로 랜덤 타이머를 시작합니다.
        StartCoroutine(RandomEventTimerRoutine());

        // 시작 시 모든 이벤트 오브젝트 숨기기 (초기 상태 통일)
        HideAllEventObjects();
    }

    void Update()
    {
        // [추가된 로직]: Android 물리 백 버튼 (KeyCode.Escape) 감지 시 애플리케이션 종료
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[GameManager] Android Back Button Pressed. Quitting application.");
            Application.Quit();

            // 에디터에서 테스트 중일 때 유니티 에디터를 종료하지 않도록
            // #if UNITY_EDITOR
            //     UnityEditor.EditorApplication.isPlaying = false;
            // #endif
        }
    }

    /// <summary>
    /// UI 버튼 등 외부에서 문을 열기 위해 사용하는 함수 (기존 요청).
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
    /// 랜덤 이벤트가 발생했을 때 호출되는 핵심 함수. (TODO: 원하는 이벤트 로직 작성).
    /// </summary>
    public void TriggerRandomEvent()
    {
        // [추가된 로직]: 문이 열려 있으면 이벤트(노크/비주얼)를 발생시키지 않습니다.
        // **주의**: DoorScript의 Door 클래스에 문이 열렸는지 확인할 수 있는 public bool 변수 'open'이 있다고 가정합니다.
        if (doorTarget != null && doorTarget.open)
        {
            Debug.LogWarning("[GameManager] 문이 열려 있어 랜덤 이벤트를 건너뛰고 다음 타이머를 시작합니다.");

            // 이벤트를 건너뛰는 경우, TimedObjectEvent()를 호출하지 않으므로 여기서 직접 다음 타이머를 시작해야 합니다.
            StartCoroutine(RandomEventTimerRoutine());
            return;
        }

        // 1. 오디오 재생 로직
        PlayRandomSound();

        // 2. 비주얼 이벤트 시작 (2초간 오브젝트 노출 및 다음 타이머 시작까지 담당)
        StartCoroutine(TimedObjectEvent());

        Debug.Log($"[GameManager] 랜덤 이벤트 발생! 여기에 오디오/비주얼과 독립적인 다른 로직을 작성하세요.");
        // TODO: 몬스터 스폰, BGM 변경, 상태 변화 등 원하는 이벤트 로직을 작성하세요.
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
    /// 랜덤 오브젝트를 선택하고 2초간 노출한 후 숨깁니다.
    /// </summary>
    private IEnumerator TimedObjectEvent()
    {
        // 1. 모든 오브젝트를 숨깁니다 (상태 통일)
        HideAllEventObjects();

        if (eventObjects == null || eventObjects.Length == 0)
        {
            Debug.LogWarning("[GameManager] 랜덤 이벤트 오브젝트가 없습니다. 비주얼 이벤트를 건너뛰고 다음 타이머를 시작합니다.");
            StartCoroutine(RandomEventTimerRoutine());
            yield break;
        }

        // 2. 랜덤 오브젝트 선택
        GameObject randomObj = eventObjects[Random.Range(0, eventObjects.Length)];

        // 3. 선택된 오브젝트 활성화 (노출)
        if (randomObj != null)
        {
            randomObj.SetActive(true);
            Debug.Log($"[GameManager] 오브젝트 활성화: {randomObj.name} (2초)");
        }
        else
        {
            Debug.LogWarning("[GameManager] 선택된 랜덤 오브젝트가 Null입니다.");
        }

        // 💡 오브젝트 활성화 후 1프레임을 대기하여 모바일 환경에서 렌더링이 확실히 되도록 보장합니다.
        yield return null;

        // 4. 4초 대기
        yield return new WaitForSeconds(4.0f);

        // 5. 오브젝트 비활성화 (다시 숨김)
        if (randomObj != null)
        {
            randomObj.SetActive(false);
            Debug.Log($"[GameManager] 오브젝트 비활성화: {randomObj.name}");
        }

        // 6. 다음 랜덤 이벤트 타이머를 시작합니다. (반복)
        StartCoroutine(RandomEventTimerRoutine());
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

        // 1. 리스트에서 랜덤 인덱스 선택
        // Random.Range(min, max)는 정수형의 경우 max를 포함하지 않습니다. 
        int randomIndex = Random.Range(0, randomEventSounds.Length);

        // 2. 오디오 클립 할당 및 재생 (PlayOneShot: 현재 재생 중인 클립에 영향을 주지 않고 재생)
        AudioClip clipToPlay = randomEventSounds[randomIndex];
        audioSource.PlayOneShot(clipToPlay);

        Debug.Log($"[GameManager] 랜덤 사운드 재생: {clipToPlay.name}");
    }

    /// <summary>
    /// 랜덤한 시간 동안 대기한 후 TriggerRandomEvent()를 호출하여 이벤트를 반복하는 코루틴.
    /// </summary>
    IEnumerator RandomEventTimerRoutine()
    {
        // 1. 최소/최대값 사이에서 랜덤 시간 계산
        float randomDelay = Random.Range(minDelay, maxDelay);

        Debug.Log($"[GameManager] 다음 랜덤 이벤트까지 {randomDelay:F2}초 대기 중...");

        // 2. 계산된 시간만큼 대기
        yield return new WaitForSeconds(randomDelay);

        // 3. 대기가 끝나면 이벤트 트리거 
        TriggerRandomEvent();
    }
}
