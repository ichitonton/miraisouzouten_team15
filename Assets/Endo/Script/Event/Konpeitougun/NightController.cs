using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class NightController : NetworkBehaviour
{
    [Header("Auto Find Settings")]
    [SerializeField] private bool autoFindSunLight = true;
    [SerializeField] private bool autoFindGlobalVolume = true;
    [SerializeField] private string sunLightTag = "";
    [SerializeField] private string globalVolumeTag = "";

    [Header("Day (Manual Restore Values) ★ここに“元の値(1.8/色)”を入れる")]
    [SerializeField] private bool useManualDayValues = true;
    [SerializeField] private float daySunIntensity = 1.8f;
    [SerializeField] private Color daySunColor = new Color(1f, 0.9f, 0.6f, 1f);

    [Header("Night Values")]
    [SerializeField] private float nightSunIntensity = 0.15f;
    [SerializeField] private Color nightSunColor = new Color(0.55f, 0.65f, 1.0f, 1f);

    [Header("Transition")]
    [SerializeField] private float transitionSeconds = 1.2f;

    [Header("Safety (Optional)")]
    [Tooltip("昼に戻した直後、一定時間だけ昼値を強制適用し続ける（他の処理が上書きする対策）")]
    [SerializeField] private bool enforceDayForShortTime = true;

    [Tooltip("強制適用する秒数（0.2-1秒くらいでOK）")]
    [SerializeField, Min(0f)] private float enforceDaySeconds = 0.6f;

    private Light _sunLight;
    //private Volume _globalVolume;

    private Coroutine _co;

    private readonly NetworkVariable<bool> isNight =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        TryResolveSceneReferences();
    }

    public override void OnNetworkSpawn()
    {
        TryResolveSceneReferences();

        isNight.OnValueChanged += OnNightChanged;
        OnNightChanged(false, isNight.Value);
    }

    public override void OnNetworkDespawn()
    {
        isNight.OnValueChanged -= OnNightChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetNightServerRpc(bool night)
    {
        isNight.Value = night;
    }

    // ===== 自動探索 =====
    private void TryResolveSceneReferences()
    {
        if (autoFindSunLight && _sunLight == null)
        {
            _sunLight = FindDirectionalLight();

            if (_sunLight == null && !string.IsNullOrEmpty(sunLightTag))
            {
                var go = GameObject.FindWithTag(sunLightTag);
                if (go != null) _sunLight = go.GetComponentInChildren<Light>(true);
            }

            if (_sunLight == null)
                Debug.LogWarning("[NightController] Directional Light (Sun) が見つかりません。");
            else
                Debug.Log($"[NightController] SunLight found: {_sunLight.name}");
        }

        /*if (autoFindGlobalVolume && _globalVolume == null)
        {
            _globalVolume = FindGlobalVolume();

            if (_globalVolume == null && !string.IsNullOrEmpty(globalVolumeTag))
            {
                var go = GameObject.FindWithTag(globalVolumeTag);
                if (go != null) _globalVolume = go.GetComponentInChildren<Volume>(true);
            }

            if (_globalVolume == null)
                Debug.LogWarning("[NightController] Global Volume (isGlobal=true) が見つかりません。");
        }*/
    }

    private static Light FindDirectionalLight()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Light fallback = null;
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            if (l.type != LightType.Directional) continue;

            if (l.isActiveAndEnabled) return l;
            fallback ??= l;
        }
        return fallback;
    }

    private static Volume FindGlobalVolume()
    {
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Volume fallback = null;
        for (int i = 0; i < volumes.Length; i++)
        {
            var v = volumes[i];
            if (v == null) continue;

            if (v.isGlobal)
            {
                if (v.isActiveAndEnabled) return v;
                fallback ??= v;
            }
        }
        return fallback;
    }

    // ===== ローカル反映 =====
    private void OnNightChanged(bool prev, bool next)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(ApplyNightRoutine(next));
    }

    private IEnumerator ApplyNightRoutine(bool night)
    {
        TryResolveSceneReferences();
        if (_sunLight == null) yield break;

        float dur = Mathf.Max(0.01f, transitionSeconds);

        float startI = _sunLight.intensity;
        Color startC = _sunLight.color;

        float targetI = night ? nightSunIntensity : (useManualDayValues ? daySunIntensity : startI);
        Color targetC = night ? nightSunColor : (useManualDayValues ? daySunColor : startC);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float tt = Mathf.Clamp01(t);

            _sunLight.intensity = Mathf.Lerp(startI, targetI, tt);
            _sunLight.color = Color.Lerp(startC, targetC, tt);

            yield return null;
        }

        // 最終値を確実にセット
        _sunLight.intensity = targetI;
        _sunLight.color = targetC;

        // ★保険：戻した直後に別処理で上書きされる対策
        if (!night && enforceDayForShortTime && useManualDayValues && enforceDaySeconds > 0f)
        {
            float end = Time.time + enforceDaySeconds;
            while (Time.time < end)
            {
                _sunLight.intensity = daySunIntensity;
                _sunLight.color = daySunColor;
                yield return null;
            }
        }
    }
}
