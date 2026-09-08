using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Guards Inspector wiring on MainMenuUI that only matters in release builds and therefore
/// never fails in the editor. The 1.2.0 TestFlight build shipped with the dev-only Shop button
/// visible because <c>shopButton</c> was empty, so SetupShopButton could not hide it.
/// </summary>
public class MainMenuWiringTests {
  private const string ScenePath = "Assets/Scenes/Make10Scene.unity";
  private Scene gameScene;
  private bool openedGameScene;

  [OneTimeSetUp]
  public void OpenScene() {
    gameScene = SceneManager.GetSceneByPath(ScenePath);
    if (gameScene.IsValid() && gameScene.isLoaded) {
      return;
    }

    try {
      gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }
    catch (System.InvalidOperationException) {
      Assert.IsTrue(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo(),
        "Save or discard the current scene before running wiring tests.");
      gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    openedGameScene = true;
    Assert.IsTrue(gameScene.isLoaded, $"Could not load {ScenePath}");
  }

  [OneTimeTearDown]
  public void CloseScene() {
    if (openedGameScene && gameScene.IsValid() && SceneManager.sceneCount > 1) {
      EditorSceneManager.CloseScene(gameScene, true);
    }
  }

  private MainMenuUI FindMainMenuUI() {
    var ui = gameScene.GetRootGameObjects()
      .SelectMany(go => go.GetComponentsInChildren<MainMenuUI>(true))
      .FirstOrDefault();
    Assert.IsNotNull(ui, "MainMenuUI not found in scene");
    return ui;
  }

  [Test]
  public void ShopButton_IsWiredOnMainMenuUI() {
    var ui = FindMainMenuUI();
    var prop = new SerializedObject(ui).FindProperty("shopButton");
    Assert.IsNotNull(prop, "MainMenuUI.shopButton field renamed? Update this test.");
    Assert.IsNotNull(prop.objectReferenceValue,
      "MainMenuUI.shopButton is empty in the scene; SetupShopButton cannot hide the Shop button in release builds.");
  }

  [Test]
  public void ShopButton_ReferenceIsTheSceneShopButton() {
    var ui = FindMainMenuUI();
    var wired = new SerializedObject(ui).FindProperty("shopButton").objectReferenceValue as Button;
    var byName = ui.transform.Find("ShopButton")?.GetComponent<Button>();
    Assert.IsNotNull(byName, "MainMenuPanel/ShopButton missing from scene");
    Assert.AreSame(byName, wired, "shopButton points at a different Button than MainMenuPanel/ShopButton");
  }
}
