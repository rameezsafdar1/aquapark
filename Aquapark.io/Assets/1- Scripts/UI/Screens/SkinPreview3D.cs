using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Live 3D preview for the Skins shop. A small stage far below the level (own camera, own layer, render texture)
/// shows the selected item: characters play an idle animation, floaties spin. Models are copies of the player's own
/// models (Player/Character children and its floaties), made on first use and kept for the next time.
/// The stage only renders while this object is active, i.e. while the Skins page is open.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SkinPreview3D : MonoBehaviour
{
    [Tooltip("The player's effects component: its SkinModels / FloatieModels are the models to copy.")]
    public PlayerEffects player;
    [Tooltip("Controller with a single looping idle state, played by every character copy.")]
    public RuntimeAnimatorController idleController;

    [Header("Stage")]
    [Tooltip("Layer only the preview camera sees (removed from every other camera).")]
    public int layer = 31;
    public Vector3 stagePosition = new Vector3(0f, -3000f, 0f);
    [Tooltip("Render texture pixels per UI design pixel.")]
    public float resolution = 3f;

    [Header("Characters")]
    [Tooltip("Lowest and highest point of the frame above the model's feet, in metres. Same for every character, so they all stand on the same line at the same scale.")]
    public float frameBottom = -0.2f;
    public float frameTop = 3.55f;
    [Tooltip("Degrees the character is turned from facing the camera (0 = straight on).")]
    public float characterTurn = 0f;

    [Header("Floaties")]
    [Tooltip("Spin speed in degrees per second.")]
    public float spinSpeed = 60f;
    [Tooltip("Camera angle above the floatie, in degrees.")]
    public float floatieViewAngle = 30f;

    // Extra turn per model, for costumes that do not face the same way as the body under the idle animation.
    private static readonly Dictionary<string, float> YawFix = new Dictionary<string, float>();

    private RawImage image;
    private Camera stageCamera;
    private Light stageLight;
    private RenderTexture texture;
    private Transform stage;
    private Transform holder;   // inactive parent, so copies do not wake up (and run player scripts) when made
    private readonly Dictionary<string, GameObject> copies = new Dictionary<string, GameObject>();
    private readonly Dictionary<GameObject, Quaternion> restRotation = new Dictionary<GameObject, Quaternion>();
    private GameObject shown;
    private bool spinning;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.enabled = false;
        BuildStage();

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != stageCamera)
            {
                cam.cullingMask &= ~(1 << layer);
            }
        }
    }

    private void OnEnable()
    {
        if (stage != null)
        {
            stage.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (stage != null)
        {
            stage.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }

        if (stage != null)
        {
            Destroy(stage.gameObject);
        }
    }

    private void Update()
    {
        if (spinning && shown != null)
        {
            shown.transform.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }

    /// <summary>Shows the item's model. False when it has no model on the player (the caller shows the picture instead).</summary>
    public bool Show(SkinData item)
    {
        GameObject copy = item != null ? GetCopy(item) : null;
        if (shown != null && shown != copy)
        {
            shown.SetActive(false);
        }

        shown = copy;
        image.enabled = copy != null;
        if (copy == null)
        {
            return false;
        }

        copy.transform.position = stage.position;
        spinning = item.slot == SkinSlot.Floatie;
        if (spinning)
        {
            FrameFloatie(copy);
        }
        else
        {
            FrameCharacter(copy, item.modelName);
        }

        copy.SetActive(true);

        // Pose it now, so the first frame is not the T-pose.
        var animator = copy.GetComponent<Animator>();
        if (animator != null && animator.enabled)
        {
            animator.Update(0f);
        }

        return true;
    }

    private void BuildStage()
    {
        stage = new GameObject("SkinPreviewStage").transform;
        stage.position = stagePosition;

        var holderGo = new GameObject("Copies");
        holderGo.SetActive(false);
        holder = holderGo.transform;
        holder.SetParent(stage, false);

        Vector2 size = ((RectTransform)transform).rect.size * resolution;
        texture = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(size.x)), Mathf.Max(64, Mathf.RoundToInt(size.y)), 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4,
            name = "SkinPreview"
        };
        image.texture = texture;

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(stage, false);
        stageCamera = camGo.AddComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        stageCamera.cullingMask = 1 << layer;
        stageCamera.orthographic = true;
        stageCamera.nearClipPlane = 0.1f;
        stageCamera.farClipPlane = 60f;
        stageCamera.targetTexture = texture;
        stageCamera.allowHDR = false;

        // Key light from the camera side; the scene's sun comes from behind the models.
        var lightGo = new GameObject("PreviewLight");
        lightGo.transform.SetParent(stage, false);
        stageLight = lightGo.AddComponent<Light>();
        stageLight.type = LightType.Directional;
        stageLight.intensity = 0.75f;
        stageLight.cullingMask = 1 << layer;
        stageLight.shadows = LightShadows.None;

        stage.gameObject.SetActive(isActiveAndEnabled);
    }

    private GameObject GetCopy(SkinData item)
    {
        if (string.IsNullOrEmpty(item.modelName) || player == null)
        {
            return null;
        }

        string key = item.slot + "/" + item.modelName;
        if (copies.TryGetValue(key, out GameObject existing) && existing != null)
        {
            return existing;
        }

        GameObject source = FindSource(item);
        if (source == null)
        {
            return null;
        }

        // Made under the inactive holder: no Awake runs, so the player's AvatarPass never sees the copy.
        GameObject copy = Instantiate(source, holder);
        copy.name = "Preview " + source.name;
        foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            DestroyImmediate(script);
        }

        foreach (ParticleSystem particles in copy.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.gameObject.SetActive(false);
        }

        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
        }

        var animator = copy.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = item.slot == SkinSlot.Character;
            animator.runtimeAnimatorController = idleController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        copy.SetActive(false);
        copy.transform.SetParent(stage, false);
        copy.transform.localScale = source.transform.lossyScale;
        // How it sits on the player (floaties lie flat around the character), without the player's own heading.
        restRotation[copy] = Quaternion.Inverse(player.transform.rotation) * source.transform.rotation;
        copies[key] = copy;
        return copy;
    }

    private GameObject FindSource(SkinData item)
    {
        if (item.slot == SkinSlot.Floatie)
        {
            foreach (GameObject floatie in player.FloatieModels)
            {
                if (floatie != null && SameName(floatie.name, item.modelName))
                {
                    return floatie;
                }
            }
        }
        else
        {
            foreach (AvatarPass skin in player.SkinModels)
            {
                if (skin != null && SameName(skin.name, item.modelName))
                {
                    return skin.gameObject;
                }
            }
        }

        return null;
    }

    /// <summary>Fixed frame for every character: feet on the same line, same scale, facing the camera.</summary>
    private void FrameCharacter(GameObject copy, string modelName)
    {
        Vector3 toCamera = Vector3.back;
        stageCamera.transform.position = stage.position + Vector3.up * (frameBottom + frameTop) * 0.5f + toCamera * 20f;
        stageCamera.transform.rotation = Quaternion.LookRotation(-toCamera);
        stageCamera.orthographicSize = (frameTop - frameBottom) * 0.5f;
        AimLight();

        // Under the idle animation the models face their local +Z.
        float yaw = YawFix.TryGetValue(modelName, out float fix) ? fix : 0f;
        copy.transform.rotation = Quaternion.LookRotation(toCamera) * Quaternion.Euler(0f, yaw + characterTurn, 0f);
    }

    /// <summary>Floaties are framed to their own size (they differ a lot) and viewed from slightly above.</summary>
    private void FrameFloatie(GameObject copy)
    {
        copy.transform.rotation = restRotation.TryGetValue(copy, out Quaternion rest) ? rest : Quaternion.identity;
        copy.SetActive(true);
        Bounds bounds = new Bounds(copy.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; } else { bounds.Encapsulate(r.bounds); }
        }

        // Spinning around the stage point: the widest it gets is the horizontal radius from that point.
        Vector3 offset = bounds.center - copy.transform.position;
        float radius = new Vector2(Mathf.Abs(offset.x) + bounds.extents.x, Mathf.Abs(offset.z) + bounds.extents.z).magnitude;
        float aspect = (float)texture.width / texture.height;
        float halfHeight = Mathf.Max(radius / aspect, radius * Mathf.Sin(floatieViewAngle * Mathf.Deg2Rad) + bounds.extents.y);

        Vector3 dir = Quaternion.Euler(floatieViewAngle, 0f, 0f) * Vector3.back;
        Vector3 target = copy.transform.position + Vector3.up * offset.y;
        stageCamera.transform.position = target + dir * 20f;
        stageCamera.transform.LookAt(target);
        stageCamera.orthographicSize = halfHeight * 1.08f;
        AimLight();
    }

    private void AimLight()
    {
        Transform cam = stageCamera.transform;
        stageLight.transform.rotation = Quaternion.LookRotation((cam.forward - Vector3.up * 0.8f - cam.right * 0.4f).normalized);
    }

    private static bool SameName(string a, string b)
    {
        return string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
