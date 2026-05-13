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
            SceneTransitionOverlay.GetOrCreate();

            MenuCanvasController.DestroyIfPresent();

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
