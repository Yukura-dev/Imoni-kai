using UnityEngine;

namespace Imoni.UI
{
    /// <summary>Animates the character artwork without moving its navigation or camera target.</summary>
    [DisallowMultipleComponent]
    public sealed class GameCharacterMotion : MonoBehaviour
    {
        public GameScreenController controller;
        public RectTransform body, leftHand, rightHand, groundShadow;

        [Header("Motion")]
        [Range(0f, 8f)] public float walkBounce = 3.6f;
        [Range(0f, 12f)] public float walkTilt = 4.5f;
        [Range(1f, 5f)] public float stepsPerSecond = 2.8f;
        [Range(4f, 30f)] public float poseSharpness = 18f;

        private RestPose bodyRest, leftRest, rightRest, shadowRest;
        private Vector2 facing = Vector2.right;
        private float walkPhase;
        private bool wasMoving;

        private struct RestPose
        {
            public RectTransform target;
            public Vector2 position;
            public Quaternion rotation;
            public Vector3 scale;

            public RestPose(RectTransform rect)
            {
                target = rect;
                position = rect ? rect.anchoredPosition : Vector2.zero;
                rotation = rect ? rect.localRotation : Quaternion.identity;
                scale = rect ? rect.localScale : Vector3.one;
            }

            public void Restore()
            {
                if (!target) return;
                target.anchoredPosition = position;
                target.localRotation = rotation;
                target.localScale = scale;
            }
        }

        private void OnEnable()
        {
            walkPhase = 0f;
            wasMoving = false;
            CaptureRestPoses();
        }

        private void OnDisable()
        {
            bodyRest.Restore();
            leftRest.Restore();
            rightRest.Restore();
            shadowRest.Restore();
            walkPhase = 0f;
            wasMoving = false;
        }

        private void LateUpdate() => ApplyPose(Time.deltaTime);

        private void ApplyPose(float delta)
        {
            CaptureRestPoses();
            if (delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;

            bool walking = controller && controller.IsMoving;
            Vector3 harvestWorldPosition = Vector3.zero;
            bool harvesting = controller && controller.IsCollecting && controller.TryGetHarvestWorldPosition(out harvestWorldPosition);
            if (controller)
            {
                Vector2 direction = controller.MotionDirection;
                if (direction.sqrMagnitude > .0001f &&
                    !float.IsNaN(direction.x) && !float.IsNaN(direction.y) &&
                    !float.IsInfinity(direction.x) && !float.IsInfinity(direction.y))
                    facing = direction.normalized;
            }

            Vector2 bodyOffset = Vector2.zero;
            Vector2 leftOffset = Vector2.zero, rightOffset = Vector2.zero;
            Vector2 bodyScale = Vector2.one, shadowScale = Vector2.one;
            float bodyAngle = 0f, leftAngle = 0f, rightAngle = 0f;
            RestPose reachingHand = default;
            Vector2 reachingOffset = Vector2.zero;
            float reach = 0f;

            if (walking)
            {
                if (!wasMoving) walkPhase = 0f;
                float speed = Mathf.Clamp(controller.MovementSpeed / 140f, .5f, 2.5f);
                // Abs(sin) makes two footfalls per cycle, so advance by PI per step.
                walkPhase = Mathf.Repeat(walkPhase + delta * stepsPerSecond * speed * Mathf.PI, Mathf.PI * 2f);
                float stride = Mathf.Sin(walkPhase);
                float lift = Mathf.Abs(stride);
                float squash = Mathf.Cos(walkPhase * 2f) * .035f;
                bodyOffset = Vector2.up * (lift * walkBounce);
                bodyAngle = -stride * walkTilt - facing.x * 1.5f;
                bodyScale = new Vector2(1f + squash, 1f - squash);
                leftOffset = new Vector2(-stride * 1.5f, stride * 4.5f + lift * walkBounce * .5f);
                rightOffset = new Vector2(stride * 1.5f, -stride * 4.5f + lift * walkBounce * .5f);
                leftAngle = -stride * 10f;
                rightAngle = stride * 10f;
                shadowScale = new Vector2(1f - lift * .14f, 1f - lift * .08f);
            }
            else if (harvesting)
            {
                float progress = Mathf.Clamp01(controller.HarvestProgress);
                float phase = progress * Mathf.PI * 2f;
                // Hold contact briefly, then return before the next gathering cycle.
                reach = progress < .42f ? Mathf.SmoothStep(0f, 1f, progress / .42f) :
                    progress <= .58f ? 1f : Mathf.SmoothStep(1f, 0f, (progress - .58f) / .42f);
                float pull = Mathf.Sin(phase);
                bool hasLeft = TryGetReachOffset(leftRest, harvestWorldPosition, out Vector2 leftTarget, out float leftDistance);
                bool hasRight = TryGetReachOffset(rightRest, harvestWorldPosition, out Vector2 rightTarget, out float rightDistance);
                bool rightLeads = hasRight && (!hasLeft || rightDistance <= leftDistance);
                Vector2 supportingOffset = facing * (reach * 2.5f) - Vector2.up * (pull * 1.5f);
                leftOffset = rightLeads ? supportingOffset : leftTarget * reach;
                rightOffset = rightLeads ? rightTarget * reach : supportingOffset;
                if (hasLeft || hasRight)
                {
                    reachingHand = rightLeads ? rightRest : leftRest;
                    reachingOffset = rightLeads ? rightOffset : leftOffset;
                }
                bodyOffset = facing * (reach * 1.5f) + Vector2.up * (pull * .8f);
                bodyAngle = -facing.x * reach * 6f;
                bodyScale = new Vector2(1f + reach * .025f, 1f - reach * .025f);
                leftAngle = -facing.x * reach * (rightLeads ? 4f : 14f);
                rightAngle = -facing.x * reach * (rightLeads ? 14f : 4f);
            }

            wasMoving = walking;
            float blend = 1f - Mathf.Exp(-poseSharpness * delta);
            BlendPose(bodyRest, bodyOffset, bodyAngle, bodyScale, blend);
            BlendPose(leftRest, leftOffset, leftAngle, Vector2.one, blend);
            BlendPose(rightRest, rightOffset, rightAngle, Vector2.one, blend);
            BlendPose(shadowRest, Vector2.zero, 0f, shadowScale, blend);
            if (reachingHand.target)
            {
                // Position follows the gathering phase directly, so smoothing cannot shorten the reach.
                reachingHand.target.anchoredPosition = reachingHand.position + reachingOffset;
                if (reach >= 1f)
                {
                    Transform parent = reachingHand.target.parent;
                    Vector3 targetLocal = parent.InverseTransformPoint(harvestWorldPosition);
                    Vector3 centerLocal = parent.InverseTransformPoint(reachingHand.target.TransformPoint(reachingHand.target.rect.center));
                    reachingHand.target.anchoredPosition += (Vector2)(targetLocal - centerLocal);
                }
            }
        }

        private static bool TryGetReachOffset(RestPose rest, Vector3 worldPosition, out Vector2 offset, out float distance)
        {
            offset = Vector2.zero;
            distance = float.PositiveInfinity;
            if (!rest.target || !rest.target.parent) return false;
            Transform parent = rest.target.parent;
            Vector3 targetLocal = parent.InverseTransformPoint(worldPosition);
            Vector3 centerLocal = parent.InverseTransformPoint(rest.target.TransformPoint(rest.target.rect.center));
            Vector2 targetPosition = rest.target.anchoredPosition + (Vector2)(targetLocal - centerLocal);
            offset = targetPosition - rest.position;
            if (float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsInfinity(offset.x) || float.IsInfinity(offset.y))
            {
                offset = Vector2.zero;
                return false;
            }
            distance = parent.TransformVector(offset).sqrMagnitude;
            return true;
        }

        private void CaptureRestPoses()
        {
            CaptureIfChanged(ref bodyRest, body);
            CaptureIfChanged(ref leftRest, leftHand);
            CaptureIfChanged(ref rightRest, rightHand);
            CaptureIfChanged(ref shadowRest, groundShadow);
        }

        private static void CaptureIfChanged(ref RestPose pose, RectTransform rect)
        {
            if (pose.target == rect) return;
            pose.Restore();
            pose = new RestPose(rect);
        }

        private static void BlendPose(RestPose rest, Vector2 offset, float angle, Vector2 scale, float blend)
        {
            if (!rest.target) return;
            rest.target.anchoredPosition = Vector2.Lerp(rest.target.anchoredPosition, rest.position + offset, blend);
            rest.target.localRotation = Quaternion.Slerp(rest.target.localRotation, rest.rotation * Quaternion.Euler(0f, 0f, angle), blend);
            Vector3 targetScale = Vector3.Scale(rest.scale, new Vector3(scale.x, scale.y, 1f));
            rest.target.localScale = Vector3.Lerp(rest.target.localScale, targetScale, blend);
        }
    }
}
