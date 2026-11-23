using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona i actualitza la interfície d'usuari del joc.
/// HUD: nom de l'arma equipada, munició actual del carregador / munició màxima del carregador
/// i munició de reserva. S'actualitza per esdeveniments (no cada frame) escoltant el WeaponManager.
/// </summary>
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Referències")]
    [Tooltip("Si es deixa buit, es busca automàticament a l'escena.")]
    [SerializeField] private WeaponManager weaponManager;

    [Header("HUD de l'arma")]
    [SerializeField] private GameObject weaponPanel;
    [SerializeField] private Text weaponNameText;
    [SerializeField] private Text ammoText;
    [SerializeField] private Text reserveText;

    [Header("Estil")]
    [SerializeField] private Color normalAmmoColor = Color.white;
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.35f, 0.25f);
    [Tooltip("Per sota d'aquest percentatge del carregador la munició es pinta en vermell.")]
    [Range(0f, 1f)] [SerializeField] private float lowAmmoThreshold = 0.25f;

    private Weapon currentWeapon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();
    }

    private void OnEnable()
    {
        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged += OnActiveWeaponChanged;
            weaponManager.AmmoChanged += OnAmmoChanged;
        }
    }

    private void Start()
    {
        OnActiveWeaponChanged(weaponManager != null ? weaponManager.ActiveWeapon : null);
    }

    private void OnDisable()
    {
        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged -= OnActiveWeaponChanged;
            weaponManager.AmmoChanged -= OnAmmoChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------- Esdeveniments ----------------

    private void OnActiveWeaponChanged(Weapon weapon)
    {
        currentWeapon = weapon;
        UpdateWeaponHUD();
    }

    private void OnAmmoChanged(Weapon weapon)
    {
        if (weapon == currentWeapon) UpdateWeaponHUD();
    }

    // ---------------- API pública ----------------

    /// <summary>Força el redibuixat del HUD de l'arma.</summary>
    public void UpdateWeaponHUD()
    {
        bool hasWeapon = currentWeapon != null && currentWeapon.Data != null;
        if (weaponPanel != null) weaponPanel.SetActive(hasWeapon);
        if (!hasWeapon) return;

        WeaponData data = currentWeapon.Data;
        int magazine = currentWeapon.Magazine;
        int maxMagazine = data.magazineSize;

        if (weaponNameText != null)
            weaponNameText.text = data.displayName;

        if (ammoText != null)
        {
            ammoText.text = magazine + " / " + maxMagazine;
            bool low = magazine <= Mathf.CeilToInt(maxMagazine * lowAmmoThreshold);
            ammoText.color = low ? lowAmmoColor : normalAmmoColor;
        }

        if (reserveText != null)
            reserveText.text = data.infiniteReserveAmmo
                ? "Reserva: ∞"
                : "Reserva: " + currentWeapon.Reserve + " / " + data.maxReserveAmmo;
    }
}
