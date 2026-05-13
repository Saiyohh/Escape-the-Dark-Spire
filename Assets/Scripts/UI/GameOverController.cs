using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DarkSpire
{
    public class GameOverController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button tryAgainButton;

        [Header("Targets")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Start()
        {
            // Tear down run-scoped UI now so the menu re-enters clean.
            MenuCanvasController.DestroyIfPresent();

            if (tryAgainButton != null) tryAgainButton.onClick.AddListener(OnTryAgainClicked);
        }

        private void OnDestroy()
        {
            if (tryAgainButton != null) tryAgainButton.onClick.RemoveListener(OnTryAgainClicked);
        }

        private void OnTryAgainClicked()
        {
            // MainMenuController.Start clears RunStateHolder + RunContext, so
            // we don't have to do that here.
            var overlay = SceneTransitionOverlay.GetOrCreate();
            if (overlay != null)
                overlay.LoadSceneTransition(mainMenuSceneName);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
