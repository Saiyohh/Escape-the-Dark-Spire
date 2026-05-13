using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    public class MenuCanvasController : MonoBehaviour
    {
        public static MenuCanvasController Instance { get; private set; }

        [Header("Panels")]
        [Tooltip("Pause menu — Esc to toggle, Resume / Quit to Main Menu buttons.")]
        [SerializeField] private PauseMenuController pauseMenu;

        [Tooltip("Pickup feedback toast feed — KeyEntity / GoldPileEntity / shrine " +
                 "results route through Pickups.ShowPickup(text).")]
        [SerializeField] private PickupNotificationManager pickups;

        [Header("Input")]
        [Tooltip("If true, the controller polls Keyboard.current.escapeKey each frame " +
                 "and toggles the pause menu when pressed. Disable if your input " +
                 "system reads Esc through an InputAction asset instead.")]
        [SerializeField] private bool listenForEsc = true;

        [Tooltip("If true, the pause menu starts CLOSED on Awake regardless of how " +
                 "the prefab was authored. Defensive — caught the case where the " +
                 "panel was saved active and showed up immediately on scene load.")]
        [SerializeField] private bool forceClosedOnAwake = true;

        public PauseMenuController PauseMenu => pauseMenu;
        public PickupNotificationManager Pickups => pickups;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            if (pauseMenu == null) pauseMenu = GetComponentInChildren<PauseMenuController>(true);
            if (pickups   == null) pickups   = GetComponentInChildren<PickupNotificationManager>(true);

            if (forceClosedOnAwake && pauseMenu != null)
                pauseMenu.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!listenForEsc) return;
            if (pauseMenu == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) TogglePause();
        }

        public void TogglePause()
        {
            if (pauseMenu == null) return;
            if (pauseMenu.gameObject.activeSelf) ClosePause();
            else                                  OpenPause();
        }

        public void OpenPause()
        {
            if (pauseMenu == null) return;
            pauseMenu.gameObject.SetActive(true);
            pauseMenu.Open();    // panel-specific Open hook (button bind, fade-in, time scale)
        }

        public void ClosePause()
        {
            if (pauseMenu == null) return;
            pauseMenu.Close();   // panel-specific Close hook (time scale restore)
            pauseMenu.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static MenuCanvasController GetOrCreate()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>("MenuCanvas");
            if (prefab == null)
            {
                Debug.LogError("[MenuCanvasController] No prefab at " +
                               "Assets/Resources/MenuCanvas.prefab. Author one with " +
                               "the Menu Canvas hierarchy + a MenuCanvasController " +
                               "script on the root.");
                return null;
            }
            var go = Instantiate(prefab);
            go.name = "MenuCanvas";
            return go.GetComponent<MenuCanvasController>();
        }

        public static void DestroyIfPresent()
        {
            if (Instance != null) Destroy(Instance.gameObject);
        }
    }
}
