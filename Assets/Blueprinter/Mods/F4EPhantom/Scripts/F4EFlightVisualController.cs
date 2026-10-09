using UnityEngine;

namespace F4EPhantom
{
    public sealed class F4EFlightVisualController : MonoBehaviour
    {
        [Range(-1f, 1f)] public float pitchInput;
        [Range(-1f, 1f)] public float rollInput;
        [Range(-1f, 1f)] public float yawInput;
        [Range(0f, 1f)] public float gearPosition;
        [Range(0f, 1f)] public float canopyPosition;
        [Range(0f, 1f)] public float afterburner;

        [SerializeField] private Transform stabilatorLeft;
        [SerializeField] private Transform stabilatorRight;
        [SerializeField] private Transform rudder;
        [SerializeField] private Transform canopy;
        [SerializeField] private Transform noseGear;
        [SerializeField] private Transform leftGear;
        [SerializeField] private Transform rightGear;
        [SerializeField] private Light leftAfterburner;
        [SerializeField] private Light rightAfterburner;

        private void LateUpdate()
        {
            SetLocalRotation(stabilatorLeft, new Vector3(pitchInput * -22f, 0f, rollInput * 14f));
            SetLocalRotation(stabilatorRight, new Vector3(pitchInput * -22f, 0f, rollInput * -14f));
            SetLocalRotation(rudder, new Vector3(0f, yawInput * 25f, 0f));
            SetLocalPosition(canopy, new Vector3(0f, canopyPosition * 0.18f, canopyPosition * 0.92f));
            SetLocalRotation(noseGear, new Vector3(gearPosition * 86f, 0f, 0f));
            SetLocalRotation(leftGear, new Vector3(gearPosition * -92f, 0f, 0f));
            SetLocalRotation(rightGear, new Vector3(gearPosition * -92f, 0f, 0f));
            SetAfterburner(leftAfterburner);
            SetAfterburner(rightAfterburner);
        }

        private static void SetLocalRotation(Transform target, Vector3 angles)
        {
            if (target != null) target.localRotation = Quaternion.Euler(angles);
        }

        private static void SetLocalPosition(Transform target, Vector3 position)
        {
            if (target != null) target.localPosition = position;
        }

        private void SetAfterburner(Light target)
        {
            if (target == null) return;
            target.enabled = afterburner > 0.02f;
            target.intensity = Mathf.Lerp(0f, 8f, afterburner);
            target.range = Mathf.Lerp(0f, 9f, afterburner);
            target.color = Color.Lerp(new Color(1f, 0.32f, 0.04f), new Color(0.35f, 0.72f, 1f), afterburner);
        }
    }
}
