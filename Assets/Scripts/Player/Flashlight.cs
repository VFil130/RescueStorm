using System;
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxCharge = 100f;
    [SerializeField] private float drainRate = 10f;
    [SerializeField] private float rechargeRate = 20f;
    [SerializeField] private float rechargeDelay = 1.5f;
    [SerializeField] private Light lightComponent;

    public bool IsOn { get; private set; }
    public float ChargeRatio => maxCharge > 0f ? currentCharge / maxCharge : 0f;
    public event Action<bool> OnStateChanged;
    public event Action<float> OnChargeChanged;

    private float currentCharge;
    private float rechargeTimer;

    private void Awake()
    {
        currentCharge = maxCharge;
        if (lightComponent == null) lightComponent = GetComponentInChildren<Light>();
        SetLight(false);
    }

    private void Update()
    {
        if (!IsOn && currentCharge >= maxCharge) return;

        if (IsOn)
        {
            currentCharge = Mathf.Max(0f, currentCharge - drainRate * Time.deltaTime);
            if (currentCharge <= 0f) SetOn(false);
        }
        else if ((rechargeTimer += Time.deltaTime) >= rechargeDelay)
        {
            currentCharge = Mathf.Min(maxCharge, currentCharge + rechargeRate * Time.deltaTime);
        }

        OnChargeChanged?.Invoke(ChargeRatio);
    }

    public void Toggle() => SetOn(!IsOn);

    public void SetOn(bool value)
    {
        if (value == IsOn || (value && currentCharge <= 0f)) return;

        IsOn = value;
        rechargeTimer = 0f;
        if (lightComponent != null) lightComponent.enabled = value;
        OnStateChanged?.Invoke(value);
    }

    private void SetLight(bool enabled) { if (lightComponent != null) lightComponent.enabled = enabled; }
}