// HandTrackingRunner.cs (회전 안정화 및 회전 폭주 방지 포함)
using UnityEngine;

public class HandTrackingRunner : MonoBehaviour
{
    public CharacterController characterController;
    public OVRCameraRig cameraRig;
    public OVRHand leftHand;
    public OVRHand rightHand;
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    public float runSpeed = 3.0f;
    public float handSpeedThreshold = 0.2f;
    public float handSpeedDiffThreshold = 0.1f;
    public int requiredSwingCount = 3;
    public float maxSwingInterval = 0.2f;
    public float runGracePeriod = 0.5f;

    public float turnSpeed = 60.0f;
    [Range(0f, 1f)] public float handTiltThreshold = 0.3f;
    public float turnSmoothingFactor = 5.0f;
    [Range(0f, 1f)] public float turnSpeedReductionWhileRunning = 0.5f;

    private bool _isInitialized = false;
    private float _smoothedTurnInput = 0f;
    private bool _isRunningState = false;

    private Vector3 _lastLeftHandPos;
    private Vector3 _lastRightHandPos;

    private float swingTimer = 0f;
    private int swingCount = 0;
    private float runGraceTimer = 0f;

    void Start()
    {
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        if (cameraRig == null)
            cameraRig = FindObjectOfType<OVRCameraRig>();

        if (cameraRig != null)
        {
            if (leftHandAnchor == null)
                leftHandAnchor = cameraRig.transform.Find("TrackingSpace/LeftHandAnchor");
            if (rightHandAnchor == null)
                rightHandAnchor = cameraRig.transform.Find("TrackingSpace/RightHandAnchor");
        }

        if (leftHand == null || rightHand == null)
        {
            OVRHand[] hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                var skeleton = hand.GetComponent<OVRSkeleton>();
                if (skeleton != null)
                {
                    if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft && leftHand == null)
                        leftHand = hand;
                    else if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight && rightHand == null)
                        rightHand = hand;
                }
                if (leftHand == null && hand.name.ToLower().Contains("left")) leftHand = hand;
                if (rightHand == null && hand.name.ToLower().Contains("right")) rightHand = hand;
                if (leftHand != null && rightHand != null) break;
            }
        }

        _isInitialized = characterController && cameraRig && leftHand && rightHand && leftHandAnchor && rightHandAnchor;

        if (_isInitialized)
        {
            _lastLeftHandPos = leftHandAnchor.position;
            _lastRightHandPos = rightHandAnchor.position;
        }
        else
        {
            Debug.LogError("HandTrackingRunner 초기화 실패! 필요한 컴포넌트가 설정되지 않았습니다.");
            enabled = false;
        }
    }

    void Update()
    {
        if (!_isInitialized || !leftHand.IsTracked || !rightHand.IsTracked)
        {
            _isRunningState = false;
            _smoothedTurnInput = 0f;
            return;
        }

        HandleMovement();
        HandleTurning();
    }

    void HandleMovement()
    {
        Vector3 currLeft = leftHandAnchor.position;
        Vector3 currRight = rightHandAnchor.position;

        float leftSpeed = (currLeft - _lastLeftHandPos).magnitude / Time.deltaTime;
        float rightSpeed = (currRight - _lastRightHandPos).magnitude / Time.deltaTime;

        _lastLeftHandPos = currLeft;
        _lastRightHandPos = currRight;

        float avgSpeed = (leftSpeed + rightSpeed) * 0.5f;
        float speedDiff = Mathf.Abs(leftSpeed - rightSpeed);

        // 흔들기 감지
        if (avgSpeed > handSpeedThreshold && speedDiff > handSpeedDiffThreshold)
        {
            swingTimer += Time.deltaTime;
            if (swingTimer >= maxSwingInterval)
            {
                swingTimer = 0f;
                swingCount++;
            }
        }
        else
        {
            swingTimer = 0f;
            swingCount = 0;
        }

        // 유예 시간 로직
        if (swingCount >= requiredSwingCount)
        {
            _isRunningState = true;
            runGraceTimer = runGracePeriod;
            swingCount = 0;
        }
        else if (runGraceTimer > 0f)
        {
            runGraceTimer -= Time.deltaTime;
            _isRunningState = true;
        }
        else
        {
            _isRunningState = false;
        }

        if (_isRunningState)
        {
            if (!characterController.gameObject.activeInHierarchy || !characterController.enabled)
                return;

            Vector3 headForward = cameraRig.centerEyeAnchor.forward;
            Vector3 forwardDir = new Vector3(headForward.x, 0, headForward.z);
            if (forwardDir.sqrMagnitude < 0.01f)
                forwardDir = characterController.transform.forward;

            forwardDir.Normalize();
            characterController.SimpleMove(forwardDir * runSpeed);
        }
    }

    void HandleTurning()
    {
        Vector3 leftPos = leftHandAnchor.position;
        Vector3 rightPos = rightHandAnchor.position;

        Vector3 handVector = rightPos - leftPos;

        // 회전 입력 안정화: 손 간 거리가 너무 작으면 무시
        if (handVector.magnitude < 0.1f)
        {
            _smoothedTurnInput = 0f;
            return;
        }

        Vector3 horizontalHandVector = Vector3.ProjectOnPlane(handVector, characterController.transform.up);
        Vector3 playerRight = characterController.transform.right;

        float tiltAmount = Vector3.Dot(horizontalHandVector.normalized, playerRight);
        tiltAmount = Mathf.Clamp(tiltAmount, -1f, 1f);

        float turnInput = Mathf.Abs(tiltAmount) > handTiltThreshold ? tiltAmount : 0f;
        _smoothedTurnInput = Mathf.Lerp(_smoothedTurnInput, turnInput, Time.deltaTime * turnSmoothingFactor);
        _smoothedTurnInput = Mathf.Clamp(_smoothedTurnInput, -1f, 1f);

        if (Mathf.Abs(_smoothedTurnInput) > 0.01f)
        {
            float actualTurnSpeed = turnSpeed * (_isRunningState ? turnSpeedReductionWhileRunning : 1f);
            characterController.transform.Rotate(Vector3.up, _smoothedTurnInput * actualTurnSpeed * Time.deltaTime);
        }
    }
}
