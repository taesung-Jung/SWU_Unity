using UnityEngine;

/// <summary>
/// 핸드 트랙킹을 사용하여 팔 흔들기로 전진하고, 팔 기울이기로 회전하는 기능을 구현합니다.
/// OVRCameraRig와 CharacterController가 씬에 설정되어 있어야 합니다.
/// </summary>
public class HandTrackingRunner : MonoBehaviour
{
    [Header("오브젝트 참조")]
    [Tooltip("플레이어 이동을 제어할 CharacterController 컴포넌트")]
    public CharacterController characterController;
    [Tooltip("머리(카메라) 방향을 얻기 위한 OVRCameraRig 참조")]
    public OVRCameraRig cameraRig;
    [Tooltip("왼손 OVRHand 컴포넌트가 있는 GameObject 참조")]
    public OVRHand leftHand;  // Inspector에서 할당 권장
    [Tooltip("오른손 OVRHand 컴포넌트가 있는 GameObject 참조")]
    public OVRHand rightHand; // Inspector에서 할당 권장

    [Header("이동 설정")]
    [Tooltip("달리기 시 전진 속도")]
    public float runSpeed = 3.0f;
    [Tooltip("달리기를 감지할 최소 평균 손 속도 (m/s)")]
    public float handSpeedThreshold = 1.5f;

    [Header("회전 설정")]
    [Tooltip("손 기울기에 따른 회전 속도 (degrees/s)")]
    public float turnSpeed = 60.0f;
    [Tooltip("회전을 감지할 최소 손 좌우 기울기 정도 (0~1, 높을수록 많이 기울여야 함)")]
    [Range(0f, 1f)]
    public float handTiltThreshold = 0.2f;

    private bool _isInitialized = false;

    void Start()
    {
        if (characterController == null)
        {
            characterController = GetComponentInParent<CharacterController>();
            if (characterController == null)
                Debug.LogError("CharacterController를 찾을 수 없습니다. 플레이어 오브젝트나 부모에 추가해주세요.");
        }
        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<OVRCameraRig>();
            if (cameraRig == null) Debug.LogError("OVRCameraRig를 씬에서 찾을 수 없습니다.");
        }

        // 사용자가 Inspector에서 leftHand와 rightHand를 직접 할당하는 것을 강력히 권장합니다.
        // 아래는 할당되지 않았을 경우를 위한 자동 찾기 로직입니다.
        if (leftHand == null || rightHand == null)
        {
            Debug.LogWarning("왼손 또는 오른손 OVRHand가 Inspector에 할당되지 않았습니다. 자동 찾기를 시도합니다. 정확한 할당을 위해 Inspector 사용을 권장합니다.");
            OVRHand[] allHands = FindObjectsOfType<OVRHand>(); // 씬에 있는 모든 OVRHand 컴포넌트를 찾습니다.

            if (allHands.Length >= 2)
            {
                foreach (OVRHand handInstance in allHands)
                {
                    // OVRHand GameObject에서 OVRSkeleton 컴포넌트를 직접 가져옵니다.
                    OVRSkeleton skeleton = handInstance.GetComponent<OVRSkeleton>();

                    if (skeleton != null)
                    {
                        if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft)
                        {
                            if (leftHand == null) // 아직 왼손이 할당되지 않았다면
                            {
                                leftHand = handInstance;
                                Debug.Log("자동으로 왼손 OVRHand를 찾았습니다: " + handInstance.gameObject.name);
                            }
                        }
                        else if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight)
                        {
                            if (rightHand == null) // 아직 오른손이 할당되지 않았다면
                            {
                                rightHand = handInstance;
                                Debug.Log("자동으로 오른손 OVRHand를 찾았습니다: " + handInstance.gameObject.name);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("OVRHand 오브젝트 " + handInstance.gameObject.name + "에서 OVRSkeleton 컴포넌트를 찾을 수 없습니다.");
                    }

                    // 양손을 모두 찾았으면 루프를 중단합니다.
                    if (leftHand != null && rightHand != null) break;
                }
            }

            if (leftHand == null) Debug.LogError("왼손 OVRHand를 찾거나 할당하지 못했습니다. OVRCameraRig의 손 설정을 확인하고 Inspector에 직접 할당해주세요.");
            if (rightHand == null) Debug.LogError("오른손 OVRHand를 찾거나 할당하지 못했습니다. OVRCameraRig의 손 설정을 확인하고 Inspector에 직접 할당해주세요.");
        }

        _isInitialized = characterController != null && cameraRig != null && leftHand != null && rightHand != null;

        if (!_isInitialized)
        {
            Debug.LogError("HandTrackingRunner 초기화 실패! Inspector에서 컴포넌트 참조를 확인해주세요.");
            enabled = false; // 초기화 실패 시 스크립트 비활성화
        }
        else
        {
            Debug.Log("HandTrackingRunner 초기화 성공!");
        }
    }

    void Update()
    {
        if (!_isInitialized) return;

        // OVRHand의 IsTracked 속성을 사용하여 손 추적 여부 확인
        if (!leftHand.IsTracked || !rightHand.IsTracked)
        {
            // 손 추적이 안 될 경우 아무것도 하지 않음
            return;
        }

        HandleMovement();
        HandleTurning();
    }

    void HandleMovement()
    {
        // OVRInput.GetLocalControllerVelocity를 사용하여 손의 속도를 가져옵니다.
        // 이 함수는 컨트롤러뿐만 아니라 핸드 트래킹 데이터의 속도도 반환합니다.
        Vector3 leftHandVelocity = OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LHand);
        Vector3 rightHandVelocity = OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RHand);

        float leftHandSpeed = leftHandVelocity.magnitude;
        float rightHandSpeed = rightHandVelocity.magnitude;

        float averageSpeed = (leftHandSpeed + rightHandSpeed) / 2.0f;
        bool isRunning = averageSpeed > handSpeedThreshold;

        if (isRunning)
        {
            Vector3 forwardDirection = Vector3.ProjectOnPlane(cameraRig.centerEyeAnchor.forward, Vector3.up).normalized;
            characterController.SimpleMove(forwardDirection * runSpeed);
        }
    }

    void HandleTurning()
    {
        // 손 위치는 OVRHand의 transform.position을 그대로 사용합니다.
        Vector3 leftHandPos = leftHand.transform.position;
        Vector3 rightHandPos = rightHand.transform.position;

        Vector3 handVector = rightHandPos - leftHandPos; // 오른손에서 왼손을 향하는 벡터
        Vector3 playerUp = characterController.transform.up; // 플레이어의 위쪽 방향
        Vector3 horizontalHandVector = Vector3.ProjectOnPlane(handVector, playerUp); // 손 벡터를 플레이어의 수평면에 투영
        Vector3 playerRight = characterController.transform.right; // 플레이어의 오른쪽 방향
        float tiltAmount = 0f;

        // 투영된 손 벡터의 길이가 충분히 클 때만 기울기 계산 (양손이 너무 가까우면 불안정)
        if (horizontalHandVector.magnitude > 0.05f) // 예: 최소 5cm 간격
        {
             // 정규화된 수평 손 벡터와 플레이어의 오른쪽 방향 사이의 내적을 계산
             // 결과: -1 (완전히 왼쪽 기울임) ~ 1 (완전히 오른쪽 기울임)
             tiltAmount = Vector3.Dot(horizontalHandVector.normalized, playerRight);
        }

        float turnInput = 0f;
        // 계산된 기울기 양의 절대값이 설정된 임계값을 넘으면 회전 입력으로 사용
        if (Mathf.Abs(tiltAmount) > handTiltThreshold)
        {
            turnInput = tiltAmount;
        }

        // 실제 회전 적용 (미미한 입력은 무시하여 의도치 않은 회전 방지 - Deadzone)
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            float rotationAmount = turnInput * turnSpeed * Time.deltaTime;
            characterController.transform.Rotate(Vector3.up, rotationAmount);
        }
    }
}
