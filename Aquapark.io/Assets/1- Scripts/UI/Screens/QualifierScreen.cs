using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// "Beach Cup" qualifier shown after every won race. 32 racers float on rings; each round some of them drown
/// (splash + sound) and stay gone in the next round. After the third round only the player is left.
/// A lost race resets the cup (UIManager), and a finished cup starts fresh on the next win.
///
/// The racers are copies of the player's own character and floatie models, on a small 3D stage far below the level
/// with its own camera (drawn over the game; the UI canvas draws over it). Progress is kept in SaveData, so it
/// survives the scene reload between races and app restarts.
/// </summary>
public class QualifierScreen : UIPanel
{
    [Header("Racers")]
    [Tooltip("The player's effects component: its SkinModels / FloatieModels are the models the racers are copied from.")]
    public PlayerEffects player;
    [Tooltip("Looping animation every racer plays while floating (treading water).")]
    public RuntimeAnimatorController floatController;
    public int racerCount = 32;
    public int rounds = 3;
    [Tooltip("How many racers drown in each round except the last (min, max). In the last round everyone but the player drowns.")]
    public Vector2Int drownPerRound = new Vector2Int(5, 10);
    public string cupName = "BEACH CUP";

    [Header("Stage")]
    [Tooltip("Layer only the qualifier camera sees (removed from every other camera).")]
    public int layer = 30;
    public Vector3 stagePosition = new Vector3(3000f, -3000f, 0f);
    public Material waterMaterial;
    public Material rippleMaterial;
    public Color waterColor = new Color(0.25f, 0.78f, 0.95f);
    [Tooltip("Camera angle below the horizon, in degrees.")]
    public float cameraPitch = 44f;
    public float cameraFov = 30f;
    [Tooltip("Gap between racers, as a share of a typical floatie's width.")]
    public float spacing = 1.3f;
    [Tooltip("Racers per row, front (player) to back. Extra racers go into more rows of the last size.")]
    public int[] rowPattern = { 1, 2, 3, 4, 4, 5, 5, 4, 4 };
    [Tooltip("Floaties longer than this (length / width) are not given to the other racers; they would overlap their neighbours.")]
    public float maxFloatieAspect = 1.35f;
    [Tooltip("Moves every character up/down inside its ring (metres), if the treading pose sits too high or low.")]
    public float characterHeightOffset = 0f;
    [Tooltip("Part of the screen the racers are fitted into (viewport, 0-1). The top is kept free for the banner.")]
    public Rect fitArea = new Rect(0.04f, 0.15f, 0.92f, 0.63f);
    [Tooltip("Name labels sit this far above the character's head (metres).")]
    public float labelAboveHead = 0.9f;

    [Header("Drowning")]
    public GameObject splashPrefab;
    public float splashScale = 3f;
    [Tooltip("Optional screams; one of them plays now and then when a racer drowns.")]
    public AudioClip[] drownVoices;
    [Range(0f, 1f)] public float voiceChance = 0.35f;
    [Range(0f, 1f)] public float voiceVolume = 0.6f;
    [Tooltip("Seconds everyone floats before the first one drowns.")]
    public float introSeconds = 1.3f;
    [Tooltip("Seconds between two racers drowning (min, max).")]
    public Vector2 drownGap = new Vector2(0.12f, 0.3f);
    [Tooltip("Seconds the result stays on screen before the game continues (min, max).")]
    public Vector2 holdSeconds = new Vector2(3f, 5f);

    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text roundText;
    public Image[] roundPips = new Image[0];
    public Color pipDone = new Color(1f, 0.55f, 0.2f);
    public Color pipTodo = new Color(0.29f, 0.2f, 0.55f);
    public RectTransform labelLayer;
    public TMP_Text nameTemplate;
    public RectTransform youTag;
    [Tooltip("How far the YOU tag sits above the player's name point, in UI units (higher = further up).")]
    public float youTagOffset = 22f;
    public TMP_Text resultText;

    private static readonly string[] Names =
    {
        "Splashy", "NoodleKid", "WaveRider", "BubbleBoss", "DripDrop", "FinnTheFish", "TubeMaster", "ReefRunner",
        "AquaAce", "SlideQueen", "CoolBreeze", "Floatzilla", "SunnyD", "PoolShark", "Ripples", "Kahuna",
        "Seashell", "Cannonball", "Marlin", "Wiggles", "SplashKing", "Coral", "BellyFlop", "LazyRiver",
        "Puddles", "Mango", "Captain", "Pickles", "Turbo", "Squidward", "Noodle_99", "Hydro",
        "Tsunami", "Lagoon", "Bubbles", "Dolphy", "Sandy", "Tiki", "Waterboy", "Zoomer"
    };

    private class Racer
    {
        public Transform root;
        public Transform ripple;
        public Transform head;   // humanoid head bone; the name follows it
        public TMP_Text label;
        public string name;
        public bool isPlayer;
        public bool alive = true;
        public bool sinking;
        public float labelHeight;
        public float bobPhase;
        public float bobSpeed;
        public float baseYaw;
        public Vector3 home;
    }

    private readonly List<Racer> racers = new List<Racer>();
    private Transform stage;
    private Transform holder;   // inactive parent, so copies do not wake up (and run player scripts) when made
    private Camera stageCamera;
    private AudioSource voice;
    private MaterialPropertyBlock rippleBlock;
    private float rippleSize = 3f;
    private bool running;

    /// <summary>Plays the next round of the cup, then calls done (the game continues from there).</summary>
    public void Play(Action done)
    {
        if (running)
        {
            return;
        }

        running = true;
        Show(true);
        StartCoroutine(Run(done));
    }

    /// <summary>Starts the cup over. Called when a race is lost.</summary>
    public static void ResetCup()
    {
        SaveData.ResetCup();
    }

    private void OnDestroy()
    {
        if (stage != null)
        {
            Destroy(stage.gameObject);
        }
    }

    #region Round

    private IEnumerator Run(Action done)
    {
        // Which round this is, and who drowns in it. Saved first, so quitting halfway still counts the round.
        int seed = SaveData.CupSeed;
        int round = SaveData.CupRound;
        var drowned = new List<int>(SaveData.CupOut);
        if (seed == 0 || round >= rounds)
        {
            seed = Random.Range(1, int.MaxValue);
            round = 0;
            drowned.Clear();
        }

        List<int> victims = PickVictims(round, drowned);
        drowned.AddRange(victims);
        SaveData.SetCup(round + 1, seed, drowned);

        // The caller faded the screen to black; build the stage behind it, then fade in.
        BuildStage(seed, SaveData.CupOut, victims);
        ShowRound(round);
        resultText.gameObject.SetActive(false);

        yield return null;   // let the camera render a frame behind the fade
        yield return ScreenFade.In(0.3f);
        yield return new WaitForSecondsRealtime(introSeconds);

        foreach (int index in victims)
        {
            StartCoroutine(Drown(racers[index]));
            yield return new WaitForSecondsRealtime(Random.Range(drownGap.x, drownGap.y));
        }

        while (racers.Exists(r => r.sinking))
        {
            yield return null;
        }

        bool last = round + 1 >= rounds;
        resultText.text = last ? "CHAMPION!" : "QUALIFIED!";
        resultText.gameObject.SetActive(true);
        resultText.transform.localScale = Vector3.zero;
        resultText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        if (roundPips.Length > round && roundPips[round] != null)
        {
            roundPips[round].color = pipDone;
            roundPips[round].transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 6).SetUpdate(true);
        }

        AudioManager.Play(Sfx.Finish);
        Haptics.Pulse();

        yield return new WaitForSecondsRealtime(Random.Range(holdSeconds.x, holdSeconds.y));
        running = false;
        done?.Invoke();
    }

    /// <summary>Random racers (never the player) that drown this round. The last round leaves only the player.</summary>
    private List<int> PickVictims(int round, List<int> alreadyOut)
    {
        var alive = new List<int>();
        for (int i = 1; i < racerCount; i++)
        {
            if (!alreadyOut.Contains(i))
            {
                alive.Add(i);
            }
        }

        int roundsLeftAfter = rounds - 1 - round;
        int count = roundsLeftAfter <= 0 ? alive.Count : Random.Range(drownPerRound.x, drownPerRound.y + 1);
        count = Mathf.Clamp(count, 0, Mathf.Max(0, alive.Count - roundsLeftAfter));   // keep someone for every later round

        for (int i = alive.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (alive[i], alive[j]) = (alive[j], alive[i]);
        }

        return alive.GetRange(0, count);
    }

    private void ShowRound(int round)
    {
        titleText.text = cupName;
        roundText.text = "ROUND " + (round + 1) + "/" + rounds;
        for (int i = 0; i < roundPips.Length; i++)
        {
            if (roundPips[i] != null)
            {
                roundPips[i].color = i < round ? pipDone : pipTodo;
            }
        }
    }

    private IEnumerator Drown(Racer racer)
    {
        racer.sinking = true;
        racer.alive = false;   // stops the bobbing, which would undo the wobble and the sinking
        Transform root = racer.root;

        // A short panic wobble, then down it goes with a splash.
        root.DOShakeRotation(0.35f, new Vector3(18f, 25f, 18f), 18, 90f, false).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.3f);

        Splash(root.position);
        AudioManager.Play(Sfx.Splash);
        if (voice != null && drownVoices.Length > 0 && Random.value < voiceChance && SaveData.Sound)
        {
            voice.pitch = Random.Range(0.95f, 1.25f);
            voice.PlayOneShot(drownVoices[Random.Range(0, drownVoices.Length)], voiceVolume);
        }

        if (racer.label != null)
        {
            racer.label.DOFade(0f, 0.25f).SetUpdate(true);
        }

        racer.ripple.DOScale(racer.ripple.localScale * 1.8f, 0.6f).SetUpdate(true);
        root.DOLocalMoveY(racer.home.y - racer.labelHeight * 1.6f, 0.75f).SetEase(Ease.InBack).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.75f);

        root.gameObject.SetActive(false);
        racer.ripple.gameObject.SetActive(false);
        racer.sinking = false;
    }

    private void Splash(Vector3 point)
    {
        if (splashPrefab == null)
        {
            return;
        }

        GameObject splash = Instantiate(splashPrefab, point, Quaternion.identity, stage);
        splash.transform.localScale = Vector3.one * splashScale;
        SetLayer(splash.transform);
        Destroy(splash, 3f);
    }

    #endregion

    #region Floating and labels

    private void Update()
    {
        if (stage == null)
        {
            return;
        }

        float t = Time.unscaledTime;
        foreach (Racer racer in racers)
        {
            if (!racer.alive || !racer.root.gameObject.activeSelf)
            {
                continue;
            }

            // Gentle bob and sway, each racer out of step with the others.
            float bob = Mathf.Sin(t * racer.bobSpeed + racer.bobPhase);
            racer.root.localPosition = racer.home + Vector3.up * bob * 0.08f;
            racer.root.localRotation = Quaternion.Euler(bob * 2.5f, racer.baseYaw + Mathf.Sin(t * 0.6f + racer.bobPhase) * 6f, Mathf.Cos(t * racer.bobSpeed + racer.bobPhase) * 2.5f);

            float pulse = Mathf.Repeat(t * 0.5f + racer.bobPhase, 1f);
            racer.ripple.localScale = Vector3.one * rippleSize * (1f + pulse * 0.25f);
            rippleBlock.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.3f * (1f - pulse * 0.7f)));   // particle shader: tint x2
            racer.ripple.GetComponent<Renderer>().SetPropertyBlock(rippleBlock);
        }
    }

    private void LateUpdate()
    {
        if (stageCamera == null)
        {
            return;
        }

        foreach (Racer racer in racers)
        {
            if (racer.label == null || !racer.root.gameObject.activeSelf)
            {
                if (racer.label != null)
                {
                    racer.label.gameObject.SetActive(false);
                }

                continue;
            }

            Place(racer.label.rectTransform, LabelPoint(racer));
        }

        Racer you = racers.Find(r => r.isPlayer);
        if (you != null && youTag != null)
        {
            Place(youTag, LabelPoint(you));
            youTag.anchoredPosition += new Vector2(0f, youTagOffset);
        }
    }

    private Vector3 LabelPoint(Racer racer)
    {
        return racer.head != null
            ? racer.head.position + Vector3.up * labelAboveHead
            : racer.root.position + Vector3.up * racer.labelHeight;
    }

    private void Place(RectTransform rt, Vector3 world)
    {
        Vector3 screen = stageCamera.WorldToScreenPoint(world);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(labelLayer, screen, null, out Vector2 local))
        {
            rt.anchoredPosition = local;
        }
    }

    #endregion

    #region Stage

    private void BuildStage(int seed, IReadOnlyList<int> gone, List<int> victims)
    {
        if (stage != null)
        {
            Destroy(stage.gameObject);
        }

        racers.Clear();
        rippleBlock = new MaterialPropertyBlock();
        stage = new GameObject("QualifierStage").transform;
        stage.position = stagePosition;

        var holderGo = new GameObject("Copies");
        holderGo.SetActive(false);
        holder = holderGo.transform;
        holder.SetParent(stage, false);

        BuildCamera();

        if (waterMaterial != null)
        {
            Transform water = Quad("Water", waterMaterial, 600f);
            water.localPosition = new Vector3(0f, -0.04f, 0f);
        }

        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }

        // The same seed gives the same racers every round of this cup.
        var rng = new System.Random(seed);
        List<int> characters = Deck(player != null ? player.SkinModels.Length : 0, rng);
        List<int> floaties = Deck(player != null ? player.FloatieModels.Length : 0, rng);
        RemoveLongFloaties(floaties);
        var names = new List<string>(Names);
        Shuffle(names, rng);

        int youCharacter = EquippedIndex(SkinSlot.Character);
        int youFloatie = EquippedIndex(SkinSlot.Floatie);

        var widths = new List<float>();
        for (int i = 0; i < racerCount; i++)
        {
            var racer = new Racer
            {
                isPlayer = i == 0,
                name = i == 0 ? "You" : names[i % names.Count],
                bobPhase = (float)rng.NextDouble() * 10f,
                bobSpeed = 1.6f + (float)rng.NextDouble() * 0.8f,
                baseYaw = 180f + ((float)rng.NextDouble() - 0.5f) * 40f
            };

            racer.root = new GameObject("Racer " + i + " " + racer.name).transform;
            racer.root.SetParent(stage, false);

            int c = i == 0 && youCharacter >= 0 ? youCharacter : characters.Count > 0 ? characters[i % characters.Count] : -1;
            int f = i == 0 && youFloatie >= 0 ? youFloatie : floaties.Count > 0 ? floaties[i % floaties.Count] : -1;
            GameObject floatie = f >= 0 ? Copy(player.FloatieModels[f], racer.root, false) : null;
            GameObject character = c >= 0 ? Copy(player.SkinModels[c].gameObject, racer.root, true) : null;
            SitOnWater(racer, floatie, character);

            if (floatie != null)
            {
                Bounds fb = RendererBounds(floatie);
                widths.Add(Mathf.Max(fb.size.x, fb.size.z));
            }

            racer.ripple = Quad("Ripple", rippleMaterial, 1f);
            racer.ripple.SetParent(stage, false);

            bool out_ = Contains(gone, i) && !victims.Contains(i);
            racer.alive = !out_;
            racer.root.gameObject.SetActive(!out_);
            racer.ripple.gameObject.SetActive(!out_ && rippleMaterial != null);
            racers.Add(racer);
        }

        widths.Sort();
        float typical = widths.Count > 0 ? widths[widths.Count / 2] : 2f;
        rippleSize = typical * 1.35f;
        Layout(typical * spacing, rng);
        FitCamera(typical);
        MakeLabels();

        stage.gameObject.SetActive(true);
        foreach (Racer racer in racers)
        {
            foreach (Animator animator in racer.root.GetComponentsInChildren<Animator>())
            {
                if (animator.enabled)
                {
                    animator.Play(0, 0, (float)rng.NextDouble());
                    animator.speed = 0.85f + (float)rng.NextDouble() * 0.3f;
                    animator.Update(0f);
                }
            }
        }
    }

    private void BuildCamera()
    {
        var camGo = new GameObject("QualifierCamera");
        camGo.transform.SetParent(stage, false);
        stageCamera = camGo.AddComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = waterColor;
        stageCamera.cullingMask = 1 << layer;
        stageCamera.fieldOfView = cameraFov;
        stageCamera.nearClipPlane = 0.3f;
        stageCamera.farClipPlane = 800f;
        stageCamera.depth = 50f;   // over the game cameras; the overlay canvas still draws on top

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != stageCamera)
            {
                cam.cullingMask &= ~(1 << layer);
            }
        }

        var lightGo = new GameObject("QualifierLight");
        lightGo.transform.SetParent(stage, false);
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.6f;
        light.cullingMask = 1 << layer;
        light.shadows = LightShadows.None;
    }

    /// <summary>A copy of one of the player's models with every script removed, placed as it sits on the player.</summary>
    private GameObject Copy(GameObject source, Transform parent, bool animate)
    {
        GameObject copy = Instantiate(source, holder);
        foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            DestroyImmediate(script);
        }

        foreach (ParticleSystem particles in copy.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.gameObject.SetActive(false);
        }

        foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
        {
            Destroy(collider);
        }

        foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(body);
        }

        SetLayer(copy.transform);

        foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = animate;
            if (animate)
            {
                animator.runtimeAnimatorController = floatController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        // Same offset and turn as on the player, without the player's own heading.
        Transform reference = player.transform;
        Quaternion inverse = Quaternion.Inverse(reference.rotation);
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = inverse * (source.transform.position - reference.position);
        copy.transform.localRotation = inverse * source.transform.rotation;
        copy.transform.localScale = source.transform.lossyScale;
        copy.SetActive(true);
        return copy;
    }

    /// <summary>Moves the character and ring so the ring floats on the water line (the racer's root).</summary>
    private void SitOnWater(Racer racer, GameObject floatie, GameObject character)
    {
        Animator animator = character != null ? character.GetComponentInChildren<Animator>() : null;
        if (animator != null && animator.enabled)
        {
            animator.Update(0f);   // measure the treading pose, not the T-pose
        }

        Vector3 shift = Vector3.zero;
        if (floatie != null)
        {
            Bounds fb = RendererBounds(floatie);
            shift = racer.root.InverseTransformPoint(fb.center);
            shift.y -= fb.extents.y * 0.25f;   // rings sit a little low in the water
            floatie.transform.localPosition -= shift;
        }

        if (character != null)
        {
            Vector3 charShift = shift;
            if (floatie == null)
            {
                Bounds cb = RendererBounds(character);
                charShift = racer.root.InverseTransformPoint(new Vector3(cb.center.x, cb.min.y + cb.size.y * 0.35f, cb.center.z));
            }

            character.transform.localPosition -= new Vector3(charShift.x, charShift.y - characterHeightOffset, charShift.z);
            if (animator != null && animator.isHuman)
            {
                racer.head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            Bounds top = RendererBounds(character);
            racer.labelHeight = top.max.y - racer.root.position.y + 0.35f;
        }
        else
        {
            racer.labelHeight = 2f;
        }
    }

    /// <summary>Rows that widen away from the camera, player alone in front (like a podium seen from above).</summary>
    private void Layout(float gap, System.Random rng)
    {
        var rows = new List<int>();
        int left = racers.Count;
        for (int i = 0; left > 0; i++)
        {
            int size = rowPattern.Length == 0 ? 5 : rowPattern[Mathf.Min(i, rowPattern.Length - 1)];
            int n = Mathf.Min(Mathf.Max(1, size), left);
            rows.Add(n);
            left -= n;
        }

        int index = 0;
        for (int row = 0; row < rows.Count; row++)
        {
            for (int j = 0; j < rows[row]; j++)
            {
                Racer racer = racers[index++];
                float jitterX = index == 1 ? 0f : ((float)rng.NextDouble() - 0.5f) * gap * 0.15f;
                float jitterZ = index == 1 ? 0f : ((float)rng.NextDouble() - 0.5f) * gap * 0.15f;
                racer.home = new Vector3((j - (rows[row] - 1) * 0.5f) * gap + jitterX, 0f, row * gap * 0.92f + jitterZ);
                racer.root.localPosition = racer.home;
                racer.root.localRotation = Quaternion.Euler(0f, racer.baseYaw, 0f);
                racer.ripple.localPosition = racer.home + Vector3.up * 0.02f;
                racer.ripple.localScale = Vector3.one * rippleSize;
            }
        }
    }

    /// <summary>Aims the camera down at the racers and moves it until all of them (and their names) fit the fit area.</summary>
    private void FitCamera(float ringSize)
    {
        var points = new List<Vector3>();
        foreach (Racer racer in racers)
        {
            Vector3 p = stage.TransformPoint(racer.home);
            points.Add(p + Vector3.left * ringSize * 0.5f);
            points.Add(p + Vector3.right * ringSize * 0.5f);
            points.Add(p + Vector3.back * ringSize * 0.5f);
            float labelY = racer.head != null ? racer.head.position.y - racer.root.position.y + labelAboveHead : racer.labelHeight;
            points.Add(p + Vector3.up * (labelY + 0.5f));
        }

        Transform cam = stageCamera.transform;
        cam.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        Vector3 target = stage.position;
        foreach (Racer racer in racers)
        {
            target += racer.home / racers.Count;
        }

        float distance = 60f;
        for (int i = 0; i < 40; i++)
        {
            cam.position = target - cam.forward * distance;
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (Vector3 p in points)
            {
                Vector3 v = stageCamera.WorldToViewportPoint(p);
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }

            Vector2 size = max - min;
            float scale = Mathf.Max(size.x / fitArea.width, size.y / fitArea.height);
            distance *= Mathf.Lerp(1f, scale, 0.6f);

            float height = 2f * distance * Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float widthWorld = height * stageCamera.aspect;
            Vector2 offset = (min + max) * 0.5f - fitArea.center;
            target += cam.right * offset.x * widthWorld * 0.8f + cam.up * offset.y * height * 0.8f;
        }

        cam.position = target - cam.forward * distance;
    }

    private void MakeLabels()
    {
        nameTemplate.gameObject.SetActive(false);
        foreach (Transform child in labelLayer)
        {
            if (child != nameTemplate.transform && child != youTag)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (Racer racer in racers)
        {
            if (racer.isPlayer || !racer.alive)
            {
                continue;
            }

            TMP_Text label = Instantiate(nameTemplate, labelLayer);
            label.text = racer.name;
            label.alpha = 1f;
            label.gameObject.SetActive(true);
            racer.label = label;
        }

        if (youTag != null)
        {
            youTag.SetAsLastSibling();
        }
    }

    private Transform Quad(string name, Material material, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.layer = layer;
        go.transform.SetParent(stage, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * size;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    #endregion

    #region Helpers

    private int EquippedIndex(SkinSlot slot)
    {
        SkinData item = SkinManager.GetEquippedItem(slot);
        if (item == null || player == null || string.IsNullOrEmpty(item.modelName))
        {
            return -1;
        }

        if (slot == SkinSlot.Character)
        {
            for (int i = 0; i < player.SkinModels.Length; i++)
            {
                if (player.SkinModels[i] != null && SameName(player.SkinModels[i].name, item.modelName))
                {
                    return i;
                }
            }
        }
        else
        {
            for (int i = 0; i < player.FloatieModels.Length; i++)
            {
                if (player.FloatieModels[i] != null && SameName(player.FloatieModels[i].name, item.modelName))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Drops the long floaties (noodles, slabs) from the deck, as long as some round ones are left.</summary>
    private void RemoveLongFloaties(List<int> deck)
    {
        var round = new List<int>();
        foreach (int index in deck)
        {
            GameObject probe = Copy(player.FloatieModels[index], stage, false);
            Bounds b = RendererBounds(probe);
            float shortSide = Mathf.Max(0.01f, Mathf.Min(b.size.x, b.size.z));
            if (Mathf.Max(b.size.x, b.size.z) / shortSide <= maxFloatieAspect)
            {
                round.Add(index);
            }

            DestroyImmediate(probe);
        }

        if (round.Count > 0)
        {
            deck.Clear();
            deck.AddRange(round);
        }
    }

    /// <summary>Every index once in random order, so the racers repeat models as little as possible.</summary>
    private static List<int> Deck(int count, System.Random rng)
    {
        var deck = new List<int>();
        for (int i = 0; i < count; i++)
        {
            deck.Add(i);
        }

        Shuffle(deck, rng);
        return deck;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static bool Contains(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; } else { bounds.Encapsulate(r.bounds); }
        }

        return bounds;
    }

    private void SetLayer(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
        }
    }

    private static bool SameName(string a, string b)
    {
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
