using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        [Header("Targets")]
        [Tooltip("Scene loaded when Play is pressed. Phase 11+ replaces this with PartySelect.")]
        [SerializeField] private string playSceneName = "DungeonFloor";

        [Header("Bootstrap Party (party-select stand-in)")]
        [Tooltip("Static party used to seed RunContext.party + RunContext.partyState " +
                 "on Play. Each entry produces one PartyMemberRuntime starting at " +
                 "full HP/SP, which then carries across the dungeon ↔ combat loop. " +
                 "Replaced by the proper PartySelect scene in a later phase.")]
        [SerializeField] private CharacterData[] bootstrapParty;

        private void Start()
        {
            // SYSTEM-tier UI: persistent across the entire game session,
            // including main menu. Born here so its first scene-load triggers
            // the cover/reveal pattern for every swap.
            SceneTransitionOverlay.GetOrCreate();

            // RUN-tier UI: pause menu, pickup toasts, future inventory etc.
            // are hosted by MenuCanvasController, which DungeonBootstrap
            // spawns on entering a run. Tear down any leftover instance from
            // a previous run so we re-enter the menu in a clean state.
            // (Editor stop/start preserves DontDestroyOnLoad objects until
            // domain reload, so this also catches replay-from-editor cases.)
            MenuCanvasController.DestroyIfPresent();

            // Reset any leftover run state from a previous play session.
            RunStateHolder.Instance?.ClearForNewRun();
            RunContext.EndRun();

            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        private void OnPlayClicked()
        {
            RunContext.NewRun(bootstrapParty, Random.Range(0, int.MaxValue));
            RunContext.InitializePartyState(bootstrapParty);

            var overlay = SceneTransitionOverlay.GetOrCreate();
            if (overlay != null)
            {
                overlay.LoadSceneTransition(playSceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(playSceneName);
            }
        }

        private void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}
