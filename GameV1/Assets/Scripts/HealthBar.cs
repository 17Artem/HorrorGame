using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health target;
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;

    [Header("Для полоски над врагом")]
    [SerializeField] private bool faceCamera;
    [SerializeField] private Transform cameraTransform;

    private void OnEnable()
    {
        ResolveTarget();
        SubscribeToTarget();
    }

    private void Start()
    {
        if (target == null)
        {
            ResolveTarget();
            SubscribeToTarget();
        }

        if (slider == null && target != null && target.CompareTag("Player"))
            CreatePlayerHud();
        else if (slider == null && target != null && target.CompareTag("Enemy"))
            CreateEnemyWorldBar();

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;
            if (fillImage == null && slider.fillRect != null)
                fillImage = slider.fillRect.GetComponent<Image>();
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Refresh();
    }

    private void OnDisable()
    {
        if (target != null)
            target.HealthChanged -= Refresh;
    }

    private void ResolveTarget()
    {
        if (target != null)
            return;

        target = GetComponentInParent<Health>();
        if (target != null || !CompareTag("Enemy"))
            return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float closestDistance = float.PositiveInfinity;

        foreach (GameObject enemy in enemies)
        {
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null)
                continue;

            float distance = (transform.position - enemy.transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                target = enemyHealth;
            }
        }
    }

    private void SubscribeToTarget()
    {
        if (target != null)
        {
            target.HealthChanged -= Refresh;
            target.HealthChanged += Refresh;
        }
    }

    private void CreateEnemyWorldBar()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        faceCamera = true;

        RectTransform root = transform as RectTransform;
        if (root != null)
            root.sizeDelta = new Vector2(180f, 22f);
        transform.localPosition = new Vector3(0f, 2f, 0f);
        transform.localScale = Vector3.one * 0.005f;

        Image background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.03f, 0.035f, 0.9f);
        background.raycastTarget = false;

        GameObject sliderObject = new GameObject("Enemy HP Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = new Vector2(3f, 3f);
        sliderRect.offsetMax = new Vector2(-3f, -3f);

        Image track = sliderObject.GetComponent<Image>();
        track.color = new Color(0.15f, 0.16f, 0.17f, 1f);
        track.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.25f, 0.82f, 0.32f, 1f);
        fillImage.raycastTarget = false;

        slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
        slider.targetGraphic = track;
        slider.fillRect = fillRect;
    }
    private void CreatePlayerHud()
    {
        GameObject canvasObject = new GameObject(
            "Player Health HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Health Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(320f, 64f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.055f, 0.88f);

        GameObject sliderObject = new GameObject("Health Slider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        sliderObject.transform.SetParent(panel.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 18f);
        sliderRect.sizeDelta = new Vector2(-32f, 18f);

        Image track = sliderObject.GetComponent<Image>();
        track.color = new Color(0.16f, 0.18f, 0.2f, 1f);
        track.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.25f, 0.82f, 0.32f, 1f);
        fillImage.raycastTarget = false;

        slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;
        slider.targetGraphic = track;
        slider.fillRect = fillRect;
    }

    private void Refresh()
    {
        if (target == null || slider == null)
            return;

        float healthRatio = target.MaxHealth > 0
            ? (float)target.CurrentHealth / target.MaxHealth
            : 0f;
        slider.value = healthRatio;

        if (fillImage != null)
        {
            Color lowHealth = new Color(0.85f, 0.08f, 0.09f, 1f);
            Color fullHealth = new Color(0.25f, 0.82f, 0.32f, 1f);
            fillImage.color = Color.Lerp(lowHealth, fullHealth, healthRatio);
        }
    }

    private void LateUpdate()
    {
        if (faceCamera && cameraTransform != null)
            transform.rotation = cameraTransform.rotation;
    }
}
