using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NightController : NetworkBehaviour
{
    [Header("Auto Find Settings")]
    [Tooltip("Directional Light を自動検出する")]
    [SerializeField] private bool autoFindSunLight = true;

    [Tooltip("Global Volume(isGlobal) を自動検出する")]
    [SerializeField] private bool autoFindGlobalVolume = true;

    [Tooltip("自動検出に失敗した場合のフォールバック用タグ（空なら未使用）")]
    [SerializeField] private string sunLightTag = "";     // 例: "Sun"
    [SerializeField] private string globalVolumeTag = ""; // 例: "GlobalVolume"

    [Header("Night Values")]
    [SerializeField] private float nightSunIntensity = 0.15f;
    [SerializeField] private Color nightSunColor = new Color(0.55f, 0.65f, 1.0f);
    [SerializeField] private float transitionSeconds = 1.2f;

    [Header("Volume Profiles (Optional)")]
    [Tooltip("夜用プロファイル（設定すると差し替え）")]
    [SerializeField] private VolumeProfile nightProfile;
    [Tooltip("昼用プロファイル（戻す用。未設定なら開始時のプロファイルへ戻す）")]
    [SerializeField] private VolumeProfile dayProfile;

    private Light _sunLight;
    private Volume _globalVolume;

    private float _daySunIntensity;
    private Color _daySunColor;
    private VolumeProfile _cachedDayProfile;

    private Coroutine _co;

    private readonly NetworkVariable<bool> isNight =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        TryResolveSceneReferences();

        if (_sunLight != null)
        {
            _daySunIntensity = _sunLight.intensity;
            _daySunColor = _sunLight.color;
        }

        if (_globalVolume != null)
            _cachedDayProfile = _globalVolume.profile;
    }

    public override void OnNetworkSpawn()
    {
        // 途中でロードされるケースもあるので一応再解決
        TryResolveSceneReferences();

        isNight.OnValueChanged += OnNightChanged;
        OnNightChanged(false, isNight.Value); // join-in-progress対応
    }

    public override void OnNetworkDespawn()
    {
        isNight.OnValueChanged -= OnNightChanged;
    }

    // ===== サーバーから呼ぶ =====
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

            // タグフォールバック
            if (_sunLight == null && !string.IsNullOrEmpty(sunLightTag))
            {
                var go = GameObject.FindWithTag(sunLightTag);
                if (go != null) _sunLight = go.GetComponentInChildren<Light>(true);
            }

            if (_sunLight == null)
                Debug.LogWarning("[NightController] Directional Light (Sun) が見つかりません。");
        }

        if (autoFindGlobalVolume && _globalVolume == null)
        {
            _globalVolume = FindGlobalVolume();

            // タグフォールバック
            if (_globalVolume == null && !string.IsNullOrEmpty(globalVolumeTag))
            {
                var go = GameObject.FindWithTag(globalVolumeTag);
                if (go != null) _globalVolume = go.GetComponentInChildren<Volume>(true);
            }

            if (_globalVolume == null)
                Debug.LogWarning("[NightController] Global Volume (isGlobal=true) が見つかりません。");
        }
    }

    private static Light FindDirectionalLight()
    {
        // まずは「DirectionalかつEnabled」優先
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Light fallback = null;
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            if (l.type != LightType.Directional) continue;

            // enabled かつ active を優先
            if (l.isActiveAndEnabled)
                return l;

            // 一応 fallback
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

            // isGlobal を最優先
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
        // 念のため：参照が失われた/遅延生成されたケース
        if (_sunLight == null || _globalVolume == null)
            TryResolveSceneReferences();

        // Volume Profile 差し替え（任意）
        if (_globalVolume != null)
        {
            if (night && nightProfile != null)
            {
                _globalVolume.profile = nightProfile;
            }
            else
            {
                // 夜用プロファイルなしならプロファイルはそのままでもOK
            }

            if (!night)
            {
                if (dayProfile != null) _globalVolume.profile = dayProfile;
                else if (_cachedDayProfile != null) _globalVolume.profile = _cachedDayProfile;
            }
        }

        // Light をスムーズに遷移
        if (_sunLight == null) yield break;

        float startIntensity = _sunLight.intensity;
        Color startColor = _sunLight.color;

        float targetIntensity = night ? nightSunIntensity : _daySunIntensity;
        Color targetColor = night ? nightSunColor : _daySunColor;

        float t = 0f;
        float dur = Mathf.Max(0.01f, transitionSeconds);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float tt = Mathf.Clamp01(t);

            _sunLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, tt);
            _sunLight.color = Color.Lerp(startColor, targetColor, tt);

            yield return null;
        }

        _sunLight.intensity = targetIntensity;
        _sunLight.color = targetColor;
    }
}
