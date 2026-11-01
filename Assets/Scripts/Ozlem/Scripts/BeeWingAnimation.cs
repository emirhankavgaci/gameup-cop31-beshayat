
using UnityEngine;

public class BeeWingAnimation : MonoBehaviour
{
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;

    [SerializeField] private float flapSpeed = 25f;
    [SerializeField] private float flapAngle = 35f;

    private void Update()
    {
        float angle = Mathf.Sin(Time.time * flapSpeed) * flapAngle;

        leftWing.localRotation = Quaternion.Euler(0f, 0f, angle);
        rightWing.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }
}
